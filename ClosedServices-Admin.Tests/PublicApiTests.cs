using ClosedServices_Admin.Api;
using ClosedServices_Admin.Data;
using ClosedServices_Admin.Data.PublicStatus;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.TestHost;
using NodaTime;
using System.Net;
using System.Text.Json;

namespace ClosedServices_Admin.Tests;

public class PublicApiTests
{
    internal sealed class ApiFactory : WebApplicationFactory<ApiAssemblyMarker>
    {
        public string PathBase { get; init; } = "";
        public int PermitLimit { get; init; } = 120;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ClosedServices:PathBase"] = PathBase,
                    ["PublicStatus:RateLimit:PermitLimit"] = PermitLimit.ToString(System.Globalization.CultureInfo.InvariantCulture)
                }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDbContextFactory<ApplicationDbContext>>();
                services.AddSingleton<IDbContextFactory<ApplicationDbContext>>(new PublicTestContextFactory());
                services.RemoveAll<IClock>();
                services.AddSingleton<IClock>(new PublicTestClock());
            });
        }

        public Task SeedAsync() => PublicStatusTestData.SeedAsync(Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>());
    }

    [Fact]
    public async Task ServiceAndTypeQueries_AreAnonymousAndReturnPublicStatus()
    {
        await using var factory = new ApiFactory();
        await factory.SeedAsync();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var single = await client.GetAsync($"/api/v1/services/{PublicStatusTestData.ServiceId}/status");
        Assert.Equal(HttpStatusCode.OK, single.StatusCode);
        Assert.True(single.Headers.CacheControl!.NoStore);
        Assert.False(single.Headers.Contains("Set-Cookie"));
        using var singleJson = JsonDocument.Parse(await single.Content.ReadAsStringAsync());
        Assert.Equal("closed", singleJson.RootElement.GetProperty("service").GetProperty("status").GetString());
        Assert.Single(singleJson.RootElement.GetProperty("service").GetProperty("upcomingClosures").EnumerateArray());
        var raw = singleJson.RootElement.GetRawText();
        Assert.DoesNotContain("private-updater", raw);
        Assert.DoesNotContain("private-source", raw);
        Assert.DoesNotContain("permissions", raw);
        using var byType = await client.GetAsync("/api/v1/services/status?servicetype=%20LIBRARIES%20");
        Assert.Equal(HttpStatusCode.OK, byType.StatusCode);
        using var typeJson = JsonDocument.Parse(await byType.Content.ReadAsStringAsync());
        Assert.Equal(2, typeJson.RootElement.GetProperty("services").GetArrayLength());
        Assert.Equal("Another Library", typeJson.RootElement.GetProperty("services")[0].GetProperty("name").GetString());
    }

    [Theory]
    [InlineData("/api/v1/services/status", HttpStatusCode.BadRequest)]
    [InlineData("/api/v1/services/status?ServiceID=bad", HttpStatusCode.BadRequest)]
    [InlineData("/api/v1/services/bad/status", HttpStatusCode.BadRequest)]
    [InlineData("/api/v1/services/status?ServiceType=missing", HttpStatusCode.NotFound)]
    [InlineData("/api/v1/services/status?ServiceType=Libraries&ServiceType=Leisure", HttpStatusCode.BadRequest)]
    [InlineData("/api/v1/services/status?ServiceID=cccccccc-cccc-cccc-cccc-cccccccccccc", HttpStatusCode.NotFound)]
    [InlineData("/api/v1/services/status?ServiceType=Libraries&ServiceID=dddddddd-dddd-dddd-dddd-dddddddddddd", HttpStatusCode.NotFound)]
    [InlineData("/api/v1/not-found", HttpStatusCode.NotFound)]
    public async Task InvalidAndMissingQueries_ReturnJsonProblems(string url, HttpStatusCode status)
    {
        await using var factory = new ApiFactory();
        await factory.SeedAsync();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var response = await client.GetAsync(url);
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task KnownEmptyType_ReturnsEmptyArray()
    {
        await using var factory = new ApiFactory();
        await factory.SeedAsync();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/services/status?ServiceType=Empty%20type");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Empty(json.RootElement.GetProperty("services").EnumerateArray());
    }

    [Fact]
    public async Task SubpathAndCombinedFilters_ReturnOneAllowlistedService()
    {
        await using var factory = new ApiFactory { PathBase = "/public" };
        await factory.SeedAsync();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync($"/public/api/v1/services/{PublicStatusTestData.ServiceId}/status?ServiceType=Libraries");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var item = json.RootElement.GetProperty("service");
        Assert.Equal(new[] { "serviceId", "name", "serviceType", "status", "currentClosure", "upcomingClosures" }, item.EnumerateObject().Select(property => property.Name));
        Assert.Equal(new[] { "status", "effectiveFrom", "effectiveTo", "reason", "message" }, item.GetProperty("currentClosure").EnumerateObject().Select(property => property.Name));
        Assert.Equal(PublicStatusTestData.Message, item.GetProperty("currentClosure").GetProperty("message").GetString());
        Assert.Equal(PublicStatusTestData.Now.ToDateTimeOffset(), json.RootElement.GetProperty("generatedAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task RateLimits_ReturnJsonProblemsWithRetryAfter()
    {
        await using var factory = new ApiFactory { PermitLimit = 1 };
        await factory.SeedAsync();
        using var client = factory.CreateClient();
        using var first = await client.GetAsync("/api/v1/services/status?ServiceType=Libraries");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var rejected = await client.GetAsync("/api/v1/services/status?ServiceType=Libraries");
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType!.MediaType);
        Assert.NotNull(rejected.Headers.RetryAfter);
        Assert.True(rejected.Headers.CacheControl!.NoStore);
    }

    [Fact]
    public async Task UnexpectedFailures_ReturnGenericJsonInsteadOfHtml()
    {
        await using var factory = new ApiFactory();
        using var failingFactory = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => {
            services.RemoveAll<IPublicStatusService>();
            services.AddScoped<IPublicStatusService, ThrowingPublicStatusService>();
        }));
        using var client = failingFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/services/status?ServiceType=Libraries");
        request.Headers.Accept.ParseAdd("text/html");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        Assert.DoesNotContain("private failure", await response.Content.ReadAsStringAsync());
        Assert.True(response.Headers.CacheControl!.NoStore);
    }
}
