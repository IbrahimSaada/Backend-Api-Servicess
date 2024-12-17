using Backend_Api_services.Models.Data;
using Backend_Api_services.Models.Entites_Admin;
using Microsoft.EntityFrameworkCore;

public class BanService : IBanService
{
    private readonly apiDbContext _context;

    public BanService(apiDbContext context)
    {
        _context = context;
    }

    public async Task<bool> BanUserAsync(int userId, string reason, DateTime? expiresAt = null)
    {
        var user = await _context.users.FindAsync(userId);
        if (user == null) return false;

        // Deactivate any previously active bans
        var activeBans = await _context.banned_users
            .Where(b => b.user_id == userId && b.is_active)
            .ToListAsync();

        foreach (var ban in activeBans)
        {
            ban.is_active = false;
        }

        var newBan = new banned_users
        {
            user_id = userId,
            ban_reason = reason,
            expires_at = expiresAt,
            is_active = true
        };

        _context.banned_users.Add(newBan);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UnbanUserAsync(int userId)
    {
        var bans = await _context.banned_users
            .Where(b => b.user_id == userId && b.is_active)
            .ToListAsync();

        if (!bans.Any()) return false;

        foreach (var ban in bans)
        {
            ban.is_active = false;
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> IsUserBannedAsync(int userId)
    {
        return await _context.banned_users
            .AnyAsync(b => b.user_id == userId && b.is_active && (b.expires_at == null || b.expires_at > DateTime.UtcNow));
    }

    public async Task<(bool IsBanned, string BanReason, DateTime? BanExpiresAt)> GetBanDetailsAsync(int userId)
    {
        var activeBan = await _context.banned_users
            .Where(b => b.user_id == userId && b.is_active && (b.expires_at == null || b.expires_at > DateTime.UtcNow))
            .Select(b => new { b.ban_reason, b.expires_at })
            .FirstOrDefaultAsync();

        if (activeBan == null)
        {
            return (false, null, null);
        }

        return (true, activeBan.ban_reason, activeBan.expires_at);
    }
}
