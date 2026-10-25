using System.Diagnostics;
using System.Text.Json;
using comply_flow_api.Models;
using comply_flow_api.Models.dtos.ProcessDtos;
using comply_flow_api.Repositories;

namespace comply_flow_api.Services;

public interface IProcessingService
{
    Task<ProcessingCreationResult> CreateForMessageAsync(
        Message message,
        int configurationId,
        CancellationToken cancellationToken);
    Task<ProcessingResultResponse?> GetResultAsync(int processingId, CancellationToken cancellationToken);
}

public enum ProcessingCreationStatus
{
    Created,
    ConfigurationNotFound
}

public sealed record ProcessingCreationResult(
    ProcessingCreationStatus Status,
    Processing? Processing = null);

public class ProcessingService : IProcessingService
{
    private readonly IConfigurationRepository _configurationRepository;
    private readonly IProcessingRepository _processingRepository;
    private readonly IAiServiceClient _aiServiceClient;
    private readonly ITextFileService _textFileService;
    private readonly ILogger<ProcessingService> _logger;

    public ProcessingService(
        IConfigurationRepository configurationRepository,
        IProcessingRepository processingRepository,
        IAiServiceClient aiServiceClient,
        ITextFileService textFileService,
        ILogger<ProcessingService> logger)
    {
        _configurationRepository = configurationRepository;
        _processingRepository = processingRepository;
        _aiServiceClient = aiServiceClient;
        _textFileService = textFileService;
        _logger = logger;
    }

    public async Task<ProcessingCreationResult> CreateForMessageAsync(
        Message message,
        int configurationId,
        CancellationToken cancellationToken)
    {
        var configuration = await _configurationRepository.GetByIdAsync(
            configurationId,
            cancellationToken);
        if (configuration is null)
        {
            return new ProcessingCreationResult(ProcessingCreationStatus.ConfigurationNotFound);
        }

        var processing = new Processing
        {
            ConversationId = message.ConversationId,
            InputMessage = message,
            ConfigurationId = configurationId,
            CreatedAt = DateTime.UtcNow
        };

        await _processingRepository.AddAsync(processing, cancellationToken);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            processing.TransitionTo(ProcessingStatus.Processing);
            await _processingRepository.SaveChangesAsync(cancellationToken);

            var ruleSetJson = await _textFileService.GetBusinessRulesAsync(cancellationToken);
            var ruleSets = JsonSerializer.Deserialize<List<RuleSetDocument>>(ruleSetJson)
                ?? throw new InvalidOperationException("The business rules file is empty or invalid.");
            if (ruleSets.Count == 0 || ruleSets.Any(ruleSet => ruleSet is null))
            {
                throw new InvalidDataException("The business rules file contains no valid rule sets.");
            }

            var matchingRuleSets = ruleSets
                .Where(ruleSet => string.Equals(
                    ruleSet.RuleSetVersion,
                    configuration.RuleSetVersion,
                    StringComparison.Ordinal))
                .ToList();
            if (matchingRuleSets.Count != 1)
            {
                throw new InvalidOperationException(
                    matchingRuleSets.Count == 0
                        ? $"No business rule set was found for configured version '{configuration.RuleSetVersion}'."
                        : $"Multiple business rule sets were found for configured version '{configuration.RuleSetVersion}'.");
            }
            var ruleSet = matchingRuleSets[0];
            ValidateRuleSet(ruleSet);

            var preprocessing = await _aiServiceClient.PreprocessAsync(
                message.Content,
                cancellationToken);
            if (preprocessing.PartialSuccess)
            {
                _logger.LogWarning(
                    "Preprocessing for processing {ProcessingId} partially succeeded: {Warnings}",
                    processing.Id,
                    string.Join(", ", preprocessing.Warnings));
            }

            var caseState = preprocessing.CaseState;
            var evaluation = RuleEngine.Evaluate(caseState, ruleSet.Rules);
            var selectedRule = evaluation.SelectedRule?.Rule;
            var decision = selectedRule?.Result.Decision ?? "NoMatch";
            var reason = selectedRule is null
                ? "No rule matched the preprocessed case."
                : selectedRule.Name;

            processing.PreProcessing = new PreProcessing
            {
                CleanedText = preprocessing.Message,
                CaseType = preprocessing.Category ?? "Unknown",
                StructuredCaseStateJson = JsonSerializer.Serialize(caseState)
            };
            processing.Rules = new Rules
            {
                Decision = decision,
                Reason = reason,
                RuleSetVersion = ruleSet.RuleSetVersion,
                MatchedRules = evaluation.Evaluations
                    .Select(result => new MatchedRules
                    {
                        Matched = result.IsMatched,
                        PrioScore = result.Rule.PrioScore,
                        ResultJson = JsonSerializer.Serialize(
                            result.Rule.Result,
                            JsonSerializerOptions.Web),
                        Source = result.Rule.Source,
                        EffectiveFrom = result.Rule.EffectiveFrom,
                        Reason = $"{result.Rule.Id}: {result.Rule.Name}"
                    })
                    .ToList()
            };
            processing.ResponsePlan = CreateResponsePlan(selectedRule, decision, reason);
            await _processingRepository.SaveChangesAsync(cancellationToken);

            var availableModels = await _aiServiceClient.GetModelsAsync(cancellationToken);
            var modelName = ResolveModelName(configuration.ModelName, availableModels);
            var modelParameters = configuration.ModelParametersJson is null
                ? null
                : JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                    configuration.ModelParametersJson);
            var promptName = configuration.PromptVersion ?? configuration.TemplateVersion;
            var inputContext = new Dictionary<string, object?>
            {
                ["case_state"] = caseState,
                ["decision"] = decision,
                ["reason"] = reason,
                ["rule_result"] = selectedRule?.Result
            };

