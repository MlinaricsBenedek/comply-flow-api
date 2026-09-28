using comply_flow_api.Data;
using comply_flow_api.Models;
using Microsoft.EntityFrameworkCore;

namespace comply_flow_api.Repositories;

public class ConversationRepository : IConversationRepository
{
    private readonly AppDbContext _dbContext;

    public ConversationRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(Conversation conversation, CancellationToken cancellationToken)
    {
        await _dbContext.Conversations.AddAsync(conversation, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<Conversation?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        _dbContext.Conversations
            .AsNoTracking()
            .SingleOrDefaultAsync(conversation => conversation.Id == id, cancellationToken);
}