using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using Backend_Api_services.Models.Data;
using Backend_Api_services.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Backend_Api_services.Models.DTOs.messageDto;
using Backend_Api_services.Models.DTOs.chatDto;
using Backend_Api_services.Services;
using Backend_Api_services.Services.Interfaces;

namespace Backend_Api_services.Hubs
{
    [Authorize] // Require authentication to access the hub
    [CheckBan]
    public class ChatHub : Hub
    {
        // Thread-safe dictionary to map user IDs to connection IDs
        private static readonly ConcurrentDictionary<int, HashSet<string>> _connections = new ConcurrentDictionary<int, HashSet<string>>();

        private readonly apiDbContext _context;
        private readonly SignatureService _signatureService;
        private readonly IChatNotificationService _chatNotificationService;
        private readonly IBlockService _blockService;
        private readonly IChatPermissionService _chatPermissionService;

        public ChatHub(apiDbContext context, SignatureService signatureService, IChatNotificationService chatNotificationService, IBlockService blockService, IChatPermissionService chatPermissionService)
        {
            _context = context;
            _signatureService = signatureService;
            _chatNotificationService = chatNotificationService;
            _blockService = blockService;
            _chatPermissionService = chatPermissionService;
        }

        // Method called when a user connects
        public override async Task OnConnectedAsync()
        {
            int userId = int.Parse(Context.UserIdentifier);

            // Add the connection ID to the user's set of connections
            _connections.AddOrUpdate(userId, new HashSet<string> { Context.ConnectionId }, (key, existingSet) =>
            {
                existingSet.Add(Context.ConnectionId);
                return existingSet;
            });

            // Update the user's online status in the database
            var status = await _context.OnlineStatus.FindAsync(userId);
            if (status != null)
            {
                status.is_online = true;
                status.last_seen = null;
            }
            else
            {
                status = new Online_Status
                {
                    user_id = userId,
                    is_online = true,
                    last_seen = null
                };
                _context.OnlineStatus.Add(status);
            }
            await _context.SaveChangesAsync();

            // Notify others that the user is online
            await Clients.Others.SendAsync("UserStatusChanged", userId, true);

            await base.OnConnectedAsync();
        }

        // Method called when a user disconnects
        public override async Task OnDisconnectedAsync(Exception exception)
        {
            int userId = int.Parse(Context.UserIdentifier);

            // Remove the connection ID from the user's set of connections
            if (_connections.TryGetValue(userId, out var connections))
            {
                connections.Remove(Context.ConnectionId);
                if (!connections.Any())
                {
                    _connections.TryRemove(userId, out _);

                    // Update the user's online status in the database
                    var status = await _context.OnlineStatus.FindAsync(userId);
                    if (status != null)
                    {
                        status.is_online = false;
                        status.last_seen = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                    }

                    // Notify others that the user is offline
                    await Clients.Others.SendAsync("UserStatusChanged", userId, false);
                }
            }

            await base.OnDisconnectedAsync(exception);
        }

        // Method to fetch messages for a specific chat (Read-only, so no signature required)
        public async Task<List<MessageDto>> FetchMessages(int chatId, int pageNumber = 1, int pageSize = 20)
        {
            int userId = int.Parse(Context.UserIdentifier);

            // Verify that the user is part of the chat
            var chat = await _context.Chats.FirstOrDefaultAsync(c =>
                c.chat_id == chatId &&
                (c.user_initiator == userId || c.user_recipient == userId)
            );

            if (chat == null)
            {
                // User is not part of the chat
                return new List<MessageDto>();
            }

            // Determine the appropriate deletion timestamp for message filtering
            DateTime? deleteTimestamp = null;
            if (chat.user_initiator == userId)
                deleteTimestamp = chat.deleted_at_initiator;
            else if (chat.user_recipient == userId)
                deleteTimestamp = chat.deleted_at_recipient;

            var messagesQuery = _context.Messages
                .Where(m => m.chat_id == chatId);

            if (deleteTimestamp.HasValue)
            {
                messagesQuery = messagesQuery.Where(m => m.created_at >= deleteTimestamp.Value);
            }

            var messages = await messagesQuery
                .OrderByDescending(m => m.created_at)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
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
                    MediaItems = m.MediaItems.Select(mi => new MediaItemDto
                    {
                        MediaUrl = mi.media_url,
                        MediaType = mi.media_type
                    }).ToList()
                })
                .ToListAsync();

