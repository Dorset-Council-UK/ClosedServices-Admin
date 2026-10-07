using ClosedServices_Admin.Data.Enums;
using NodaTime;
using System.ComponentModel.DataAnnotations;

namespace ClosedServices_Admin.Data.Models
{
    public class ServiceStatusUpdate
    {
        public Guid Id { get; set; }

        public Guid ServiceId { get; set; }
        public Service Service { get; set; } = null!;

        public ClosureState ClosureState { get; set; }
        public ClosureReason? ClosureReason { get; set; }
        public Guid? ClosureReasonId { get; set; }

        public string? Message { get; set; }

        public Instant EffectiveFrom { get; set; }

        public Instant? EffectiveTo { get; set; }

        [MaxLength(200)]
        public string UpdatedByExternalUserId { get; set; } = string.Empty;

        public Instant UpdatedAt { get; set; } = SystemClock.Instance.GetCurrentInstant();
    }
}
