using ClosedServices_Admin.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace ClosedServices_Admin.Data.EntitiesConfiguration
{
    internal sealed class ServiceOperatingDayEntityConfiguration : IEntityTypeConfiguration<ServiceOperatingDay>
    {
        public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<ServiceOperatingDay> builder)
        {
            builder.HasKey(s => s.Id);
            builder.HasOne(x => x.Service)
                .WithMany(x => x.OperatingDays)
                .HasForeignKey(x => x.ServiceId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasIndex(x => new { x.ServiceId, x.DayOfWeek }).IsUnique();

        }
    }
}
