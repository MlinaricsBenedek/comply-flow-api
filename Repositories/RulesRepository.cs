using System;
using comply_flow_api.Data;
using comply_flow_api.Models;
using Microsoft.EntityFrameworkCore;

namespace comply_flow_api.Repositories;

public interface IRuleRepository
{
    Task<Rules> GetByIdAsync(int ruleId, CancellationToken cancellationToken);
}

public class RuleRepository : IRuleRepository
{
    private readonly AppDbContext _dbContext;

    public RuleRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    public async Task<Rules?> GetByIdAsync(int ruleId, CancellationToken cancellationToken)
    {
        var foundRule = await _dbContext.Rules.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == ruleId, cancellationToken);
        if (foundRule is null)
            throw new InvalidOperationException($"Rule with ID {ruleId} not found.");
        return foundRule;
    }
}
