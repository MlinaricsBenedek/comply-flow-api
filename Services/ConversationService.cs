using comply_flow_api.Models;
using comply_flow_api.Models.dtos.Conversations;
using comply_flow_api.Repositories;

namespace comply_flow_api.Services;

public interface IConversationService
{
	Task<ConversationResponse> CreateAsync(RequestConversation request, CancellationToken cancellationToken);
	Task<ConversationResponse?> GetByIdAsync(int id, CancellationToken cancellationToken);
}

public class ConversationService : IConversationService
{
	private const string InitialStatus = "Created";
	private readonly IConversationRepository _conversationRepository;

	public ConversationService(IConversationRepository conversationRepository)
	{
		_conversationRepository = conversationRepository;
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

	public async Task<ConversationResponse?> GetByIdAsync(int id, CancellationToken cancellationToken)
	{
		var conversation = await _conversationRepository.GetByIdAsync(id, cancellationToken);

		return conversation is null ? null : ToResponse(conversation);
	}

	private static ConversationResponse ToResponse(Conversation conversation) =>
		new(conversation.Id, conversation.Title, conversation.Status, conversation.CreatedAt);

}
