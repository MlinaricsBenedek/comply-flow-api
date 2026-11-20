using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace comply_flow_api.Models.dtos.Configurations;

public sealed class RequestRagConfiguration
{
    [Required]
    [StringLength(100)]
    public required string Name { get; init; }

    [Required]
    [RegularExpression(@"\A(otp|raiffeisen)\z", ErrorMessage = "Bank must be either 'otp' or 'raiffeisen'.")]
    public required string Bank { get; init; }

    [Range(1, int.MaxValue)]
    public int? RetrievalTopK { get; init; }

    [Range(0.0, 1.0)]
    public double? RetrievalMinSimilarity { get; init; }

    [StringLength(100, MinimumLength = 1)]
    public string? ModelName { get; init; }

    public Dictionary<string, JsonElement>? ModelParameters { get; init; }
}
