using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Backend_Api_services.Models.Data;
using Microsoft.EntityFrameworkCore;

namespace Backend_Api_services.BackgroundServices
{
    public class StoryExpirationService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<StoryExpirationService> _logger;
        private readonly IConfiguration _configuration;

        public StoryExpirationService(IServiceProvider serviceProvider, ILogger<StoryExpirationService> logger, IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("StoryExpirationService is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<apiDbContext>();

                    _logger.LogInformation("Checking for expired stories...");

                    // Fetch stories that have expired but are still marked as active
                    var expiredStories = await context.Stories
                                                      .Where(s => s.isactive && s.expiresat <= DateTime.UtcNow)
                                                      .ToListAsync();

                    // Mark these stories as inactive
                    foreach (var story in expiredStories)
                    {
                        story.isactive = false;
                        _logger.LogInformation("Story {StoryId} marked as inactive.", story.story_id);
                    }

                    // Save changes if there were any updates
                    if (expiredStories.Any())
                    {
                        await context.SaveChangesAsync();
                        _logger.LogInformation("Expired stories have been updated.");
                    }
                    else
                    {
                        _logger.LogInformation("No expired stories found.");
                    }
                }

                // Retrieve the delay time from the environment variable, defaulting to 15 minutes
                var delayMinutes = _configuration.GetValue<int>("STORY_EXPIRATION_CHECK_INTERVAL_MINUTES", 15);
                _logger.LogInformation("Next check will occur in {DelayMinutes} minutes.", delayMinutes);
                await Task.Delay(TimeSpan.FromMinutes(delayMinutes), stoppingToken);
            }

            _logger.LogInformation("StoryExpirationService is stopping.");
        }
    }
}
