public interface IBlockService
{
    Task<(bool isBlocked, string reason)> IsBlockedAsync(int viewerUserId, int profileUserId);
    Task HandleBlockAsync(int userId, int targetUserId);
    Task HandleUnblockAsync(int userId, int targetUserId);

    Task<(bool viewerIsBlocked, bool profileUserIsBlocked, string reason)> GetBlockStatusAsync(int viewerUserId, int profileUserId);
}
