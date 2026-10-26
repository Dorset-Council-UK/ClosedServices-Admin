using System.Security.Claims;
using ClosedServices_Admin.Data;
using ClosedServices_Admin.Data.Models;
using ClosedServices_Admin.Data.Services;
using ClosedServices_Admin_Shared.Options;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ClosedServices_Admin.Tests;

public class UserServicePermissionTests
{
    private static readonly Guid TypeId = Guid.NewGuid();
    private static readonly Guid OtherTypeId = Guid.NewGuid();
    private static readonly Guid EmptyTypeId = Guid.NewGuid();
    private static readonly Guid ServiceId = Guid.NewGuid();
    private static readonly Guid InactiveServiceId = Guid.NewGuid();
    private static readonly Guid OtherServiceId = Guid.NewGuid();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Read_ReturnsOnlyExplicitGrantsForTargetUser()
    {
        using var store = new TestStore();
        var grant = TypeGrant(TypeId);
        await store.Seed(grant, ServiceGrant(OtherServiceId, "other-user"));

        var result = await store.Users.GetUserPermissions("user", Ct);

        Assert.Equal(grant.Id, Assert.Single(result).Id);
        Assert.Null(result.Single().ServiceId);
        Assert.Empty(await store.Users.GetUserPermissions("unknown-user", Ct));
        Assert.Equal(0, store.SaveCalls);
    }

