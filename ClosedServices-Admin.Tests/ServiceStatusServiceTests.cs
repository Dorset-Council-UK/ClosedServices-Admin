using ClosedServices_Admin.Data;
using ClosedServices_Admin.Data.Enums;
using ClosedServices_Admin.Data.Models;
using ClosedServices_Admin.Data.Services;
using ClosedServices_Admin_Shared.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NodaTime;

namespace ClosedServices_Admin.Tests;

public class ServiceStatusServiceTests
{
    [Fact]
    public async Task GetCurrentOrNextStatusUpdate_ReturnsCurrent_WhenCurrentExists()
    {
        var serviceId = Guid.NewGuid();
        var now = SystemClock.Instance.GetCurrentInstant();
        var currentId = Guid.NewGuid();

        await using var contextFactory = CreateContextFactory();
        await SeedAsync(contextFactory, serviceId,
            new ServiceStatusUpdate
            {
                Id = currentId,
                ServiceId = serviceId,
                ClosureState = ClosureState.Closed,
                EffectiveFrom = now.Minus(Duration.FromHours(1)),
                EffectiveTo = now.Plus(Duration.FromHours(1)),
                UpdatedByExternalUserId = "test-user"
            },
            new ServiceStatusUpdate
            {
                Id = Guid.NewGuid(),
                ServiceId = serviceId,
                ClosureState = ClosureState.PartiallyClosed,
                EffectiveFrom = now.Plus(Duration.FromHours(2)),
                EffectiveTo = now.Plus(Duration.FromHours(3)),
                UpdatedByExternalUserId = "test-user"
            });

        var sut = CreateSut(contextFactory);

        var result = await sut.GetCurrentOrNextStatusUpdate(serviceId, now);

        Assert.NotNull(result);
        Assert.Equal(currentId, result.Id);
        Assert.True(result.IsCurrent);
    }

    [Fact]
    public async Task GetCurrentOrNextStatusUpdate_ReturnsNearestUpcoming_WhenNoCurrentExists()
    {
        var serviceId = Guid.NewGuid();
        var now = SystemClock.Instance.GetCurrentInstant();
        var nearestUpcomingId = Guid.NewGuid();

        await using var contextFactory = CreateContextFactory();
        await SeedAsync(contextFactory, serviceId,
            new ServiceStatusUpdate
            {
                Id = Guid.NewGuid(),
                ServiceId = serviceId,
                ClosureState = ClosureState.PartiallyClosed,
                EffectiveFrom = now.Plus(Duration.FromHours(5)),
                EffectiveTo = now.Plus(Duration.FromHours(6)),
                UpdatedByExternalUserId = "test-user"
            },
            new ServiceStatusUpdate
            {
                Id = nearestUpcomingId,
                ServiceId = serviceId,
                ClosureState = ClosureState.Closed,
                EffectiveFrom = now.Plus(Duration.FromHours(2)),
                EffectiveTo = now.Plus(Duration.FromHours(3)),
                UpdatedByExternalUserId = "test-user"
            });

        var sut = CreateSut(contextFactory);

        var result = await sut.GetCurrentOrNextStatusUpdate(serviceId, now);

        Assert.NotNull(result);
        Assert.Equal(nearestUpcomingId, result.Id);
        Assert.False(result.IsCurrent);
    }

    [Fact]
    public async Task GetCurrentOrNextStatusUpdate_ReturnsNull_WhenNoCurrentOrUpcomingExists()
    {
        var serviceId = Guid.NewGuid();
        var now = SystemClock.Instance.GetCurrentInstant();

        await using var contextFactory = CreateContextFactory();
        await SeedAsync(contextFactory, serviceId,
            new ServiceStatusUpdate
            {
                Id = Guid.NewGuid(),
                ServiceId = serviceId,
                ClosureState = ClosureState.Closed,
                EffectiveFrom = now.Minus(Duration.FromHours(5)),
                EffectiveTo = now.Minus(Duration.FromHours(1)),
                UpdatedByExternalUserId = "test-user"
            });

        var sut = CreateSut(contextFactory);

        var result = await sut.GetCurrentOrNextStatusUpdate(serviceId, now);

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteStatusUpdate_DeletesOnlyMatchingServiceRecord()
    {
        var serviceId = Guid.NewGuid();
        var otherServiceId = Guid.NewGuid();
        var now = SystemClock.Instance.GetCurrentInstant();
        var updateId = Guid.NewGuid();
        var otherUpdateId = Guid.NewGuid();

        await using var contextFactory = CreateContextFactory();
        await SeedAsync(contextFactory, serviceId,
            new ServiceStatusUpdate
            {
                Id = updateId,
                ServiceId = serviceId,
                ClosureState = ClosureState.Closed,
                EffectiveFrom = now,
                EffectiveTo = now.Plus(Duration.FromHours(1)),
                UpdatedByExternalUserId = "test-user"
            },
            new ServiceStatusUpdate
            {
                Id = otherUpdateId,
                ServiceId = otherServiceId,
                ClosureState = ClosureState.PartiallyClosed,
                EffectiveFrom = now,
                EffectiveTo = now.Plus(Duration.FromHours(2)),
                UpdatedByExternalUserId = "test-user"
            });

        var sut = CreateSut(contextFactory);

        var deletedFromWrongService = await sut.DeleteStatusUpdate(otherServiceId, updateId);
        var deletedFromCorrectService = await sut.DeleteStatusUpdate(serviceId, updateId);

        Assert.False(deletedFromWrongService);
        Assert.True(deletedFromCorrectService);

        await using var verificationContext = await contextFactory.CreateDbContextAsync();
        var remainingIds = await verificationContext.ServiceStatusUpdates
            .Select(x => x.Id)
            .ToListAsync();

        Assert.DoesNotContain(updateId, remainingIds);
        Assert.Contains(otherUpdateId, remainingIds);
    }

    private static ServiceStatusService CreateSut(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        return new ServiceStatusService(contextFactory, NullLogger<ServiceStatusService>.Instance);
    }

    private static TestDbContextFactory CreateContextFactory()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TestDbContextFactory(options);
    }

    private static async Task SeedAsync(TestDbContextFactory contextFactory, Guid serviceId, params ServiceStatusUpdate[] updates)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var knownServiceIds = new HashSet<Guid>();

        context.Services.Add(new Service
        {
            Id = serviceId,
            Name = "Test Service",
            ServiceType = ServiceType.Libraries,
            IsActive = true
        });
        knownServiceIds.Add(serviceId);

        foreach (var update in updates)
        {
            if (!knownServiceIds.Contains(update.ServiceId))
            {
                context.Services.Add(new Service
                {
                    Id = update.ServiceId,
                    Name = "Other Service",
                    ServiceType = ServiceType.Libraries,
                    IsActive = true
                });

                knownServiceIds.Add(update.ServiceId);
            }

            context.ServiceStatusUpdates.Add(update);
        }

        await context.SaveChangesAsync();
    }

    private sealed class TestDbContextFactory(DbContextOptions<ApplicationDbContext> options) : IDbContextFactory<ApplicationDbContext>, IAsyncDisposable
    {
        private readonly IOptions<DatabaseOptions> databaseOptions = Options.Create(new DatabaseOptions());

        public ApplicationDbContext CreateDbContext()
        {
            return new ApplicationDbContext(options, databaseOptions);
        }

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateDbContext());
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
