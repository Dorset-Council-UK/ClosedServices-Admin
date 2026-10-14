namespace ClosedServices_Admin.Tests;

public class ServiceTypeRouteHelperTests
{
    [Theory]
    [InlineData("Adult/Family Services")]
    [InlineData("Adult Family Services")]
    [InlineData("Libraries & Archives")]
    [InlineData("Help%Support")]
    public void EncodeSegment_ThenDecodeSegment_RoundTripsFriendlyServiceTypeNames(string serviceTypeName)
    {
        var encoded = ServiceTypeRouteHelper.EncodeSegment(serviceTypeName);

        var decoded = ServiceTypeRouteHelper.DecodeSegment(encoded);

        Assert.Equal(serviceTypeName, decoded);
    }

    [Fact]
    public void EncodeSegment_EncodesPathSeparatorsAndSpaces()
    {
        var encoded = ServiceTypeRouteHelper.EncodeSegment("Adult/Family Services");

        Assert.Equal("Adult%2FFamily%20Services", encoded);
    }

    [Fact]
    public void DecodeSegment_InvalidEscapeSequence_ReturnsOriginalValue()
    {
        const string routeValue = "Adult%2GFamily Services";

        var decoded = ServiceTypeRouteHelper.DecodeSegment(routeValue);

        Assert.Equal(routeValue, decoded);
    }
}
