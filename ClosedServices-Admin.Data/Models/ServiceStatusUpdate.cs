using ClosedServices_Admin.Data.Enums;
using System.ComponentModel.DataAnnotations;

namespace ClosedServices_Admin.Data.Models
{
    public class ServiceStatusUpdate
    {
        public Guid Id { get; set; }

        public Guid ServiceId { get; set; }
        public Service Service { get; set; } = null!;

        public ClosureState ClosureState { get; set; }

        [Required]
        [MaxLength(2000)]
        public string Message { get; set; } = string.Empty;

        public DateTimeOffset EffectiveFrom { get; set; }

        public DateTimeOffset? EffectiveTo { get; set; }

        [MaxLength(200)]
        public string UpdatedByExternalUserId { get; set; } = string.Empty;

        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
