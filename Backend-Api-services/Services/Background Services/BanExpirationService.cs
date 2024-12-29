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
using Backend_Api_services.Models.Entites_Admin;

namespace Backend_Api_services.BackgroundServices
{
    public class BanExpirationService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BanExpirationService> _logger;
        private readonly IConfiguration _configuration;

        // Fallback if not defined in configuration
        private const int DefaultDelayMinutes = 15;
        private const int DefaultBatchSize = 500;

        public BanExpirationService(
            IServiceProvider serviceProvider,
            ILogger<BanExpirationService> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("BanExpirationService is starting.");

            // Get the check interval from config or use a default
            int delayMinutes = _configuration.GetValue("BAN_EXPIRATION_CHECK_INTERVAL_MINUTES", DefaultDelayMinutes);

            // Get the batch size from config or use a default
            int batchSize = _configuration.GetValue("BAN_EXPIRATION_BATCH_SIZE", DefaultBatchSize);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Checking for expired bans...");

                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<apiDbContext>();
                        await MarkExpiredBansInactiveAsync(context, batchSize, stoppingToken);
                    }

                    _logger.LogInformation(
                        "Next ban-expiration check will occur in {DelayMinutes} minute(s).",
                        delayMinutes
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while marking bans as expired.");
                }

                // If the service is canceled during the delay, an exception is thrown and the loop ends
                await Task.Delay(TimeSpan.FromMinutes(delayMinutes), stoppingToken);
            }

            _logger.LogInformation("BanExpirationService is stopping.");
        }

        /// <summary>
        /// Marks all active bans that are past their expiration time as inactive, using batch processing to handle large sets.
        /// </summary>
        private async Task MarkExpiredBansInactiveAsync(
            apiDbContext context,
            int batchSize,
            CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // Fetch a batch of active bans that have expired
                var expiredBans = await context.banned_users
                    .Where(b =>
                        b.is_active &&
                        b.expires_at.HasValue &&
                        b.expires_at.Value <= DateTime.UtcNow
                    )
                    .OrderBy(b => b.ban_id) // stable ordering so we can batch
                    .Take(batchSize)
                    .ToListAsync(stoppingToken);

                if (!expiredBans.Any())
                {
                    _logger.LogInformation("No expired bans found in this batch.");
                    break;
                }

                // Mark each ban as inactive
                foreach (var ban in expiredBans)
                {
                    ban.is_active = false;
                    _logger.LogInformation("Ban {BanId} for user {UserId} marked as inactive.", ban.ban_id, ban.user_id);
                }

                // Save changes for this batch
                await context.SaveChangesAsync(stoppingToken);
                _logger.LogInformation(
                    "Expired bans batch of size {Count} updated.",
                    expiredBans.Count
                );
            }
        }
    }
}
