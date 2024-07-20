using Backend_Api_services.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Collections.Generic;

[Route("api/[controller]")]
[ApiController]
public class UsersController : ControllerBase
{
    private readonly apiDbContext _context;
    private readonly ILogger<UsersController> _logger;

    public UsersController(apiDbContext context, ILogger<UsersController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // GET: api/Users
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Users>>> GetUsers()
    {
        return await _context.users.ToListAsync();
    }

    // GET: api/Users/5
    [HttpGet("{id}")]
    public async Task<ActionResult<Users>> GetUser(int id)
    {
        var user = await _context.users.FindAsync(id);

        if (user == null)
        {
            return NotFound();
        }

        return user;
    }

    // Search users by username
    [HttpGet("search/{username}")]
    public async Task<ActionResult<IEnumerable<Users>>> SearchUsers(string username)
    {
        var users = await _context.users
            .Where(u => u.username.Contains(username))
            .ToListAsync();

        if (users == null || users.Count == 0)
        {
            return NotFound();
        }

        return users;
    }

    // PUT: api/Users/5
    [HttpPut("{id}")]
    public async Task<IActionResult> PutUser(int id, Users user)
    {
        if (id != user.user_id)
        {
            return BadRequest();
        }

        _context.Entry(user).State = EntityState.Modified;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!UserExists(id))
            {
                return NotFound();
            }
            else
            {
                throw;
            }
        }

        return NoContent();
    }

    // POST: api/Users/register
    [HttpPost("register")]
    public async Task<ActionResult<Users>> RegisterUser([FromBody] Users user)
    {
        _logger.LogInformation("Starting registration process for user: {Email}", user.email);

        // Normalize email to lowercase
        user.email = user.email.ToLower();

        // Validate email format
        if (!IsValidEmail(user.email))
        {
            _logger.LogWarning("Invalid email format: {Email}", user.email);
            return BadRequest("Invalid email format.");
        }

        // Validate phone number format
        if (!IsValidPhoneNumber(user.phone_number))
        {
            _logger.LogWarning("Invalid phone number format: {PhoneNumber}", user.phone_number);
            return BadRequest("Invalid phone number format.");
        }

        // Validate password complexity
        if (!IsValidPassword(user.password))
        {
            _logger.LogWarning("Password does not meet complexity requirements.");
            return BadRequest("Password must be at least 8 characters long and include at least one uppercase letter, one lowercase letter, one number, and one special character.");
        }

        // Validate fullname
        if (string.IsNullOrEmpty(user.fullname))
        {
            _logger.LogWarning("Full name is required.");
            return BadRequest("Full name is required.");
        }

        // Check if the email already exists
        var existingEmailUser = await _context.users.FirstOrDefaultAsync(u => u.email == user.email);
        if (existingEmailUser != null)
        {
            _logger.LogWarning("A user with this email already exists: {Email}", user.email);
            return Conflict("A user with this email already exists.");
        }

        // Check if the phone number already exists
        var existingPhoneUser = await _context.users.FirstOrDefaultAsync(u => u.phone_number == user.phone_number);
        if (existingPhoneUser != null)
        {
            _logger.LogWarning("A user with this phone number already exists: {PhoneNumber}", user.phone_number);
            return Conflict("A user with this phone number already exists.");
        }

        // Generate verification code
        user.verification_code = GenerateVerificationCode();

        // Generate unique username
        user.username = GenerateUniqueUsername(user.email);

        // Convert dob to UTC
        user.dob = DateTime.SpecifyKind(user.dob, DateTimeKind.Utc);

        // Add user to the database
        _context.users.Add(user);
        await _context.SaveChangesAsync();

        _logger.LogInformation("User registered successfully with ID: {UserId}", user.user_id);

        // Return the newly created user
        return CreatedAtAction(nameof(GetUser), new { id = user.user_id }, user);
    }

    // GET: api/Users/email-exists/{email}
    [HttpGet("email-exists/{email}")]
    public async Task<IActionResult> EmailExists(string email)
    {
        var exists = await _context.users.AnyAsync(u => u.email == email);
        return Ok(exists);
    }

    // GET: api/Users/phone-exists/{phoneNumber}]
    [HttpGet("phone-exists/{phoneNumber}")]
    public async Task<IActionResult> PhoneExists(string phoneNumber)
    {
        var exists = await _context.users.AnyAsync(u => u.phone_number == phoneNumber);
        return Ok(exists);
    }
    [HttpPost("verify")]
    public async Task<IActionResult> VerifyUser([FromBody] VerifyUserModel model)
    {
        var user = await _context.users.FirstOrDefaultAsync(u => u.email == model.Email);

        if (user == null)
        {
            return NotFound("User not found.");
        }

        if (user.verification_code == model.VerificationCode)
        {
            user.verified_at = DateTime.UtcNow;
            user.verification_code = null; // Clear the verification code after successful verification
            await _context.SaveChangesAsync();
            return Ok("Account successfully verified.");
        }
        else
        {
            return BadRequest("Invalid verification code.");
        }
    }

    // Model for verifying the user
    public class VerifyUserModel
    {
        public string Email { get; set; }
        public string VerificationCode { get; set; }
    }
    [HttpPost("request-password-reset")]
    public async Task<IActionResult> RequestPasswordReset([FromBody] PasswordResetModel model)
    {
        var user = await _context.users.FirstOrDefaultAsync(u =>
            u.email == model.EmailOrPhoneNumber || u.phone_number == model.EmailOrPhoneNumber);

        if (user == null)
        {
            return NotFound("User not found.");
        }

        user.verification_code = GenerateVerificationCode();
        await _context.SaveChangesAsync();

        // Send the verification code to the user's email or phone number
        // (Implementation of sending the code via email or SMS is not included in this example)

        return Ok("Verification code sent.");
    }
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] PasswordResetModel model)
    {
        var user = await _context.users.FirstOrDefaultAsync(u => u.email == model.EmailOrPhoneNumber);

        if (user == null)
        {
            return NotFound("User not found.");
        }

        if (user.verification_code == model.VerificationCode)
        {
            // Reset the password
            user.password = model.NewPassword;
            user.verification_code = null; // Clear the verification code after successful reset
            await _context.SaveChangesAsync();
            return Ok("Password has been reset.");
        }
        else
        {
            return BadRequest("Invalid verification code.");
        }
    }

    public class PasswordResetModel
    {
        public string EmailOrPhoneNumber { get; set; }
        public string VerificationCode { get; set; }
        public string NewPassword { get; set; }
    }

    private bool UserExists(int id)
    {
        return _context.users.Any(e => e.user_id == id);
    }

    private bool IsValidEmail(string email)
    {
        var emailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
        return Regex.IsMatch(email, emailPattern);
    }

    private bool IsValidPhoneNumber(string phoneNumber)
    {
        var phonePattern = @"^\+?[1-9]\d{0,2}\d{7,12}$";
        return Regex.IsMatch(phoneNumber, phonePattern);
    }

    private bool IsValidPassword(string password)
    {
        var passwordPattern = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$";
        return Regex.IsMatch(password, passwordPattern);
    }

    private string GenerateVerificationCode()
    {
        Random random = new Random();
        return random.Next(100000, 999999).ToString(); // Generates a 6-digit verification code
    }

    private string GenerateUniqueUsername(string email)
    {
        var baseUsername = email.Split('@')[0];
        var username = baseUsername;
        int suffix = 1;

        while (_context.users.Any(u => u.username == username))
        {
            username = $"{baseUsername}{suffix}";
            suffix++;
        }

        return username;
    }
}
