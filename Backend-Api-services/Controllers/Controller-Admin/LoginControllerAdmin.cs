using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using Backend_Api_services.Models.Data;
using Backend_Api_services.Models.DTOs_Admin;
using Backend_Api_services.Models.Entites_Admin;
using System.Security.Cryptography;
using BCrypt.Net;
using Backend_Api_services.Models.DTOs;
using Backend_Api_services.Models.Entities;

namespace Backend_Api_services.Controllers.Controllers_Admin
{
    [Route("api/admin/[controller]")]
    [ApiController]
    public class LoginControllerAdmin : ControllerBase
    {
        private readonly apiDbContext _context;
        private readonly IConfiguration _configuration;

        public LoginControllerAdmin(apiDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpPost]
        public async Task<IActionResult> Login([FromBody] LoginModelAdmin loginModel)
        {
            if (loginModel == null || string.IsNullOrEmpty(loginModel.Email) || string.IsNullOrEmpty(loginModel.Password))
            {
                return BadRequest("Email and password are required.");
            }

            var admin = await _context.Admins.FirstOrDefaultAsync(a => a.email == loginModel.Email);

            if (admin == null || !BCrypt.Net.BCrypt.Verify(loginModel.Password, admin.password))
            {
                return Unauthorized("Invalid email or password combination.");
            }

            // Generate JWT token with the admin's role
            var accessToken = GenerateJwtToken(admin.username, admin.role);
            var refreshToken = GenerateRefreshToken();

            // Save the refresh token in the database
            var adminRefreshToken = new UserRefreshToken
            {
                adminid = admin.admin_id, // Set the AdminId
                userid = null, // Ensure UserId is null
                token = refreshToken,
                expiresat = DateTime.UtcNow.AddMinutes(double.Parse(_configuration["Jwt:RefreshTokenLifetime"]))
            };

            _context.UserRefreshTokens.Add(adminRefreshToken);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                Token = accessToken,
                RefreshToken = adminRefreshToken.token,
                AdminId = admin.admin_id
            });
        }

        private string GenerateJwtToken(string username, string role)
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, role) // Adding the actual Admin role (admin or superadmin)
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

            var storedRefreshToken = await _context.UserRefreshTokens
                .FirstOrDefaultAsync(rt => rt.token == refreshTokenRequest.token && rt.adminid != null); // Ensure it's an admin token

            if (storedRefreshToken == null || storedRefreshToken.expiresat <= DateTime.UtcNow)
            {
                return Unauthorized("Invalid or expired refresh token.");
            }

            var admin = await _context.Admins.FirstOrDefaultAsync(a => a.admin_id == storedRefreshToken.adminid);
            if (admin == null)
            {
                return Unauthorized("Admin not found.");
            }

            var newAccessToken = GenerateJwtToken(admin.username, admin.role);
            var newRefreshToken = GenerateRefreshToken();

            storedRefreshToken.token = newRefreshToken;
            storedRefreshToken.expiresat = DateTime.UtcNow.AddMinutes(double.Parse(_configuration["Jwt:RefreshTokenLifetime"]));

            await _context.SaveChangesAsync();

            return Ok(new { AccessToken = newAccessToken, RefreshToken = newRefreshToken });
        }

        [HttpPost("Logout")]
        public async Task<IActionResult> Logout([FromBody] AdminLogoutModel logoutModel)
        {
            var refreshToken = await _context.UserRefreshTokens
                .FirstOrDefaultAsync(rt => rt.adminid == logoutModel.AdminId && rt.token == logoutModel.RefreshToken);

            if (refreshToken == null)
            {
                return BadRequest("Invalid admin ID or refresh token.");
            }

            _context.UserRefreshTokens.Remove(refreshToken);
            await _context.SaveChangesAsync();

            return Ok("Logged out successfully.");
        }

        public class AdminLogoutModel
        {
            public int AdminId { get; set; } // Changed from UserId to AdminId
            public string RefreshToken { get; set; }
        }
    }
}
