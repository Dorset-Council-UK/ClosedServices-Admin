using System.ComponentModel.DataAnnotations;

namespace ClosedServices_Admin.Data.Models
{
    public class ServiceOperatingDay
    {
        public Guid Id { get; set; }

        public Guid ServiceId { get; set; }
        public Service Service { get; set; } = null!;

        public DayOfWeek DayOfWeek { get; set; }

        public bool IsOpen { get; set; }

        public TimeOnly? OpenTime { get; set; }

        public TimeOnly? CloseTime { get; set; }

        [MaxLength(200)]
        public string? Notes { get; set; }
    }
}
