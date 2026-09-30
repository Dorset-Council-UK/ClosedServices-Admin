namespace ClosedServices_Admin_Shared.Options;

public record ClosedServicesOptions
{
    public const string SectionName = "ClosedServices";
    public const string ConnectionStringName = "closedservices-admin";

    public required ApplicationInsightsOptions ApplicationInsights { get; init; }
    public required AzureAdOptions AzureAd { get; init; }
    public required KeyVaultOptions KeyVault { get; init; }
    public required GovNotifySettings GovNotify { get; init; }
    public ThemeOptions Theme { get; init; } = new();
    public DatabaseOptions Database { get; init; } = new();

    public string AppName { get; init; } = "Closed Services Admin";
    public string Version { get; init; } = "DEVELOPMENT";

    /// <summary>
    /// The base path the app is hosted under.
    /// Always normalised (see <see cref="NormalisePathBase"/>) to either:
    /// - "" (empty string) when hosted at the root, or
    /// - "/segment" (leading slash, no trailing slash) when hosted under a sub-path.
    /// Normalisation is applied once, centrally, when options are bound (see
    /// WebApplicationBuilderExtension.AddClosedServicesOptions), so consumers never need to
    /// trim/pad slashes themselves. Set with a regular setter (rather than init) so the
    /// post-configure normalisation step can update it after binding.
    /// </summary>
    public string PathBase { get; set; } = "";

    public bool UseHttpsRedirection { get; init; }
    public string HelpDocsURL { get; init; } = "";
    public bool ShowLanguageSwitcher { get; init; }

    /// <summary>
    /// Normalises a raw PathBase value to the app-wide convention: "" for root, or "/segment"
    /// (leading slash, no trailing slash) for a sub-path. Any leading/trailing slashes or
    /// whitespace in the source value are stripped.
    /// </summary>
    public static string NormalisePathBase(string? pathBase)
    {
        if (string.IsNullOrWhiteSpace(pathBase))
        {
            return "";
        }

        var trimmed = pathBase.Trim().Trim('/');
        return trimmed.Length == 0 ? "" : $"/{trimmed}";
    }
}