using System.Diagnostics;
using System.Globalization;
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
    Task<ProcessingDetailsDto> GetProcessingDetailsAsync(int processingId, CancellationToken cancellationToken);
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

            if (string.Equals(configuration.GenerationMode, "Rag", StringComparison.OrdinalIgnoreCase))
            {
                await ProcessRagAsync(processing, message, configuration, stopwatch, cancellationToken);
            }
            else
            {
                var ruleSetJson = await _textFileService.GetBusinessRulesAsync(
                    configuration.Bank,
                    cancellationToken);
                var ruleSets = JsonSerializer.Deserialize<List<RuleSetDocument>>(ruleSetJson)
                    ?? throw new InvalidOperationException("The business rules file is empty or invalid.");
                if (ruleSets.Count != 1 || ruleSets[0] is null)
                {
                    throw new InvalidDataException("The business rules file must contain exactly one valid rule set.");
                }
                var ruleSet = ruleSets[0];
                ValidateRuleSet(ruleSet);

                var preprocessing = await _aiServiceClient.PreprocessAsync(
                    message.Content,
                    configuration.Bank,
                    cancellationToken);
                if (preprocessing.PartialSuccess)
                {
                    _logger.LogWarning(
                        "Preprocessing for processing {ProcessingId} partially succeeded. Warnings: {Warnings}; details: {WarningDetails}",
                        processing.Id,
                        string.Join(", ", preprocessing.Warnings),
                        JsonSerializer.Serialize(preprocessing.WarningDetails));
                }

                var caseState = preprocessing.CaseState;
                var category = preprocessing.Category;
                if (string.Equals(configuration.Bank, "otp", StringComparison.OrdinalIgnoreCase) &&
                    IsCardDeliveryRequest(message.Content))
                {
                    category = "CARD_DELIVERY";
                    caseState["category"] = category;
                }

                var evaluation = RuleEngine.Evaluate(caseState, ruleSet.Rules);
                var selectedRule = evaluation.SelectedRule?.Rule;
                var decision = selectedRule?.Result.Decision ?? "NoMatch";
                var reason = evaluation.Reason;
                var feeAssessment = string.Equals(
                        selectedRule?.Result.Decision,
                        "ClaimValid",
                        StringComparison.Ordinal)
                    ? TransferFeeComplaintEvaluator.Evaluate(
                        configuration.Bank,
                        message.Content,
                        selectedRule)
                    : null;

                processing.PreProcessing = new PreProcessing
                {
                    CleanedText = preprocessing.Message,
                    CaseType = category ?? "Unknown",
                    StructuredCaseStateJson = JsonSerializer.Serialize(caseState)
                };
                processing.Rules = new Rules
                {
                    Decision = decision,
                    Reason = reason,
                    Bank = configuration.Bank,
                    EvaluationTraceJson = JsonSerializer.Serialize(evaluation.EvaluationTrace)
                };
                processing.ResponsePlan = CreateResponsePlan(
                    selectedRule,
                    decision,
                    reason);
                await _processingRepository.SaveChangesAsync(cancellationToken);

                var responseValidationRequirements = ResponseContentValidator.GetRequirements(selectedRule);
                var ruleDecision = selectedRule?.Result.Decision;
                var inputContext = new Dictionary<string, object?>
                {
                    ["case_state"] = caseState,
                    ["decision"] = ruleDecision,
                    ["reason"] = reason,
                    ["rule_result"] = selectedRule?.Result,
                    ["bank"] = configuration.Bank,
                    ["complaint_assessment"] = feeAssessment,
                    ["response_plan"] = new
                    {
                        decision = ruleDecision,
                        reason = processing.ResponsePlan.Reason,
                        required_elements = JsonSerializer.Deserialize<JsonElement>(
                            processing.ResponsePlan.RequiredElementsJson),
                        response_structure = JsonSerializer.Deserialize<JsonElement>(
                            processing.ResponsePlan.ResponseStructureJson),
                        sources = JsonSerializer.Deserialize<JsonElement>(
                            processing.ResponsePlan.SourcesJson),
                        validation_requirements = responseValidationRequirements
                    }
                };
                if (feeAssessment is not null)
                {
                    inputContext["customer_response_instruction"] =
                        "Use the deterministic complaint_assessment as the sole source of the fee conclusion. Include its CustomerMessage verbatim. Do not claim a bank-system fault or apologize for one unless independently verified.";
                }

                AiGenerationResult? generation = null;
                string responseText;
                if (string.Equals(configuration.GenerationMode, "Template", StringComparison.OrdinalIgnoreCase))
                {
                    responseText = TemplateResponseRenderer.Render(
                        await _textFileService.GetResponseTemplateSetAsync(
                            configuration.TemplateVersion!,
                            cancellationToken),
                        decision,
                        reason,
                        feeAssessment?.CustomerMessage ?? string.Empty,
                        processing.ResponsePlan.RequiredElementsJson,
                        processing.ResponsePlan.ResponseStructureJson,
                        processing.ResponsePlan.SourcesJson);
                }
                else if (string.Equals(configuration.GenerationMode, "Llm", StringComparison.OrdinalIgnoreCase))
                {
                    if (selectedRule is null ||
                        string.IsNullOrWhiteSpace(selectedRule.Id) ||
                        string.IsNullOrWhiteSpace(selectedRule.Result.CustomerDecision) ||
                        string.IsNullOrWhiteSpace(selectedRule.Result.CustomerReason))
                    {
                        throw new InvalidDataException(
                            "The selected rule must provide an ID, customer decision and customer reason for AI generation.");
                    }

                    var customerDecision = selectedRule.Result.CustomerDecision;
                    var customerReason = selectedRule.Result.CustomerReason;
                    if (string.Equals(ruleDecision, "BankTransferFeesGuidance", StringComparison.Ordinal) ||
                        string.Equals(ruleDecision, "RaiffeisenSavingsTransferFeeClarification", StringComparison.Ordinal))
                    {
                        var requireDate = string.Equals(configuration.Bank, "otp", StringComparison.OrdinalIgnoreCase);
                        var assessment = TransferFeeComplaintEvaluator.Evaluate(
                            configuration.Bank, message.Content, selectedRule, requireTransactionDate: requireDate)
                            ?? throw new InvalidDataException(
                                "No transfer fee evaluator is available for the selected guidance rule.");
                        (customerDecision, customerReason) = GetTransferFeeFacts(assessment);
                    }

                    string? modelName = null;
                    Dictionary<string, JsonElement>? modelParameters = null;
                    if (RequiresLlmModel(ruleDecision))
                    {
                        if (!string.IsNullOrWhiteSpace(configuration.ModelName))
                        {
                            var availableModels = await _aiServiceClient.GetModelsAsync(cancellationToken);
                            modelName = ResolveModelName(configuration.ModelName, availableModels);
                        }
                        modelParameters = configuration.ModelParametersJson is null
                            ? null
                            : JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                                configuration.ModelParametersJson);
                    }

                    generation = await _aiServiceClient.GenerateAsync(
                        new AiGenerationRequest(
                            inputContext,
                            preprocessing.Message,
                            preprocessing.Facts,
                            category,
                            customerDecision,
                            customerReason,
                            [selectedRule.Id],
                            configuration.PromptVersion,
                            modelName,
                            modelParameters),
                        cancellationToken);
                    var llmResponse = feeAssessment is not null
                        ? $"Tisztelt Ügyfelünk!{Environment.NewLine}{Environment.NewLine}{feeAssessment.CustomerMessage}"
                        : generation.Response;
                    responseText = AppendSourceCitations(
                        llmResponse,
                        processing.ResponsePlan.SourcesJson);
                }
                else
                {
                    throw new InvalidDataException(
                        $"Unsupported response generation mode '{configuration.GenerationMode}'.");
                }

                var validationResult = ResponseContentValidator.Validate(
                    responseText,
                    responseValidationRequirements,
                    caseState);
                if (validationResult.CheckedCount > 0)
                {
                    processing.EvaluationResults.Add(new EvaluationResult
                    {
                        Metric = "required_content_coverage",
                        Score = validationResult.Coverage,
                        Unit = "0-1",
                        Comment = JsonSerializer.Serialize(new
                        {
                            status = validationResult.IsComplete ? "complete" : "incomplete",
                            checkedCount = validationResult.CheckedCount,
                            missingRequirements = validationResult.MissingRequirements
                        })
                    });
                }

                if (generation?.ReferenceSimilarity is decimal referenceSimilarity &&
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
                    ResponseText = responseText,
                    TemplateVersion = configuration.TemplateVersion,
                    PromptVersion = generation?.PromptVersion,
                    ModelName = generation?.ModelName,
                    ModelParametersJson = generation?.ModelParameters is null
                        ? null
                        : JsonSerializer.Serialize(generation.ModelParameters),
                    InputContextJson = JsonSerializer.Serialize(inputContext),
                    ProcessingTimeMs = (int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue)
                };
                await _processingRepository.SaveChangesAsync(cancellationToken);
            }

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
                var persistedErrorMessage = exception is AiServiceException aiException
                    ? GetProcessingErrorMessage(aiException)
                    : exception.Message;
                processing.Fail(
                    errorType,
                    persistedErrorMessage,
                    completionWasNotPersisted);
                try
                {
                    var failureSaved = await _processingRepository.MarkFailedAsync(
                        processing.Id,
                        errorType,
                        persistedErrorMessage,
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

    private async Task ProcessRagAsync(
        Processing processing,
        Message message,
        Configuration configuration,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        var preprocessing = await _aiServiceClient.PreprocessAsync(message.Content, configuration.Bank, cancellationToken);
        if (preprocessing.PartialSuccess)
        {
            _logger.LogWarning(
                "Preprocessing for processing {ProcessingId} partially succeeded. Warnings: {Warnings}; details: {WarningDetails}",
                processing.Id, string.Join(", ", preprocessing.Warnings), JsonSerializer.Serialize(preprocessing.WarningDetails));
        }

        processing.PreProcessing = new PreProcessing
        {
            CleanedText = preprocessing.Message,
            CaseType = preprocessing.Category ?? "Unknown",
            StructuredCaseStateJson = JsonSerializer.Serialize(preprocessing.CaseState)
        };
        await _processingRepository.SaveChangesAsync(cancellationToken);

        var bank = configuration.Bank.ToLowerInvariant() switch
        {
            "otp" => "OTP",
            "raiffeisen" => "Raiffeisen",
            _ => throw new InvalidDataException($"Unsupported bank '{configuration.Bank}'.")
        };
        var modelName = configuration.ModelName;
        if (!string.IsNullOrWhiteSpace(modelName))
            modelName = ResolveModelName(modelName, await _aiServiceClient.GetModelsAsync(cancellationToken));

        var request = new AiRagGenerationRequest(
            preprocessing.Message,
            bank,
            preprocessing.Facts,
            preprocessing.Category,
            configuration.RetrievalTopK,
            configuration.RetrievalMinSimilarity,
            modelName,
            configuration.ModelParametersJson is null
                ? null
                : JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(configuration.ModelParametersJson));
        var generation = await _aiServiceClient.GenerateRagAsync(request, cancellationToken);
        if (generation.ReferenceSimilarity is decimal similarity && generation.ReferenceSimilarityMetric is string metric)
        {
            processing.EvaluationResults.Add(new EvaluationResult
            {
                Metric = "reference_similarity",
                Score = similarity,
                Unit = "0-1",
                Comment = metric
            });
        }

        processing.Response = new Response
        {
            GenerationMode = configuration.GenerationMode,
            ResponseText = generation.Response,
            PromptVersion = generation.PromptVersion,
            ModelName = generation.ModelName,
            ModelParametersJson = JsonSerializer.Serialize(generation.ModelParameters),
            InputContextJson = JsonSerializer.Serialize(request),
            RagSourcesJson = JsonSerializer.Serialize(generation.Sources),
            ProcessingTimeMs = (int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue)
        };
        await _processingRepository.SaveChangesAsync(cancellationToken);
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
                        processing.Rules.Bank,
                        JsonSerializer.Deserialize<IReadOnlyList<RuleEvaluationTrace>>(
                            processing.Rules.EvaluationTraceJson) ?? []),
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
                        processing.Response.ProcessingTimeMs,
                        processing.Response.RagSourcesJson is null
                            ? null
                            : (JsonSerializer.Deserialize<List<AiRagSource>>(processing.Response.RagSourcesJson)
                                ?? throw new InvalidDataException("The stored RAG sources are invalid."))
                                .Select(source => new RagSourceResponse(source.SourceId, source.DocumentName,
                                    source.ChunkIndex, source.Text, source.StartChar, source.EndChar, source.Similarity))
                                .ToList()),
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
        if (ruleSet.Rules is null || ruleSet.Rules.Count == 0)
        {
            throw new InvalidDataException("The business rule set contains no rules.");
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
                    "The business rule set contains a rule with missing decision.");
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

    private static ResponsePlan CreateResponsePlan(
        Rule? selectedRule,
        string decision,
        string reason)
    {
        var resultData = selectedRule?.Result.AdditionalData
            ?? new Dictionary<string, JsonElement>();
        var responseStructure = resultData
            .Where(item =>
                !string.Equals(item.Key, "requiredElements", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(item.Key, "transferFeeAssessment", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(item => item.Key, item => item.Value);
        var requiredElements = GetRequiredElements(resultData, responseStructure);

        var sources = selectedRule is null ||
            string.Equals(selectedRule.Id, "HU-BANKING-001", StringComparison.OrdinalIgnoreCase)
            ? Array.Empty<string>()
            : new[] { selectedRule.Source, selectedRule.EffectiveFrom }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
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

    private static (string Decision, string Reason) GetTransferFeeFacts(
        TransferFeeComplaintAssessment assessment)
    {
        if (assessment.Status == TransferFeeAssessmentStatus.InsufficientData)
        {
            if (assessment.MissingInformation.Count == 0)
            {
                throw new InvalidDataException("A transfer fee assessment must identify missing information.");
            }

            return ("Rule Not Found",
                "A díj összevetéséhez nem áll rendelkezésre elegendő adat: " +
                string.Join(", ", assessment.MissingInformation) + ".");
        }

        if (assessment.ExpectedFeeHuf is not decimal expected ||
            assessment.ChargedFeeHuf is not decimal charged)
        {
            throw new InvalidDataException("A completed transfer fee assessment must contain both fees.");
        }

        var decision = assessment.Status switch
        {
            TransferFeeAssessmentStatus.TariffMismatchReported =>
                "A közölt terhelés magasabb a szabály szerint számított díjnál.",
            TransferFeeAssessmentStatus.TariffBelowPublished =>
                "A közölt terhelés alacsonyabb a szabály szerint számított díjnál.",
            TransferFeeAssessmentStatus.TariffMatches =>
                "A közölt terhelés megegyezik a szabály szerint számított díjjal.",
            _ => throw new InvalidDataException("The transfer fee assessment status is invalid.")
        };
        var culture = CultureInfo.GetCultureInfo("hu-HU");
        return (decision,
            $"A megadott adatok alapján a szabály szerinti díj {expected.ToString("0.##", culture)} Ft, " +
            $"a közölt terhelés {charged.ToString("0.##", culture)} Ft.");
    }

    private static string AppendSourceCitations(string response, string sourcesJson)
    {
        var sources = JsonSerializer.Deserialize<List<string>>(sourcesJson)
            ?? throw new InvalidDataException("Response plan sources must be a JSON string array.");
        if (sources.Count == 0)
        {
            return response;
        }

        var missingSources = sources
            .Select(source => source.Trim())
            .Where(source => source.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(source => !response.Contains(source, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (missingSources.Length == 0)
        {
            return response.Trim();
        }

        return $"{response.Trim()}{Environment.NewLine}{Environment.NewLine}Források:{Environment.NewLine}" +
            string.Join(Environment.NewLine, missingSources.Select(source => $"- {source}"));
    }

    private static string GetProcessingErrorMessage(AiServiceException exception)
    {
        var message = exception.FailureKind switch
        {
            AiServiceFailureKind.Timeout =>
                "A válasz előállítása időtúllépés miatt nem sikerült.",
            AiServiceFailureKind.Unavailable =>
                "A válaszszolgáltatás átmenetileg nem érhető el.",
            AiServiceFailureKind.ConfigurationError =>
                "A válasz előállításához szükséges szolgáltatás-beállítás érvénytelen.",
            _ => "A válaszszolgáltatás nem tudta feldolgozni a kérést."
        };
        var identifiers = new List<string>();
        if (!string.IsNullOrWhiteSpace(exception.ErrorCode))
        {
            identifiers.Add($"hibakód: {exception.ErrorCode}");
        }

        if (!string.IsNullOrWhiteSpace(exception.CorrelationId))
        {
            identifiers.Add($"azonosító: {exception.CorrelationId}");
        }

        return identifiers.Count == 0
            ? message
            : $"{message} ({string.Join(", ", identifiers)}).";
    }

    private static bool IsCardDeliveryRequest(string message)
    {
        var normalizedMessage = message.ToLowerInvariant();
        var mentionsCard = normalizedMessage.Contains("bankkárty", StringComparison.Ordinal) ||
            normalizedMessage.Contains("bankkartya", StringComparison.Ordinal);
        var mentionsDelivery = normalizedMessage.Contains("postai", StringComparison.Ordinal) ||
            normalizedMessage.Contains("kiszáll", StringComparison.Ordinal) ||
            normalizedMessage.Contains("kiszall", StringComparison.Ordinal) ||
            normalizedMessage.Contains("kézbes", StringComparison.Ordinal) ||
            normalizedMessage.Contains("kezbes", StringComparison.Ordinal) ||
            normalizedMessage.Contains("megérkez", StringComparison.Ordinal) ||
            normalizedMessage.Contains("megerkez", StringComparison.Ordinal) ||
            normalizedMessage.Contains("küldj", StringComparison.Ordinal) ||
            normalizedMessage.Contains("kuldj", StringComparison.Ordinal);
        return mentionsCard && mentionsDelivery;
    }

    private static bool IsCardDeliveryRule(Rule? rule) =>
        string.Equals(
            rule?.Id,
            "HU-OTP-CARD-DELIVERY-001",
            StringComparison.OrdinalIgnoreCase);

    private static bool RequiresLlmModel(string? ruleDecision) =>
        string.Equals(ruleDecision, "ClaimValid", StringComparison.Ordinal) ||
        string.Equals(ruleDecision, "ClaimInvalid", StringComparison.Ordinal) ||
        string.Equals(ruleDecision, "BankTransferFeesGuidance", StringComparison.Ordinal) ||
        string.Equals(ruleDecision, "RaiffeisenSavingsTransferFeeClarification", StringComparison.Ordinal);

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
    public async Task<ProcessingDetailsDto> GetProcessingDetailsAsync(int processingId, CancellationToken cancellationToken)
    {
        var processingDetails = new ProcessingDetailsDto();
        processingDetails.Processing = await _processingRepository.GetResultByIdAsync(processingId, cancellationToken);
        if (processingDetails.Processing is null)
            throw new InvalidOperationException($"Processing with ID {processingId} not found.");

        processingDetails.Configuration = processingDetails.Processing.Configuration;
        processingDetails.Preprocessing = processingDetails.Processing.PreProcessing;
        processingDetails.Message = processingDetails.Processing.InputMessage;
        processingDetails.Response = processingDetails.Processing.Response;
        if (processingDetails.Processing.Rules is null)
            throw new InvalidOperationException($"Rules for processing with ID {processingId} not found.");
        processingDetails.Rule = processingDetails.Processing.Rules;
        return processingDetails;
    }
}