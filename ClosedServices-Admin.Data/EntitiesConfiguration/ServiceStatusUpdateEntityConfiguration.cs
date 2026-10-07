using ClosedServices_Admin.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace ClosedServices_Admin.Data.EntitiesConfiguration
{
    internal sealed class ServiceStatusUpdateEntityConfiguration : IEntityTypeConfiguration<ServiceStatusUpdate>
    {
        public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<ServiceStatusUpdate> builder)
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Message).HasMaxLength(1000);
            builder.Property(x => x.UpdatedByExternalUserId).IsRequired().HasMaxLength(200);

            builder.HasOne(x => x.Service)
                .WithMany(x => x.StatusUpdates)
                .HasForeignKey(x => x.ServiceId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(x => new { x.ServiceId, x.UpdatedAt });

        }
    }
}
