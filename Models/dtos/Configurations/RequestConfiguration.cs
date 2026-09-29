using System.ComponentModel.DataAnnotations;

namespace comply_flow_api.Models.dtos.Configurations;

public sealed class RequestConfiguration
{
    [Required]
    [StringLength(100)]
    public required string Name { get; init; }

    [Required]
    [StringLength(50)]
    public required string RuleSetVersion { get; init; }

    [Required]
    [StringLength(30)]
    public required string GenerationMode { get; init; }

    [StringLength(50)]
    public string? TemplateVersion { get; init; }

    [StringLength(100)]
    public string? ModelName { get; init; }

    public string? ModelParametersJson { get; init; }
}