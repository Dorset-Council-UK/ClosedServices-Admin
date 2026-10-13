using ClosedServices_Admin.Data;
using ClosedServices_Admin.Data.PublicStatus;
using ClosedServices_Admin.Pages.Embed;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using NodaTime;
using System.Globalization;
using System.Net;

namespace ClosedServices_Admin.Tests;

public class PublicWidgetTests
{
    internal sealed class WidgetFactory : WebApplicationFactory<ServiceStatusModel>
    {
        public string PathBase { get; init; } = "/closed-services";
        public int PermitLimit { get; init; } = 120;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseStaticWebAssets();
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ClosedServices:PathBase"] = PathBase,
                    ["ClosedServices:KeyVault:Name"] = "",
                    ["ClosedServices:ApplicationInsights:ConnectionString"] = "",
                    ["ClosedServices:AzureAd:Instance"] = "https://login.microsoftonline.com/",
                    ["ClosedServices:AzureAd:TenantId"] = "11111111-1111-1111-1111-111111111111",
                    ["ClosedServices:AzureAd:ClientId"] = "22222222-2222-2222-2222-222222222222",
                    ["PublicStatus:RateLimit:PermitLimit"] = PermitLimit.ToString(CultureInfo.InvariantCulture)
                }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDbContextFactory<ApplicationDbContext>>();
                services.AddSingleton<IDbContextFactory<ApplicationDbContext>>(new PublicTestContextFactory());
                services.RemoveAll<IClock>();
                services.AddSingleton<IClock>(new PublicTestClock());
                services.PostConfigure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
                {
                    var configuration = new OpenIdConnectConfiguration
                    {
                        AuthorizationEndpoint = "https://identity.example/authorize",
                        TokenEndpoint = "https://identity.example/token",
                        Issuer = "https://identity.example/"
                    };
                    options.Configuration = configuration;
                    options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
                });
            });
        }

        public Task SeedAsync() => PublicStatusTestData.SeedAsync(Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>());
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("/closed-services", false)]
    [InlineData("/closed-services", true)]
    public async Task Widget_IsAnonymousEncodedLightweightAndPathBaseAware(string pathBase, bool showMessages)
    {
        await using var factory = new WidgetFactory { PathBase = pathBase };
        await factory.SeedAsync();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"{pathBase}/embed/service-status?ServiceType=Libraries&ShowMessages={showMessages}");
        request.Headers.Add("Origin", "https://external.example");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType!.MediaType);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        Assert.False(response.Headers.Contains("X-Frame-Options"));
        Assert.Contains("frame-ancestors *", response.Headers.GetValues("Content-Security-Policy").Single());
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Central &lt;Library&gt;", html);
        Assert.Contains("Upcoming disruptions", html);
        Assert.Contains("No current disruption reported", html);
        Assert.Contains("until further notice", html);
        Assert.Contains("13:00 BST", html);
        Assert.DoesNotContain("Inactive Library", html);
        Assert.DoesNotContain("<script>alert", html);
        Assert.DoesNotContain("_framework", html);
        Assert.DoesNotContain("blazor", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("govuk", html);
        Assert.Contains($"{pathBase}/embed-assets/service-status.js", html);
        Assert.Contains($"{pathBase}/embed-assets/service-status.css", html);
        Assert.Contains($"{pathBase}/embed/service-status?ServiceType=Libraries&amp;ShowMessages={showMessages}", html);
        Assert.Equal(showMessages, html.Contains("&lt;script&gt;alert", StringComparison.Ordinal));
        Assert.DoesNotContain("private-updater", html);
        using var scriptResponse = await client.GetAsync($"{pathBase}/embed-assets/service-status.js");
        Assert.Equal(HttpStatusCode.OK, scriptResponse.StatusCode);
        var script = await scriptResponse.Content.ReadAsStringAsync();
        Assert.Contains("60000", script);
        Assert.Contains("window.location.reload()", script);
        using var styles = await client.GetAsync($"{pathBase}/embed-assets/service-status.css");
        Assert.Equal(HttpStatusCode.OK, styles.StatusCode);
    }

    [Theory]
    [InlineData("", HttpStatusCode.BadRequest)]
    [InlineData("?ServiceID=invalid", HttpStatusCode.BadRequest)]
    [InlineData("?ServiceType=Libraries&ShowMessages=invalid", HttpStatusCode.BadRequest)]
    [InlineData("?ServiceType=missing", HttpStatusCode.NotFound)]
    [InlineData("?ServiceType=Libraries&ServiceID=dddddddd-dddd-dddd-dddd-dddddddddddd", HttpStatusCode.NotFound)]
    [InlineData("?ServiceType=Empty%20type", HttpStatusCode.OK)]
    public async Task Widget_QueryErrorsStayLightweight(string query, HttpStatusCode expectedStatus)
    {
        await using var factory = new WidgetFactory();
        await factory.SeedAsync();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var response = await client.GetAsync($"/closed-services/embed/service-status{query}");
        Assert.Equal(expectedStatus, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("blazor", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Refresh now", html);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.False(response.Headers.Contains("X-Frame-Options"));
        if (expectedStatus == HttpStatusCode.OK)
        {
            Assert.Contains("No active services", html);
        }
    }

    [Fact]
    public async Task Widget_UnexpectedFailuresRemainGenericHtmlAndRefreshable()
    {
        await using var factory = new WidgetFactory();
        using var failingFactory = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPublicStatusService>();
            services.AddScoped<IPublicStatusService, ThrowingPublicStatusService>();
        }));
        using var client = failingFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var response = await client.GetAsync("/closed-services/embed/service-status?ServiceType=Libraries");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType!.MediaType);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("temporarily unavailable", html);
        Assert.Contains("service-status.js", html);
        Assert.DoesNotContain("private failure", html);
        Assert.DoesNotContain("blazor", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("frame-ancestors *", response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.True(response.Headers.CacheControl!.NoStore);
    }

    [Fact]
    public async Task RateLimiting_IsWidgetOnlyAndAdminStillRequiresAuthentication()
    {
        await using var factory = new WidgetFactory { PermitLimit = 1 };
        await factory.SeedAsync();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var first = await client.GetAsync("/closed-services/embed/service-status?ServiceType=Libraries");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var rejected = await client.GetAsync("/closed-services/embed/service-status?ServiceType=Libraries");
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal("text/html", rejected.Content.Headers.ContentType!.MediaType);
        Assert.Contains("Refresh now", await rejected.Content.ReadAsStringAsync());
        Assert.NotNull(rejected.Headers.RetryAfter);
        Assert.Contains("frame-ancestors *", rejected.Headers.GetValues("Content-Security-Policy").Single());
        using var admin = await client.GetAsync("/closed-services/services/Libraries");
        Assert.Equal(HttpStatusCode.Redirect, admin.StatusCode);
        Assert.NotNull(admin.Headers.Location);
        Assert.Equal("SAMEORIGIN", admin.Headers.GetValues("X-Frame-Options").Single());
        Assert.DoesNotContain("frame-ancestors *", admin.Headers.GetValues("Content-Security-Policy").Single());
    }

    [Fact]
    public void DateFormatting_UsesLondonWinterAndSummerOffsets()
    {
        Assert.EndsWith("GMT", ServiceStatusModel.FormatDate(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero)));
        Assert.EndsWith("BST", ServiceStatusModel.FormatDate(new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero)));
    }
}

internal sealed class ThrowingPublicStatusService : IPublicStatusService
{
    public Task<PublicStatusResult> GetStatusAsync(PublicStatusFilter filter, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("private failure details");
}
