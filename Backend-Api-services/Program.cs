using Amazon.Extensions.NETCore.Setup;
using Amazon.S3;
using Amazon.SimpleEmail;
using Backend_Api_services.Models.Data;
using Backend_Api_services.Services;
using Backend_Api_services.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Backend_Api_services.BackgroundServices;
using System.Text;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.SignalR;
using Backend_Api_services.Hubs;
using Newtonsoft.Json.Serialization;
using Backend_Api_services.Services.RatingService;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
// Add SignalR services with detailed errors enabled
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
});

// Configure Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "v1" });
});

// Configure PostgreSQL with connection string
builder.Services.AddDbContext<apiDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add CORS policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Configure JWT authentication
var key = Encoding.ASCII.GetBytes(builder.Configuration["Jwt:Key"]);
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ClockSkew = TimeSpan.Zero
    };

    // For SignalR authentication
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/chathub"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

// Configure AWS credentials and options
var awsCredentials = new Amazon.Runtime.BasicAWSCredentials(
    builder.Configuration["AWS:AccessKey"],
    builder.Configuration["AWS:SecretKey"]
);

var awsOptions = builder.Configuration.GetAWSOptions();
awsOptions.Credentials = awsCredentials;
builder.Services.AddDefaultAWSOptions(awsOptions);

builder.Services.AddSingleton<IAwsS3Service, AwsS3Service>();
builder.Services.AddSingleton<IAwsSettings>(sp => new AwsSettings
{
    AccessKeyId = builder.Configuration["AWS:AccessKey"],
    SecretKey = builder.Configuration["AWS:SecretKey"],
    RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(builder.Configuration["AWS:Region"])
});
builder.Services.AddSingleton<IEnvironmentSettings>(sp => new EnvironmentSettings
{
    ShortName = "cookingApp-dev" // Set this to your specific environment's short name
});

// Register AWS S3 client
builder.Services.AddAWSService<IAmazonS3>();

// Register email services
builder.Services.AddSingleton<EmailService>(); // For general email sending
builder.Services.AddSingleton<MessagesEmail>(); // For messages-specific email sending

// Register the StoryExpirationService background service
builder.Services.AddHostedService<StoryExpirationService>();

builder.Services.AddScoped<SignatureService>();
builder.Services.AddTransient<IQRCodeService, QRCodeService>();
builder.Services.AddScoped<IFileService, FileService>();

// Register Ratingsystem for ***REMOVED***s
builder.Services.AddScoped<RatingService>();
// Register Notification Service
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IChatNotificationService, ChatNotificationService>();

var app = builder.Build();

// Initialize Firebase
var serviceAccountPath = Path.Combine(Directory.GetCurrentDirectory(), "Keys", "cooktalk-cd05d-firebase-adminsdk-ela2u-a3aa2219b7.json");
FirebaseApp.Create(new AppOptions
{
    Credential = GoogleCredential.FromFile(serviceAccountPath),
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API v1");
        c.RoutePrefix = string.Empty;
    });
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("AllowAllOrigins");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Map the SignalR hub
app.MapHub<ChatHub>("/chathub");

// Add startup logging
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Application started. Environment: {env}", app.Environment.EnvironmentName);

app.Run();
