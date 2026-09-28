using comply_flow_api.Models.dtos.Conversations;
using comply_flow_api.Services;
using Microsoft.AspNetCore.Mvc;

namespace comply_flow_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ConversationController : ControllerBase
    {
        private readonly IConversationService _conversationService;

        public ConversationController(IConversationService conversationService)
        {
            _conversationService = conversationService;
        }

        [HttpPost("conversation")]
        public async Task<ActionResult<ConversationResponse>> CreateConversation(
            RequestConversation request,
            CancellationToken cancellationToken)
        {
            var conversation = await _conversationService.CreateAsync(request, cancellationToken);

            return CreatedAtAction(
                nameof(GetConversationById),
                new { id = conversation.Id },
                conversation);
        }

        [HttpGet("conversation/{id:int}", Name = nameof(GetConversationById))]
        public async Task<ActionResult<ConversationResponse>> GetConversationById(
            int id,
            CancellationToken cancellationToken)
        {
            var conversation = await _conversationService.GetByIdAsync(id, cancellationToken);

            return conversation is null ? NotFound() : Ok(conversation);
        }
    }
}
