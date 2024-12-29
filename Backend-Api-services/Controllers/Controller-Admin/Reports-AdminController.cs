using Backend_Api_services.Models.Data;
using Backend_Api_services.Models.DTOs;
using Backend_Api_services.Models.Entities;
using Backend_Api_services.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace Backend_Api_services.Controllers.Controller_Admin
{
    [ApiController]
    [Route("api/[controller]")]
    public class Reports_AdminController : ControllerBase
    {
        private readonly apiDbContext _context;
        private readonly SignatureService _signatureService;

        public Reports_AdminController(apiDbContext context, SignatureService signatureService)
        {
            _context = context;
            _signatureService = signatureService;
        }
        // GET: api/Reports
        [HttpGet("GetReports")]
        public async Task<ActionResult<IEnumerable<ReportRequest>>> GetReports([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            if (page <= 0 || pageSize <= 0)
                return BadRequest("Page and pageSize must be greater than zero.");

            // Validate signature, etc.

            var query = _context.Reports
                .Include(r => r.ReportedBy)
                .Include(r => r.ReportedUser)
                .OrderByDescending(r => r.created_at); // Apply ordering before pagination

            var totalReports = await query.CountAsync();
            var reports = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var reportDtos = reports.Select(r => new ReportRequest
            {
                ReportId = r.report_id,
                ReportedBy = r.reported_by,
                ReportedByUsername = r.ReportedBy.username,
                ReportedUser = r.reported_user,
                ReportedUserUsername = r.ReportedUser.username,
                content_type = r.content_type,
                ContentId = r.content_id,
                ReportReason = r.report_reason,
                ReportStatus = r.report_status,
                SeverityLevel = r.severity_level,
                CreatedAt = r.created_at,
                ResolvedAt = r.resolved_at
            })
            .ToList();

            var response = new
            {
                TotalReports = totalReports,
                Page = page,
                PageSize = pageSize,
                Reports = reportDtos
            };

            return Ok(response);
        }

        [HttpGet("CountUnresolved")]
        public async Task<ActionResult<int>> CountUnresolvedReports()
        {
            // Count the unresolved reports (e.g., assuming "unresolved" means a specific status value)
            var unresolvedCount = await _context.Reports
                .Where(r => r.report_status == "Pending") // Adjust based on your actual status value
                .CountAsync();

            return Ok(unresolvedCount);
        }

        [HttpPost("ResolveReport/{reportId}")]
        public async Task<ActionResult> ResolveReport(int reportId)
        {
            // Validate the reportId
            if (reportId <= 0)
                return BadRequest("Invalid report ID.");

            // Fetch the report from the database
            var report = await _context.Reports.FindAsync(reportId);

            if (report == null)
                return NotFound($"No report found with ID {reportId}.");

            if (report.report_status == "Resolved")
                return BadRequest("The report is already resolved.");

            // Update the report status and resolved timestamp
            report.report_status = "Resolved";
            report.resolved_at = DateTime.UtcNow;

            // Save changes to the database
            try
            {
                await _context.SaveChangesAsync();
                return Ok(new { message = "Report resolved successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while resolving the report: {ex.Message}");
            }
        }
    }
}
