using ClosedServices_Admin.Data.PublicStatus;

namespace ClosedServices_Admin.Api.Endpoints;

public static class PublicStatusEndpoints
{
    public static IEndpointRouteBuilder MapPublicStatusEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1").AllowAnonymous()
            .RequireRateLimiting(PublicStatusRateLimitingExtensions.PolicyName);
        group.MapGet("/services/status", (HttpContext context, IPublicStatusService service, CancellationToken cancellationToken) =>
            GetStatusAsync(context, service, cancellationToken));
        group.MapGet("/services/{serviceId}/status", (string serviceId, HttpContext context,
            IPublicStatusService service, CancellationToken cancellationToken) =>
            GetStatusAsync(context, service, cancellationToken, serviceId));
        return endpoints;
    }

    private static async Task<IResult> GetStatusAsync(HttpContext context, IPublicStatusService service,
        CancellationToken cancellationToken, string? serviceId = null)
    {
        var parameters = context.Request.Query.Select(pair => new KeyValuePair<string, string[]>(
            pair.Key, pair.Value.Select(value => value ?? string.Empty).ToArray()));
        if (!PublicStatusRequest.TryParse(parameters, out var request, out var error, serviceId))
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid service filters.", detail: error);
        }
        var result = await service.GetStatusAsync(request!.Filter, cancellationToken);
        if (result.Snapshot is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Service status not found.",
                detail: result.NotFoundReason);
        }
        if (serviceId is not null)
        {
            return Results.Ok(new PublicServiceStatusResponse(result.Snapshot.GeneratedAt, result.Snapshot.Services.Single()));
        }
        return Results.Ok(result.Snapshot);
    }
}
