using Backend_Api_services;
using Backend_Api_services.Models.Data;
using Backend_Api_services.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QRCoder;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Amazon.S3;
using Microsoft.AspNetCore.Http;
using Backend_Api_services.Services.Interfaces;
using Backend_Api_services.Models.DTOs;
using Microsoft.AspNetCore.Authorization;

[Route("api/[controller]")]
[ApiController]
public class RegistrationController : ControllerBase
{
    private readonly apiDbContext _context;
    private readonly ILogger<RegistrationController> _logger;
    private readonly EmailService _emailService;
    private readonly IFileService _fileService; // Use the file service
    private readonly IQRCodeService _qrCodeService; // QR Code Service

    public RegistrationController(apiDbContext context, ILogger<RegistrationController> logger, EmailService emailService, IFileService fileService, IQRCodeService qrCodeService)
    {
        _context = context;
        _logger = logger;
        _emailService = emailService;
        _fileService = fileService;
        _qrCodeService = qrCodeService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Users>>> GetUsers()
    {
        return await _context.users.ToListAsync();
    }

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

    [HttpPost("register")]
    public async Task<ActionResult<Users>> RegisterUser([FromBody] RegisterUserDto userDto)
    {
        _logger.LogInformation("Starting registration process for user: {Email}", userDto.email);

        var email = userDto.email.ToLower();
        if (!IsValidEmail(email))
        {
            _logger.LogWarning("Invalid email format: {Email}", email);
            return BadRequest("Invalid email format.");
        }

        if (!IsValidPassword(userDto.password))
        {
            _logger.LogWarning("Password does not meet complexity requirements.");
            return BadRequest("Password must be at least 8 characters long and include at least one uppercase letter, one lowercase letter, one number, and one special character.");
        }

        if (string.IsNullOrEmpty(userDto.fullname))
        {
            _logger.LogWarning("Full name is required.");
            return BadRequest("Full name is required.");
        }

        var existingEmailUser = await _context.users.FirstOrDefaultAsync(u => u.email == email);
        if (existingEmailUser != null)
        {
            _logger.LogWarning("A user with this email already exists: {Email}", email);
            return Conflict("A user with this email already exists.");
        }

        // Map DTO to Entity and hash the password
        var user = new Users
        {
            email = email,
            // Hash the password using bcrypt
            password = BCrypt.Net.BCrypt.HashPassword(userDto.password),
            fullname = userDto.fullname,
            dob = DateTime.SpecifyKind(userDto.dob, DateTimeKind.Utc),
            gender = userDto.gender
        };

        user.verification_code = GenerateVerificationCode();
        user.username = GenerateUniqueUsername(email);

        _context.users.Add(user);
        await _context.SaveChangesAsync();

        _logger.LogInformation("User registered successfully with ID: {UserId}", user.user_id);

        // Generate QR code after user registration
        var qrCodeResult = await GenerateQRCodeForUser(user.user_id);
        if (qrCodeResult is OkObjectResult)
        {
            _logger.LogInformation("QR code successfully generated for user ID: {UserId}", user.user_id);
        }
        else
        {
            _logger.LogWarning("Failed to generate QR code for user ID: {UserId}", user.user_id);
        }

        await _emailService.SendVerificationEmailAsync(user.email, user.verification_code);

        return CreatedAtAction(nameof(GetUser), new { id = user.user_id }, user);
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
            user.verification_code = null;
            await _context.SaveChangesAsync();
            return Ok("Account successfully verified.");
        }
        else
        {
            return BadRequest("Invalid verification code.");
        }
    }

    [HttpGet("email-exists/{email}")]
    public async Task<IActionResult> EmailExists(string email)
    {
        var exists = await _context.users.AnyAsync(u => u.email == email);
        return Ok(exists);
    }

    public class VerifyUserModel
    {
        public string? Email { get; set; }
        public string? VerificationCode { get; set; }
    }

    private bool IsValidEmail(string email)
    {
        var emailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
        return Regex.IsMatch(email, emailPattern);
    }

    private bool IsValidPassword(string password)
    {
        var passwordPattern = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$";
        return Regex.IsMatch(password, passwordPattern);
    }

    private string GenerateVerificationCode()
    {
        Random random = new Random();
        return random.Next(100000, 999999).ToString();
    }
    [HttpPost("generate-qr/{userId}")]
    public async Task<IActionResult> GenerateQRCodeForUser(int userId)
    {
        var user = await _context.users.FindAsync(userId);
        if (user == null)
        {
            return NotFound("User not found.");
        }

        // Generate the QR code in base64
        var qrCodeText = $"cooktalk://profile/{user.user_id}";
        var qrCodeBase64 = _qrCodeService.GenerateQRCodeBase64(qrCodeText);

        // Convert base64 to byte array for uploading
        byte[] qrCodeBinary = Convert.FromBase64String(qrCodeBase64);
        using (var stream = new MemoryStream(qrCodeBinary))
        {
            var file = new FormFile(stream, 0, stream.Length, null, $"{user.username}-qrcode.png")
            {
                Headers = new HeaderDictionary(),
                ContentType = "image/png"
            };

            string bucketName = "homepagecooking";
            string qrCodeFolder = "qrcode";

            // Upload the QR code to S3 using the IFileService
            var s3Url = await _fileService.UploadFileAsync(file, bucketName, qrCodeFolder);

            user.qr_code = s3Url;
            await _context.SaveChangesAsync();

            return Ok($"QR code generated and uploaded to S3: {s3Url}");
        }

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

    private bool UserExists(int id)
    {
        return _context.users.Any(e => e.user_id == id);
    }
}
