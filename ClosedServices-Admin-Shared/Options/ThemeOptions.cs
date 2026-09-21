namespace ClosedServices_Admin_Shared.Options;

public record ThemeOptions
{
    public const string SectionName = "Theme";

    public string PrimaryColour { get; init; } = "#006754";
    public string IconsRoot { get; init; } = "https://gistaticprod.blob.core.windows.net/closed-services/icons";
}
