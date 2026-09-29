using comply_flow_api.Models;

namespace comply_flow_api.Repositories;

public enum ConfigurationDeleteResult
{
    Deleted,
    NotFound,
    InUse
}

public interface IConfigurationRepository
{
    Task AddAsync(Configuration configuration, CancellationToken cancellationToken);
    Task<List<Configuration>> GetAllAsync(CancellationToken cancellationToken);
    Task<Configuration?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task UpdateAsync(Configuration configuration, CancellationToken cancellationToken);
    Task<ConfigurationDeleteResult> DeleteAsync(int id, CancellationToken cancellationToken);
}