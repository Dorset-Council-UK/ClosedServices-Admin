using ClosedServices_Admin_Shared.Options;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClosedServices_Admin.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IOptions<DatabaseOptions> databaseOptions) : DbContext(options)
    {

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
