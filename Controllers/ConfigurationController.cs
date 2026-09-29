using comply_flow_api.Models.dtos.Configurations;
using comply_flow_api.Repositories;
using comply_flow_api.Services;
using Microsoft.AspNetCore.Mvc;

namespace comply_flow_api.Controllers;

[Route("api/configurations")]
[ApiController]
public class ConfigurationController : ControllerBase
{
    private readonly IConfigurationService _configurationService;

    public ConfigurationController(IConfigurationService configurationService)
    {
        _configurationService = configurationService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ConfigurationSummaryResponse>>> GetConfigurations(
        CancellationToken cancellationToken)
    {
        var configurations = await _configurationService.GetAllAsync(cancellationToken);

        return Ok(configurations);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ConfigurationResponse>> GetConfigurationById(
        int id,
        CancellationToken cancellationToken)
    {
        var configuration = await _configurationService.GetByIdAsync(id, cancellationToken);

        return configuration is null ? NotFound() : Ok(configuration);
    }

    [HttpPost("template")]
    public async Task<ActionResult<ConfigurationResponse>> CreateTemplateConfiguration(
        RequestTemplateConfiguration request,
        CancellationToken cancellationToken)
    {
        var configuration = await _configurationService.CreateTemplateAsync(request, cancellationToken);

        return CreatedAtAction(
            nameof(GetConfigurationById),
            new { id = configuration.Id },
            configuration);
    }

    [HttpPost("llm")]
    public async Task<ActionResult<ConfigurationResponse>> CreateLlmConfiguration(
        RequestLlmConfiguration request,
        CancellationToken cancellationToken)
    {
        var configuration = await _configurationService.CreateLlmAsync(request, cancellationToken);

        return CreatedAtAction(
            nameof(GetConfigurationById),
            new { id = configuration.Id },
            configuration);
    }
}