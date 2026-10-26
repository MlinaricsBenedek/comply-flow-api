using comply_flow_api.Models;
using comply_flow_api.Models.dtos.Conversations;
using comply_flow_api.Repositories;

namespace comply_flow_api.Services;

public interface IConversationService
{
	Task<ConversationResponse> CreateAsync(RequestConversation request, CancellationToken cancellationToken);
	Task<IReadOnlyList<ConversationResponse>> GetAllAsync(CancellationToken cancellationToken);
	Task<ConversationResponse?> GetByIdAsync(int id, CancellationToken cancellationToken);
	Task<ConversationMessagesResponse?> GetMessagesAsync(int conversationId, CancellationToken cancellationToken);
	Task<MessageCreationResult> CreateMessageAsync(
		int conversationId,
		RequestMessage request,
		CancellationToken cancellationToken);
}

public enum MessageCreationStatus
{
	Created,
	ConversationNotFound,
	ConfigurationNotFound,
	InvalidContent
}

public sealed record MessageCreationResult(MessageCreationStatus Status, MessageResponse? Message = null);

public class ConversationService : IConversationService
{
	private const string InitialStatus = "Created";
	private readonly IConversationRepository _conversationRepository;
	private readonly IProcessingService _processingService;

	public ConversationService(
		IConversationRepository conversationRepository,
		IProcessingService processingService)
	{
		_conversationRepository = conversationRepository;
		_processingService = processingService;
	}

	public async Task<ConversationResponse> CreateAsync(
		RequestConversation request,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(request.Title))
		{
			throw new ArgumentException("Conversation title cannot be empty.", nameof(request));
		}

		var conversation = new Conversation
		{
			Title = request.Title.Trim(),
			Status = InitialStatus,
			CreatedAt = DateTime.UtcNow
		};

		await _conversationRepository.AddAsync(conversation, cancellationToken);

		return ToResponse(conversation);
	}

	public async Task<IReadOnlyList<ConversationResponse>> GetAllAsync(CancellationToken cancellationToken)
	{
		var conversations = await _conversationRepository.GetAllAsync(cancellationToken);

		return conversations.Select(ToResponse).ToList();
	}

	public async Task<ConversationResponse?> GetByIdAsync(int id, CancellationToken cancellationToken)
	{
		var conversation = await _conversationRepository.GetByIdAsync(id, cancellationToken);

		return conversation is null ? null : ToResponse(conversation);
	}

	public async Task<ConversationMessagesResponse?> GetMessagesAsync(
		int conversationId,
		CancellationToken cancellationToken)
	{
		var conversation = await _conversationRepository.GetWithProcessingsAsync(
			conversationId,
			cancellationToken);
		if (conversation is null)
		{
			return null;
		}

		var messages = conversation.Processings
			.OrderBy(processing => processing.CreatedAt)
			.ThenBy(processing => processing.Id)
			.Select(processing => new ConversationExchangeResponse(
				processing.Id,
				processing.Status.ToString(),
				new ConversationRequestMessageResponse(
					processing.InputMessage.Id,
					processing.InputMessage.Content,
					ToUtcDateTimeOffset(processing.InputMessage.CreatedAt)),
				processing.Response is null
					? null
					: new ConversationGeneratedMessageResponse(
						processing.Response.ResponseText,
						processing.Response.GenerationMode,
						processing.FinishedAt is null
							? null
							: ToUtcDateTimeOffset(processing.FinishedAt.Value)),
				processing.ErrorType is null && processing.ErrorMessage is null
					? null
					: new ConversationMessageErrorResponse(
						processing.ErrorType ?? "ProcessingError",
						processing.ErrorMessage ?? "The processing failed without an error message.")))
			.ToList();

		return new ConversationMessagesResponse(conversation.Id, messages);
	}

	public async Task<MessageCreationResult> CreateMessageAsync(
		int conversationId,
		RequestMessage request,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(request.Content))
		{
			return new MessageCreationResult(MessageCreationStatus.InvalidContent);
		}

		var conversation = await _conversationRepository.GetByIdAsync(conversationId, cancellationToken);
		if (conversation is null)
		{
			return new MessageCreationResult(MessageCreationStatus.ConversationNotFound);
		}

		var message = new Message
		{
			ConversationId = conversationId,
			Role = "User",
			Content = request.Content.Trim(),
			CreatedAt = DateTime.UtcNow
		};

		var processingResult = await _processingService.CreateForMessageAsync(
			message,
			request.ConfigurationId,
			cancellationToken);
		if (processingResult.Status == ProcessingCreationStatus.ConfigurationNotFound)
		{
			return new MessageCreationResult(MessageCreationStatus.ConfigurationNotFound);
		}

		var processing = processingResult.Processing!;

		return new MessageCreationResult(
			MessageCreationStatus.Created,
			new MessageResponse(
				message.Id,
				message.ConversationId,
				processing.Id,
				message.Role,
				message.Content,
				message.CreatedAt));
	}

	private static ConversationResponse ToResponse(Conversation conversation) =>
		new(conversation.Id, conversation.Title, conversation.Status, conversation.CreatedAt);

	private static DateTimeOffset ToUtcDateTimeOffset(DateTime value) =>
		new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

}
