using System.ComponentModel.DataAnnotations;

namespace comply_flow_api.Models.dtos.Configurations;

public sealed class RequestTemplateConfiguration
{
    [Required]
    [StringLength(100)]
    public required string Name { get; init; }

    [Required]
    [StringLength(50)]
    public required string RuleSetVersion { get; init; }

    [Required]
    [StringLength(50)]
    public required string TemplateVersion { get; init; }
}