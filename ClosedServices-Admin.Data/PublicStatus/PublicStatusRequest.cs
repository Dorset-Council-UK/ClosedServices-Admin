namespace ClosedServices_Admin.Data.PublicStatus;

public sealed record PublicStatusRequest(PublicStatusFilter Filter)
{
    public static bool TryParse(
        IEnumerable<KeyValuePair<string, string[]>> parameters,
        out PublicStatusRequest? request,
        out string? error,
        string? serviceIdFromPath = null)
    {
        request = null;
        error = null;
        var values = parameters
            .GroupBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key,
                group => group.SelectMany(pair => pair.Value).Select(value => value.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                StringComparer.OrdinalIgnoreCase);

        var recognisedKeys = new[] { "ServiceType", "ServiceID" };
        foreach (var key in recognisedKeys)
        {
            if (values.TryGetValue(key, out var entries) && entries.Length != 1)
            {
                error = $"Provide only one value for {key}.";
                return false;
            }
        }

        var serviceType = values.GetValueOrDefault("ServiceType")?.FirstOrDefault();
        if (serviceType is not null && serviceType.Length == 0)
        {
            error = "ServiceType must not be empty.";
            return false;
        }

        Guid? serviceId = null;
        var idText = values.GetValueOrDefault("ServiceID")?.FirstOrDefault();
        if (idText is not null)
        {
            if (!Guid.TryParse(idText, out var id) || id == Guid.Empty)
            {
                error = "ServiceID must be a nonempty GUID.";
                return false;
            }
            serviceId = id;
        }

        if (serviceIdFromPath is not null)
        {
            if (!Guid.TryParse(serviceIdFromPath, out var pathId) || pathId == Guid.Empty)
            {
                error = "The service ID must be a nonempty GUID.";
                return false;
            }
            if (serviceId.HasValue && serviceId.Value != pathId)
            {
                error = "ServiceID must match the service ID in the URL path.";
                return false;
            }
            serviceId = pathId;
        }

        if (serviceType is null && serviceId is null)
        {
            error = "Provide ServiceType, ServiceID, or both.";
            return false;
        }

        request = new PublicStatusRequest(new PublicStatusFilter(serviceType, serviceId));
        return true;
    }
}
