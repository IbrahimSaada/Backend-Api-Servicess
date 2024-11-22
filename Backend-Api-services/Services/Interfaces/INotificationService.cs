﻿using Backend_Api_services.Models.DTOs;
using Backend_Api_services.Models.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Backend_Api_services.Services.Interfaces
{
    public interface INotificationService
    {
        Task SendNotificationAsync(NotificationRequest request);
        Task CreateNotificationAsync(Notification notification);
        Task<List<Notification>> GetUserNotificationsAsync(int userId);
        Task MarkAsReadAsync(int notificationId);
    }
}
