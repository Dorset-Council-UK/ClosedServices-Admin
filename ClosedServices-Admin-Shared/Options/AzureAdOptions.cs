namespace ClosedServices_Admin_Shared.Options;

public record AzureAdOptions
{
    public const string SectionName = "AzureAd";

    public string Instance { get; init; } = "";
    public string Domain { get; init; } = "";
    public string ClientId { get; init; } = "";
    public string TenantId { get; init; } = "";
    public string ClientSecret { get; init; } = "";
    public string SignedOutCallbackPath { get; init; } = "";
    public string SignUpSignInPolicyId { get; init; } = "";
}
