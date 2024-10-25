using Backend_Api_services.Models.Data;
using Backend_Api_services.Models.DTOs.chatDto;
using Backend_Api_services.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;


namespace Backend_Api_services.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly apiDbContext _context;

        public ChatController(apiDbContext context)
        {
            _context = context;
        }
        /*
        // Create a new chat this endpoint is depracted
        [HttpPost("create-chat")]
        public async Task<IActionResult> CreateChat([FromBody] CreateChatDto dto)
        {
            var followedUser = await _context.Followers
                .FirstOrDefaultAsync(f => f.followed_user_id == dto.RecipientUserId
                                       && f.follower_user_id == dto.InitiatorUserId
                                       && f.approval_status == "approved");

            if (followedUser == null)
            {
                return StatusCode(403, "You cannot chat with this user until they approve your follow request.");
            }

            // Proceed to create the chat if approved
            var chat = new Chat
            {
                user_initiator = dto.InitiatorUserId,
                user_recipient = dto.RecipientUserId,
                created_at = DateTime.UtcNow
            };

            _context.Chats.Add(chat);
            await _context.SaveChangesAsync();

            return Ok(new ChatDto
            {
                ChatId = chat.chat_id,
                InitiatorUserId = chat.user_initiator,
                RecipientUserId = chat.user_recipient,
                CreatedAt = chat.created_at
            });
        }
        */


        // Fetch all chats for a user
        [HttpGet("get-chats/{userId}")]
        public async Task<IActionResult> GetUserChats(int userId)
        {
            var chats = await _context.Chats
                .Where(c => (c.user_initiator == userId && !c.is_deleted_by_initiator)
                            || (c.user_recipient == userId && !c.is_deleted_by_recipient))
                .Select(c => new ChatDto
                {
                    ChatId = c.chat_id,
                    InitiatorUserId = c.user_initiator,
                    InitiatorUsername = c.InitiatorUser.fullname,
                    InitiatorProfilePic = c.InitiatorUser.profile_pic,
                    RecipientUserId = c.user_recipient,
                    RecipientUsername = c.RecipientUser.fullname,
                    RecipientProfilePic = c.RecipientUser.profile_pic,
                    CreatedAt = c.created_at
                })
                .ToListAsync();

            return Ok(chats);
        }

        /* this endpoint is depracted
        // Soft delete a chat
        [HttpPost("delete-chat")]
        public async Task<IActionResult> DeleteChat([FromBody] DeleteChatDto dto)
        {
            var chat = await _context.Chats.FindAsync(dto.ChatId);

            if (chat == null)
            {
                return NotFound();
            }

            if (chat.user_initiator == dto.UserId)
            {
                chat.is_deleted_by_initiator = true;
            }
            else if (chat.user_recipient == dto.UserId)
            {
                chat.is_deleted_by_recipient = true;
            }
            else
            {
                return BadRequest("User is not part of this chat");
            }

            await _context.SaveChangesAsync();
            return Ok();
        }
        */
    }
}
