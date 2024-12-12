using Backend_Api_services.Models.Data;
using Microsoft.EntityFrameworkCore;

public class BlockService : IBlockService
{
    private readonly apiDbContext _context;
    public BlockService(apiDbContext context)
    {
        _context = context;
    }

    public async Task<(bool isBlocked, string reason)> IsBlockedAsync(int viewerUserId, int profileUserId)
    {
        // Check if viewer is blocked by profileUser
        bool viewerIsBlocked = await _context.blocked_users
            .AnyAsync(b => b.blocked_by_user_id == profileUserId && b.blocked_user_id == viewerUserId);

        // Check if profileUser is blocked by viewer
        bool profileUserIsBlocked = await _context.blocked_users
            .AnyAsync(b => b.blocked_by_user_id == viewerUserId && b.blocked_user_id == profileUserId);

        // Determine the reason
        if (viewerIsBlocked)
        {
            return (true, "You are blocked by the post owner.");
        }
        else if (profileUserIsBlocked)
        {
            return (true, "You have blocked the post owner.");
        }

        // Neither is blocked
        return (false, string.Empty);
    }


    public async Task HandleBlockAsync(int userId, int targetUserId)
    {
        // Remove any follow relationships
        var followRecords = await _context.Followers
            .Where(f => (f.followed_user_id == userId && f.follower_user_id == targetUserId)
                     || (f.followed_user_id == targetUserId && f.follower_user_id == userId))
            .ToListAsync();

        if (followRecords.Any())
        {
            _context.Followers.RemoveRange(followRecords);
        }

        // Remove or mark chats as deleted
        var chats = await _context.Chats
            .Where(c => (c.user_initiator == userId && c.user_recipient == targetUserId)
                     || (c.user_initiator == targetUserId && c.user_recipient == userId))
            .ToListAsync();

        if (chats.Any())
        {
            // If you want to delete completely:
            _context.Chats.RemoveRange(chats);

            // OR if you prefer to mark as deleted:
            /*
            foreach (var chat in chats)
            {
                chat.is_deleted_by_initiator = true;
                chat.is_deleted_by_recipient = true;
                chat.deleted_at_initiator = DateTime.UtcNow;
                chat.deleted_at_recipient = DateTime.UtcNow;
            }
            */
        }

        await _context.SaveChangesAsync();
    }

    public async Task HandleUnblockAsync(int userId, int targetUserId)
    {
        // Currently, no automatic restore is needed. 
        // Unblocking doesn't restore follows or chats automatically, 
        // but you could decide to handle something here if needed.
        await Task.CompletedTask;
    }
}
