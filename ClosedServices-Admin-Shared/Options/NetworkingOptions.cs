namespace ClosedServices_Admin_Shared.Options;

public record NetworkingOptions
{
    public const string SectionName = "Networking";

    public bool UseForwardedHeadersMiddleware { get; init; }
    public string[] KnownProxies { get; init; } = [];

}
