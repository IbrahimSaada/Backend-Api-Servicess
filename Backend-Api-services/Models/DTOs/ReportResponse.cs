

namespace Backend_Api_services.Models.DTOs
{
    public class ReportResponse
    {
        public int ReportedBy { get; set; }
        public int ReportedUser { get; set; }
        public string ContentType { get; set; }  // Use the enum here
        public int ContentId { get; set; }
        public string ReportReason { get; set; } = "";
        public string? resolution_details { get; set; }
    }
}
