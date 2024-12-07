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


        // Fetch all chats for a user with last message and unread count
        [HttpGet("get-chats/{userId}")]
        public async Task<IActionResult> GetUserChats(int userId)
        {
            // Include Messages so we can determine lastMessage and unreadCount.
            // We'll retrieve all chats that are not soft-deleted for this user.
            var chats = await _context.Chats
                .Where(c =>
                    (c.user_initiator == userId && !c.is_deleted_by_initiator) ||
                    (c.user_recipient == userId && !c.is_deleted_by_recipient)
                )
                .Include(c => c.InitiatorUser)
                .Include(c => c.RecipientUser)
                .Include(c => c.Messages) // Include messages
                .ToListAsync();

            var chatDtos = new List<ChatDto>();

            foreach (var c in chats)
            {
                bool isInitiator = (c.user_initiator == userId);
                DateTime? deleteTimestamp = isInitiator ? c.deleted_at_initiator : c.deleted_at_recipient;

                // Filter messages based on deletion logic (similar to FetchMessages)
                var filteredMessages = c.Messages
                    .Where(m => !m.is_unsent) // Exclude unsent (deleted) messages
                    .Where(m =>
                    {
                        // Apply deletion timestamp filtering
                        if (deleteTimestamp.HasValue && m.created_at < deleteTimestamp.Value)
                            return false;
                        return true;
                    })
                    .ToList();

                // Find the last message (by created_at)
                var lastMsg = filteredMessages
                    .OrderByDescending(m => m.created_at)
                    .FirstOrDefault();

                string lastMessageText = lastMsg?.message_content ?? "";
                DateTime lastMessageTime = lastMsg?.created_at ?? c.created_at;

                // Count unread messages:
                // Unread = messages where sender != current user & read_at is null
                int unreadCount = filteredMessages
                    .Where(m => m.sender_id != userId && m.read_at == null)
                    .Count();

                var chatDto = new ChatDto
                {
                    ChatId = c.chat_id,
                    InitiatorUserId = c.user_initiator,
                    InitiatorUsername = c.InitiatorUser.fullname,
                    InitiatorProfilePic = c.InitiatorUser.profile_pic,
                    RecipientUserId = c.user_recipient,
                    RecipientUsername = c.RecipientUser.fullname,
                    RecipientProfilePic = c.RecipientUser.profile_pic,
                    CreatedAt = c.created_at,
                    deleted_at_initiator = c.deleted_at_initiator ?? default(DateTime),
                    deleted_at_recipient = c.deleted_at_recipient ?? default(DateTime),

                    // New fields:
                    LastMessage = lastMessageText,
                    LastMessageTime = lastMessageTime,
                    UnreadCount = unreadCount
                };

                chatDtos.Add(chatDto);
            }

            return Ok(chatDtos);
        }


        [HttpGet("{userId}/contacts")]
        public async Task<ActionResult> GetContacts(int userId, string search = "", int pageNumber = 1, int pageSize = 10)
        {
            // Fetch followers
            var followers = await _context.Followers
                .Where(f => f.followed_user_id == userId && !f.is_dismissed)
                .Select(f => new ViewContacsDto
                {
                    UserId = f.follower_user_id,
                    Fullname = f.Follower.fullname,
                    ProfilePicUrl = f.Follower.profile_pic,
                })
                .ToListAsync();

            // Fetch following
            var following = await _context.Followers
                .Where(f => f.follower_user_id == userId && !f.is_dismissed)
                .Select(f => new ViewContacsDto
                {
                    UserId = f.followed_user_id,
                    Fullname = f.User.fullname,
                    ProfilePicUrl = f.User.profile_pic,
                })
                .ToListAsync();

            // Combine both lists and remove duplicates based on UserId
            var allContacts = followers
                .Concat(following)
                .GroupBy(c => c.UserId)
                .Select(g => g.First())
                .ToList();

            // Apply search if a search term is provided
            if (!string.IsNullOrEmpty(search))
            {
                allContacts = allContacts
                    .Where(c => c.Fullname.Contains(search, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            // Calculate total records after search filtering
            var totalRecords = allContacts.Count;

            // Apply pagination in memory
            var paginatedContacts = allContacts
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Ok(new
            {
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize,
                Contacts = paginatedContacts
            });
        }


        // Soft delete a chat
        [HttpPost("delete-chat")]
        public async Task<IActionResult> DeleteChat([FromBody] DeleteChatDto dto)
        {
            var chat = await _context.Chats.FindAsync(dto.ChatId);

            if (chat == null)
            {
                return NotFound("Chat not found.");
            }

            if (chat.user_initiator == dto.UserId)
            {
                chat.is_deleted_by_initiator = true;
                chat.deleted_at_initiator = DateTime.UtcNow;
            }
            else if (chat.user_recipient == dto.UserId)
            {
                chat.is_deleted_by_recipient = true;
                chat.deleted_at_recipient = DateTime.UtcNow;
            }
            else
            {
                return BadRequest("User is not part of this chat.");
            }

            await _context.SaveChangesAsync();
            return Ok("Chat deleted successfully.");
        }

    }
}
