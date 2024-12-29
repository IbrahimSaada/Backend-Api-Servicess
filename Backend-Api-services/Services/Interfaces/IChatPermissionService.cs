namespace Backend_Api_services.Services.Interfaces
{
    public interface IChatPermissionService
    {
        Task<ChatPermissionResult> CheckChatPermission(int senderId, int recipientId);
    }
}
