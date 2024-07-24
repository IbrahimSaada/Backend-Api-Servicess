using Backend_Api_services.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

[Route("api/[controller]")]
[ApiController]
public class ResetPasswordController : ControllerBase
{
    private readonly apiDbContext _context;
    private readonly ILogger<ResetPasswordController> _logger;

    public ResetPasswordController(apiDbContext context, ILogger<ResetPasswordController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // POST: api/ResetPassword/request
    [HttpPost("request")]
    public async Task<IActionResult> RequestPasswordReset([FromBody] PasswordResetRequestModel model)
    {
        var user = await _context.users.FirstOrDefaultAsync(u => u.email == model.EmailOrPhoneNumber || u.phone_number == model.EmailOrPhoneNumber);

        if (user == null)
        {
            return NotFound("User not found.");
        }

        user.verification_code = GenerateVerificationCode();
        await _context.SaveChangesAsync();

        // Here you would send the verification code to the user's email or phone number
        // (Implementation of sending the code via email or SMS is not included in this example)

        _logger.LogInformation("Verification code sent to user: {EmailOrPhoneNumber}", model.EmailOrPhoneNumber);
        return Ok("Verification code sent.");
    }

    // POST: api/ResetPassword/verify
    [HttpPost("verify")]
    public async Task<IActionResult> VerifyCode([FromBody] VerificationModel model)
    {
        var user = await _context.users.FirstOrDefaultAsync(u => u.email == model.EmailOrPhoneNumber || u.phone_number == model.EmailOrPhoneNumber);

        if (user == null)
        {
            return NotFound("User not found.");
        }

        if (user.verification_code == model.VerificationCode)
        {
            return Ok(true);
        }
        else
        {
            return BadRequest(false);
        }
    }

    // POST: api/ResetPassword/reset
    [HttpPost("reset")]
    public async Task<IActionResult> ResetPassword([FromBody] PasswordResetModel model)
    {
        var user = await _context.users.FirstOrDefaultAsync(u => u.email == model.EmailOrPhoneNumber || u.phone_number == model.EmailOrPhoneNumber);

        if (user == null)
        {
            return NotFound("User not found.");
        }

        if (user.verification_code == model.VerificationCode)
        {
            if (!IsValidPassword(password: model.NewPassword))
            {
                return BadRequest("Password must be at least 8 characters long and include at least one uppercase letter, one lowercase letter, one number, and one special character.");
            }

            user.password = model.NewPassword;
            user.verification_code = null; // Clear the verification code after successful reset
            await _context.SaveChangesAsync();

            _logger.LogInformation("Password reset successfully for user: {EmailOrPhoneNumber}", model.EmailOrPhoneNumber);
            return Ok("Password has been reset.");
        }
        else
        {
            return BadRequest("Invalid verification code.");
        }
    }

    public class PasswordResetRequestModel
    {
        public string? EmailOrPhoneNumber { get; set; }
    }

    public class PasswordResetModel
    {
        public string? EmailOrPhoneNumber { get; set; }
        public string? VerificationCode { get; set; }
        public string? NewPassword { get; set; }
    }

    public class VerificationModel
    {
        public string? EmailOrPhoneNumber { get; set; }
        public string? VerificationCode { get; set; }
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
}