using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace comply_flow_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProcessingController : ControllerBase
    {

        [HttpGet("GetProcessingResult/{processingId}")]
        public IActionResult GetProcessingResult(string processingId)
        {
            // Implementation for getting processing result
            return Ok();
        }
    }
}
