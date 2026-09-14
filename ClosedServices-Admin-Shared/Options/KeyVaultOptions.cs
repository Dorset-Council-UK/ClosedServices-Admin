namespace ClosedServices_Admin_Shared.Options;

public record KeyVaultOptions
{
    public const string SectionName = "KeyVault";

    public string Name { get; init; } = "";
    public KeyVaultAzureAdOptions AzureAd { get; init; } = new();
}
