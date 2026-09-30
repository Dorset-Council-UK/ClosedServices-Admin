using ClosedServices_Admin.Data.Enums;
using System.ComponentModel.DataAnnotations;
using NetTopologySuite.Geometries;

namespace ClosedServices_Admin.Data.Models
{
    public class Service
    {
        public Guid Id { get; set; }

        [MaxLength(100)]
        public string? SourceDataId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        public ServiceType ServiceType { get; set; }

        [MaxLength(500)]
        public string? ShortDescription { get; set; }

        [MaxLength(1000)]
        public string? Address { get; set; }

        [MaxLength(20)]
        public string? Postcode { get; set; }

        public bool IsActive { get; set; } = true;

        public Point? Geom { get; set; }

        public ICollection<ServiceOperatingDay> OperatingDays { get; set; } = new List<ServiceOperatingDay>();
        public ICollection<ServiceStatusUpdate> StatusUpdates { get; set; } = new List<ServiceStatusUpdate>();
        public ICollection<UserServicePermission> Permissions { get; set; } = new List<UserServicePermission>();

    }
}