            return messages;
        }

        // Method to send a message to a specific user
        public async Task SendMessage(
            int recipientUserId,
            string messageContent,
            string messageType,
            List<MediaItemDto> mediaItems,
            string signature
        )
        {
            int senderId = int.Parse(Context.UserIdentifier);

            // 1. Validate signature
            var dataToSign = $"{senderId}:{recipientUserId}:{messageContent}";
            if (string.IsNullOrEmpty(signature) || !_signatureService.ValidateSignature(signature, dataToSign))
            {
                await Clients.Caller.SendAsync("Error", "Invalid or missing signature.");
                return;
            }

            // 2. Check permission
            var permissionResult = await _chatPermissionService.CheckChatPermission(senderId, recipientUserId);
            if (permissionResult.Permission != ChatPermission.Allowed)
            {
                await Clients.Caller.SendAsync("Error", permissionResult.Reason);
                return;
            }

            // 3. Find or create chat
            var chat = await _context.Chats.FirstOrDefaultAsync(c =>
                (c.user_initiator == senderId && c.user_recipient == recipientUserId) ||
                (c.user_initiator == recipientUserId && c.user_recipient == senderId)
            );

            if (chat == null)
            {
                chat = new Chat
                {
                    user_initiator = senderId,
                    user_recipient = recipientUserId,
                    created_at = DateTime.UtcNow
                };
                _context.Chats.Add(chat);
                await _context.SaveChangesAsync();
            }
            else
            {
                // if it was soft-deleted, restore it
                if (chat.user_initiator == recipientUserId && chat.is_deleted_by_initiator)
                {
                    chat.is_deleted_by_initiator = false;
                }
                else if (chat.user_recipient == recipientUserId && chat.is_deleted_by_recipient)
                {
                    chat.is_deleted_by_recipient = false;
                }
                _context.Entry(chat).Property(c => c.deleted_at_initiator).IsModified = false;
                _context.Entry(chat).Property(c => c.deleted_at_recipient).IsModified = false;
            }

            await _context.SaveChangesAsync();

            // 4. Create the message
            var message = new Messages
            {
                chat_id = chat.chat_id,
                sender_id = senderId,
                message_type = messageType,
                message_content = messageContent,
                created_at = DateTime.UtcNow
            };
            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            // optional: attach media items
            if (mediaItems != null && mediaItems.Any())
            {
                foreach (var mediaItem in mediaItems)
                {
                    var media = new Chat_Media
                    {
                        message_id = message.message_id,
                        media_url = mediaItem.MediaUrl,
                        media_type = mediaItem.MediaType
                    };
                    _context.ChatMedia.Add(media);
                }
                await _context.SaveChangesAsync();
            }

            // 5. Broadcast
            var messageDto = new MessageDto
            {
                MessageId = message.message_id,
                ChatId = message.chat_id,
                SenderId = message.sender_id,
                MessageType = message.message_type,
                MessageContent = message.message_content,
                CreatedAt = message.created_at,
                MediaItems = mediaItems,
                IsEdited = message.is_edited,
                IsUnsent = message.is_unsent
            };

            if (_connections.TryGetValue(recipientUserId, out var recipientConnections))
            {
                await Clients.Clients(recipientConnections).SendAsync("ReceiveMessage", messageDto);
            }

            if (_connections.TryGetValue(senderId, out var senderConnections))
            {
                await Clients.Clients(senderConnections).SendAsync("MessageSent", messageDto);
            }
            // **Send Push Notification to the Recipient**
            await _chatNotificationService.NotifyUserOfNewMessageAsync(recipientUserId, senderId, messageContent);
        }

        // Method to handle typing indicators
        public async Task Typing(int recipientUserId, string signature)
        {
            int senderId = int.Parse(Context.UserIdentifier);

            // Validate signature
            var dataToSign = $"{senderId}:{recipientUserId}";
            if (string.IsNullOrEmpty(signature) || !_signatureService.ValidateSignature(signature, dataToSign))
            {
                await Clients.Caller.SendAsync("Error", "Invalid or missing signature.");
                return;
            }

            if (_connections.TryGetValue(recipientUserId, out var recipientConnections))
            {
                await Clients.Clients(recipientConnections).SendAsync("UserTyping", senderId);
            }
        }

        // Method to mark messages as read
        public async Task MarkMessagesAsRead(int chatId, string signature)
        {
            int userId = int.Parse(Context.UserIdentifier);

            // Validate signature
            var dataToSign = $"{userId}:{chatId}";
            if (string.IsNullOrEmpty(signature) || !_signatureService.ValidateSignature(signature, dataToSign))
            {
                await Clients.Caller.SendAsync("Error", "Invalid or missing signature.");
                return;
            }

            var messages = await _context.Messages
                .Where(m => m.chat_id == chatId && m.sender_id != userId && m.read_at == null)
                .ToListAsync();

            foreach (var message in messages)
            {
                message.read_at = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            var senderIds = messages.Select(m => m.sender_id).Distinct();

            foreach (var senderId in senderIds)
            {
                if (_connections.TryGetValue(senderId, out var senderConnections))
                {
                    await Clients.Clients(senderConnections).SendAsync("MessagesRead", chatId, userId);
                }
            }
        }

        // create chat
        public async Task CreateChat(int recipientUserId, string signature)
        {
            int initiatorUserId = int.Parse(Context.UserIdentifier);

            // 1. Validate signature
            var dataToSign = $"{initiatorUserId}:{recipientUserId}";
            if (string.IsNullOrEmpty(signature) || !_signatureService.ValidateSignature(signature, dataToSign))
            {
                await Clients.Caller.SendAsync("Error", "Invalid or missing signature.");
                return;
            }

            // 2. Check permission
            var permissionResult = await _chatPermissionService.CheckChatPermission(initiatorUserId, recipientUserId);
            if (permissionResult.Permission == ChatPermission.NotAllowed ||
                permissionResult.Permission == ChatPermission.MustMutualFollow)
            {
                await Clients.Caller.SendAsync("Error", permissionResult.Reason);
                return;
            }

            // 3. Check if a chat already exists
            var existingChat = await _context.Chats
                .FirstOrDefaultAsync(c =>
                    (c.user_initiator == initiatorUserId && c.user_recipient == recipientUserId) ||
                    (c.user_initiator == recipientUserId && c.user_recipient == initiatorUserId)
                );

            if (existingChat != null)
            {
                // Possibly restore if it's soft-deleted
                bool restored = false;
                if (existingChat.user_initiator == initiatorUserId && existingChat.is_deleted_by_initiator)
                {
                    existingChat.is_deleted_by_initiator = false;
                    restored = true;
                }
                if (existingChat.user_recipient == initiatorUserId && existingChat.is_deleted_by_recipient)
                {
                    existingChat.is_deleted_by_recipient = false;
                    restored = true;
                }

                if (restored)
                {
                    await _context.SaveChangesAsync();

                    var chatDto = new ChatDto
                    {
                        ChatId = existingChat.chat_id,
                        InitiatorUserId = existingChat.user_initiator,
                        RecipientUserId = existingChat.user_recipient,
                        CreatedAt = existingChat.created_at
                    };

                    // notify frontends
                    if (_connections.TryGetValue(recipientUserId, out var recConns))
                    {
                        await Clients.Clients(recConns).SendAsync("ChatRestored", chatDto);
                    }

                    if (_connections.TryGetValue(initiatorUserId, out var initConns))
                    {
                        await Clients.Clients(initConns).SendAsync("ChatCreated", chatDto);
                    }

                    return;
                }
                else
                {
                    await Clients.Caller.SendAsync("Error", "Chat already exists with this user.");
                    return;
                }
            }

            // 4. Create new chat
            var chat = new Chat
            {
                user_initiator = initiatorUserId,
                user_recipient = recipientUserId,
                created_at = DateTime.UtcNow
            };

            _context.Chats.Add(chat);
            await _context.SaveChangesAsync();

            var newChatDto = new ChatDto
            {
                ChatId = chat.chat_id,
                InitiatorUserId = chat.user_initiator,
                RecipientUserId = chat.user_recipient,
                CreatedAt = chat.created_at
            };

            // notify frontends
            if (_connections.TryGetValue(recipientUserId, out var recipientConns))
            {
                await Clients.Clients(recipientConns).SendAsync("NewChatNotification", newChatDto);
            }

            if (_connections.TryGetValue(initiatorUserId, out var initiatorConns))
            {
                await Clients.Clients(initiatorConns).SendAsync("ChatCreated", newChatDto);
            }
        }

        // Method to edit a message
        public async Task EditMessage(int messageId, string newContent, string signature)
        {
            int userId = int.Parse(Context.UserIdentifier);

            // Validate signature
            var dataToSign = $"{userId}:{messageId}:{newContent}";
            if (string.IsNullOrEmpty(signature) || !_signatureService.ValidateSignature(signature, dataToSign))
            {
                await Clients.Caller.SendAsync("Error", "Invalid or missing signature.");
                return;
            }

            var message = await _context.Messages.Include(m => m.Chats).FirstOrDefaultAsync(m => m.message_id == messageId);

            if (message == null || message.sender_id != userId || message.is_unsent)
            {
                await Clients.Caller.SendAsync("Error", "Message not found or user is not the sender.");
                return;
            }

            message.message_content = newContent;
            message.is_edited = true;

            await _context.SaveChangesAsync();

            var messageDto = new MessageDto
            {
                MessageId = message.message_id,
                ChatId = message.chat_id,
                SenderId = message.sender_id,
                MessageType = message.message_type,
                MessageContent = message.message_content,
                CreatedAt = message.created_at,
                IsEdited = true,
                IsUnsent = message.is_unsent,
                MediaUrls = message.MediaItems?.Select(mi => mi.media_url).ToList()
            };

            int recipientUserId = message.Chats.user_initiator == userId ? message.Chats.user_recipient : message.Chats.user_initiator;

            if (_connections.TryGetValue(recipientUserId, out var recipientConnections))
            {
                await Clients.Clients(recipientConnections).SendAsync("MessageEdited", messageDto);
            }

            if (_connections.TryGetValue(userId, out var senderConnections))
            {
                await Clients.Clients(senderConnections).SendAsync("MessageEdited", messageDto);
            }
        }

        // Method to unsend a message
        public async Task UnsendMessage(int messageId, string signature)
        {
            int userId = int.Parse(Context.UserIdentifier);

            // Validate signature
            var dataToSign = $"{userId}:{messageId}";
            if (string.IsNullOrEmpty(signature) || !_signatureService.ValidateSignature(signature, dataToSign))
            {
                await Clients.Caller.SendAsync("Error", "Invalid or missing signature.");
                return;
            }

            var message = await _context.Messages.Include(m => m.Chats).FirstOrDefaultAsync(m => m.message_id == messageId);

            if (message == null || message.sender_id != userId)
            {
                await Clients.Caller.SendAsync("Error", "Message not found or user is not the sender.");
                return;
            }

            message.is_unsent = true;
            await _context.SaveChangesAsync();

            int recipientUserId = message.Chats.user_initiator == userId ? message.Chats.user_recipient : message.Chats.user_initiator;

            if (_connections.TryGetValue(recipientUserId, out var recipientConnections))
            {
                await Clients.Clients(recipientConnections).SendAsync("MessageUnsent", messageId);
            }

            if (_connections.TryGetValue(userId, out var senderConnections))
            {
                await Clients.Clients(senderConnections).SendAsync("MessageUnsent", messageId);
            }
        }
    }
}
