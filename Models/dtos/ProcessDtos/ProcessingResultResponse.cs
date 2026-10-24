namespace comply_flow_api.Models.dtos.ProcessDtos;

public sealed record ProcessingResultResponse(
    int Id,
    string Status,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? FinishedAt,
    bool IsFinished,
    bool ResultsAvailable,
    string? AvailabilityMessage,
    ProcessingErrorResponse? Error,
    ProcessingArtifactsResponse? Results);

public sealed record ProcessingErrorResponse(string Type, string Message);

public sealed record ProcessingArtifactsResponse(
    PreProcessingResultResponse? PreProcessing,
    RuleDecisionResultResponse? RuleDecision,
    ResponsePlanResultResponse? ResponsePlan,
    GeneratedResponseResultResponse? Response,
    IReadOnlyList<EvaluationResultResponse> EvaluationResults);

public sealed record PreProcessingResultResponse(
    string CleanedText,
    string CaseType,
    string StructuredCaseStateJson);

public sealed record RuleDecisionResultResponse(
    string Decision,
    string Reason,
    string RuleSetVersion,
    IReadOnlyList<MatchedRuleResultResponse> MatchedRules);

public sealed record MatchedRuleResultResponse(bool Matched, string Reason);

public sealed record ResponsePlanResultResponse(
    string Decision,
    string Reason,
    string RequiredElementsJson,
    string ResponseStructureJson,
    string SourcesJson);

public sealed record GeneratedResponseResultResponse(
    string Text,
    string GenerationMode,
    string? TemplateVersion,
    string? PromptVersion,
    string? ModelName,
    int ProcessingTimeMs);

public sealed record EvaluationResultResponse(
    string Metric,
    decimal Score,
    string Unit,
    string? Comment);