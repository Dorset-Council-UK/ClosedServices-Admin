using ClosedServices_Admin.Components.Pages.Management;
using ClosedServices_Admin.Data.Models;
using ClosedServices_Admin.Data.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging.Abstractions;
using GraphUser = Microsoft.Graph.Models.User;

namespace ClosedServices_Admin.Tests;

public class UserEditTests
{
    [Fact]
    public async Task SaveFailure_PreservesSelectionAndAllowsRetry_ThenNavigatesOnSuccess()
    {
        var users = new FakeUsers();
        var navigation = new TestNavigation();
        var editor = new TestEditor(users, navigation);
        await editor.Load("user");
        users.Save = (_, _, _, _) => throw new InvalidOperationException("Save failed");

        await editor.OnSubmit();

        Assert.Empty(navigation.Destinations);
        users.Save = (_, _, _, _) => Task.CompletedTask;
        await editor.OnSubmit();
        Assert.Equal(2, users.Saves.Count);
        Assert.Equal(users.Saves[0].Types, users.Saves[1].Types);
        Assert.Equal(users.Saves[0].Services, users.Saves[1].Services);
        Assert.Equal(new[] { FakeCatalog.TypeId }, users.Saves[1].Types);
        Assert.Equal(new[] { FakeCatalog.OtherServiceId }, users.Saves[1].Services);
        Assert.Equal("management/users", Assert.Single(navigation.Destinations));
    }

    [Fact]
    public async Task BusySave_IgnoresRepeatedSubmissions()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var users = new FakeUsers { Save = (_, _, _, _) => completion.Task };
        var navigation = new TestNavigation();
        var editor = new TestEditor(users, navigation);
        await editor.Load("user");

        var first = editor.OnSubmit();
        await editor.OnSubmit();
        Assert.Single(users.Saves);
        Assert.Empty(navigation.Destinations);
        completion.SetResult();
        await first;
        Assert.Single(navigation.Destinations);
    }

    [Fact]
    public async Task FailedLoad_PreventsSave_AndRetryRecovers()
    {
        var users = new FakeUsers { ReadUser = (_, _) => throw new InvalidOperationException("Graph unavailable") };
        var navigation = new TestNavigation();
        var editor = new TestEditor(users, navigation);

        await editor.Load("user");
        await editor.OnSubmit();
        Assert.Empty(users.Saves);
        Assert.Empty(navigation.Destinations);
        users.ReadUser = (id, _) => Task.FromResult<GraphUser?>(new() { Id = id });
        await editor.Load("user");
        await editor.OnSubmit();

        Assert.Equal("user", Assert.Single(users.Saves).UserId);
        Assert.Single(navigation.Destinations);
    }

    [Fact]
    public async Task MissingUser_RedirectsWithoutAllowingSave()
    {
        var users = new FakeUsers { ReadUser = (_, _) => Task.FromResult<GraphUser?>(null) };
        var navigation = new TestNavigation();
        var editor = new TestEditor(users, navigation);

        await editor.Load("missing");
        await editor.OnSubmit();

        Assert.Empty(users.Saves);
        Assert.Equal("management/users", Assert.Single(navigation.Destinations));
    }

    private sealed class TestEditor : UserEdit
    {
        [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
        public TestEditor(FakeUsers users, TestNavigation navigation)
            : base(users, new FakeCatalog(), NullLogger<UserEdit>.Instance, navigation)
        {
            Id = "";
        }

        public Task Load(string id)
        {
            Id = id;
            return OnInitializedAsync();
        }
    }

    private sealed class TestNavigation : NavigationManager
    {
        public List<string> Destinations { get; } = [];
        public TestNavigation() => Initialize("https://localhost/", "https://localhost/management/users");
        protected override void NavigateToCore(string uri, bool forceLoad) => Destinations.Add(uri);
    }

    private sealed class FakeUsers : IUserService
    {
        public List<(string UserId, Guid[] Types, Guid[] Services)> Saves { get; } = [];
        public Func<string, CancellationToken, Task<GraphUser?>> ReadUser { get; set; } =
            (id, _) => Task.FromResult<GraphUser?>(new() { Id = id });
        public Func<string, Guid[], Guid[], CancellationToken, Task> Save { get; set; } =
            (_, _, _, _) => Task.CompletedTask;
        public Task<GraphUser?> GetUser(string id, CancellationToken ct = default) => ReadUser(id, ct);
        public Task<IReadOnlyCollection<UserServicePermission>> GetUserPermissions(string userId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyCollection<UserServicePermission>>(
            [
                new() { ExternalUserId = userId, ServiceTypeId = FakeCatalog.TypeId },
                new() { ExternalUserId = userId, ServiceId = FakeCatalog.OtherServiceId }
            ]);
        public Task SaveUserPermissions(string userId, IEnumerable<Guid> serviceTypeIds, IEnumerable<Guid> serviceIds, CancellationToken ct = default)
        {
            var types = serviceTypeIds.ToArray();
            var services = serviceIds.ToArray();
            Saves.Add((userId, types, services));
            return Save(userId, types, services, ct);
        }
        public Task<IList<GraphUser>> GetUsers() => throw new NotSupportedException();
        public Task<IList<GraphUser>> GetUsers(string[] userIds) => throw new NotSupportedException();
        public Task<IList<string>> GetActiveUserIds() => throw new NotSupportedException();
    }

    private sealed class FakeCatalog : IServicesService
    {
        public static readonly Guid TypeId = Guid.NewGuid();
        public static readonly Guid OtherTypeId = Guid.NewGuid();
        public static readonly Guid OtherServiceId = Guid.NewGuid();
        public Task<IReadOnlyCollection<ServiceType>> GetServiceTypes(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyCollection<ServiceType>>(
            [new() { Id = TypeId, Name = "Libraries" }, new() { Id = OtherTypeId, Name = "Centres" }]);
        public Task<IReadOnlyCollection<Service>> GetAllServices(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyCollection<Service>>(
            [new() { Id = OtherServiceId, Name = "Centre", ServiceTypeId = OtherTypeId }]);
        public Task<ServiceType?> GetServiceTypeByName(string name, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Service?> GetService(Guid serviceId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Service?> GetServiceForUser(string userId, Guid serviceId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<Service>> GetServicesByType(ServiceType serviceType, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<Service>> GetServicesForUser(string userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<Service>> GetServicesForUserByType(string userId, ServiceType serviceType, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
