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

namespace Backend_Api_services.Hubs
{
    [Authorize] // Require authentication to access the hub
    public class ChatHub : Hub
    {
        // Thread-safe dictionary to map user IDs to connection IDs
        private static readonly ConcurrentDictionary<int, HashSet<string>> _connections = new ConcurrentDictionary<int, HashSet<string>>();

        private readonly apiDbContext _context;

        public ChatHub(apiDbContext context)
        {
            _context = context;
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

        // Method to fetch messages for a specific chat
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

            // Fetch messages with pagination, applying the deletion timestamp filter if necessary
            var messagesQuery = _context.Messages
                .Where(m => m.chat_id == chatId);

            // Apply the deletion timestamp filter to show only messages after the last deletion
            if (deleteTimestamp.HasValue)
            {
                messagesQuery = messagesQuery.Where(m => m.created_at >= deleteTimestamp.Value);

            }

            var messages = await messagesQuery
                .OrderByDescending(m => m.created_at) // Get messages in reverse chronological order
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
        public async Task SendMessage(int recipientUserId, string messageContent, string messageType, List<MediaItemDto> mediaItems)
        {
            int senderId = int.Parse(Context.UserIdentifier);

            // Check if the sender is allowed to chat with the recipient
            var followedUser = await _context.Followers
                .FirstOrDefaultAsync(f => f.followed_user_id == recipientUserId
                                           && f.follower_user_id == senderId
                                           && f.approval_status == "approved");

            if (followedUser == null)
            {
                await Clients.Caller.SendAsync("Error", "You cannot chat with this user until they approve your follow request.");
                return;
            }

            // Find or create the chat between the sender and recipient
            var chat = await _context.Chats.FirstOrDefaultAsync(c =>
                (c.user_initiator == senderId && c.user_recipient == recipientUserId) ||
                (c.user_initiator == recipientUserId && c.user_recipient == senderId));

            if (chat == null)
            {
                // Create a new chat if it doesn't exist
                chat = new Chat
                {
                    user_initiator = senderId,
                    user_recipient = recipientUserId,
                    created_at = DateTime.UtcNow
                };
                _context.Chats.Add(chat);
            }
            else
            {
                // Reset the deletion flag for the recipient
                if (chat.user_initiator == recipientUserId && chat.is_deleted_by_initiator)
                {
                    chat.is_deleted_by_initiator = false;
                }
                else if (chat.user_recipient == recipientUserId && chat.is_deleted_by_recipient)
                {
                    chat.is_deleted_by_recipient = false;
                }

                // Ensure that deletion timestamps are not modified
                _context.Entry(chat).Property(c => c.deleted_at_initiator).IsModified = false;
                _context.Entry(chat).Property(c => c.deleted_at_recipient).IsModified = false;
            }

            await _context.SaveChangesAsync();

            // Create the message
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

            // Add media items if any
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

            // Prepare the message DTO to send to clients
            var messageDto = new MessageDto
            {
                MessageId = message.message_id,
                ChatId = message.chat_id,
                SenderId = message.sender_id,
                MessageType = message.message_type,
                MessageContent = message.message_content,
                CreatedAt = message.created_at,
                MediaItems = mediaItems, // Include media items with media types
                IsEdited = message.is_edited,
                IsUnsent = message.is_unsent
            };

            // Send the message to the recipient if they are connected
            if (_connections.TryGetValue(recipientUserId, out var recipientConnections))
            {
                await Clients.Clients(recipientConnections).SendAsync("ReceiveMessage", messageDto);
            }

            // Send the message back to the sender
            if (_connections.TryGetValue(senderId, out var senderConnections))
            {
                await Clients.Clients(senderConnections).SendAsync("MessageSent", messageDto);
            }
        }

        // Method to handle typing indicators
        public async Task Typing(int recipientUserId)
        {
            int senderId = int.Parse(Context.UserIdentifier);

            if (_connections.TryGetValue(recipientUserId, out var recipientConnections))
            {
                await Clients.Clients(recipientConnections).SendAsync("UserTyping", senderId);
            }
        }

        // Method to mark messages as read
        public async Task MarkMessagesAsRead(int chatId)
        {
            int userId = int.Parse(Context.UserIdentifier);

            var messages = await _context.Messages
                .Where(m => m.chat_id == chatId && m.sender_id != userId && m.read_at == null)
                .ToListAsync();

            foreach (var message in messages)
            {
                message.read_at = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            // Notify the sender(s) that messages have been read
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
        public async Task CreateChat(int recipientUserId)
        {
            int initiatorUserId = int.Parse(Context.UserIdentifier);

            // Fetch recipient user's profile
            var recipientUser = await _context.users.FindAsync(recipientUserId);
            if (recipientUser == null)
            {
                await Clients.Caller.SendAsync("Error", "Recipient user not found.");
                return;
            }

            // If recipient's profile is private, verify follow approval
            if (!recipientUser.is_public)
            {
                var followRelationship = await _context.Followers
                    .FirstOrDefaultAsync(f =>
                        (f.followed_user_id == recipientUserId && f.follower_user_id == initiatorUserId && f.approval_status == "approved") ||
                        (f.followed_user_id == initiatorUserId && f.follower_user_id == recipientUserId && f.approval_status == "approved"));

                if (followRelationship == null)
                {
                    await Clients.Caller.SendAsync("Error", "You cannot chat with this user until they approve your follow request or you approve theirs.");
                    return;
                }
            }

            // Check if a chat already exists between these users
            var existingChat = await _context.Chats
                .FirstOrDefaultAsync(c =>
                    (c.user_initiator == initiatorUserId && c.user_recipient == recipientUserId) ||
                    (c.user_initiator == recipientUserId && c.user_recipient == initiatorUserId)
                );

            if (existingChat != null)
            {
                // The chat exists. Check if it was soft-deleted by the current user and restore if necessary.
                bool restored = false;

                // If the current user was the initiator and had soft-deleted the chat, restore it
                if (existingChat.user_initiator == initiatorUserId && existingChat.is_deleted_by_initiator)
                {
                    existingChat.is_deleted_by_initiator = false;
                    // Do NOT reset deleted_at_initiator; keep the timestamp so old messages stay filtered
                    restored = true;
                }

                // If the current user was the recipient and had soft-deleted the chat, restore it
                if (existingChat.user_recipient == initiatorUserId && existingChat.is_deleted_by_recipient)
                {
                    existingChat.is_deleted_by_recipient = false;
                    // Do NOT reset deleted_at_recipient; keep the timestamp so old messages stay filtered
                    restored = true;
                }

                if (restored)
                {
                    // Save changes and notify about the restored chat
                    await _context.SaveChangesAsync();

                    var chatDto = new ChatDto
                    {
                        ChatId = existingChat.chat_id,
                        InitiatorUserId = existingChat.user_initiator,
                        RecipientUserId = existingChat.user_recipient,
                        CreatedAt = existingChat.created_at
                    };

                    // Notify recipient that chat is restored (if they are connected)
                    if (_connections.TryGetValue(recipientUserId, out var recipientConnections))
                    {
                        await Clients.Clients(recipientConnections).SendAsync("ChatRestored", chatDto);
                    }

                    // Notify initiator that chat is restored
                    if (_connections.TryGetValue(initiatorUserId, out var initiatorConnections))
                    {
                        await Clients.Clients(initiatorConnections).SendAsync("ChatCreated", chatDto);
                    }

                    return;
                }
                else
                {
                    // The chat exists and is not soft-deleted by the current user.
                    // Therefore, we can't create a new one.
                    await Clients.Caller.SendAsync("Error", "Chat already exists with this user.");
                    return;
                }
            }

            // If no chat exists, create a new one
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

            // Send real-time notification to the recipient
            if (_connections.TryGetValue(recipientUserId, out var recipientConns))
            {
                await Clients.Clients(recipientConns).SendAsync("NewChatNotification", newChatDto);
            }

            // Notify initiator about the successful chat creation
            if (_connections.TryGetValue(initiatorUserId, out var initiatorConns))
            {
                await Clients.Clients(initiatorConns).SendAsync("ChatCreated", newChatDto);
            }
        }


        // Method to edit a message
        public async Task EditMessage(int messageId, string newContent)
        {
            int userId = int.Parse(Context.UserIdentifier);

            var message = await _context.Messages.Include(m => m.Chats).FirstOrDefaultAsync(m => m.message_id == messageId);

            if (message == null || message.sender_id != userId || message.is_unsent)
            {
                await Clients.Caller.SendAsync("Error", "Message not found or user is not the sender.");
                return;
            }

            message.message_content = newContent;
            message.is_edited = true;

            await _context.SaveChangesAsync();

            // Prepare the message DTO
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

            // Notify both sender and recipient about the edited message
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
        public async Task UnsendMessage(int messageId)
        {
            int userId = int.Parse(Context.UserIdentifier);

            var message = await _context.Messages.Include(m => m.Chats).FirstOrDefaultAsync(m => m.message_id == messageId);

            if (message == null || message.sender_id != userId)
            {
                await Clients.Caller.SendAsync("Error", "Message not found or user is not the sender.");
                return;
            }

            message.is_unsent = true;

            await _context.SaveChangesAsync();

            // Notify both sender and recipient about the unsent message
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
