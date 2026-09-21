namespace ClosedServices_Admin_Shared.Options;

public record ClosedServicesOptions
{
    public const string SectionName = "ClosedServices";
    public const string ConnectionStringName = "ClosedServices";

    public required ApplicationInsightsOptions ApplicationInsights { get; init; }
    public required AzureAdOptions AzureAd { get; init; }
    public required KeyVaultOptions KeyVault { get; init; }
    public required GovNotifySettings GovNotify { get; init; }
    public ThemeOptions Theme { get; init; } = new();
    public DatabaseOptions Database { get; init; } = new();

    public string AppName { get; init; } = "Closed Services Admin";
    public string Version { get; init; } = "DEVELOPMENT";
    public string PathBase { get; init; } = "";
    public bool UseHttpsRedirection { get; init; }
    public string HelpDocsURL { get; init; } = "";
    public bool ShowLanguageSwitcher { get; init; }
}