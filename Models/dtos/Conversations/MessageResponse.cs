namespace comply_flow_api.Models.dtos.Conversations;

public sealed record MessageResponse(
    int Id,
    int ConversationId,
    int ProcessingId,
    string Role,
    string Content,
    DateTime CreatedAt);

/// <summary>A conversation's processing history in request and response form.</summary>
public sealed record ConversationMessagesResponse(
    int ConversationId,
    IReadOnlyList<ConversationExchangeResponse> Messages);

/// <summary>A user request, its generated response if available, and processing status.</summary>
public sealed record ConversationExchangeResponse(
    int ProcessingId,
    string Status,
    ConversationRequestMessageResponse Request,
    ConversationGeneratedMessageResponse? Response,
    ConversationMessageErrorResponse? Error);

/// <summary>A user message submitted for processing.</summary>
public sealed record ConversationRequestMessageResponse(
    int Id,
    string Content,
    DateTimeOffset CreatedAt);

/// <summary>The generated assistant response, when generation has completed.</summary>
public sealed record ConversationGeneratedMessageResponse(
    string Content,
    string GenerationMode,
    DateTimeOffset? CreatedAt);

/// <summary>An error recorded while processing a user request.</summary>
public sealed record ConversationMessageErrorResponse(string Type, string Message);