    [Fact]
    public async Task Save_NormalizesDuplicatesAndTypeCoverage_WithoutWideningIndividualGrant()
    {
        using var store = new TestStore();
        await store.Seed();

        await store.Users.SaveUserPermissions("user", [TypeId, TypeId],
            [ServiceId, ServiceId, InactiveServiceId, OtherServiceId, OtherServiceId], Ct);

        var rows = await store.Rows();
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, row => row.ServiceTypeId == TypeId && row.ServiceId is null);
        Assert.Contains(rows, row => row.ServiceId == OtherServiceId && row.ServiceTypeId is null);
        Assert.All(rows, row => Assert.NotEqual(Guid.Empty, row.Id));
        Assert.Equal(1, store.SaveCalls);
    }

    [Fact]
    public async Task Save_PreservesMatchingRows_RemovesDuplicatesAndObsoleteRows_IsIdempotent()
    {
        using var store = new TestStore();
        var preserved = TypeGrant(TypeId);
        var individual = ServiceGrant(OtherServiceId);
        await store.Seed(preserved, individual, ServiceGrant(OtherServiceId), ServiceGrant(ServiceId));

        await store.Users.SaveUserPermissions("user", [TypeId], [OtherServiceId], Ct);
        var first = await store.Rows();
        Assert.Equal(2, first.Count);
        Assert.Contains(first, row => row.Id == preserved.Id);
        Assert.Contains(first, row => row.Id == individual.Id);

        await store.Users.SaveUserPermissions("user", [TypeId, TypeId], [OtherServiceId, OtherServiceId], Ct);
        var second = await store.Rows();
        Assert.Equal(first.Select(row => row.Id).Order(), second.Select(row => row.Id).Order());
        Assert.Equal(2, store.SaveCalls);
    }

    [Fact]
    public async Task Save_AddsRemovesReplacesAndRevokes_WithoutChangingOtherUsers()
    {
        using var store = new TestStore();
        var otherUser = ServiceGrant(ServiceId, "other-user");
        await store.Seed(otherUser);

        await store.Users.SaveUserPermissions("user", [], [ServiceId, OtherServiceId], Ct);
        Assert.Equal(2, (await store.Rows()).Count);
        await store.Users.SaveUserPermissions("user", [], [OtherServiceId], Ct);
        Assert.Equal(OtherServiceId, Assert.Single(await store.Rows()).ServiceId);
        await store.Users.SaveUserPermissions("user", [OtherTypeId], [ServiceId], Ct);
        var replaced = await store.Rows();
        Assert.Equal(2, replaced.Count);
        Assert.Contains(replaced, row => row.ServiceTypeId == OtherTypeId && row.ServiceId is null);
        Assert.Contains(replaced, row => row.ServiceId == ServiceId && row.ServiceTypeId is null);
        await store.Users.SaveUserPermissions("user", [], [], Ct);

        Assert.Empty(await store.Rows());
        var untouched = Assert.Single(await store.Users.GetUserPermissions("other-user", Ct));
        Assert.Equal(otherUser.Id, untouched.Id);
        Assert.Equal(ServiceId, untouched.ServiceId);
        Assert.Equal(4, store.SaveCalls);
    }

    [Fact]
    public async Task Save_AllIndividualServices_DoesNotCreateTypeGrant_AndIncludesInactive()
    {
        using var store = new TestStore();
        await store.Seed();

        await store.Users.SaveUserPermissions("user", [], [ServiceId, InactiveServiceId], Ct);

        var rows = await store.Rows();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, row => Assert.Null(row.ServiceTypeId));
        var accessible = await store.Services.GetServicesForUser("user", Ct);
        Assert.Equal(2, accessible.Count);
        Assert.Contains(accessible, service => service.Id == InactiveServiceId && !service.IsActive);
    }

    [Fact]
    public async Task TypeGrant_CoversFutureServices_AndTypesWithoutServicesCanBeGranted()
    {
        using var store = new TestStore();
        await store.Seed();
        await store.Users.SaveUserPermissions("user", [TypeId, EmptyTypeId], [], Ct);
        var futureId = Guid.NewGuid();
        await using (var context = store.CreateDbContext())
        {
            context.Services.Add(new Service { Id = futureId, Name = "Future", ServiceTypeId = TypeId });
            await context.SaveChangesAsync(Ct);
        }

        var accessible = await store.Services.GetServicesForUser("user", Ct);
        Assert.Contains(accessible, service => service.Id == futureId);
        Assert.Equal(2, (await store.Rows()).Count);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task Save_InvalidSelection_RejectsBeforeAnyMutation(bool invalidType, bool emptyId)
    {
        using var store = new TestStore();
        var original = ServiceGrant(ServiceId);
        await store.Seed(original);
        var invalid = emptyId ? Guid.Empty : Guid.NewGuid();

        await Assert.ThrowsAsync<ArgumentException>(() => store.Users.SaveUserPermissions("user",
            invalidType ? [invalid] : [TypeId], invalidType ? [OtherServiceId] : [invalid], Ct));

        Assert.Equal(original.Id, Assert.Single(await store.Rows()).Id);
        Assert.Equal(0, store.SaveCalls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task InvalidUserId_IsRejectedForReadsAndSaves(string? userId)
    {
        using var store = new TestStore();
        await store.Seed();

        await Assert.ThrowsAsync<ArgumentException>(() => store.Users.GetUserPermissions(userId!, Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => store.Users.SaveUserPermissions(userId!, [], [], Ct));
        Assert.Equal(0, store.SaveCalls);
    }

    [Fact]
    public async Task OverlongUserId_IsRejectedBeforeSave()
    {
        using var store = new TestStore();
        await store.Seed();
        await Assert.ThrowsAsync<ArgumentException>(() => store.Users.SaveUserPermissions(new string('x', 201), [], [], Ct));
        Assert.Equal(0, store.SaveCalls);
    }

    [Fact]
    public async Task Save_CancellationLeavesExistingPermissionsUnchanged()
    {
        using var store = new TestStore();
        var original = TypeGrant(TypeId);
        await store.Seed(original);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            store.Users.SaveUserPermissions("user", [], [], cancellation.Token));

        Assert.Equal(original.Id, Assert.Single(await store.Rows()).Id);
        Assert.Equal(0, store.SaveCalls);
    }

    [Fact]
    public async Task Save_FailureDoesNotPersistChanges()
    {
        using var store = new TestStore();
        var original = TypeGrant(TypeId);
        await store.Seed(original);
        store.FailSave = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.Users.SaveUserPermissions("user", [], [OtherServiceId], Ct));

        Assert.Equal(original.Id, Assert.Single(await store.Rows()).Id);
        Assert.Equal(1, store.SaveCalls);
    }

    [Fact]
    public async Task Catalog_ReturnsOrderedTypesIncludingEmpty_AndAllServicesIncludingInactive()
    {
        using var store = new TestStore();
        await store.Seed();

        var types = await store.Services.GetServiceTypes(Ct);
        Assert.Equal(new[] { OtherTypeId, TypeId, EmptyTypeId }, types.Select(type => type.Id));
        var services = await store.Services.GetAllServices(Ct);
        Assert.Equal(3, services.Count);
        Assert.Contains(services, service => !service.IsActive);
    }

    [Fact]
    public async Task NonAdmin_IsDeniedByEveryPublicMethod()
    {
        using var store = new TestStore(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "user")], "Test")));
        await store.Seed();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => store.Users.GetUsers());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => store.Users.GetUsers(["user"]));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => store.Users.GetUser("user", Ct));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => store.Users.GetUserPermissions("user", Ct));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => store.Users.GetActiveUserIds());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => store.Users.SaveUserPermissions("user", [], [], Ct));
        Assert.Equal(0, store.SaveCalls);
    }

    private static UserServicePermission TypeGrant(Guid typeId) =>
        new() { Id = Guid.NewGuid(), ExternalUserId = "user", ServiceTypeId = typeId };

    private static UserServicePermission ServiceGrant(Guid serviceId, string userId = "user") =>
        new() { Id = Guid.NewGuid(), ExternalUserId = userId, ServiceId = serviceId };

    private static ClaimsPrincipal AdminPrincipal() => new(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Admin")], "Test"));

    private sealed class FixedAuthenticationStateProvider(ClaimsPrincipal user) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(user));
    }

    private sealed class TestStore : IDbContextFactory<ApplicationDbContext>, IDisposable
    {
        private readonly DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        private readonly MemoryCache cache = new(new MemoryCacheOptions());
        private readonly FixedAuthenticationStateProvider authenticationStateProvider;

        public TestStore(ClaimsPrincipal? user = null)
        {
            authenticationStateProvider = new(user ?? AdminPrincipal());
        }
        public int SaveCalls { get; set; }
        public bool FailSave { get; set; }
        public UserService Users => new(this, cache, Options.Create(new ClosedServicesOptions
        {
            ApplicationInsights = new(),
            AzureAd = new(),
            KeyVault = new(),
            GovNotify = new() { ApiKey = "", Templates = new() }
        }), NullLogger<UserService>.Instance, authenticationStateProvider);
        public ServicesService Services => new(this, NullLogger<ServicesService>.Instance);

        public ApplicationDbContext CreateDbContext() => new CountingContext(options, this);

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CreateDbContext());
        }

        public async Task Seed(params UserServicePermission[] permissions)
        {
            await using var context = CreateDbContext();
            context.ServiceTypes.AddRange(
                new ServiceType { Id = EmptyTypeId, Name = "Empty", Order = 2 },
                new ServiceType { Id = TypeId, Name = "Libraries", Order = 1 },
                new ServiceType { Id = OtherTypeId, Name = "Centres", Order = 1 });
            context.Services.AddRange(
                new Service { Id = ServiceId, Name = "Library", ServiceTypeId = TypeId },
                new Service { Id = InactiveServiceId, Name = "Inactive library", ServiceTypeId = TypeId, IsActive = false },
                new Service { Id = OtherServiceId, Name = "Centre", ServiceTypeId = OtherTypeId });
            context.UserServicePermissions.AddRange(permissions);
            await context.SaveChangesAsync(Ct);
            SaveCalls = 0;
        }

        public async Task<IReadOnlyCollection<UserServicePermission>> Rows() => await Users.GetUserPermissions("user", Ct);
        public void Dispose() => cache.Dispose();

        private sealed class CountingContext(DbContextOptions<ApplicationDbContext> options, TestStore store)
            : ApplicationDbContext(options, Options.Create(new DatabaseOptions()))
        {
            public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            {
                store.SaveCalls++;
                if (store.FailSave)
                {
                    throw new InvalidOperationException("Simulated persistence failure");
                }
                return base.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
