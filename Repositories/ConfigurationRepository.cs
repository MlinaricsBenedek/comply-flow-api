using comply_flow_api.Data;
using comply_flow_api.Models;
using Microsoft.EntityFrameworkCore;

namespace comply_flow_api.Repositories;

public class ConfigurationRepository : IConfigurationRepository
{
    private readonly AppDbContext _dbContext;

    public ConfigurationRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(Configuration configuration, CancellationToken cancellationToken)
    {
        await _dbContext.Configurations.AddAsync(configuration, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<List<Configuration>> GetAllAsync(CancellationToken cancellationToken) =>
        _dbContext.Configurations
            .AsNoTracking()
            .OrderByDescending(configuration => configuration.CreatedAt)
            .ThenByDescending(configuration => configuration.Id)
            .ToListAsync(cancellationToken);

    public Task<Configuration?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        _dbContext.Configurations
            .AsNoTracking()
            .SingleOrDefaultAsync(configuration => configuration.Id == id, cancellationToken);

    public async Task UpdateAsync(Configuration configuration, CancellationToken cancellationToken)
    {
        _dbContext.Configurations.Update(configuration);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ConfigurationDeleteResult> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var configuration = await _dbContext.Configurations
            .SingleOrDefaultAsync(configuration => configuration.Id == id, cancellationToken);

        if (configuration is null)
        {
            return ConfigurationDeleteResult.NotFound;
        }

        var isInUse = await _dbContext.Processings
            .AnyAsync(processing => processing.ConfigurationId == id, cancellationToken);

        if (isInUse)
        {
            return ConfigurationDeleteResult.InUse;
        }

        _dbContext.Configurations.Remove(configuration);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ConfigurationDeleteResult.Deleted;
    }
}