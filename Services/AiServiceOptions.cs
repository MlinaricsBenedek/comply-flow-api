using System.ComponentModel.DataAnnotations;

namespace comply_flow_api.Services;

public sealed class AiServiceOptions
{
    public const string SectionName = "AiService";

    [Required]
    [Url]
    public string BaseUrl { get; set; } = string.Empty;

    [Range(1, 3600)]
    public int ModelsTimeoutSeconds { get; set; } = 10;

    [Range(1, 3600)]
    public int PreprocessingTimeoutSeconds { get; set; } = 60;

    [Range(1, 3600)]
    public int GenerationTimeoutSeconds { get; set; } = 180;
}