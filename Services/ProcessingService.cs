using comply_flow_api.Models;
using comply_flow_api.Repositories;

namespace comply_flow_api.Services;

public interface IProcessingService
{
    Task<ProcessingCreationResult> CreateForMessageAsync(
        Message message,
        int configurationId,
        CancellationToken cancellationToken);
}

public enum ProcessingCreationStatus
{
    Created,
    ConfigurationNotFound
}

public sealed record ProcessingCreationResult(
    ProcessingCreationStatus Status,
    Processing? Processing = null);

public class ProcessingService : IProcessingService
{
    private readonly IConfigurationRepository _configurationRepository;
    private readonly IProcessingRepository _processingRepository;

    public ProcessingService(
        IConfigurationRepository configurationRepository,
        IProcessingRepository processingRepository)
    {
        _configurationRepository = configurationRepository;
        _processingRepository = processingRepository;
    }

    public async Task<ProcessingCreationResult> CreateForMessageAsync(
        Message message,
        int configurationId,
        CancellationToken cancellationToken)
    {
        var configuration = await _configurationRepository.GetByIdAsync(
            configurationId,
            cancellationToken);
        if (configuration is null)
        {
            return new ProcessingCreationResult(ProcessingCreationStatus.ConfigurationNotFound);
        }

        var processing = new Processing
        {
            ConversationId = message.ConversationId,
            InputMessage = message,
            ConfigurationId = configurationId,
            Status = "Created",
            CreatedAt = DateTime.UtcNow
        };

        await _processingRepository.AddAsync(processing, cancellationToken);

        return new ProcessingCreationResult(ProcessingCreationStatus.Created, processing);
    }
}