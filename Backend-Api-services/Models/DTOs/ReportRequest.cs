namespace Backend_Api_services.Models.DTOs
{
    public class ReportRequest
    {
        public int ReportId { get; set; }
        public int ReportedBy { get; set; }
        public string ReportedByUsername { get; set; } = "";  // Add the username for clarity
        public int ReportedUser { get; set; }
        public string ReportedUserUsername { get; set; } = "";  // Add the username for clarity
        public string content_type { get; set; } = "";
        public int ContentId { get; set; }
        public string ReportReason { get; set; } = "";
        public string ReportStatus { get; set; } = "Pending";
        public string SeverityLevel { get; set; } = "Medium";
        public string? resolution_details { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }  // Nullable in case it's unresolved
    }
}
