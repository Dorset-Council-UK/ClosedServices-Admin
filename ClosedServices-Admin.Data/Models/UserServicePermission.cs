using ClosedServices_Admin.Data.Enums;
using System.ComponentModel.DataAnnotations;

namespace ClosedServices_Admin.Data.Models
{
    public class UserServicePermission
    {
        public Guid Id { get; set; }
        [Required]
        [MaxLength(200)]
        public string ExternalUserId { get; set; } = string.Empty;
        public Guid? ServiceId { get; set; }
        public Service? Service { get; set; }
        public ServiceType? ServiceType { get; set; }
    }
}
