namespace ClosedServices_Admin;

internal static class ServiceTypeRouteHelper
{
    internal static string EncodeSegment(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return Uri.EscapeDataString(value);
    }

    internal static string DecodeSegment(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        try
        {
            return Uri.UnescapeDataString(value);
        }
        catch (UriFormatException)
        {
            return value;
        }
    }
}
