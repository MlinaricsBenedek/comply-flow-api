namespace comply_flow_api.Models.dtos.ProcessDtos;

public sealed record ProcessingStatusResponse(
    int Id,
    string Status,
    string? ErrorType,
    string? ErrorMessage,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? FinishedAt);