using comply_flow_api.Models;
using comply_flow_api.Models.dtos.Configurations;
using comply_flow_api.Repositories;

namespace comply_flow_api.Services;

public interface IConfigurationService
{
    Task<ConfigurationResponse> CreateAsync(RequestConfiguration request, CancellationToken cancellationToken);
    Task<IReadOnlyList<ConfigurationResponse>> GetAllAsync(CancellationToken cancellationToken);
    Task<ConfigurationResponse?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<bool> UpdateAsync(int id, RequestConfiguration request, CancellationToken cancellationToken);
    Task<ConfigurationDeleteResult> DeleteAsync(int id, CancellationToken cancellationToken);
}

public class ConfigurationService : IConfigurationService
{
    private readonly IConfigurationRepository _configurationRepository;

    public ConfigurationService(IConfigurationRepository configurationRepository)
    {
        _configurationRepository = configurationRepository;
    }

    public async Task<ConfigurationResponse> CreateAsync(
        RequestConfiguration request,
        CancellationToken cancellationToken)
    {
        var configuration = new Configuration
        {
            Name = request.Name.Trim(),
            RuleSetVersion = request.RuleSetVersion.Trim(),
            GenerationMode = request.GenerationMode.Trim(),
            TemplateVersion = request.TemplateVersion,
            ModelName = request.ModelName,
            ModelParametersJson = request.ModelParametersJson,
            CreatedAt = DateTime.UtcNow
        };

        await _configurationRepository.AddAsync(configuration, cancellationToken);

        return ToResponse(configuration);
    }

    public async Task<IReadOnlyList<ConfigurationResponse>> GetAllAsync(CancellationToken cancellationToken)
    {
        var configurations = await _configurationRepository.GetAllAsync(cancellationToken);

        return configurations.Select(ToResponse).ToList();
    }

    public async Task<ConfigurationResponse?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var configuration = await _configurationRepository.GetByIdAsync(id, cancellationToken);

        return configuration is null ? null : ToResponse(configuration);
    }

    public async Task<bool> UpdateAsync(
        int id,
        RequestConfiguration request,
        CancellationToken cancellationToken)
    {
        var configuration = await _configurationRepository.GetByIdAsync(id, cancellationToken);

        if (configuration is null)
        {
            return false;
        }

        configuration.Name = request.Name.Trim();
        configuration.RuleSetVersion = request.RuleSetVersion.Trim();
        configuration.GenerationMode = request.GenerationMode.Trim();
        configuration.TemplateVersion = request.TemplateVersion;
        configuration.ModelName = request.ModelName;
        configuration.ModelParametersJson = request.ModelParametersJson;

        await _configurationRepository.UpdateAsync(configuration, cancellationToken);

        return true;
    }

    public Task<ConfigurationDeleteResult> DeleteAsync(int id, CancellationToken cancellationToken) =>
        _configurationRepository.DeleteAsync(id, cancellationToken);

    private static ConfigurationResponse ToResponse(Configuration configuration) =>
        new(
            configuration.Id,
            configuration.Name,
            configuration.RuleSetVersion,
            configuration.GenerationMode,
            configuration.TemplateVersion,
            configuration.ModelName,
            configuration.ModelParametersJson,
            configuration.CreatedAt);
}