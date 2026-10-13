using ClosedServices_Admin.Data.PublicStatus;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using NodaTime;
using System.Globalization;

namespace ClosedServices_Admin.Pages.Embed;

[AllowAnonymous]
[EnableRateLimiting(PublicStatusRateLimitingExtensions.PolicyName)]
public sealed class ServiceStatusModel(IPublicStatusService statusService) : PageModel
{
    public PublicStatusSnapshot? Snapshot { get; private set; }
    public string? Error { get; private set; }
    public string RefreshUrl => $"{Request.PathBase}{Request.Path}{Request.QueryString}";
    public string AssetPath(string fileName) => Request.PathBase.Add($"/embed-assets/{fileName}").Value!;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var parameters = Request.Query.Select(pair => new KeyValuePair<string, string[]>(
            pair.Key, pair.Value.Select(value => value ?? string.Empty).ToArray()));
        if (!PublicStatusRequest.TryParse(parameters, out var request, out var error))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            Error = error;
            return;
        }
        var result = await statusService.GetStatusAsync(request.Filter, cancellationToken);
        Snapshot = result.Snapshot;
        if (Snapshot is null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            Error = result.NotFoundReason;
        }
    }

    public static string StatusLabel(string status) => status switch
    {
        "closed" => "Closed",
        "partiallyClosed" => "Partially closed",
        _ => "No disruption"
    };

    public static string FormatDate(DateTimeOffset value)
    {
        var local = Instant.FromDateTimeOffset(value).InZone(DateTimeZoneProviders.Tzdb["Europe/London"]);
        return $"{local.ToDateTimeUnspecified().ToString("d MMM yyyy, HH:mm", CultureInfo.GetCultureInfo("en-GB"))} "
            + (local.Offset == Offset.Zero ? "GMT" : "BST");
    }

    public static string MachineDate(DateTimeOffset value) => value.ToString("O", CultureInfo.InvariantCulture);
}