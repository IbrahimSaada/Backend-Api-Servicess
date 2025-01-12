﻿using Backend_Api_services.Models.Entites_Admin;
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
        public DbSet<Comment> Comments { get; set; }
        public DbSet<Reports> Reports { get; set; }
        public DbSet<shared_posts> SharedPosts { get; set; }
        public DbSet<Admin> Admins { get; set; }
        public DbSet<stories> Stories { get; set; }
        public DbSet<storiesmedia> StoriesMedia { get; set; }
        public DbSet<storyviews> StoryViews { get; set; }
        public DbSet<Bookmark> Bookmarks { get; set; }
        public DbSet<Followers> Followers { get; set; }
        public DbSet<Chat> Chats { get; set; }
        public DbSet<Chat_Media> ChatMedia { get; set; }
        public DbSet<Messages> Messages { get; set; }
        public DbSet<Online_Status> OnlineStatus { get; set; }
        public DbSet<Notification> notification { get; set; }
        public DbSet<muted_users> muted_users { get; set; }
        public DbSet<BlockedUsers> blocked_users { get; set; }
        public DbSet<banned_users> banned_users { get; set; }
    }
}
