
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Backend_Api_services.Models.Entities
{
    [Table("reports")]
    public class Reports
    {
        [Key]
        public int report_id { get; set; }

        public int reported_by { get; set; }

        public int reported_user { get; set; }

        [Required]
        public string? content_type { get; set; }

        public int content_id { get; set; }

        public string report_reason { get; set; } = "";

        public string report_status { get; set; } = "Pending";

        public string? resolution_details { get; set; }

        public string severity_level { get; set; } = "Medium";

        public DateTime created_at { get; set; } = DateTime.UtcNow;

        public DateTime? resolved_at { get; set; }

        // Navigation properties
        [ForeignKey("reported_by")]
        public Users ReportedBy { get; set; }

        [ForeignKey("reported_user")]
        public Users ReportedUser { get; set; }

        [ForeignKey("content_id")]
        public Post PostContent { get; set; }

        [ForeignKey("content_id")]
        public Comment CommentContent { get; set; }
    }
}
