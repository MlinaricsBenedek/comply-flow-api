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
    public async Task<ActionResult<IReadOnlyList<ConfigurationResponse>>> GetConfigurations(
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

    [HttpPost]
    public async Task<ActionResult<ConfigurationResponse>> CreateConfiguration(
        RequestConfiguration request,
        CancellationToken cancellationToken)
    {
        var configuration = await _configurationService.CreateAsync(request, cancellationToken);

        return CreatedAtAction(
            nameof(GetConfigurationById),
            new { id = configuration.Id },
            configuration);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateConfiguration(
        int id,
        RequestConfiguration request,
        CancellationToken cancellationToken)
    {
        var updated = await _configurationService.UpdateAsync(id, request, cancellationToken);

        return updated ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteConfiguration(int id, CancellationToken cancellationToken)
    {
        var result = await _configurationService.DeleteAsync(id, cancellationToken);

        return result switch
        {
            ConfigurationDeleteResult.Deleted => NoContent(),
            ConfigurationDeleteResult.NotFound => NotFound(),
            ConfigurationDeleteResult.InUse => Conflict(new
            {
                message = "Configuration cannot be deleted while it is referenced by a processing."
            }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}