﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using Backend_Api_services.Models.Data;
using Backend_Api_services.Models.DTOs;
using Backend_Api_services.Models.Entities;
using System.Security.Cryptography;

[Route("api/[controller]")]
[ApiController]
public class LoginController : ControllerBase
{
    private readonly apiDbContext _context;
    private readonly ILogger<LoginController> _logger;
    private readonly IConfiguration _configuration;

    public LoginController(apiDbContext context, ILogger<LoginController> logger, IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
    }

    // Helper method to generate the signature
    private string GenerateSignature(string data)
    {
        var secretKey = _configuration["AppSecretKey"];
        using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey)))
        {
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToBase64String(hash);
        }
    }

    // Helper method to validate the signature
    private bool ValidateSignature(string receivedSignature, string data)
    {
        var generatedSignature = GenerateSignature(data);
        return generatedSignature == receivedSignature;
    }

    // POST: api/Login
    [HttpPost]
    public async Task<IActionResult> Login([FromBody] LoginModel loginModel)
    {
        // Validate the input
        if (loginModel == null || string.IsNullOrEmpty(loginModel.EmailOrPhoneNumber) || string.IsNullOrEmpty(loginModel.Password))
        {
            return BadRequest("Email or phone number and password are required.");
        }

        // Extract signature from headers
        // var signature = Request.Headers["X-Signature"].FirstOrDefault();
        // if (string.IsNullOrEmpty(signature))
        // {
        //     return Unauthorized("Signature missing.");
        // }

        // Create string representation for signing (could use JSON serialization)
        // var requestData = $"{loginModel.EmailOrPhoneNumber}:{loginModel.Password}:{loginModel.FcmToken}";

        // Validate the signature
        // if (!ValidateSignature(signature, requestData))
        // {
        //     return Unauthorized("Invalid signature.");
        // }

        // Retrieve the user from the database
        var user = await _context.users
            .AsNoTracking() // Ensures no accidental tracking of the entity
            .FirstOrDefaultAsync(u =>
                (u.email == loginModel.EmailOrPhoneNumber || u.phone_number == loginModel.EmailOrPhoneNumber) &&
                u.password == loginModel.Password); // Direct comparison (consider hashing for security)

        if (user == null)
        {
            return Unauthorized("Invalid email or phone number and password combination.");
        }

        // Update only the FCM token if provided
        if (!string.IsNullOrEmpty(loginModel.FcmToken) && user.fcm_token != loginModel.FcmToken)
        {
            user.fcm_token = loginModel.FcmToken;
            _context.Entry(user).Property(u => u.fcm_token).IsModified = true; // Only modify the FCM token
            await _context.SaveChangesAsync();
        }

        _logger.LogInformation("User logged in successfully: {EmailOrPhoneNumber}", loginModel.EmailOrPhoneNumber);

        // Generate tokens
        var accessToken = GenerateJwtToken(user);
        var refreshToken = GenerateRefreshToken();

        // Save the refresh token in the database
        var userRefreshToken = new UserRefreshToken
        {
            userid = user.user_id,   // Set UserId
            adminid = null,          // Ensure AdminId is null for users
            token = refreshToken,
            expiresat = DateTime.UtcNow.AddMinutes(double.Parse(_configuration["Jwt:RefreshTokenLifetime"])) // Refresh token lifetime from config
        };

        _context.UserRefreshTokens.Add(userRefreshToken);
        await _context.SaveChangesAsync();

        // Return the response
        return Ok(new
        {
            Token = accessToken,
            RefreshToken = refreshToken,
            UserId = user.user_id,
            Username = user.username,
            Fullname = user.fullname,
            ProfilePic = user.profile_pic
        });
    }


    [HttpPut("UpdateFcmToken/{userId}")]
    public async Task<IActionResult> UpdateFcmToken(int userId, [FromBody] UpdateFcmTokenModel model)
    {
        if (model == null || string.IsNullOrEmpty(model.FcmToken))
        {
            return BadRequest("FCM token is required.");
        }

        // Extract signature from headers
         var signature = Request.Headers["X-Signature"].FirstOrDefault();
         if (string.IsNullOrEmpty(signature))
         {
             return Unauthorized("Signature missing.");
         }

         // Validate the signature
         if (!ValidateSignature(signature, model.FcmToken))
         {
             return Unauthorized("Invalid signature.");
         }

        // Retrieve the user by userId
        var user = await _context.users.AsNoTracking().FirstOrDefaultAsync(u => u.user_id == userId);

        if (user == null)
        {
            return NotFound("User not found.");
        }

        // Update only the FCM token
        user.fcm_token = model.FcmToken;

        // Attach the user and mark only the fcm_token as modified
        _context.Attach(user);
        _context.Entry(user).Property(u => u.fcm_token).IsModified = true;

        // Save changes
        await _context.SaveChangesAsync();

        return Ok("FCM token updated successfully.");
    }


    private string GenerateJwtToken(Users user)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.user_id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var tokenLifetime = double.Parse(_configuration["Jwt:AccessTokenLifetime"]);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(tokenLifetime),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private string GenerateRefreshToken()
    {
        var randomNumber = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }
    }

    [HttpPost("RefreshToken")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest refreshTokenRequest)
    {
        if (refreshTokenRequest == null || string.IsNullOrEmpty(refreshTokenRequest.token))
        {
            return BadRequest("Invalid request.");
        }

        // Extract signature from headers
        var signature = Request.Headers["X-Signature"].FirstOrDefault();
        if (string.IsNullOrEmpty(signature))
        {
            return Unauthorized("Signature missing.");
        }

        // Validate the signature
        if (!ValidateSignature(signature, refreshTokenRequest.token))
        {
            return Unauthorized("Invalid signature.");
        }

        var storedRefreshToken = await _context.UserRefreshTokens
            .FirstOrDefaultAsync(rt => rt.token == refreshTokenRequest.token && rt.userid != null);

        if (storedRefreshToken == null || storedRefreshToken.expiresat <= DateTime.UtcNow)
        {
            return Unauthorized("Invalid or expired refresh token.");
        }

        var user = await _context.users.FirstOrDefaultAsync(u => u.user_id == storedRefreshToken.userid);
        if (user == null)
        {
            return Unauthorized("User not found.");
        }

        var newAccessToken = GenerateJwtToken(user);
        var newRefreshToken = GenerateRefreshToken();

        storedRefreshToken.token = newRefreshToken;
        storedRefreshToken.expiresat = DateTime.UtcNow.AddMinutes(double.Parse(_configuration["Jwt:RefreshTokenLifetime"]));

        await _context.SaveChangesAsync();

        return Ok(new { AccessToken = newAccessToken, RefreshToken = newRefreshToken });
    }

    [HttpPost("Logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutModel logoutModel)
    {
        // Validate the refresh token
        var refreshToken = await _context.UserRefreshTokens
            .FirstOrDefaultAsync(rt => rt.userid == logoutModel.UserId && rt.token == logoutModel.RefreshToken);

        if (refreshToken == null)
        {
            return BadRequest("Invalid user ID or refresh token.");
        }

        // Remove the refresh token
        _context.UserRefreshTokens.Remove(refreshToken);

        // Clear only the FCM token of the user
        var user = await _context.users
            .AsNoTracking() // Ensure no other fields are tracked or modified
            .FirstOrDefaultAsync(u => u.user_id == logoutModel.UserId);

        if (user != null)
        {
            user.fcm_token = null;

            // Attach the user entity and mark only the FCM token as modified
            _context.Attach(user);
            _context.Entry(user).Property(u => u.fcm_token).IsModified = true;
        }

        // Save changes to the database
        await _context.SaveChangesAsync();

        _logger.LogInformation("User logged out successfully: {UserId}", logoutModel.UserId);

        return Ok("Logged out successfully.");
    }

    public class LogoutModel
    {
            public int UserId { get; set; }
            public string RefreshToken { get; set; }
    }
    public class UpdateFcmTokenModel
    {
        public string? FcmToken { get; set; }
    }
}
