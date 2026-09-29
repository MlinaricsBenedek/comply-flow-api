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

}