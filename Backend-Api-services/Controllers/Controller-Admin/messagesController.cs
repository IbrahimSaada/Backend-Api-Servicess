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
    private readonly MessagesEmail _messagesEmail;
    private readonly IFileService _fileService;

    public messagesController(apiDbContext context, ILogger<messagesController> logger, MessagesEmail messagesEmail, IFileService fileService)
    {
        _context = context;
        _logger = logger;
        _messagesEmail = messagesEmail;
        _fileService = fileService;
    }

    [HttpPost("send-email-to-all")]
    public async Task<IActionResult> SendEmailToAllUsers([FromForm] EmailRequestModel request)
    {
        _logger.LogInformation("Starting to send email to all users");

        if (string.IsNullOrEmpty(request.Subject) || string.IsNullOrEmpty(request.Body))
        {
            return BadRequest("Subject and body are required.");
        }

        var users = await _context.users.ToListAsync();
        if (!users.Any())
        {
            return NotFound("No users found.");
        }

        List<string> attachmentUrls = new List<string>();
        if (request.Attachments != null)
        {
            foreach (var attachment in request.Attachments)
            {
                var fileUrl = await UploadAttachment(attachment);
                attachmentUrls.Add(fileUrl);
            }
        }

        foreach (var user in users)
        {
            await _messagesEmail.SendEmailAsync(
                user.email,
                request.Subject,
                "./Templates/EmailTemplate.html",
                new Dictionary<string, string>
                {
                    { "BODY", request.Body },
                    { "SUBJECT", request.Subject }
                },
                attachmentPaths: attachmentUrls
            );
        }

        return Ok("Emails sent successfully.");
    }

    private async Task<string> UploadAttachment(IFormFile attachment)
    {
        return await _fileService.UploadFileAsync(attachment, "homepagecooking", "attachments");
    }

    public class EmailRequestModel
    {
        public string? Subject { get; set; }
        public string? Body { get; set; }
        public List<IFormFile>? Attachments { get; set; }
    }
}
