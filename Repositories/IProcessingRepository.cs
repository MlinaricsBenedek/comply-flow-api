using comply_flow_api.Models;

namespace comply_flow_api.Repositories;

public interface IProcessingRepository
{
    Task AddAsync(Processing processing, CancellationToken cancellationToken);
}