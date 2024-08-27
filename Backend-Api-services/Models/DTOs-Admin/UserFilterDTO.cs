namespace Backend_Api_services.Models.DTOs_Admin
{
    public class UserFilterDTO
    {
        private DateTime? _startDate;
        private DateTime? _endDate;

        public DateTime? StartDate
        {
            get => _startDate;
            set => _startDate = value.HasValue ? DateTime.SpecifyKind(value.Value.Date, DateTimeKind.Utc).AddHours(0) : (DateTime?)null;
        }

        public DateTime? EndDate
        {
            get => _endDate;
            set => _endDate = value.HasValue ? DateTime.SpecifyKind(value.Value.Date, DateTimeKind.Utc).AddHours(23).AddMinutes(59).AddSeconds(59) : (DateTime?)null;
        }
    }
}
