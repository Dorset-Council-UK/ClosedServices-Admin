using ClosedServices_Admin.Endpoints.Account;
using ClosedServices_Admin_Shared.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace ClosedServices_Admin.Tests;

public class AuthenticationExtensionsTests
{
    private const string RetryCookie = "authretry-closedservices";

    [Theory]
    [InlineData("", "AADSTS50133")]
    [InlineData("", "AADSTS165000")]
    [InlineData("/closed-services", "AADSTS50133")]
    [InlineData("/closed-services", "AADSTS165000")]
    public async Task RemoteFailure_FirstAttempt_RedirectsToSignIn(string pathBase, string error)
    {
        using var host = CreateHost(pathBase);
        var context = CreateRemoteFailure(host.Services, pathBase, error, hasRetried: false);

        await context.Options.Events.RemoteFailure(context);

        Assert.Equal($"{pathBase}/sign-in", context.Response.Headers.Location.ToString());
        Assert.True(context.Result?.Handled);
        var cookie = Assert.Single(context.Response.Headers.SetCookie);
        Assert.Contains($"{RetryCookie}=1", cookie);
        Assert.Contains("max-age=300", cookie);
        Assert.Contains("secure", cookie);
        Assert.Contains("httponly", cookie);
        Assert.Contains("samesite=lax", cookie);

        // /sign-in accepts redirectUri, not returnUrl; without it, return to the app root.
        var options = host.Services.GetRequiredService<IOptions<ClosedServicesOptions>>();
        var result = AccountEndpoints.SignIn(options, redirectUri: null, loginHint: null, domainHint: null);
        var challenge = Assert.IsType<ChallengeHttpResult>(result.Result);
        Assert.Equal(pathBase.Length == 0 ? "/" : pathBase, challenge.Properties?.RedirectUri);
    }

    [Theory]
    [InlineData("", "AADSTS50133")]
    [InlineData("", "AADSTS165000")]
    [InlineData("/closed-services", "AADSTS50133")]
    [InlineData("/closed-services", "AADSTS165000")]
    public async Task RemoteFailure_SecondAttempt_RedirectsToLoginFailed(string pathBase, string error)
    {
        using var host = CreateHost(pathBase);
        var context = CreateRemoteFailure(host.Services, pathBase, error, hasRetried: true);

        await context.Options.Events.RemoteFailure(context);

        Assert.Equal($"{pathBase}/account/login-failed", context.Response.Headers.Location.ToString());
        Assert.True(context.Result?.Handled);
        Assert.Contains($"{RetryCookie}=;", Assert.Single(context.Response.Headers.SetCookie));
    }

