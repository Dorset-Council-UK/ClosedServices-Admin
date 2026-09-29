using ClosedServices_Admin.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace ClosedServices_Admin.Data.EntitiesConfiguration
{
    internal sealed class ServiceEntityConfiguration : IEntityTypeConfiguration<Service>
    {
        public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Service> builder)
        {
            builder.HasKey(s => s.Id);
            builder.Property(s => s.Name).IsRequired().HasMaxLength(200);
            builder.Property(s => s.ShortDescription).HasMaxLength(500);
            builder.Property(s => s.Address).HasMaxLength(1000);
            builder.Property(s => s.Postcode).HasMaxLength(20);
            builder.Property(s => s.ServiceType).IsRequired();
            builder.Property(s => s.IsActive).IsRequired();

        }
    }
}
