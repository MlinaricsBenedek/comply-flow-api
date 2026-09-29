using System.ComponentModel.DataAnnotations;

namespace comply_flow_api.Models.dtos.Conversations;

public sealed class RequestMessage
{
    [Required]
    public required string Content { get; init; }

    [Range(1, int.MaxValue)]
    public int ConfigurationId { get; init; }
}