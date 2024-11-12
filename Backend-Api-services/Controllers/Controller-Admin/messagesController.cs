using Backend_Api_services;
using Backend_Api_services.Models.Data;
using Backend_Api_services.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Backend_Api_services.Services.Interfaces;

[Route("api/[controller]")]
[ApiController]
public class messagesController : ControllerBase
{
    private readonly apiDbContext _context;
    private readonly ILogger<messagesController> _logger;
    private readonly MessagesEmail _messagesEmail;  // Update here to match DI registration
    private readonly IFileService _fileService;

    public messagesController(apiDbContext context, ILogger<messagesController> logger, MessagesEmail messagesEmail, IFileService fileService)
    {
        _context = context;
        _logger = logger;
        _messagesEmail = messagesEmail; // Corrected to MessagesEmail
        _fileService = fileService;
    }

    [HttpPost("send-email-to-all")]
    public async Task<IActionResult> SendEmailToAllUsers([FromForm] EmailRequestModel request)
    {
        _logger.LogInformation("Starting to send email to all users");

        // Validate input
        if (string.IsNullOrEmpty(request.Subject) || string.IsNullOrEmpty(request.Body))
        {
            return BadRequest("Subject and body are required.");
        }

        var users = await _context.users.ToListAsync();
        if (!users.Any())
        {
            return NotFound("No users found.");
        }

        // Process attachments if available
        List<string> attachmentUrls = new List<string>();
        if (request.Attachments != null && request.Attachments.Count > 0)
        {
            foreach (var attachment in request.Attachments)
            {
                if (attachment.Length > 0)
                {
                    var fileUrl = await UploadAttachment(attachment);
                    attachmentUrls.Add(fileUrl);
                }
            }
        }

        // Send email to each user
        foreach (var user in users)
        {
            await _messagesEmail.SendEmailAsync(user.email, request.Subject, request.Body, attachmentUrls);
        }

        _logger.LogInformation("Email sent to all users successfully.");

        return Ok("Email sent to all users.");
    }

    private async Task<string> UploadAttachment(IFormFile attachment)
    {
        string bucketName = "homepagecooking";
        string attachmentFolder = "attachments";

        // Upload the file to S3 using the IFileService
        return await _fileService.UploadFileAsync(attachment, bucketName, attachmentFolder);
    }

    public class EmailRequestModel
    {
        public string? Subject { get; set; }
        public string? Body { get; set; }
        public List<IFormFile>? Attachments { get; set; } // List of files to be attached
    }
}