            var generation = await _aiServiceClient.GenerateAsync(
                new AiGenerationRequest(
                    inputContext,
                    preprocessing.Message,
                    preprocessing.Facts,
                    preprocessing.Category,
                    promptName,
                    modelName,
                    modelParameters),
                cancellationToken);

            if (generation.ReferenceSimilarity is decimal referenceSimilarity &&
                generation.ReferenceSimilarityMetric is string referenceSimilarityMetric)
            {
                processing.EvaluationResults.Add(new EvaluationResult
                {
                    Metric = "reference_similarity",
                    Score = referenceSimilarity,
                    Unit = "0-1",
                    Comment = referenceSimilarityMetric
                });
            }

            processing.Response = new Response
            {
                GenerationMode = configuration.GenerationMode,
                ResponseText = generation.Response,
                TemplateVersion = configuration.TemplateVersion,
                PromptVersion = generation.PromptVersion,
                ModelName = generation.ModelName,
                ModelParametersJson = JsonSerializer.Serialize(generation.ModelParameters),
                InputContextJson = JsonSerializer.Serialize(inputContext),
                ProcessingTimeMs = (int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue)
            };
            await _processingRepository.SaveChangesAsync(cancellationToken);

            processing.TransitionTo(ProcessingStatus.Completed);
            await _processingRepository.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            var completionWasNotPersisted = processing.Status == ProcessingStatus.Completed;
            if (processing.Status == ProcessingStatus.Processing || completionWasNotPersisted)
            {
                var errorType = exception is AiServiceException aiServiceException
                    ? aiServiceException.FailureKind.ToString()
                    : exception.GetType().Name;
                processing.Fail(
                    errorType,
                    exception.Message,
                    completionWasNotPersisted);
                try
                {
                    var failureSaved = await _processingRepository.MarkFailedAsync(
                        processing.Id,
                        errorType,
                        exception.Message,
                        processing.FinishedAt!.Value,
                        CancellationToken.None);
                    if (!failureSaved)
                    {
                        _logger.LogWarning(
                            "No processing row was updated to Failed for processing {ProcessingId}.",
                            processing.Id);
                    }
                }
                catch (Exception persistenceException)
                {
                    _logger.LogError(
                        persistenceException,
                        "Could not persist failure details for processing {ProcessingId}.",
                        processing.Id);
                }
            }

