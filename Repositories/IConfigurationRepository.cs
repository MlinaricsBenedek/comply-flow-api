using comply_flow_api.Models;

namespace comply_flow_api.Repositories;

public interface IConfigurationRepository
{
    Task AddAsync(Configuration configuration, CancellationToken cancellationToken);
    Task<List<Configuration>> GetAllAsync(CancellationToken cancellationToken);
    Task<Configuration?> GetByIdAsync(int id, CancellationToken cancellationToken);
}