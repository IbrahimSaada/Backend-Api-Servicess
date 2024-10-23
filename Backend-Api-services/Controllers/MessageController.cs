using Backend_Api_services.Models.Data;
using Backend_Api_services.Models.DTOs.messageDto;
using Backend_Api_services.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;


namespace Backend_Api_services.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MessageController : ControllerBase
    {
        private readonly apiDbContext _context;

        public MessageController(apiDbContext context)
        {
            _context = context;
        }

        // Send a new message
        [HttpPost("create-message")]
        public async Task<IActionResult> CreateMessage([FromBody] CreateMessageDto dto)
        {
            // Ensure that either message_content or MediaUrls is provided
            if (string.IsNullOrWhiteSpace(dto.MessageContent) && (dto.MediaUrls == null || !dto.MediaUrls.Any()))
            {
                return BadRequest("Message content or media must be provided.");
            }

            // Create the message object
            var message = new Messages
            {
                chat_id = dto.ChatId,
                sender_id = dto.SenderId,
                message_type = dto.MessageType, // Can be 'text', 'image', etc.
                message_content = dto.MessageContent, // Nullable for media-only messages
                created_at = DateTime.UtcNow
            };

            // Add the message to the database
            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            // If media is provided, link it to the message
            if (dto.MediaUrls != null && dto.MediaUrls.Any())
            {
                foreach (var mediaUrl in dto.MediaUrls)
                {
                    var media = new Chat_Media
                    {
                        message_id = message.message_id,
                        media_url = mediaUrl,
                        media_type = dto.MessageType // Ensure correct media type
                    };
                    _context.ChatMedia.Add(media);
                }

                await _context.SaveChangesAsync(); // Save media entries
            }

            // Prepare the result DTO
            var result = new MessageDto
            {
                MessageId = message.message_id,
                ChatId = message.chat_id,
                SenderId = message.sender_id,
                MessageType = message.message_type,
                MessageContent = message.message_content, // Nullable for media-only messages
                CreatedAt = message.created_at,
                MediaUrls = dto.MediaUrls // Media URLs, can be null for text-only messages
            };

            return Ok(result);
        }


        // Fetch messages in a chat
        [HttpGet("get-messages/{chatId}")]
        public async Task<IActionResult> GetMessagesByChat(int chatId)
        {
            var messages = await _context.Messages
                .Where(m => m.chat_id == chatId)
                .Select(m => new MessageDto
                {
                    MessageId = m.message_id,
                    ChatId = m.chat_id,
                    SenderId = m.sender_id,
                    MessageType = m.message_type,
                    MessageContent = m.message_content,
                    CreatedAt = m.created_at,
                    ReadAt = m.read_at,
                    IsEdited = m.is_edited,
                    IsUnsent = m.is_unsent,
                    MediaUrls = m.MediaItems.Select(mi => mi.media_url).ToList()
                })
                .ToListAsync();

            return Ok(messages);
        }

        // Edit a message
        [HttpPost("edit-message")]
        public async Task<IActionResult> EditMessage([FromBody] EditMessageDto dto)
        {
            var message = await _context.Messages.FindAsync(dto.MessageId);

            if (message == null || message.is_unsent)
            {
                return NotFound();
            }

            message.message_content = dto.NewMessageContent;
            message.is_edited = true;

            await _context.SaveChangesAsync();
            return Ok();
        }

        // Unsend a message
        [HttpPost("unsend-message")]
        public async Task<IActionResult> UnsendMessage([FromBody] UnsendMessageDto dto)
        {
            var message = await _context.Messages.FindAsync(dto.MessageId);

            if (message == null || message.sender_id != dto.UserId)
            {
                return BadRequest("Message not found or user is not the sender");
            }

            message.is_unsent = true;

            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}
