using Bogus;
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
        ClosureReason[] closureReasons =
        [
            new()
            {
                Name = "Weather",
                Order = 1,
            },
            new()
            {
                Name = "Staff Shortage",
                Order = 2,
            },
            new()
            {
                Name = "Maintenance issue",
                Order = 3,
            },
            new()
            {
                Name = "Illness",
                Order = 4,
            },
            new()
            {
                Name = "Strike action",
                Order = 5,
            },
            new()
            {
                Name = "Government mandated",
                Order = 6,
            },
            new()
            {
                Name = "Other",
                Order = 99,
            },
        ];

        var fakeSchool = new Faker<Service>()
            .RuleFor(x => x.Name, f => f.Company.CompanyName())
            .RuleFor(x => x.ServiceType, f => Data.Enums.ServiceType.Schools)
            .RuleFor(x => x.ShortDescription, f => f.Lorem.Sentence())
            .RuleFor(x => x.Geom, f => new NetTopologySuite.Geometries.Point(f.Random.Double(330000, 423000), f.Random.Double(67000, 423000)));

        var fakeLibrary = new Faker<Service>()
            .RuleFor(x => x.Name, f => f.Company.CompanyName())
            .RuleFor(x => x.ServiceType, f => Data.Enums.ServiceType.Libraries)
            .RuleFor(x => x.ShortDescription, f => f.Lorem.Sentence())
            .RuleFor(x => x.Geom, f => new NetTopologySuite.Geometries.Point(f.Random.Double(330000, 423000), f.Random.Double(67000, 423000)));

        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            // Seed the database
            await using var transaction = await dbContext.Database
                .BeginTransactionAsync(cancellationToken);

            if(!await dbContext.ClosureReasons.AnyAsync(cancellationToken))
            {
                await dbContext.ClosureReasons.AddRangeAsync(closureReasons, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            if (!await dbContext.Services.AnyAsync(cancellationToken))
            {
                await dbContext.Services.AddRangeAsync(fakeSchool.GenerateBetween(100,200), cancellationToken);
                await dbContext.Services.AddRangeAsync(fakeLibrary.GenerateBetween(10, max: 20), cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        });
    }
}
