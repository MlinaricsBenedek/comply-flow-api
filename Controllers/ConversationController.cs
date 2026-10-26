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

        [HttpGet("~/api/conversations")]
        public async Task<ActionResult<IReadOnlyList<ConversationResponse>>> GetConversations(
            CancellationToken cancellationToken)
        {
            var conversations = await _conversationService.GetAllAsync(cancellationToken);

            return Ok(conversations);
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

        [HttpGet("~/api/conversations/{conversationId:int}/messages")]
        [ProducesResponseType<ConversationMessagesResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ConversationMessagesResponse>> GetMessages(
            int conversationId,
            CancellationToken cancellationToken)
        {
            var messages = await _conversationService.GetMessagesAsync(
                conversationId,
                cancellationToken);

            return messages is null ? NotFound() : Ok(messages);
        }

        [HttpPost("~/api/conversations/{conversationId:int}/messages")]
        public async Task<IActionResult> CreateMessage(
            int conversationId,
            RequestMessage request,
            CancellationToken cancellationToken)
        {
            var result = await _conversationService.CreateMessageAsync(
                conversationId,
                request,
                cancellationToken);

            return result.Status switch
            {
                MessageCreationStatus.Created => StatusCode(StatusCodes.Status201Created, result.Message),
                MessageCreationStatus.ConversationNotFound => NotFound(new
                {
                    message = $"Conversation {conversationId} was not found."
                }),
                MessageCreationStatus.ConfigurationNotFound => NotFound(new
                {
                    message = $"Configuration {request.ConfigurationId} was not found."
                }),
                MessageCreationStatus.InvalidContent => BadRequest(new
                {
                    message = "Message content cannot be empty."
                }),
                _ => StatusCode(StatusCodes.Status500InternalServerError)
            };
        }
    }
}
