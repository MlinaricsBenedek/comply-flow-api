using comply_flow_api.Models;

namespace comply_flow_api.Repositories;

public interface IProcessingRepository
{
    Task AddAsync(Processing processing, CancellationToken cancellationToken);
    Task<Processing?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}