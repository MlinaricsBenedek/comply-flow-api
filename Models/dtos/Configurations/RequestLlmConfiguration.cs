using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace comply_flow_api.Models.dtos.Configurations;

public sealed class RequestLlmConfiguration
{
    [Required]
    [StringLength(100)]
    public required string Name { get; init; }

    [Required]
    [StringLength(50)]
    public required string RuleSetVersion { get; init; }

    [Required]
    [StringLength(50)]
    public required string PromptVersion { get; init; }

    [Required]
    [StringLength(100)]
    public required string ModelName { get; init; }

    [Required]
    public required Dictionary<string, JsonElement> ModelParameters { get; init; }
}