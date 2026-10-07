using Microsoft.AspNetCore.Components;

namespace ClosedServices_Admin.Components.Layout;

public partial class GridCard
{
    [Parameter]
    public string HeaderText { get; set; } = "";

    [Parameter]
    public string BodyText { get; set; } = "";

    [Parameter, EditorRequired]
    public CardSizes CardSize { get; set; }

    [Parameter]
    public string LinkURL { get; set; } = "#";

    [Parameter]
    public string AriaLabel { get; set; } = "";

    private string? ComputedAriaLabel => string.IsNullOrWhiteSpace(AriaLabel) ? null : AriaLabel;
    private string? SizeOption { get; set; }

    public enum CardSizes
    {
        None,
        OneHalf,
        OneThird,
    }

    internal virtual string CssClass(CardSizes _cssType)
    {
        return _cssType switch
        {
            CardSizes.OneHalf => "govuk-grid-column-one-half",
            CardSizes.OneThird => "govuk-grid-column-one-third",
            _ => string.Empty,
        };
    }

    protected override void OnParametersSet()
    {
        SizeOption = CssClass(CardSize);
    }
}
