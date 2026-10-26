using ClosedServices_Admin.Data.Models;

namespace ClosedServices_Admin.Components.Pages.Management;

public sealed class UserPermissionSelection
{
    public IReadOnlyList<ServiceTypePermissionSelection> Groups { get; }

    public UserPermissionSelection(IEnumerable<ServiceType> types, IEnumerable<Service> services,
        IEnumerable<UserServicePermission> permissions)
    {
        var grants = permissions.ToList();
        var typeIds = grants.Where(grant => grant.ServiceTypeId.HasValue)
            .Select(grant => grant.ServiceTypeId!.Value).ToHashSet();
        var serviceIds = grants.Where(grant => grant.ServiceId.HasValue)
            .Select(grant => grant.ServiceId!.Value).ToHashSet();
        var servicesByType = services.ToLookup(service => service.ServiceTypeId);
        Groups = types.OrderBy(type => type.Order).ThenBy(type => type.Name)
            .Select(type => new ServiceTypePermissionSelection(type, servicesByType[type.Id],
                typeIds.Contains(type.Id), serviceIds)).ToList();
    }

    public IEnumerable<Guid> SelectedTypeIds => Groups.Where(group => group.AllServices).Select(group => group.Id);
    public IEnumerable<Guid> SelectedServiceIds => Groups.Where(group => !group.AllServices)
        .SelectMany(group => group.Services).Where(service => service.IsSelected).Select(service => service.Id);
}

public sealed class ServiceTypePermissionSelection
{
    public Guid Id { get; }
    public string Name { get; }
    public bool AllServices { get; set; }
    public IReadOnlyList<ServicePermissionSelection> Services { get; }

    public ServiceTypePermissionSelection(ServiceType type, IEnumerable<Service> services, bool allServices,
        ISet<Guid> selectedServiceIds)
    {
        Id = type.Id;
        Name = type.Name;
        AllServices = allServices;
        Services = services.OrderBy(service => service.Name)
            .Select(service => new ServicePermissionSelection(service.Id, service.Name,
                !allServices && selectedServiceIds.Contains(service.Id))).ToList();
    }

    public void SetAllServices(bool selected)
    {
        AllServices = selected;
        foreach (var service in Services)
        {
            service.IsSelected = false;
        }
    }

    public void SetService(ServicePermissionSelection service, bool selected)
    {
        if (!AllServices && Services.Contains(service))
        {
            service.IsSelected = selected;
        }
    }
}

public sealed class ServicePermissionSelection(Guid id, string name, bool isSelected)
{
    public Guid Id { get; } = id;
    public string Name { get; } = name;
    public bool IsSelected { get; set; } = isSelected;
}
