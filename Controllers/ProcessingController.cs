using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using comply_flow_api.Models.dtos.ProcessDtos;
using comply_flow_api.Services;

namespace comply_flow_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProcessingController : ControllerBase
    {
        private readonly IProcessingService _processingService;

        public ProcessingController(IProcessingService processingService)
        {
            _processingService = processingService;
        }

        [HttpGet("GetProcessingResult/{processingId:int}")]
        public async Task<ActionResult<ProcessingStatusResponse>> GetProcessingResult(
            int processingId,
            CancellationToken cancellationToken)
        {
            var processing = await _processingService.GetStatusAsync(processingId, cancellationToken);
            return processing is null ? NotFound() : Ok(processing);
        }
    }
}
