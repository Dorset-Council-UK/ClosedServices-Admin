using ClosedServices_Admin.Data;
using ClosedServices_Admin.Data.Models;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace ClosedServices_Admin.MigrationService;

public class Worker(
    IServiceProvider serviceProvider,
    IHostApplicationLifetime hostApplicationLifetime) : BackgroundService
{
    public const string ActivitySourceName = "Migrations";
    private static readonly ActivitySource s_activitySource = new(ActivitySourceName);

    protected override async Task ExecuteAsync(
        CancellationToken cancellationToken)
    {
        using var activity = s_activitySource.StartActivity(
            "Migrating database", ActivityKind.Client);

        try
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            await RunMigrationAsync(dbContext, cancellationToken);
            await SeedDataAsync(dbContext, cancellationToken);
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            throw;
        }

        hostApplicationLifetime.StopApplication();
    }

    private static async Task RunMigrationAsync(
        ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            // Run migration in a transaction to avoid partial migration if it fails.
            await dbContext.Database.MigrateAsync(cancellationToken);
        });
    }

    private static async Task SeedDataAsync(
        ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        Service school = new()
        {
            Name = "Test School",
            ServiceType = Data.Enums.ServiceType.School,
            ShortDescription = "A test school, designed for testing",
            Geom = new NetTopologySuite.Geometries.Point(366646, 101677),
        };

        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            // Seed the database
            await using var transaction = await dbContext.Database
                .BeginTransactionAsync(cancellationToken);

            if (!await dbContext.Services.AnyAsync(x => x.Name == school.Name, cancellationToken))
            {
                await dbContext.Services.AddAsync(school, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        });
    }
}