            _logger.LogError(exception, "Processing {ProcessingId} failed.", processing.Id);
            throw;
        }

        return new ProcessingCreationResult(ProcessingCreationStatus.Created, processing);
    }

    public async Task<ProcessingResultResponse?> GetResultAsync(
        int processingId,
        CancellationToken cancellationToken)
    {
        var processing = await _processingRepository.GetResultByIdAsync(processingId, cancellationToken);
        if (processing is null)
        {
            return null;
        }

        var evaluationResults = processing.EvaluationResults
            .Select(result => new EvaluationResultResponse(
                result.Metric,
                result.Score,
                result.Unit,
                result.Comment))
            .ToList();
        var resultsAvailable =
            processing.PreProcessing is not null ||
            processing.Rules is not null ||
            processing.ResponsePlan is not null ||
            processing.Response is not null ||
            evaluationResults.Count > 0;
        var isFinished = processing.Status is ProcessingStatus.Completed or ProcessingStatus.Failed;
        var availabilityMessage = GetAvailabilityMessage(
            processing.Status,
            resultsAvailable);
        var results = resultsAvailable
            ? new ProcessingArtifactsResponse(
                processing.PreProcessing is null
                    ? null
                    : new PreProcessingResultResponse(
                        processing.PreProcessing.CleanedText,
                        processing.PreProcessing.CaseType,
                        processing.PreProcessing.StructuredCaseStateJson),
                processing.Rules is null
                    ? null
                    : new RuleDecisionResultResponse(
                        processing.Rules.Decision,
                        processing.Rules.Reason,
                        processing.Rules.RuleSetVersion,
                        processing.Rules.MatchedRules
                            .Select(rule => new MatchedRuleResultResponse(
                                rule.Matched,
                                rule.PrioScore,
                                rule.ResultJson,
                                rule.Source,
                                rule.EffectiveFrom,
                                rule.Reason))
                            .ToList()),
                processing.ResponsePlan is null
                    ? null
                    : new ResponsePlanResultResponse(
                        processing.ResponsePlan.Decision,
                        processing.ResponsePlan.Reason,
                        processing.ResponsePlan.RequiredElementsJson,
                        processing.ResponsePlan.ResponseStructureJson,
                        processing.ResponsePlan.SourcesJson),
                processing.Response is null
                    ? null
                    : new GeneratedResponseResultResponse(
                        processing.Response.ResponseText,
                        processing.Response.GenerationMode,
                        processing.Response.TemplateVersion,
                        processing.Response.PromptVersion,
                        processing.Response.ModelName,
                        processing.Response.ProcessingTimeMs),
                evaluationResults)
            : null;

        var error = processing.ErrorType is null && processing.ErrorMessage is null
            ? null
            : new ProcessingErrorResponse(
                processing.ErrorType ?? "ProcessingError",
                processing.ErrorMessage ?? "The processing failed without an error message.");

        return new ProcessingResultResponse(
            processing.Id,
            processing.Status.ToString(),
            processing.CreatedAt,
            processing.StartedAt,
            processing.FinishedAt,
            isFinished,
            resultsAvailable,
            availabilityMessage,
            error,
            results);
    }

    private static string? GetAvailabilityMessage(ProcessingStatus status, bool resultsAvailable) =>
        status switch
        {
            ProcessingStatus.Created => "Processing has not started; results are not available yet.",
            ProcessingStatus.Processing when resultsAvailable => "Processing is still running; available results are partial.",
            ProcessingStatus.Processing => "Processing is still running; results are not available yet.",
            ProcessingStatus.Failed when resultsAvailable => "Processing failed; partial results are available.",
            ProcessingStatus.Failed => "Processing failed before producing results.",
            _ => null
        };

    private static void ValidateRuleSet(RuleSetDocument ruleSet)
    {
        if (string.IsNullOrWhiteSpace(ruleSet.RuleSetVersion) ||
            ruleSet.Rules is null ||
            ruleSet.Rules.Count == 0)
        {
            throw new InvalidDataException("The selected business rule set is missing its version or rules.");
        }

        foreach (var rule in ruleSet.Rules)
        {
            if (rule is null ||
                string.IsNullOrWhiteSpace(rule.Id) ||
                string.IsNullOrWhiteSpace(rule.Name) ||
                rule.Conditions is null ||
                rule.Result is null ||
                string.IsNullOrWhiteSpace(rule.Result.Decision))
            {
                throw new InvalidDataException(
                    $"The business rule set '{ruleSet.RuleSetVersion}' contains a rule with missing required fields.");
            }

            ValidateConditions(rule, rule.Conditions.All);
            ValidateConditions(rule, rule.Conditions.Any);
        }
    }

    private static void ValidateConditions(Rule rule, IEnumerable<Condition>? conditions)
    {
        if (conditions is null)
        {
            return;
        }

        foreach (var condition in conditions)
        {
            if (condition is null ||
                string.IsNullOrWhiteSpace(condition.Field) ||
                string.IsNullOrWhiteSpace(condition.Operator) ||
                !IsValidConditionValue(condition))
            {
                throw new InvalidDataException(
                    $"Rule '{rule.Id}' contains a condition with a missing field, operator, or invalid value type.");
            }
        }
    }

    private static bool IsValidConditionValue(Condition condition)
    {
        var operation = condition.Operator.Trim().ToLowerInvariant();
        if (operation is not (
                "equals" or
                "notequals" or
                "in" or
                "notin" or
                "greaterthan" or
                "greaterthanorequal" or
                "lessthan" or
                "lessthanorequal"))
        {
            return false;
        }

        if (condition.Value is not JsonElement value)
        {
            return condition.Value is null && operation is "equals" or "notequals";
        }

        return operation switch
        {
            "equals" or "notequals" => IsScalarJsonValue(value),
            "in" or "notin" => value.ValueKind == JsonValueKind.Array &&
                value.EnumerateArray().All(IsScalarJsonValue),
            "greaterthan" or "greaterthanorequal" or "lessthan" or "lessthanorequal" =>
                value.ValueKind == JsonValueKind.Number,
            _ => false
        };
    }

    private static bool IsScalarJsonValue(JsonElement value) =>
        value.ValueKind is JsonValueKind.String or
            JsonValueKind.Number or
            JsonValueKind.True or
            JsonValueKind.False or
            JsonValueKind.Null;

    private static ResponsePlan CreateResponsePlan(Rule? selectedRule, string decision, string reason)
    {
        var resultData = selectedRule?.Result.AdditionalData
            ?? new Dictionary<string, JsonElement>();
        var responseStructure = resultData
            .Where(item => !string.Equals(item.Key, "requiredElements", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(item => item.Key, item => item.Value);
        var requiredElements = GetRequiredElements(resultData, responseStructure);

        var sources = selectedRule is null
            ? Array.Empty<string>()
            : new[] { selectedRule.Source, selectedRule.EffectiveFrom }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .ToArray();

        return new ResponsePlan
        {
            Decision = decision,
            Reason = reason,
            RequiredElementsJson = JsonSerializer.Serialize(requiredElements),
            ResponseStructureJson = JsonSerializer.Serialize(responseStructure),
            SourcesJson = JsonSerializer.Serialize(sources)
        };
    }

    private static IReadOnlyList<string> GetRequiredElements(
        IReadOnlyDictionary<string, JsonElement> resultData,
        IReadOnlyDictionary<string, JsonElement> responseStructure)
    {
        if (resultData.TryGetValue("requiredElements", out var explicitElements))
        {
            if (explicitElements.ValueKind != JsonValueKind.Array ||
                explicitElements.EnumerateArray().Any(element => element.ValueKind != JsonValueKind.String))
            {
                throw new InvalidDataException("Rule requiredElements must be an array of strings.");
            }

            return explicitElements.EnumerateArray()
                .Select(element => element.GetString()!)
                .Where(element => !string.IsNullOrWhiteSpace(element))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        var requiredElements = new List<string>();
        foreach (var (name, value) in responseStructure)
        {
            AddRequiredElementPaths(value, name, requiredElements);
        }

        return requiredElements;
    }

    private static void AddRequiredElementPaths(
        JsonElement value,
        string path,
        ICollection<string> requiredElements)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var properties = value.EnumerateObject().ToArray();
            if (properties.Length == 0)
            {
                requiredElements.Add(path);
                return;
            }

            foreach (var property in properties)
            {
                AddRequiredElementPaths(property.Value, $"{path}.{property.Name}", requiredElements);
            }

            return;
        }

        requiredElements.Add(path);
    }

    private static string ResolveModelName(
        string? configuredModelName,
        IReadOnlyList<AiModelOption> availableModels)
    {
        var model = string.IsNullOrWhiteSpace(configuredModelName)
            ? availableModels.FirstOrDefault(option => option.IsDefault) ?? availableModels.FirstOrDefault()
            : availableModels.FirstOrDefault(option => string.Equals(
                option.Name,
                configuredModelName,
                StringComparison.OrdinalIgnoreCase));

        return model?.Name
            ?? throw new InvalidOperationException(
                $"Configured model '{configuredModelName}' is not available from the AI service.");
    }
}