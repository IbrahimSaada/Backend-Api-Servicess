using Backend_Api_services.Models.Data;
using Backend_Api_services.Models.DTOs_Admin;
using Backend_Api_services.Models.Entites_Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Backend_Api_services.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "superadmin")] // Only superadmins can access any action in this controller
    public class AdminController : ControllerBase
    {
        private readonly apiDbContext _context;

        public AdminController(apiDbContext context)
        {
            _context = context;
        }

        // Helper method to generate a unique username based on email
        private string GenerateUniqueUsername(string email)
        {
            // Extract the part before the '@' symbol in the email address
            string baseUsername = Regex.Match(email, @"^[^@]+").Value.ToLower();

            // Check if the username is already taken
            var existingUsername = _context.Admins
                                           .Where(a => a.username.StartsWith(baseUsername))
                                           .OrderByDescending(a => a.username)
                                           .FirstOrDefault();

            if (existingUsername == null)
            {
                return baseUsername; // The username is available
            }

            // If the username exists, append a number to make it unique
            string latestUsername = existingUsername.username;
            string numberPart = latestUsername.Substring(baseUsername.Length);
            int newNumber = 1;

            if (int.TryParse(numberPart, out int existingNumber))
            {
                newNumber = existingNumber + 1;
            }

            return baseUsername + newNumber;
        }

        // GET: api/Admin/all
        [HttpGet("all")]
        public async Task<IActionResult> GetAllAdmins()
        {
            var admins = await _context.Admins
                                       .Select(a => new AdminResponse
                                       {
                                           admin_id = a.admin_id,
                                           username = a.username,
                                           email = a.email,
                                           role = a.role
                                       })
                                       .ToListAsync();

            return Ok(admins);
        }

        // POST: api/Admin/add
        [HttpPost("add")]
        public async Task<IActionResult> AddAdmin([FromBody] AdminRequest adminRequest)
        {
            // Check if the email is unique
            if (_context.Admins.Any(a => a.email == adminRequest.email))
            {
                return BadRequest("An admin with this email already exists.");
            }

            // Generate a unique username based on the email
            string username = GenerateUniqueUsername(adminRequest.email);

            var newAdmin = new Admin
            {
                username = username,
                email = adminRequest.email,
                password = BCrypt.Net.BCrypt.HashPassword(adminRequest.password),
                role = adminRequest.role
            };

            _context.Admins.Add(newAdmin);
            await _context.SaveChangesAsync();

            var adminResponse = new AdminResponse
            {
                admin_id = newAdmin.admin_id,
                username = newAdmin.username,
                email = newAdmin.email,
                role = newAdmin.role
            };

            return CreatedAtAction(nameof(GetAllAdmins), new { id = newAdmin.admin_id }, adminResponse);
        }

        // DELETE: api/Admin/delete/{adminId}
        [HttpDelete("delete/{adminId}")]
        public async Task<IActionResult> DeleteAdmin(int adminId)
        {
            var admin = await _context.Admins.FindAsync(adminId);
            if (admin == null)
            {
                return NotFound("Admin not found.");
            }

            _context.Admins.Remove(admin);
            await _context.SaveChangesAsync();

            return Ok("Admin deleted successfully.");
        }

        // PUT: api/Admin/update-password/{adminId}
        [HttpPut("update-password/{adminId}")]
        public async Task<IActionResult> UpdateAdminPassword(int adminId, [FromBody] UpdatePasswordRequest updatePasswordRequest)
        {
            var admin = await _context.Admins.FindAsync(adminId);
            if (admin == null)
            {
                return NotFound("Admin not found.");
            }

            // Hash the new password
            admin.password = BCrypt.Net.BCrypt.HashPassword(updatePasswordRequest.NewPassword);

            await _context.SaveChangesAsync();

            return Ok("Password updated successfully.");
        }

        // DTO for updating the password
        public class UpdatePasswordRequest
        {
            public string NewPassword { get; set; }
        }
    }
}
