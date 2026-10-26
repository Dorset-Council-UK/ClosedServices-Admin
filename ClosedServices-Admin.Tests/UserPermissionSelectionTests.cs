using ClosedServices_Admin.Components.Pages.Management;
using ClosedServices_Admin.Data.Models;

namespace ClosedServices_Admin.Tests;

public class UserPermissionSelectionTests
{
    private readonly ServiceType type = new() { Id = Guid.NewGuid(), Name = "Libraries", Order = 1 };
    private readonly ServiceType otherType = new() { Id = Guid.NewGuid(), Name = "Centres", Order = 1 };
    private readonly ServiceType emptyType = new() { Id = Guid.NewGuid(), Name = "Empty", Order = 2 };

    [Fact]
    public void EmptyGrants_StartUnchecked_AndIncludeEmptyTypesAndInactiveServicesInOrder()
    {
        var services = Services();
        var selection = Create(services);

        Assert.Equal(new[] { otherType.Id, type.Id, emptyType.Id }, selection.Groups.Select(group => group.Id));
        var group = selection.Groups.Single(group => group.Id == type.Id);
        Assert.Equal(new[] { "Alpha", "Zulu" }, group.Services.Select(service => service.Name));
        Assert.All(selection.Groups, group => Assert.False(group.AllServices));
        Assert.All(group.Services, service => Assert.False(service.IsSelected));
        Assert.Empty(selection.Groups.Single(group => group.Id == emptyType.Id).Services);
        Assert.Empty(selection.SelectedServiceIds);
        Assert.Empty(selection.SelectedTypeIds);
    }

    [Fact]
    public void MixedGrants_InitializeExplicitSelections_WithTypePrecedence()
    {
        var services = Services();
        var otherService = new Service { Id = Guid.NewGuid(), Name = "Centre", ServiceTypeId = otherType.Id };
        services.Add(otherService);
        var selection = Create(services,
            new UserServicePermission { ServiceTypeId = type.Id },
            new UserServicePermission { ServiceId = services[0].Id },
            new UserServicePermission { ServiceId = otherService.Id });

        var group = selection.Groups.Single(group => group.Id == type.Id);
        Assert.True(group.AllServices);
        Assert.All(group.Services, service => Assert.False(service.IsSelected));
        Assert.Equal(new[] { type.Id }, selection.SelectedTypeIds);
        Assert.Equal(new[] { otherService.Id }, selection.SelectedServiceIds);
    }

    [Fact]
    public void IndividualGrant_DoesNotSelectSiblingsOrType()
    {
        var services = Services();
        var selection = Create(services, new UserServicePermission { ServiceId = services[0].Id });
        var group = selection.Groups.Single(group => group.Id == type.Id);

        Assert.False(group.AllServices);
        Assert.Single(group.Services.Where(service => service.IsSelected));
        Assert.Equal(new[] { services[0].Id }, selection.SelectedServiceIds);
        Assert.Empty(selection.SelectedTypeIds);
    }

    [Fact]
    public void SelectingType_ClearsChildren_AndDeselectingDoesNotRestoreThem()
    {
        var services = Services();
        var selection = Create(services, new UserServicePermission { ServiceId = services[0].Id });
        var group = selection.Groups.Single(group => group.Id == type.Id);

        group.SetAllServices(true);

        Assert.True(group.AllServices);
        Assert.All(group.Services, service => Assert.False(service.IsSelected));
        Assert.Empty(selection.SelectedServiceIds);
        Assert.Equal(new[] { type.Id }, selection.SelectedTypeIds);
        group.SetService(group.Services[0], true);
        Assert.False(group.Services[0].IsSelected);

        group.SetAllServices(false);

        Assert.False(group.AllServices);
        Assert.All(group.Services, service => Assert.False(service.IsSelected));
        Assert.Empty(selection.SelectedTypeIds);
        Assert.Empty(selection.SelectedServiceIds);
    }

    [Fact]
    public void SelectingEveryIndividual_NeverPromotesToTypeWideGrant()
    {
        var selection = Create(Services());
        var group = selection.Groups.Single(group => group.Id == type.Id);
        foreach (var service in group.Services)
        {
            group.SetService(service, true);
        }

        Assert.False(group.AllServices);
        Assert.Empty(selection.SelectedTypeIds);
        Assert.Equal(2, selection.SelectedServiceIds.Count());

        group.SetService(group.Services[0], false);
        Assert.Single(selection.SelectedServiceIds);
    }

    [Fact]
    public void EmptyType_CanBeSelectedForFutureAccess()
    {
        var selection = Create(Services());
        var group = selection.Groups.Single(group => group.Id == emptyType.Id);

        group.SetAllServices(true);

        Assert.Equal(new[] { emptyType.Id }, selection.SelectedTypeIds);
        Assert.Empty(selection.SelectedServiceIds);
        group.SetAllServices(false);
        Assert.Empty(selection.SelectedTypeIds);
    }

    private List<Service> Services() =>
    [
        new() { Id = Guid.NewGuid(), Name = "Zulu", ServiceTypeId = type.Id },
        new() { Id = Guid.NewGuid(), Name = "Alpha", ServiceTypeId = type.Id, IsActive = false }
    ];

    private UserPermissionSelection Create(IEnumerable<Service> services, params UserServicePermission[] grants) =>
        new([emptyType, type, otherType], services, grants);
}
