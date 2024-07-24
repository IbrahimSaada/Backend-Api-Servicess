﻿using Backend_Api_services.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System;
using System.Linq;
using System.Threading.Tasks;

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

    // POST: api/Login
    [HttpPost]
    public async Task<IActionResult> Login([FromBody] LoginModel loginModel)
    {
        if (loginModel == null || string.IsNullOrEmpty(loginModel.EmailOrPhoneNumber) || string.IsNullOrEmpty(loginModel.Password))
        {
            return BadRequest("Email or phone number and password are required.");
        }

        var user = await _context.users.FirstOrDefaultAsync(u =>
            (u.email == loginModel.EmailOrPhoneNumber || u.phone_number == loginModel.EmailOrPhoneNumber) &&
            u.password == loginModel.Password); // Consider hashing the password before comparing

        if (user == null)
        {
            return Unauthorized("Invalid email or phone number and password combination.");
        }

        _logger.LogInformation("User logged in successfully: {EmailOrPhoneNumber}", loginModel.EmailOrPhoneNumber);

        return Ok(new { Message = "Login successful", UserId = user.user_id, Username = user.username });
    }
}
