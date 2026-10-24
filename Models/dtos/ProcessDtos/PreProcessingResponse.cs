using System.Text.Json;

namespace comply_flow_api.Models.dtos.ProcessDtos;

/// <summary>Preprocessing result for a processing run.</summary>
public sealed record PreProcessingResponse(
    int Id,
    int ProcessingRunId,
    string CleanedText,
    string CaseType,
    JsonElement StructuredCaseState);