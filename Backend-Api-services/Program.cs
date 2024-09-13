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
using Backend_Api_services.Services.Interfaces;
using Backend_Api_services.BackgroundServices;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "v1" });
});

// Configure PostgreSQL with connection string from environment variables
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
var tokenLifetime = double.Parse(builder.Configuration["Jwt:AccessTokenLifetime"]);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
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
});

// Configure AWS credentials
var awsCredentials = new Amazon.Runtime.BasicAWSCredentials(
    builder.Configuration["AWS:AccessKey"],
    builder.Configuration["AWS:SecretKey"]
);

var awsOptions = builder.Configuration.GetAWSOptions();
awsOptions.Credentials = awsCredentials;
builder.Services.AddDefaultAWSOptions(awsOptions);
// Inside ConfigureServices method
builder.Services.AddSingleton<IAwsS3Service, AwsS3Service>();
builder.Services.AddSingleton<IAwsSettings>(sp => new AwsSettings
{
    AccessKeyId = builder.Configuration["AWS:AccessKey"],
    SecretKey = builder.Configuration["AWS:SecretKey"],
    RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(builder.Configuration["AWS:Region"])
});
builder.Services.AddSingleton<IEnvironmentSettings>(sp => new EnvironmentSettings
{
    ShortName = "cookingApp-dev" // Set this to your specific environment's short name, e.g., "prod" or "dev"
});

// Register AWS S3 client
builder.Services.AddAWSService<IAmazonS3>();

// Register the email service
builder.Services.AddSingleton<EmailService>();

// Register the StoryExpirationService background service
builder.Services.AddHostedService<StoryExpirationService>(); // Add this line

builder.Services.AddScoped<SignatureService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API v1");
        c.RoutePrefix = string.Empty; // Serve Swagger UI at the app's root
    });
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("AllowAllOrigins");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Add startup logging
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Application started. Environment: {env}", app.Environment.EnvironmentName);

app.Run();
