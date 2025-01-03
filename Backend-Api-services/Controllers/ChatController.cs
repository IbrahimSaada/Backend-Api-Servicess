﻿using Backend_Api_services.Models.Data;
using Backend_Api_services.Models.DTOs;
using Backend_Api_services.Models.DTOs.chatDto;
using Backend_Api_services.Models.Entities;
using Backend_Api_services.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace Backend_Api_services.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [CheckBan]
    public class ChatController : ControllerBase
    {
        private readonly apiDbContext _context;
        private readonly SignatureService _signatureService;
        private readonly IBlockService _blockService;

        public ChatController(apiDbContext context, SignatureService signatureService, IBlockService blockService)
        {
            _context = context;
            _signatureService = signatureService;
            _blockService = blockService;
        }

        // Fetch all chats for a user with last message and unread count
        [HttpGet("get-chats/{userId}")]
        public async Task<IActionResult> GetUserChats(int userId)
        {
            // Extract the signature from headers
            var signature = Request.Headers["X-Signature"].FirstOrDefault();
            // Construct the dataToSign. Here we only have userId for simplicity.
            // If you want more complexity (like a timestamp), include it in the dataToSign as well.
            var dataToSign = $"{userId}";

            // Validate signature
            if (string.IsNullOrEmpty(signature) || !_signatureService.ValidateSignature(signature, dataToSign))
            {
                return Unauthorized("Invalid or missing signature.");
            }

            // Include Messages so we can determine lastMessage and unreadCount.
            var chats = await _context.Chats
                .Where(c =>
                    (c.user_initiator == userId && !c.is_deleted_by_initiator) ||
                    (c.user_recipient == userId && !c.is_deleted_by_recipient)
                )
                .Include(c => c.InitiatorUser)
                .Include(c => c.RecipientUser)
                .Include(c => c.Messages)
                .ToListAsync();

            var mutedUsers = await _context.muted_users
                .Where(m => m.muted_by_user_id == userId)
                .Select(m => m.muted_user_id)
                .ToListAsync();

            var chatDtos = new List<ChatDto>();

            foreach (var c in chats)
            {
                bool isInitiator = (c.user_initiator == userId);
                DateTime? deleteTimestamp = isInitiator ? c.deleted_at_initiator : c.deleted_at_recipient;

                var filteredMessages = c.Messages
                    .Where(m => !m.is_unsent)
                    .Where(m =>
                    {
                        if (deleteTimestamp.HasValue && m.created_at < deleteTimestamp.Value)
                            return false;
                        return true;
                    })
                    .ToList();

                var lastMsg = filteredMessages
                    .OrderByDescending(m => m.created_at)
                    .FirstOrDefault();

                string lastMessageText = lastMsg?.message_content ?? "";
                DateTime lastMessageTime = lastMsg?.created_at ?? c.created_at;

                int unreadCount = filteredMessages
                    .Where(m => m.sender_id != userId && m.read_at == null)
                    .Count();

                var otherUserId = isInitiator ? c.user_recipient : c.user_initiator;
                bool isMuted = mutedUsers.Contains(otherUserId);

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
                    LastMessage = lastMessageText,
                    LastMessageTime = lastMessageTime,
                    UnreadCount = unreadCount,
                    IsMuted = isMuted
                };

                chatDtos.Add(chatDto);
            }

            return Ok(chatDtos);
        }

        [HttpGet("{userId}/contacts")]
        public async Task<ActionResult> GetContacts(int userId, string search = "", int pageNumber = 1, int pageSize = 10)
        {
            // Extract the signature
            var signature = Request.Headers["X-Signature"].FirstOrDefault();

            // Include all query parameters in dataToSign to ensure they haven't been tampered with
            // For example: userId:search:pageNumber:pageSize
            // If search can contain colons, you may want a different delimiter or URL-encode it first.
            var dataToSign = $"{userId}:{search}:{pageNumber}:{pageSize}";

            if (string.IsNullOrEmpty(signature) || !_signatureService.ValidateSignature(signature, dataToSign))
            {
                return Unauthorized("Invalid or missing signature.");
            }

            // If there's no search term, keep existing logic:
            if (string.IsNullOrWhiteSpace(search))
            {
                var followers = await _context.Followers
                    .Where(f => f.followed_user_id == userId && !f.is_dismissed)
                    .Select(f => new ViewContacsDto
                    {
                        UserId = f.follower_user_id,
                        Fullname = f.Follower.fullname,
                        ProfilePicUrl = f.Follower.profile_pic,
                    })
                    .ToListAsync();

                var following = await _context.Followers
                    .Where(f => f.follower_user_id == userId && !f.is_dismissed)
                    .Select(f => new ViewContacsDto
                    {
                        UserId = f.followed_user_id,
                        Fullname = f.User.fullname,
                        ProfilePicUrl = f.User.profile_pic,
                    })
                    .ToListAsync();

                // Union of followers & following
                var allContacts = followers
                    .Concat(following)
                    .GroupBy(c => c.UserId)
                    .Select(g => g.First())
                    .ToList();

                // Paginate
                var totalRecords = allContacts.Count;
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
            else
            {
                // If search term is provided, search across ALL users except the current user
                var query = _context.users
                    .Where(u => u.user_id != userId &&
                                EF.Functions.Like(u.fullname, $"%{search}%"));

                var totalRecords = await query.CountAsync();

                // Implement pagination
                var users = await query
                    .OrderBy(u => u.fullname)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(u => new ViewContacsDto
                    {
                        UserId = u.user_id,
                        Fullname = u.fullname,
                        ProfilePicUrl = u.profile_pic
                    })
                    .ToListAsync();

                return Ok(new
                {
                    TotalRecords = totalRecords,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    Contacts = users
                });
            }
        }

        // Soft delete a chat (and if both users have deleted it, remove it entirely)
        [HttpPost("delete-chat")]
        public async Task<IActionResult> DeleteChat([FromBody] DeleteChatDto dto)
        {
            var signature = Request.Headers["X-Signature"].FirstOrDefault();
            var dataToSign = $"{dto.UserId}:{dto.ChatId}";

            // Validate the signature
            if (string.IsNullOrEmpty(signature) || !_signatureService.ValidateSignature(signature, dataToSign))
            {
                return Unauthorized("Invalid or missing signature.");
            }

            // 2. Fetch the chat
            var chat = await _context.Chats.FindAsync(dto.ChatId);

            if (chat == null)
            {
                return NotFound("Chat not found.");
            }

            // 3. Identify the other user in the chat
            int otherUserId = chat.user_initiator == dto.UserId
                ? chat.user_recipient
                : chat.user_initiator;

            // 4. Check if blocked
            var (isBlocked, blockReason) = await _blockService.IsBlockedAsync(dto.UserId, otherUserId);
            if (isBlocked)
            {
                return StatusCode(403, $"Action not allowed: {blockReason}");
            }

            // 5. Perform soft delete based on the user's role in the chat
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

            // 6. If both users have deleted the chat, remove it permanently
            if (chat.is_deleted_by_initiator && chat.is_deleted_by_recipient)
            {
                // We can remove any messages, media, etc. associated with the chat
                var messages = _context.Messages
                    .Where(m => m.chat_id == chat.chat_id);

                _context.Messages.RemoveRange(messages);
                _context.Chats.Remove(chat);

                // Optionally remove Chat_Media rows or handle them as needed if you store them
                // For example:
                // var mediaItems = _context.ChatMedia.Where(cm => messages.Select(m => m.message_id).Contains(cm.message_id));
                // _context.ChatMedia.RemoveRange(mediaItems);
            }

            // 7. Save changes
            await _context.SaveChangesAsync();

            return Ok("Chat deleted successfully.");
        }

        [HttpPost("mute-user")]
        public async Task<IActionResult> MuteUser([FromBody] MuteUserDto dto)
        {
            // Validate the DTO
            if (dto == null || dto.MutedByUserId <= 0 || dto.MutedUserId <= 0)
            {
                return BadRequest(new { message = "Invalid request data." });
            }

            // Retrieve the signature from the request headers
            var signature = Request.Headers["X-Signature"].FirstOrDefault();

            // Generate the data to sign
            var dataToSign = $"MutedByUserId:{dto.MutedByUserId}|MutedUserId:{dto.MutedUserId}";

            // Validate the signature
            if (string.IsNullOrEmpty(signature) || !_signatureService.ValidateSignature(signature, dataToSign))
            {
                return Unauthorized(new { message = "Invalid or missing signature." });
            }

            // Check if the user is blocked
            var (isBlocked, blockReason) = await _blockService.IsBlockedAsync(dto.MutedByUserId, dto.MutedUserId);
            if (isBlocked)
            {
                return StatusCode(403, new { message = $"Action not allowed: {blockReason}" });
            }

            // Check if the user is already muted
            var existingMute = await _context.muted_users
                .FirstOrDefaultAsync(m => m.muted_by_user_id == dto.MutedByUserId && m.muted_user_id == dto.MutedUserId);

            if (existingMute != null)
            {
                return BadRequest(new { message = "User is already muted." });
            }

            // Create a new muted user entry
            var muteUser = new muted_users
            {
                muted_by_user_id = dto.MutedByUserId,
                muted_user_id = dto.MutedUserId,
                created_at = DateTime.UtcNow
            };

            _context.muted_users.Add(muteUser);
            await _context.SaveChangesAsync();

            return Ok(new { message = "User muted successfully." });
        }

        [HttpPost("unmute-user")]
        public async Task<IActionResult> UnmuteUser([FromBody] MuteUserDto dto)
        {
            // Validate the DTO
            if (dto == null || dto.MutedByUserId <= 0 || dto.MutedUserId <= 0)
            {
                return BadRequest(new { message = "Invalid request data." });
            }

            // Retrieve the signature from the request headers
            var signature = Request.Headers["X-Signature"].FirstOrDefault();

            // Generate the data to sign
            var dataToSign = $"MutedByUserId:{dto.MutedByUserId}|MutedUserId:{dto.MutedUserId}";

            // Validate the signature
            if (string.IsNullOrEmpty(signature) || !_signatureService.ValidateSignature(signature, dataToSign))
            {
                return Unauthorized(new { message = "Invalid or missing signature." });
            }

            // Check if the user is blocked
            var (isBlocked, blockReason) = await _blockService.IsBlockedAsync(dto.MutedByUserId, dto.MutedUserId);
            if (isBlocked)
            {
                return StatusCode(403, new { message = $"Action not allowed: {blockReason}" });
            }


            // Check if the user is muted
            var existingMute = await _context.muted_users
                .FirstOrDefaultAsync(m => m.muted_by_user_id == dto.MutedByUserId && m.muted_user_id == dto.MutedUserId);

            if (existingMute == null)
            {
                return NotFound(new { message = "User is not muted." });
            }

            // Remove the mute record
            _context.muted_users.Remove(existingMute);
            await _context.SaveChangesAsync();

            return Ok(new { message = "User unmuted successfully." });
        }

    }
}
