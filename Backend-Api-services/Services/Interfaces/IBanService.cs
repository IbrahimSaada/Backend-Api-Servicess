﻿
using Backend_Api_services.Models.DTOs_Admin;

public interface IBanService
{
    Task<bool> BanUserAsync(int userId, string reason, DateTime? expiresAt = null);
    Task<bool> UnbanUserAsync(int userId);
    Task<bool> IsUserBannedAsync(int userId);

    Task<(bool IsBanned, string BanReason, DateTime? BanExpiresAt)> GetBanDetailsAsync(int userId);

    Task<(List<BannedUserDto> BannedUsers, int TotalCount)> GetAllBannedUsersAsync(int page, int pageSize);
}
