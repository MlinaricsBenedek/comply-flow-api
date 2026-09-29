namespace comply_flow_api.Models.dtos.Configurations;

public sealed record ConfigurationResponse(
    int Id,
    string Name,
    string RuleSetVersion,
    string GenerationMode,
    string? TemplateVersion,
    string? ModelName,
    string? ModelParametersJson,
    DateTime CreatedAt);