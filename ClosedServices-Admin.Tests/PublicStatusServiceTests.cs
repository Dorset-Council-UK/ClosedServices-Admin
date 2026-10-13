using ClosedServices_Admin.Data.Enums;
using ClosedServices_Admin.Data.PublicStatus;
using ClosedServices_Admin.Data.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;

namespace ClosedServices_Admin.Tests;

public class PublicStatusServiceTests
{
    private static PublicStatusService CreateService(PublicTestContextFactory factory, PublicTestClock clock) => new(
        factory, new ServiceStatusService(factory, NullLogger<ServiceStatusService>.Instance), clock);

    [Fact]
    public async Task TypeQuery_FiltersInactiveAndOrdersServicesWithoutInferringOpeningHours()
    {
        var factory = new PublicTestContextFactory();
        await PublicStatusTestData.SeedAsync(factory);
        var clock = new PublicTestClock();
        var result = await CreateService(factory, clock).GetStatusAsync(new PublicStatusFilter(" LIBRARIES ", null));
        Assert.Null(result.NotFoundReason);
        var snapshot = Assert.IsType<PublicStatusSnapshot>(result.Snapshot);
        Assert.Equal(PublicStatusTestData.Now.ToDateTimeOffset(), snapshot.GeneratedAt);
        Assert.Equal(1, clock.Calls);
        Assert.Equal(new[] { PublicStatusTestData.OtherServiceId, PublicStatusTestData.ServiceId },
            snapshot.Services.Select(service => service.ServiceId));
        Assert.Equal("noDisruption", snapshot.Services[0].Status);
        Assert.Null(snapshot.Services[0].CurrentClosure);
        var closure = Assert.IsType<PublicClosure>(snapshot.Services[1].CurrentClosure);
        Assert.Equal("Maintenance", closure.Reason);
        Assert.Equal(PublicStatusTestData.Message, closure.Message);
        Assert.Equal(TimeSpan.Zero, closure.EffectiveFrom.Offset);
        var upcoming = Assert.Single(snapshot.Services[1].UpcomingClosures);
        Assert.Equal("partiallyClosed", upcoming.Status);
        Assert.Null(upcoming.EffectiveTo);
    }

    [Theory]
    [InlineData("Libraries", "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", true)]
    [InlineData("Leisure centres", "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", false)]
    [InlineData(null, "cccccccc-cccc-cccc-cccc-cccccccccccc", false)]
    [InlineData(null, "ffffffff-ffff-ffff-ffff-ffffffffffff", false)]
    [InlineData("Unknown", null, false)]
    [InlineData("Empty type", null, true)]
    public async Task Queries_RespectIntersectionAndNotFoundSemantics(string? type, string? id, bool found)
    {
        var factory = new PublicTestContextFactory();
        await PublicStatusTestData.SeedAsync(factory);
        var result = await CreateService(factory, new PublicTestClock()).GetStatusAsync(
            new PublicStatusFilter(type, id is null ? null : Guid.Parse(id)));
        Assert.Equal(found, result.Snapshot is not null);
        Assert.Equal(!found, result.NotFoundReason is not null);
        if (type == "Empty type")
        {
            Assert.Empty(result.Snapshot!.Services);
        }
    }

    [Fact]
    public async Task ClockBoundaries_ExpireCurrentAndActivateUpcomingWithoutCaching()
    {
        var factory = new PublicTestContextFactory();
        await PublicStatusTestData.SeedAsync(factory);
        var clock = new PublicTestClock();
        var service = CreateService(factory, clock);
        var filter = new PublicStatusFilter(null, PublicStatusTestData.ServiceId);
        clock.Now = PublicStatusTestData.Now.Plus(Duration.FromHours(2));
        var expired = await service.GetStatusAsync(filter);
        Assert.Equal("noDisruption", Assert.Single(expired.Snapshot!.Services).Status);
        clock.Now = PublicStatusTestData.Now.Plus(Duration.FromHours(3));
        var active = await service.GetStatusAsync(filter);
        var status = Assert.Single(active.Snapshot!.Services);
        Assert.Equal("partiallyClosed", status.Status);
        Assert.Null(status.CurrentClosure!.EffectiveTo);
        Assert.Empty(status.UpcomingClosures);
        Assert.Equal(2, clock.Calls);
    }

    [Fact]
    public async Task OverlappingClosures_UseExistingLatestStartAndUpdateResolution()
    {
        var factory = new PublicTestContextFactory();
        await PublicStatusTestData.SeedAsync(factory);
        await using (var context = factory.CreateDbContext())
        {
            var older = PublicStatusTestData.Closure(PublicStatusTestData.ServiceId, ClosureState.Closed,
                PublicStatusTestData.Now.Minus(Duration.FromHours(1)), null);
            var newer = PublicStatusTestData.Closure(PublicStatusTestData.ServiceId, ClosureState.PartiallyClosed,
                older.EffectiveFrom, null);
            older.UpdatedAt = PublicStatusTestData.Now.Minus(Duration.FromMinutes(5));
            newer.UpdatedAt = PublicStatusTestData.Now;
            newer.Message = "Most recently updated";
            context.ServiceStatusUpdates.AddRange(older, newer);
            await context.SaveChangesAsync();
        }
        var result = await CreateService(factory, new PublicTestClock()).GetStatusAsync(
            new PublicStatusFilter(null, PublicStatusTestData.ServiceId));
        var status = Assert.Single(result.Snapshot!.Services);
        Assert.Equal("partiallyClosed", status.Status);
        Assert.Equal("Most recently updated", status.CurrentClosure!.Message);
    }

    [Fact]
    public async Task CancelledQueries_PropagateCancellation()
    {
        var factory = new PublicTestContextFactory();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateService(factory, new PublicTestClock())
            .GetStatusAsync(new PublicStatusFilter("Libraries", null), cancellation.Token));
    }
}
