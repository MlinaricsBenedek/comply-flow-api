using comply_flow_api.Models;

namespace comply_flow_api.Repositories;

public interface IConversationRepository
{
    Task AddAsync(Conversation conversation, CancellationToken cancellationToken);
    Task<Conversation?> GetByIdAsync(int id, CancellationToken cancellationToken);
}