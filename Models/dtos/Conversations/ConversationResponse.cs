namespace comply_flow_api.Models.dtos.Conversations;

/// <summary>Conversation returned by the API.</summary>
public sealed record ConversationResponse(
    int Id,
    string? Title,
    string Status,
    DateTime CreatedAt);