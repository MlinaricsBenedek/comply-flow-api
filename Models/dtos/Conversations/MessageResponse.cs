namespace comply_flow_api.Models.dtos.Conversations;

public sealed record MessageResponse(
    int Id,
    int ConversationId,
    int ProcessingId,
    string Role,
    string Content,
    DateTime CreatedAt);