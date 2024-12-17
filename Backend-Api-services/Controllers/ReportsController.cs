using Backend_Api_services.Models.Data;
using Backend_Api_services.Models.DTOs;
using Backend_Api_services.Models.Entities;
using Backend_Api_services.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace Backend_Api_services.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]  // JWT Authorization applied to the entire controller
    [CheckBan]
    public class ReportsController : ControllerBase
    {
        private readonly apiDbContext _context;
        private readonly SignatureService _signatureService;

        public ReportsController(apiDbContext context, SignatureService signatureService)
        {
            _context = context;
            _signatureService = signatureService;
        }

        // GET: api/Reports
        [HttpGet]
        [AllowAnonymous]  // Allow anonymous access to GET requests if needed
        public async Task<ActionResult<IEnumerable<ReportRequest>>> GetReports()
        {
            var reports = await _context.Reports
                .Include(r => r.ReportedBy)
                .Include(r => r.ReportedUser)
                .ToListAsync();

            var reportDtos = reports.Select(report => new ReportRequest
            {
                ReportId = report.report_id,
                ReportedBy = report.reported_by,
                ReportedByUsername = report.ReportedBy.username,
                ReportedUser = report.reported_user,
                ReportedUserUsername = report.ReportedUser.username,
                content_type = report.content_type,
                ContentId = report.content_id,
                ReportReason = report.report_reason,
                ReportStatus = report.report_status,
                SeverityLevel = report.severity_level,
                CreatedAt = report.created_at,
                ResolvedAt = report.resolved_at
            }).ToList();

            return Ok(reportDtos);
        }

        // POST: api/Reports
        [HttpPost]
        public async Task<ActionResult<ReportResponse>> CreateReport([FromBody] ReportResponse reportDto)
        {
            // Signature validation: Extract signature from the 'X-Signature' header (make sure this matches Flutter)
            var signature = Request.Headers["X-Signature"].ToString();

            // Data to sign: Combine critical fields (ReportedBy, ReportedUser, ContentId)
            var dataToSign = $"ReportedBy={reportDto.ReportedBy}&ReportedUser={reportDto.ReportedUser}&ContentId={reportDto.ContentId}";

             //Validate the signature
            if (!_signatureService.ValidateSignature(signature, dataToSign))
            {
                return Unauthorized("Invalid signature.");
            }

            var report = new Reports
            {
                reported_by = reportDto.ReportedBy,
                reported_user = reportDto.ReportedUser,
                content_type = reportDto.ContentType,
                content_id = reportDto.ContentId,
                report_reason = reportDto.ReportReason,
                resolution_details = reportDto.resolution_details,
            };
            var post = await _context.Posts.FindAsync(reportDto.ContentId);
            if (post != null)
            {
                post.report_count++;
            }
            _context.Reports.Add(report);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetReportById), new { id = report.report_id }, reportDto);
        }

        // This method is needed for the CreatedAtAction reference above
        [HttpGet("{id}")]
        public async Task<ActionResult<ReportRequest>> GetReportById(int id)
        {
            var report = await _context.Reports.FindAsync(id);

            if (report == null)
            {
                return NotFound();
            }

            var reportDto = new ReportResponse
            {
                ReportedBy = report.reported_by,
                ReportedUser = report.reported_user,
                ContentType = report.content_type,
                ContentId = report.content_id,
                ReportReason = report.report_reason,
                resolution_details = report.resolution_details,
            };

            return Ok(reportDto);
        }
    }
}
