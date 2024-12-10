// IChatNotificationService.cs
using Backend_Api_services.Models.Data;
using Backend_Api_services.Services.Interfaces;
using System.Threading.Tasks;

namespace Backend_Api_services.Services.Interfaces
{
    public interface IChatNotificationService
    {
        Task NotifyUserOfNewMessageAsync(int recipientUserId, int senderUserId, string messageContent);
    }
}