    [Fact]
    public async Task RemoteFailure_UnrelatedError_ChainsExistingHandler()
    {
        var called = false;
        using var host = CreateHost("/closed-services", options =>
            options.Events.OnRemoteFailure = context =>
            {
                called = true;
                context.HandleResponse();
                return Task.CompletedTask;
            });
        var context = CreateRemoteFailure(host.Services, "/closed-services", "Other error", hasRetried: false);

        await context.Options.Events.RemoteFailure(context);

        Assert.True(called);
        Assert.True(context.Result?.Handled);
        Assert.Equal("", context.Response.Headers.Location.ToString());
        Assert.Equal("", context.Response.Headers.SetCookie.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("/closed-services")]
    public async Task SignedOutCallback_DefaultRedirect_StaysUnderPathBase(string pathBase)
    {
        using var host = CreateHost(pathBase);
        using var scope = host.Services.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Method = HttpMethods.Get;
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("example.test");
        context.Request.PathBase = pathBase;
        context.Request.Path = "/signout-callback-oidc";
        var provider = scope.ServiceProvider.GetRequiredService<IAuthenticationHandlerProvider>();
        var handler = await provider.GetHandlerAsync(context, OpenIdConnectDefaults.AuthenticationScheme);
        // Let middleware construct the protected state as it does during a real sign-out.
        await Assert.IsAssignableFrom<IAuthenticationSignOutHandler>(handler).SignOutAsync(new AuthenticationProperties());
        var signOutUrl = new Uri(context.Response.Headers.Location.ToString());
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(signOutUrl.Query);
        context.Request.QueryString = QueryString.Create("state", query["state"].ToString());
        context.Response.Headers.Clear();

        Assert.True(await Assert.IsAssignableFrom<IAuthenticationRequestHandler>(handler).HandleRequestAsync());

        Assert.Equal($"https://example.test{pathBase}/account/signed-out", context.Response.Headers.Location.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("/closed-services")]
    public async Task AccessDeniedCallback_RedirectsUnderPathBaseExactlyOnce(string pathBase)
    {
        using var host = CreateHost(pathBase);
        using var scope = host.Services.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Method = HttpMethods.Get;
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("example.test");
        context.Request.PathBase = pathBase;
        var options = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
            .Get(OpenIdConnectDefaults.AuthenticationScheme);
        context.Request.Path = options.CallbackPath;
        var properties = new AuthenticationProperties { RedirectUri = $"{pathBase}/" };
        properties.Items[".xsrf"] = "test-correlation";
        context.Request.Headers.Cookie = $"{options.CorrelationCookie.Name}test-correlation=N";
        context.Request.QueryString = QueryString.Create(new Dictionary<string, string?>
        {
            ["state"] = options.StateDataFormat.Protect(properties),
            ["error"] = "access_denied",
        });
        var provider = scope.ServiceProvider.GetRequiredService<IAuthenticationHandlerProvider>();
        var handler = await provider.GetHandlerAsync(context, OpenIdConnectDefaults.AuthenticationScheme);

        Assert.True(await Assert.IsAssignableFrom<IAuthenticationRequestHandler>(handler).HandleRequestAsync());

        var location = new Uri(context.Response.Headers.Location.ToString());
        Assert.Equal($"{pathBase}/account/access-denied", location.AbsolutePath);
    }

    private static IHost CreateHost(string pathBase, Action<OpenIdConnectOptions>? configureEvents = null)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ClosedServices:PathBase"] = pathBase,
            ["ClosedServices:AzureAd:Instance"] = "https://login.microsoftonline.com/",
            ["ClosedServices:AzureAd:TenantId"] = "11111111-1111-1111-1111-111111111111",
            ["ClosedServices:AzureAd:ClientId"] = "22222222-2222-2222-2222-222222222222",
        });
        builder.Services.Configure<ClosedServicesOptions>(builder.Configuration.GetSection("ClosedServices"));
        builder.AddAuthentication();
        if (configureEvents is not null)
        {
            // Register after Identity.Web's setup but before our event-wrapping configuration.
            var wrapperIndex = builder.Services.Select((descriptor, index) => (descriptor, index))
                .Last(item => item.descriptor.ServiceType == typeof(IConfigureOptions<OpenIdConnectOptions>)).index;
            builder.Services.Configure(OpenIdConnectDefaults.AuthenticationScheme, configureEvents);
            var handlerRegistration = builder.Services[^1];
            builder.Services.RemoveAt(builder.Services.Count - 1);
            builder.Services.Insert(wrapperIndex, handlerRegistration);
        }
        // Keep middleware tests offline; no Entra metadata or token requests are needed.
        builder.Services.PostConfigure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme,
            options => options.ConfigurationManager = new Microsoft.IdentityModel.Protocols.StaticConfigurationManager<OpenIdConnectConfiguration>(new OpenIdConnectConfiguration
            {
                EndSessionEndpoint = "https://identity.example.test/logout",
            }));
        return builder.Build();
    }

    private static RemoteFailureContext CreateRemoteFailure(IServiceProvider services, string pathBase,
        string error, bool hasRetried)
    {
        var options = services.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
            .Get(OpenIdConnectDefaults.AuthenticationScheme);
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.PathBase = pathBase;
        if (hasRetried)
        {
            context.Request.Headers.Cookie = $"{RetryCookie}=1";
        }
        var scheme = new AuthenticationScheme(OpenIdConnectDefaults.AuthenticationScheme, null,
            typeof(OpenIdConnectHandler));
        return new RemoteFailureContext(context, scheme, options, new InvalidOperationException(error));
    }
}
