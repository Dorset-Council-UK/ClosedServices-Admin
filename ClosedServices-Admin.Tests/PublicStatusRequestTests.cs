using ClosedServices_Admin.Data.PublicStatus;

namespace ClosedServices_Admin.Tests;

public class PublicStatusRequestTests
{
    [Fact]
    public void Filters_AreCaseInsensitiveTrimmedAndAcceptIdenticalDuplicates()
    {
        var parameters = new Dictionary<string, string[]>
        {
            ["servicetype"] = [" Libraries ", "LIBRARIES"],
            ["serviceid"] = [PublicStatusTestData.ServiceId.ToString()],
        };
        Assert.True(PublicStatusRequest.TryParse(parameters, out var request, out var error));
        Assert.Null(error);
        Assert.Equal("Libraries", request!.Filter.ServiceType);
        Assert.Equal(PublicStatusTestData.ServiceId, request.Filter.ServiceId);
    }

    [Theory]
    [InlineData("ServiceID", "bad")]
    [InlineData("ServiceID", "00000000-0000-0000-0000-000000000000")]
    [InlineData("ServiceID", "")]
    [InlineData("ServiceType", " ")]
    public void InvalidValues_AreRejected(string key, string value)
    {
        var parameters = new Dictionary<string, string[]> { ["ServiceType"] = ["Libraries"] };
        parameters[key] = [value];
        Assert.False(PublicStatusRequest.TryParse(parameters, out var request, out var error));
        Assert.Null(request);
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("ServiceType", "Libraries", "Leisure")]
    [InlineData("ServiceID", "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")]
    public void ConflictingDuplicates_AreRejected(string key, string first, string second)
    {
        var parameters = new Dictionary<string, string[]> { ["ServiceType"] = ["Libraries"] };
        parameters[key] = [first, second];
        Assert.False(PublicStatusRequest.TryParse(parameters, out _, out _));
    }

    [Fact]
    public void MissingFilters_AreRejectedAndMessagesDefaultToHidden()
    {
        Assert.False(PublicStatusRequest.TryParse(new Dictionary<string, string[]>(), out _, out _));
        Assert.True(PublicStatusRequest.TryParse(
            new Dictionary<string, string[]> { ["ServiceType"] = ["Libraries"] }, out var request, out _));
    }

    [Fact]
    public void PathAndQueryIds_MustMatch()
    {
        var parameters = new Dictionary<string, string[]> { ["ServiceID"] = [PublicStatusTestData.ServiceId.ToString()] };
        Assert.False(PublicStatusRequest.TryParse(parameters, out _, out _, PublicStatusTestData.OtherServiceId.ToString()));
        Assert.True(PublicStatusRequest.TryParse(parameters, out _, out _, PublicStatusTestData.ServiceId.ToString()));
    }
}
