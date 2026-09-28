using System.ComponentModel.DataAnnotations;

namespace comply_flow_api.Models.dtos.Conversations;

public class RequestConversation
{
    [Required]
    [StringLength(200)]
    public required string Title { get; init; }
}
