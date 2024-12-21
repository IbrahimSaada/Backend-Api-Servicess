using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
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

        // Fallback if not defined in configuration
        private const int DefaultDelayMinutes = 15;
        private const int DefaultBatchSize = 500;

        public StoryExpirationService(
            IServiceProvider serviceProvider,
            ILogger<StoryExpirationService> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("StoryExpirationService is starting.");

            // Get the check interval from config or use a default
            int delayMinutes = _configuration.GetValue("STORY_EXPIRATION_CHECK_INTERVAL_MINUTES", DefaultDelayMinutes);

            // Get the batch size from config or use a default
            int batchSize = _configuration.GetValue("STORY_EXPIRATION_BATCH_SIZE", DefaultBatchSize);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Checking for expired stories...");

                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<apiDbContext>();
                        await MarkExpiredStoriesInactiveAsync(context, batchSize, stoppingToken);
                    }

                    _logger.LogInformation(
                        "Next check will occur in {DelayMinutes} minute(s).",
                        delayMinutes
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while marking stories as expired.");
                }

                // If the service is canceled during the delay, an exception is thrown and the loop ends
                await Task.Delay(TimeSpan.FromMinutes(delayMinutes), stoppingToken);
            }

            _logger.LogInformation("StoryExpirationService is stopping.");
        }

        /// <summary>
        /// Marks all expired (but still active) stories as inactive, using batch processing to handle large sets.
        /// </summary>
        private async Task MarkExpiredStoriesInactiveAsync(
            apiDbContext context,
            int batchSize,
            CancellationToken stoppingToken)
        {
            // Keep fetching batches until no more expired stories remain in this batch
            while (!stoppingToken.IsCancellationRequested)
            {
                var expiredStories = await context.Stories
                    .Where(s => s.isactive && s.expiresat <= DateTime.UtcNow)
                    .OrderBy(s => s.story_id) // stable ordering
                    .Take(batchSize)
                    .ToListAsync(stoppingToken);

                if (!expiredStories.Any())
                {
                    _logger.LogInformation("No expired stories found in this batch.");
                    break;
                }

                // Mark each story as inactive
                foreach (var story in expiredStories)
                {
                    story.isactive = false;
                    _logger.LogInformation("Story {StoryId} marked as inactive.", story.story_id);
                }

                // Save changes for this batch
                await context.SaveChangesAsync(stoppingToken);
                _logger.LogInformation(
                    "Expired stories batch of size {Count} updated.",
                    expiredStories.Count
                );
            }
        }
    }
}