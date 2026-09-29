using comply_flow_api.Models;
using comply_flow_api.Models.dtos.Configurations;
using comply_flow_api.Repositories;
using System.Text.Json;

namespace comply_flow_api.Services;

public interface IConfigurationService
{
    Task<ConfigurationResponse> CreateTemplateAsync(
        RequestTemplateConfiguration request,
        CancellationToken cancellationToken);
    Task<ConfigurationResponse> CreateLlmAsync(
        RequestLlmConfiguration request,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<ConfigurationSummaryResponse>> GetAllAsync(CancellationToken cancellationToken);
    Task<ConfigurationResponse?> GetByIdAsync(int id, CancellationToken cancellationToken);
}

public class ConfigurationService : IConfigurationService
{
    private readonly IConfigurationRepository _configurationRepository;

    public ConfigurationService(IConfigurationRepository configurationRepository)
    {
        _configurationRepository = configurationRepository;
    }

    public async Task<ConfigurationResponse> CreateTemplateAsync(
        RequestTemplateConfiguration request,
        CancellationToken cancellationToken)
    {
        var configuration = new Configuration
        {
            Name = request.Name.Trim(),
            RuleSetVersion = request.RuleSetVersion.Trim(),
            GenerationMode = "Template",
            TemplateVersion = request.TemplateVersion.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        await _configurationRepository.AddAsync(configuration, cancellationToken);

        return ToResponse(configuration);
    }

    public async Task<ConfigurationResponse> CreateLlmAsync(
        RequestLlmConfiguration request,
        CancellationToken cancellationToken)
    {
        var configuration = new Configuration
        {
            Name = request.Name.Trim(),
            RuleSetVersion = request.RuleSetVersion.Trim(),
            GenerationMode = "Llm",
            PromptVersion = request.PromptVersion.Trim(),
            ModelName = request.ModelName.Trim(),
            ModelParametersJson = JsonSerializer.Serialize(request.ModelParameters),
            CreatedAt = DateTime.UtcNow
        };

        await _configurationRepository.AddAsync(configuration, cancellationToken);

        return ToResponse(configuration);
    }

    public async Task<IReadOnlyList<ConfigurationSummaryResponse>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        var configurations = await _configurationRepository.GetAllAsync(cancellationToken);

        return configurations
            .Select(configuration => new ConfigurationSummaryResponse(configuration.Id, configuration.Name))
            .ToList();
    }

    public async Task<ConfigurationResponse?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var configuration = await _configurationRepository.GetByIdAsync(id, cancellationToken);

        return configuration is null ? null : ToResponse(configuration);
    }

    private static ConfigurationResponse ToResponse(Configuration configuration) => new(
        configuration.Id,
        configuration.Name,
        configuration.RuleSetVersion,
        configuration.GenerationMode,
        configuration.TemplateVersion,
        configuration.PromptVersion,
        configuration.ModelName,
        configuration.ModelParametersJson is null
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(configuration.ModelParametersJson));
}