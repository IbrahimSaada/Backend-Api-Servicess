using Backend_Api_services.Models.Data;
using Backend_Api_services.Services.Interfaces;
using Backend_Api_services.Models.Entities;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace Backend_Api_services.Services
{
    public enum ChatPermission
    {
        Allowed,
        MustMutualFollow,
        NotAllowed
    }

    public class ChatPermissionResult
    {
        public ChatPermission Permission { get; set; }
        public string Reason { get; set; } = "";
    }


    public class ChatPermissionService : IChatPermissionService
    {
        private readonly apiDbContext _context;
        private readonly IBlockService _blockService;

        public ChatPermissionService(apiDbContext context, IBlockService blockService)
        {
            _context = context;
            _blockService = blockService;
        }

        /// <summary>
        /// Checks whether 'senderId' can message 'recipientId'
        /// based purely on public/private statuses and the presence
        /// of a 'Followers' record. 'approval_status' is ignored.
        /// </summary>
        public async Task<ChatPermissionResult> CheckChatPermission(int senderId, int recipientId)
        {
            // 1. Check if blocked
            var (isBlocked, blockReason) = await _blockService.IsBlockedAsync(senderId, recipientId);
            if (isBlocked)
            {
                return new ChatPermissionResult
                {
                    Permission = ChatPermission.NotAllowed,
                    Reason = $"You are blocked or have blocked this user: {blockReason}"
                };
            }

            // 2. Find both users
            var sender = await _context.users.FindAsync(senderId);
            var recipient = await _context.users.FindAsync(recipientId);

            if (sender == null || recipient == null)
            {
                return new ChatPermissionResult
                {
                    Permission = ChatPermission.NotAllowed,
                    Reason = "Sender or recipient user not found in database."
                };
            }

            // 3. Public ↔ Public => Allowed
            if (sender.is_public && recipient.is_public)
            {
                return new ChatPermissionResult
                {
                    Permission = ChatPermission.Allowed
                };
            }

            // 4. Private ↔ Private => Must mutually follow (two follow rows)
            if (!sender.is_public && !recipient.is_public)
            {
                bool senderFollowsRecipient = await IsFollowing(senderId, recipientId);
                bool recipientFollowsSender = await IsFollowing(recipientId, senderId);

                if (senderFollowsRecipient && recipientFollowsSender)
                {
                    return new ChatPermissionResult
                    {
                        Permission = ChatPermission.Allowed
                    };
                }
                else
                {
                    return new ChatPermissionResult
                    {
                        Permission = ChatPermission.MustMutualFollow,
                        Reason = "Both users are private. They must mutually follow each other to chat."
                    };
                }
            }

            // 5. Private (sender) → Public (recipient) => Allowed
            if (!sender.is_public && recipient.is_public)
            {
                return new ChatPermissionResult
                {
                    Permission = ChatPermission.Allowed
                };
            }

            // 6. Public (sender) → Private (recipient) => Allowed only if private user follows public user
            bool privateUserFollowsPublic = await IsFollowing(recipientId, senderId);
            if (privateUserFollowsPublic)
            {
                return new ChatPermissionResult
                {
                    Permission = ChatPermission.Allowed
                };
            }
            else
            {
                // check if user already sent a message before
                bool hasSentBefore = await HasUserAlreadySentMessage(senderId, recipientId);
                if (hasSentBefore)
                {
                    return new ChatPermissionResult
                    {
                        Permission = ChatPermission.NotAllowed,
                        Reason = "You've already used your one free message. The private user must follow you now."
                    };
                }
                else
                {
                    // let them send 1 message
                    return new ChatPermissionResult
                    {
                        Permission = ChatPermission.Allowed,
                        Reason = "You can send only one message to this private user who doesn't follow you."
                    };
                }
            }

        }

        /// <summary>
        /// Checks if 'followerUserId' is following 'followedUserId' 
        /// simply by verifying if a row in 'Followers' exists.
        /// </summary>
        private async Task<bool> IsFollowing(int followerUserId, int followedUserId)
        {
            return await _context.Followers.AnyAsync(f =>
                f.follower_user_id == followerUserId &&
                f.followed_user_id == followedUserId
            );
        }

        private async Task<bool> HasUserAlreadySentMessage(int senderId, int recipientId)
        {
            return await _context.Messages.AnyAsync(m =>
                m.sender_id == senderId &&
                (
                  (m.Chats.user_initiator == senderId && m.Chats.user_recipient == recipientId) ||
                  (m.Chats.user_initiator == recipientId && m.Chats.user_recipient == senderId)
                )
            );
        }
    }
}
