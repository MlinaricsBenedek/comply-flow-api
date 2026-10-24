using comply_flow_api.Data;
using comply_flow_api.Models;
using Microsoft.EntityFrameworkCore;

namespace comply_flow_api.Repositories;

public class ProcessingRepository : IProcessingRepository
{
    private readonly AppDbContext _dbContext;

    public ProcessingRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(Processing processing, CancellationToken cancellationToken)
    {
        await _dbContext.Processings.AddAsync(processing, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        _dbContext.SaveChangesAsync(cancellationToken);

    public Task<Processing?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        _dbContext.Processings
            .AsNoTracking()
            .SingleOrDefaultAsync(processing => processing.Id == id, cancellationToken);
}