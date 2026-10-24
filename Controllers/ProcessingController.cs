using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using comply_flow_api.Models.dtos.ProcessDtos;
using comply_flow_api.Services;

namespace comply_flow_api.Controllers
{
    [Route("api")]
    [ApiController]
    public class ProcessingController : ControllerBase
    {
        private readonly IProcessingService _processingService;

        public ProcessingController(IProcessingService processingService)
        {
            _processingService = processingService;
        }

        [HttpGet("processings/{processingId:int}")]
        [HttpGet("processing/GetProcessingResult/{processingId:int}")]
        public async Task<ActionResult<ProcessingResultResponse>> GetProcessingResult(
            int processingId,
            CancellationToken cancellationToken)
        {
            var processing = await _processingService.GetResultAsync(processingId, cancellationToken);
            if (processing is null)
            {
                return NotFound();
            }

            return processing.IsFinished ? Ok(processing) : Accepted(processing);
        }
    }
}
