using ClosedServices_Admin.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ClosedServices_Admin.Data.EntitiesConfiguration
{
    internal sealed class UserServicePermissionEntityConfiguration : IEntityTypeConfiguration<UserServicePermission>
    {
        public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<UserServicePermission> builder)
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.ExternalUserId).IsRequired().HasMaxLength(200);

            builder.HasOne(x => x.Service)
                .WithMany(x => x.Permissions)
                .HasForeignKey(x => x.ServiceId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(x => x.ExternalUserId);
            builder.HasIndex(x => new { x.ExternalUserId, x.ServiceType });
            builder.HasIndex(x => new { x.ExternalUserId, x.ServiceId });

            var tableIdentifier = StoreObjectIdentifier.Table(
                builder.Metadata.GetTableName()!,
                builder.Metadata.GetSchema());

            var serviceIdColumn = builder.Metadata.FindProperty(nameof(UserServicePermission.ServiceId))!
                .GetColumnName(tableIdentifier)!;
            var serviceTypeColumn = builder.Metadata.FindProperty(nameof(UserServicePermission.ServiceType))!
                .GetColumnName(tableIdentifier)!;

            builder.ToTable(t =>
                t.HasCheckConstraint(
                    "ck_userservicepermission_target",
                    $"""
                    ({serviceIdColumn} IS NOT NULL AND {serviceTypeColumn} IS NULL)
                    OR ({serviceIdColumn} IS NULL AND {serviceTypeColumn} IS NOT NULL)
                    """));
        }
    }
}
