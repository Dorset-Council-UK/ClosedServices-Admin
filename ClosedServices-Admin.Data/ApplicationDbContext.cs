using ClosedServices_Admin.Data.Models;
using ClosedServices_Admin_Shared.Options;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClosedServices_Admin.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IOptions<DatabaseOptions> databaseOptions) : DbContext(options)
    {
        public DbSet<Service> Services { get; set; }
        public DbSet<ServiceOperatingDay> ServiceOperatingDays { get; set; }
        public DbSet<ServiceStatusUpdate> ServiceStatusUpdates { get; set; }
        public DbSet<UserServicePermission> UserServicePermissions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            if (!string.IsNullOrWhiteSpace(databaseOptions.Value.Schema))
            {
                modelBuilder.HasDefaultSchema(databaseOptions.Value.Schema);
            }
            
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        }
    }
}
