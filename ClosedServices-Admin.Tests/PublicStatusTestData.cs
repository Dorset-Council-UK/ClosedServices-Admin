using ClosedServices_Admin.Data;
using ClosedServices_Admin.Data.Enums;
using ClosedServices_Admin.Data.Models;
using ClosedServices_Admin_Shared.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NodaTime;

namespace ClosedServices_Admin.Tests;

internal sealed class PublicTestContextFactory : IDbContextFactory<ApplicationDbContext>
{
    private readonly DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

    public ApplicationDbContext CreateDbContext() => new(options, Options.Create(new DatabaseOptions()));

    public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(CreateDbContext());
    }
}

internal sealed class PublicTestClock : IClock
{
    public Instant Now { get; set; } = PublicStatusTestData.Now;
    public int Calls { get; private set; }
    public Instant GetCurrentInstant()
    {
        Calls++;
        return Now;
    }
}

internal static class PublicStatusTestData
{
    public static readonly Instant Now = Instant.FromUtc(2026, 7, 15, 12, 0);
    public static readonly Guid LibrariesId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid LeisureId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid EmptyTypeId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid ServiceId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid OtherServiceId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    public static readonly Guid InactiveId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    public static readonly Guid LeisureServiceId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    public const string Message = "<script>alert('unsafe')</script> Public closure information";

    public static async Task SeedAsync(IDbContextFactory<ApplicationDbContext> factory)
    {
        await using var context = await factory.CreateDbContextAsync();
        context.ServiceTypes.AddRange(
            new ServiceType { Id = LibrariesId, Name = "Libraries" },
            new ServiceType { Id = LeisureId, Name = "Leisure centres" },
            new ServiceType { Id = EmptyTypeId, Name = "Empty type" });
        context.Services.AddRange(
            new Service { Id = ServiceId, Name = "Central <Library>", ServiceTypeId = LibrariesId, SourceDataId = "private-source" },
            new Service { Id = OtherServiceId, Name = "Another Library", ServiceTypeId = LibrariesId },
            new Service { Id = InactiveId, Name = "Inactive Library", ServiceTypeId = LibrariesId, IsActive = false },
            new Service { Id = LeisureServiceId, Name = "Leisure Centre", ServiceTypeId = LeisureId });
        var reasonId = Guid.NewGuid();
        context.ClosureReasons.Add(new ClosureReason { Id = reasonId, Name = "Maintenance" });
        context.ServiceStatusUpdates.AddRange(
            Closure(ServiceId, ClosureState.Closed, Now.Minus(Duration.FromHours(2)), Now.Plus(Duration.FromHours(2)), reasonId),
            Closure(ServiceId, ClosureState.PartiallyClosed, Now.Plus(Duration.FromHours(3)), null, reasonId),
            Closure(ServiceId, ClosureState.Closed, Now.Minus(Duration.FromDays(1)), Now, reasonId));
        await context.SaveChangesAsync();
    }

    public static ServiceStatusUpdate Closure(Guid serviceId, ClosureState state, Instant from, Instant? to, Guid? reason = null) => new()
    {
        Id = Guid.NewGuid(),
        ServiceId = serviceId,
        ClosureState = state,
        EffectiveFrom = from,
        EffectiveTo = to,
        ClosureReasonId = reason,
        Message = Message,
        UpdatedAt = Now.Minus(Duration.FromMinutes(10)),
        UpdatedByExternalUserId = "private-updater"
    };
}
