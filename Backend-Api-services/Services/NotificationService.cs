using Backend_Api_services.Controllers;
using Backend_Api_services.Models.Data;
using Backend_Api_services.Models.DTOs;
using Backend_Api_services.Models.Entities;
using Backend_Api_services.Services.Interfaces;
using FirebaseAdmin.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Backend_Api_services.Services
{
    public class NotificationService : INotificationService
    {
        private readonly apiDbContext _context;
        private readonly ILogger<***REMOVED***> _logger;

        public NotificationService(apiDbContext context, ILogger<***REMOVED***> logger)
        {
            _context = context;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task SendNotificationAsync(NotificationRequest request)
        {
            var message = new Message
            {
                Token = request.Token,
                Notification = new FirebaseAdmin.Messaging.Notification
                {
                    Title = request.Title,
                    Body = request.Body
                }
            };

            try
            {
                string response = await FirebaseMessaging.DefaultInstance.SendAsync(message);
                // Optionally log the response or handle it as needed
            }
            catch (FirebaseMessagingException ex)
            {
                // Handle exceptions appropriately
                throw new Exception($"Error sending notification: {ex.Message}", ex);
            }
        }

        public async Task CreateNotificationAsync(Models.Entities.Notification notification)
        {
            // Insert the notification into the database
            _context.notification.Add(notification);
            await _context.SaveChangesAsync();
        }

        public async Task<List<Models.Entities.Notification>> GetUserNotificationsAsync(int userId)
        {
            var notifications = await _context.notification
                .Where(n => n.recipient_user_id == userId)
                .OrderByDescending(n => n.created_at)
                .ToListAsync();

            return notifications;
        }

        public async Task MarkAsReadAsync(int notificationId)
        {
            // Update the notification's is_read status in the database
            var notification = await _context.notification.FindAsync(notificationId);
            if (notification != null)
            {
                notification.is_read = true;
                await _context.SaveChangesAsync();
            }
        }

        // **New Method to Send and Save Notifications**
        public async Task SendAndSaveNotificationAsync(int recipientUserId, int senderUserId, string type, int? relatedEntityId, string message)
        {
            // Create the notification record
            var notification = new Models.Entities.Notification
            {
                recipient_user_id = recipientUserId,
                sender_user_id = senderUserId,
                type = type,
                related_entity_id = relatedEntityId,
                message = message,
                created_at = DateTime.UtcNow
            };

            _context.notification.Add(notification);
            await _context.SaveChangesAsync();

            // Attempt to send the push notification
            try
            {
                // Retrieve the recipient user
                var recipientUser = await _context.users
                    .FirstOrDefaultAsync(u => u.user_id == recipientUserId);

                if (recipientUser != null && !string.IsNullOrEmpty(recipientUser.fcm_token))
                {
                    // Prepare the notification request
                    var notificationRequest = new NotificationRequest
                    {
                        Token = recipientUser.fcm_token,
                        Title = $"New {type}",
                        Body = message
                    };

                    // Send the push notification
                    await SendNotificationAsync(notificationRequest);
                }
                else
                {
                    // Optionally log that the recipient has no FCM token
                    _logger.LogInformation($"User {recipientUserId} does not have a valid FCM token.");
                }
            }
            catch (Exception ex)
            {
                // Log the exception details
                _logger.LogError(ex, $"Failed to send push notification to user {recipientUserId}");
                // Optionally, you might want to update the notification record with a failed status
            }
        }
        public async Task HandleAggregatedNotificationAsync(int recipientUserId, int senderUserId, string type, int? relatedEntityId, string action)
        {
            // Get recipient FCM token
            var recipientUser = await _context.users.FindAsync(recipientUserId);
            string recipientFcmToken = recipientUser?.fcm_token;

            // Check if there is an existing notification for this type and entity
            var existingNotification = await _context.notification
                .FirstOrDefaultAsync(n => n.recipient_user_id == recipientUserId &&
                                          n.type == type &&
                                          n.related_entity_id == relatedEntityId);

            if (existingNotification == null)
            {
                // No existing notification, create a new one
                var senderUser = await _context.users.FindAsync(senderUserId);
                var senderName = senderUser?.fullname ?? "Someone";

                string message = $"{senderName} {action} your post.";

                var notification = new Models.Entities.Notification
                {
                    recipient_user_id = recipientUserId,
                    sender_user_id = senderUserId,
                    type = type,
                    related_entity_id = relatedEntityId,
                    message = message,
                    created_at = DateTime.UtcNow,
                    is_read = false,
                    last_push_sent_at = DateTime.UtcNow,
                    aggregated_user_ids = senderUserId.ToString()
                };

                _context.notification.Add(notification);
                await _context.SaveChangesAsync();

                // Send push notification
                if (!string.IsNullOrEmpty(recipientFcmToken))
                {
                    try
                    {
                        await SendNotificationAsync(new NotificationRequest
                        {
                            Token = recipientFcmToken,
                            Title = $"New {type}",
                            Body = message
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to send push notification to user {recipientUserId}");
                    }
                }
            }
            else
            {
                // Existing notification found, update it

                // Deserialize the aggregated_user_ids
                var userIds = existingNotification.aggregated_user_ids.Split(',')
                    .Select(id => int.Parse(id))
                    .ToList();

                // Add or move the senderUserId to the front
                if (!userIds.Contains(senderUserId))
                {
                    userIds.Insert(0, senderUserId);
                }
                else
                {
                    userIds.Remove(senderUserId);
                    userIds.Insert(0, senderUserId);
                }

                // Fetch user names
                var usersDict = await _context.users
                    .Where(u => userIds.Contains(u.user_id))
                    .ToDictionaryAsync(u => u.user_id, u => u.fullname);

                // Build names in order
                var userNames = userIds
                    .Where(id => usersDict.ContainsKey(id))
                    .Select(id => usersDict[id])
                    .ToList();

                int userCount = userNames.Count;
                int namesToDisplay = 2;

                string message;
                if (userCount <= namesToDisplay)
                {
                    message = $"{string.Join(" and ", userNames)} {action} your post.";
                }
                else
                {
                    int othersCount = userCount - namesToDisplay;
                    var displayedNames = userNames.Take(namesToDisplay);
                    message = $"{string.Join(", ", displayedNames)}, and {othersCount} others {action} your post.";
                }

                // Update notification
                existingNotification.message = message;
                existingNotification.aggregated_user_ids = string.Join(",", userIds);

                // Explicitly mark properties as modified
                _context.Entry(existingNotification).Property(n => n.message).IsModified = true;
                _context.Entry(existingNotification).Property(n => n.aggregated_user_ids).IsModified = true;

                // Decide whether to send a push notification
                TimeSpan pushCooldown = TimeSpan.FromMinutes(5);
                if (existingNotification.last_push_sent_at == null ||
                    DateTime.UtcNow - existingNotification.last_push_sent_at >= pushCooldown)
                {
                    // Send push notification
                    if (!string.IsNullOrEmpty(recipientFcmToken))
                    {
                        try
                        {
                            await SendNotificationAsync(new NotificationRequest
                            {
                                Token = recipientFcmToken,
                                Title = $"New {type}",
                                Body = message
                            });

                            // Update 'last_push_sent_at' and mark it as modified
                            existingNotification.last_push_sent_at = DateTime.UtcNow;
                            _context.Entry(existingNotification).Property(n => n.last_push_sent_at).IsModified = true;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Failed to send push notification to user {recipientUserId}");
                        }
                    }
                }

                await _context.SaveChangesAsync();
            }
        }
        public async Task SendAndSaveNotificationAsync(int recipientUserId, int senderUserId, string type, int? relatedEntityId, int? commentId, string message)
        {
            // Create the notification record
            var notification = new Models.Entities.Notification
            {
                recipient_user_id = recipientUserId,
                sender_user_id = senderUserId,
                type = type,
                related_entity_id = relatedEntityId,
                comment_id = commentId,
                message = message,
                created_at = DateTime.UtcNow,
                is_read = false
            };

            _context.notification.Add(notification);
            await _context.SaveChangesAsync();

            // Attempt to send the push notification
            try
            {
                // Retrieve the recipient user
                var recipientUser = await _context.users
                    .FirstOrDefaultAsync(u => u.user_id == recipientUserId);

                if (recipientUser != null && !string.IsNullOrEmpty(recipientUser.fcm_token))
                {
                    // Prepare the notification request
                    var notificationRequest = new NotificationRequest
                    {
                        Token = recipientUser.fcm_token,
                        Title = $"New {type}",
                        Body = message
                    };

                    // Send the push notification
                    await SendNotificationAsync(notificationRequest);
                }
                else
                {
                    // Optionally log that the recipient has no FCM token
                    _logger.LogInformation($"User {recipientUserId} does not have a valid FCM token.");
                }
            }
            catch (Exception ex)
            {
                // Log the exception details
                _logger.LogError(ex, $"Failed to send push notification to user {recipientUserId}");
                // Optionally, you might want to update the notification record with a failed status
            }
        }
        public async Task HandleShareNotificationAsync(int recipientUserId, int senderUserId, int postId, string action)
        {
            // Get recipient FCM token
            var recipientUser = await _context.users.FindAsync(recipientUserId);
            string recipientFcmToken = recipientUser?.fcm_token;

            // Check if there is an existing notification for this type and post
            var existingNotification = await _context.notification
                .FirstOrDefaultAsync(n => n.recipient_user_id == recipientUserId &&
                                          n.type == "Share" &&
                                          n.related_entity_id == postId);

            if (existingNotification == null)
            {
                // No existing notification, create a new one
                var senderUser = await _context.users.FindAsync(senderUserId);
                var senderName = senderUser?.fullname ?? "Someone";

                string message = $"{senderName} {action} your post.";

                var notification = new Models.Entities.Notification
                {
                    recipient_user_id = recipientUserId,
                    sender_user_id = senderUserId,
                    type = "Share",
                    related_entity_id = postId,
                    message = message,
                    created_at = DateTime.UtcNow,
                    is_read = false,
                    last_push_sent_at = DateTime.UtcNow,
                    aggregated_user_ids = senderUserId.ToString()
                };

                _context.notification.Add(notification);
                await _context.SaveChangesAsync();

                // Send push notification
                if (!string.IsNullOrEmpty(recipientFcmToken))
                {
                    try
                    {
                        await SendNotificationAsync(new NotificationRequest
                        {
                            Token = recipientFcmToken,
                            Title = "New Share",
                            Body = message
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to send push notification to user {recipientUserId}");
                    }
                }
            }
            else
            {
                // Existing notification found, update it

                // Deserialize the aggregated_user_ids
                var userIds = existingNotification.aggregated_user_ids.Split(',')
                    .Select(id => int.Parse(id))
                    .ToList();

                // Add or move the senderUserId to the front
                if (!userIds.Contains(senderUserId))
                {
                    userIds.Insert(0, senderUserId);
                }
                else
                {
                    userIds.Remove(senderUserId);
                    userIds.Insert(0, senderUserId);
                }

                // Fetch user names
                var usersDict = await _context.users
                    .Where(u => userIds.Contains(u.user_id))
                    .ToDictionaryAsync(u => u.user_id, u => u.fullname);

                // Build names in order
                var userNames = userIds
                    .Where(id => usersDict.ContainsKey(id))
                    .Select(id => usersDict[id])
                    .ToList();

                int userCount = userNames.Count;
                int namesToDisplay = 2;

                string message;
                if (userCount <= namesToDisplay)
                {
                    message = $"{string.Join(" and ", userNames)} {action} your post.";
                }
                else
                {
                    int othersCount = userCount - namesToDisplay;
                    var displayedNames = userNames.Take(namesToDisplay);
                    message = $"{string.Join(", ", displayedNames)}, and {othersCount} others {action} your post.";
                }

                // Update notification
                existingNotification.message = message;
                existingNotification.aggregated_user_ids = string.Join(",", userIds);

                // Explicitly mark properties as modified
                _context.Entry(existingNotification).Property(n => n.message).IsModified = true;
                _context.Entry(existingNotification).Property(n => n.aggregated_user_ids).IsModified = true;

                // Decide whether to send a push notification
                TimeSpan pushCooldown = TimeSpan.FromMinutes(5); // Adjust cooldown period as needed
                if (existingNotification.last_push_sent_at == null ||
                    DateTime.UtcNow - existingNotification.last_push_sent_at >= pushCooldown)
                {
                    // Send push notification
                    if (!string.IsNullOrEmpty(recipientFcmToken))
                    {
                        try
                        {
                            await SendNotificationAsync(new NotificationRequest
                            {
                                Token = recipientFcmToken,
                                Title = "New Share",
                                Body = message
                            });

                            // Update 'last_push_sent_at' and mark it as modified
                            existingNotification.last_push_sent_at = DateTime.UtcNow;
                            _context.Entry(existingNotification).Property(n => n.last_push_sent_at).IsModified = true;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Failed to send push notification to user {recipientUserId}");
                        }
                    }
                }

                await _context.SaveChangesAsync();
            }
        }
        public async Task HandleFollowNotificationAsync(int recipientUserId, int senderUserId, bool isMutualFollow)
        {
            // Get recipient FCM token
            var recipientUser = await _context.users.FindAsync(recipientUserId);
            string recipientFcmToken = recipientUser?.fcm_token;

            // Get sender's full name
            var senderUser = await _context.users.FindAsync(senderUserId);
            string senderFullName = senderUser?.fullname ?? "Someone";

            // Prepare base action message
            string baseAction = isMutualFollow ? "started following you." : "followed you, follow back.";

            // Check if there is an existing notification of type "Follow"
            var existingNotification = await _context.notification
                .FirstOrDefaultAsync(n => n.recipient_user_id == recipientUserId &&
                                          n.type == "Follow");

            if (existingNotification == null)
            {
                // No existing notification, create a new one
                var notification = new Models.Entities.Notification
                {
                    recipient_user_id = recipientUserId,
                    sender_user_id = senderUserId,
                    type = "Follow",
                    related_entity_id = null, // Not needed for aggregated follows
                    message = $"{senderFullName} {baseAction}",
                    created_at = DateTime.UtcNow,
                    is_read = false,
                    last_push_sent_at = DateTime.UtcNow,
                    aggregated_user_ids = senderUserId.ToString()
                };

                _context.notification.Add(notification);
                await _context.SaveChangesAsync();

                // Send push notification
                if (!string.IsNullOrEmpty(recipientFcmToken))
                {
                    try
                    {
                        await SendNotificationAsync(new NotificationRequest
                        {
                            Token = recipientFcmToken,
                            Title = "New Follower",
                            Body = notification.message
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to send push notification to user {recipientUserId}");
                    }
                }
            }
            else
            {
                // Existing notification found, update it

                // Deserialize the aggregated_user_ids
                var userIds = existingNotification.aggregated_user_ids.Split(',')
                    .Select(id => int.Parse(id))
                    .ToList();

                // Add or move the senderUserId to the front
                if (!userIds.Contains(senderUserId))
                {
                    userIds.Insert(0, senderUserId);
                }
                else
                {
                    userIds.Remove(senderUserId);
                    userIds.Insert(0, senderUserId);
                }

                // Fetch user names
                var usersDict = await _context.users
                    .Where(u => userIds.Contains(u.user_id))
                    .ToDictionaryAsync(u => u.user_id, u => u.fullname);

                // Build names in order
                var userNames = userIds
                    .Where(id => usersDict.ContainsKey(id))
                    .Select(id => usersDict[id])
                    .ToList();

                int userCount = userNames.Count;
                int namesToDisplay = 2;

                string message;
                if (userCount <= namesToDisplay)
                {
                    message = $"{string.Join(" and ", userNames)} {baseAction}";
                }
                else
                {
                    int othersCount = userCount - namesToDisplay;
                    var displayedNames = userNames.Take(namesToDisplay);
                    message = $"{string.Join(", ", displayedNames)}, and {othersCount} others {baseAction}";
                }

                // Update notification
                existingNotification.message = message;
                existingNotification.aggregated_user_ids = string.Join(",", userIds);

                // Explicitly mark properties as modified
                _context.Entry(existingNotification).Property(n => n.message).IsModified = true;
                _context.Entry(existingNotification).Property(n => n.aggregated_user_ids).IsModified = true;

                // Decide whether to send a push notification based on cooldown
                TimeSpan pushCooldown = TimeSpan.FromMinutes(5); // Adjust cooldown period as needed
                if (existingNotification.last_push_sent_at == null ||
                    DateTime.UtcNow - existingNotification.last_push_sent_at >= pushCooldown)
                {
                    // Send push notification
                    if (!string.IsNullOrEmpty(recipientFcmToken))
                    {
                        try
                        {
                            await SendNotificationAsync(new NotificationRequest
                            {
                                Token = recipientFcmToken,
                                Title = "New Followers",
                                Body = message
                            });

                            // Update 'last_push_sent_at' and mark it as modified
                            existingNotification.last_push_sent_at = DateTime.UtcNow;
                            _context.Entry(existingNotification).Property(n => n.last_push_sent_at).IsModified = true;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Failed to send push notification to user {recipientUserId}");
                        }
                    }
                }

                await _context.SaveChangesAsync();
            }
        }
        public async Task HandleAcceptFollowRequestNotificationAsync(int recipientUserId, int senderUserId)
        {
            // Get recipient FCM token
            var recipientUser = await _context.users.FindAsync(recipientUserId);
            string recipientFcmToken = recipientUser?.fcm_token;

            // Get sender's full name
            var senderUser = await _context.users.FindAsync(senderUserId);
            string senderFullName = senderUser?.fullname ?? "Someone";

            string baseAction = "has accepted your follow request.";

            // Check if there is an existing notification of type "Accept"
            var existingNotification = await _context.notification
                .FirstOrDefaultAsync(n => n.recipient_user_id == recipientUserId &&
                                          n.type == "Accept");

            if (existingNotification == null)
            {
                // Create new notification
                var notification = new Models.Entities.Notification
                {
                    recipient_user_id = recipientUserId,
                    sender_user_id = senderUserId,
                    type = "Accept",
                    related_entity_id = null, // Not needed for aggregated accepts
                    message = $"{senderFullName} {baseAction}",
                    created_at = DateTime.UtcNow,
                    is_read = false,
                    last_push_sent_at = DateTime.UtcNow,
                    aggregated_user_ids = senderUserId.ToString()
                };

                _context.notification.Add(notification);
                await _context.SaveChangesAsync();

                // Send push notification
                if (!string.IsNullOrEmpty(recipientFcmToken))
                {
                    try
                    {
                        await SendNotificationAsync(new NotificationRequest
                        {
                            Token = recipientFcmToken,
                            Title = "Follow Request Accepted",
                            Body = notification.message
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to send push notification to user {recipientUserId}");
                    }
                }
            }
            else
            {
                // Existing notification found, update it

                // Deserialize the aggregated_user_ids
                var userIds = existingNotification.aggregated_user_ids.Split(',')
                    .Select(id => int.Parse(id))
                    .ToList();

                // Add or move the senderUserId to the front
                if (!userIds.Contains(senderUserId))
                {
                    userIds.Insert(0, senderUserId);
                }
                else
                {
                    userIds.Remove(senderUserId);
                    userIds.Insert(0, senderUserId);
                }

                // Fetch user names
                var usersDict = await _context.users
                    .Where(u => userIds.Contains(u.user_id))
                    .ToDictionaryAsync(u => u.user_id, u => u.fullname);

                // Build names in order
                var userNames = userIds
                    .Where(id => usersDict.ContainsKey(id))
                    .Select(id => usersDict[id])
                    .ToList();

                int userCount = userNames.Count;
                int namesToDisplay = 2;

                string message;
                if (userCount <= namesToDisplay)
                {
                    message = $"{string.Join(" and ", userNames)} {baseAction}";
                }
                else
                {
                    int othersCount = userCount - namesToDisplay;
                    var displayedNames = userNames.Take(namesToDisplay);
                    message = $"{string.Join(", ", displayedNames)}, and {othersCount} others {baseAction}";
                }

                // Update notification
                existingNotification.message = message;
                existingNotification.aggregated_user_ids = string.Join(",", userIds);

                // Explicitly mark properties as modified
                _context.Entry(existingNotification).Property(n => n.message).IsModified = true;
                _context.Entry(existingNotification).Property(n => n.aggregated_user_ids).IsModified = true;

                // Decide whether to send a push notification based on cooldown
                TimeSpan pushCooldown = TimeSpan.FromMinutes(5); // Adjust cooldown period as needed
                if (existingNotification.last_push_sent_at == null ||
                    DateTime.UtcNow - existingNotification.last_push_sent_at >= pushCooldown)
                {
                    // Send push notification
                    if (!string.IsNullOrEmpty(recipientFcmToken))
                    {
                        try
                        {
                            await SendNotificationAsync(new NotificationRequest
                            {
                                Token = recipientFcmToken,
                                Title = "Follow Requests Accepted",
                                Body = message
                            });

                            // Update 'last_push_sent_at' and mark it as modified
                            existingNotification.last_push_sent_at = DateTime.UtcNow;
                            _context.Entry(existingNotification).Property(n => n.last_push_sent_at).IsModified = true;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Failed to send push notification to user {recipientUserId}");
                        }
                    }
                }

                await _context.SaveChangesAsync();
            }
        }

        public async Task ***REMOVED***(int recipientUserId, int senderUserId, int ***REMOVED***)
        {
            // Get recipient FCM token
            var recipientUser = await _context.users.FindAsync(recipientUserId);
            string recipientFcmToken = recipientUser?.fcm_token;

            // Get sender's full name
            var senderUser = await _context.users.FindAsync(senderUserId);
            string senderFullName = senderUser?.fullname ?? "Someone";

            // Check if there is an existing notification of type "***REMOVED***" for this ***REMOVED***
            var existingNotification = await _context.notification
                .FirstOrDefaultAsync(n => n.recipient_user_id == recipientUserId &&
                                          n.type == "***REMOVED***" &&
                                          n.related_entity_id == ***REMOVED***);

            if (existingNotification == null)
            {
                // No existing notification, create a new one
                var notification = new Models.Entities.Notification
                {
                    recipient_user_id = recipientUserId,
                    sender_user_id = senderUserId,
                    type = "***REMOVED***",
                    related_entity_id = ***REMOVED***,
                    message = $"{senderFullName} liked your ***REMOVED***.",
                    created_at = DateTime.UtcNow,
                    is_read = false,
                    last_push_sent_at = DateTime.UtcNow,
                    aggregated_user_ids = senderUserId.ToString()
                };

                _context.notification.Add(notification);
                await _context.SaveChangesAsync();

                // Send push notification
                if (!string.IsNullOrEmpty(recipientFcmToken))
                {
                    try
                    {
                        await SendNotificationAsync(new NotificationRequest
                        {
                            Token = recipientFcmToken,
                            Title = "***REMOVED***",
                            Body = notification.message
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to send push notification to user {recipientUserId}");
                    }
                }
            }
            else
            {
                // Existing notification found, update it

                // Deserialize the aggregated_user_ids
                var userIds = existingNotification.aggregated_user_ids.Split(',')
                    .Select(id => int.Parse(id))
                    .ToList();

                // Add or move the senderUserId to the front
                if (!userIds.Contains(senderUserId))
                {
                    userIds.Insert(0, senderUserId);
                }
                else
                {
                    userIds.Remove(senderUserId);
                    userIds.Insert(0, senderUserId);
                }

                // Fetch user names
                var usersDict = await _context.users
                    .Where(u => userIds.Contains(u.user_id))
                    .ToDictionaryAsync(u => u.user_id, u => u.fullname);

                // Build names in order
                var userNames = userIds
                    .Where(id => usersDict.ContainsKey(id))
                    .Select(id => usersDict[id])
                    .ToList();

                int userCount = userNames.Count;
                int namesToDisplay = 2;

                string message;
                if (userCount <= namesToDisplay)
                {
                    // If there are 1 or 2 users, list them
                    message = $"{string.Join(" and ", userNames)} liked your ***REMOVED***.";
                }
                else
                {
                    // If there are more than 2 users, list the first two and show the count of others
                    int othersCount = userCount - namesToDisplay;
                    var displayedNames = userNames.Take(namesToDisplay);
                    message = $"{string.Join(", ", displayedNames)}, and {othersCount} others liked your ***REMOVED***.";
                }

                // Update notification
                existingNotification.message = message;
                existingNotification.aggregated_user_ids = string.Join(",", userIds);

                // Explicitly mark properties as modified
                _context.Entry(existingNotification).Property(n => n.message).IsModified = true;
                _context.Entry(existingNotification).Property(n => n.aggregated_user_ids).IsModified = true;

                // Decide whether to send a push notification based on cooldown
                TimeSpan pushCooldown = TimeSpan.FromMinutes(5); // Adjust cooldown period as needed
                if (existingNotification.last_push_sent_at == null ||
                    DateTime.UtcNow - existingNotification.last_push_sent_at >= pushCooldown)
                {
                    // Send push notification
                    if (!string.IsNullOrEmpty(recipientFcmToken))
                    {
                        try
                        {
                            await SendNotificationAsync(new NotificationRequest
                            {
                                Token = recipientFcmToken,
                                Title = "***REMOVED***s",
                                Body = message
                            });

                            // Update 'last_push_sent_at' and mark it as modified
                            existingNotification.last_push_sent_at = DateTime.UtcNow;
                            _context.Entry(existingNotification).Property(n => n.last_push_sent_at).IsModified = true;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Failed to send push notification to user {recipientUserId}");
                        }
                    }
                }

                await _context.SaveChangesAsync();
            }
        }


    }
}
