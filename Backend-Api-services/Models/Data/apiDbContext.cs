using Backend_Api_services.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backend_Api_services.Models.Data
{
    public class apiDbContext : DbContext
    {
        private readonly ILogger<apiDbContext> _logger;

        public apiDbContext(DbContextOptions<apiDbContext> options, ILogger<apiDbContext> logger) : base(options)
        {
            _logger = logger;
            _logger.LogInformation("apiDbContext initialized");
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);

            if (!optionsBuilder.IsConfigured)
            {
                var connectionString = "Your Connection String";
                optionsBuilder.UseNpgsql(connectionString);
                _logger.LogInformation("OnConfiguring called, connection string: {connectionString}", connectionString);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            _logger.LogInformation("OnModelCreating called");
        }

        public DbSet<Users> users { get; set; }
        public DbSet<Post> Posts { get; set; }
        public DbSet<PostMedia> PostMedias { get; set; }
        public DbSet<Like> Likes { get; set; }
        public DbSet<UserRefreshToken> UserRefreshTokens { get; set; }
    }
}
