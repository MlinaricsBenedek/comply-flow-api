using comply_flow_api.Models;
using System.Text.Json.Serialization;

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
    string Bank,
    IReadOnlyList<RuleEvaluationTrace> EvaluationTrace);

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
    int ProcessingTimeMs,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<RagSourceResponse>? Sources = null);

public sealed record RagSourceResponse(
    string? SourceId,
    string? DocumentName,
    int? ChunkIndex,
    string? Text,
    int? StartChar,
    int? EndChar,
    decimal? Similarity);

public sealed record EvaluationResultResponse(
    string Metric,
    decimal Score,
    string Unit,
    string? Comment);