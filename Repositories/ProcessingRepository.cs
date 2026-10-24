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

    public Task<Processing?> GetResultByIdAsync(int id, CancellationToken cancellationToken) =>
        _dbContext.Processings
            .AsNoTracking()
            .AsSplitQuery()
            .Include(processing => processing.PreProcessing)
            .Include(processing => processing.Rules)
                .ThenInclude(rules => rules!.MatchedRules)
            .Include(processing => processing.ResponsePlan)
            .Include(processing => processing.Response)
            .Include(processing => processing.EvaluationResults)
            .SingleOrDefaultAsync(processing => processing.Id == id, cancellationToken);

    public async Task<bool> MarkFailedAsync(
        int processingId,
        string errorType,
        string errorMessage,
        DateTime finishedAt,
        CancellationToken cancellationToken)
    {
        var affectedRows = await _dbContext.Processings
            .Where(processing => processing.Id == processingId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(processing => processing.Status, ProcessingStatus.Failed)
                .SetProperty(processing => processing.ErrorType, errorType)
                .SetProperty(processing => processing.ErrorMessage, errorMessage)
                .SetProperty(processing => processing.FinishedAt, finishedAt),
                cancellationToken);

        return affectedRows == 1;
    }
}