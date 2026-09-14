using Azure.Identity;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using ClosedServices_Admin.Data;
using ClosedServices_Admin_Shared.Options;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using Npgsql;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.AspNetCore.Builder;
#pragma warning restore IDE0130 // Namespace does not match folder structure

internal static class WebApplicationBuilderExtension
{
    /// <summary>
    /// Add resilience to the HttpClient
    /// </summary>
    internal static WebApplicationBuilder AddClosedServicesNetworking(this WebApplicationBuilder builder)
    {
        //add reslience handlers to http client
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Turn on resilience by default
            http.AddStandardResilienceHandler();
        });

        //add optional forwarded headers middleware handler
        var section = builder.Configuration
            .GetSection(ClosedServicesOptions.SectionName)
            .GetSection(NetworkingOptions.SectionName);
        var networkingOptions = section.Get<NetworkingOptions>();

        if(networkingOptions is not null && networkingOptions.UseForwardedHeadersMiddleware)
        {
            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedProto;

                if (networkingOptions.KnownProxies is not null)
                {
                    foreach (var proxy in networkingOptions.KnownProxies)
                    {
                        if (IPAddress.TryParse(proxy, out var ipAddress))
                        {
                            options.KnownProxies.Add(ipAddress);
                        }
                    }
                }
            });
        }
        

        return builder;
    }

    /// <summary>
    /// Add all the Closed Services options
    /// </summary>
    internal static WebApplicationBuilder AddClosedServicesOptions(this WebApplicationBuilder builder)
    {
        var sectionClosedServices = builder.Configuration.GetSection(ClosedServicesOptions.SectionName);
        var sectionApplicationInsights = sectionClosedServices.GetSection(ApplicationInsightsOptions.SectionName);
        var sectionKeyVault = sectionClosedServices.GetSection(KeyVaultOptions.SectionName);
        var sectionTheme = sectionClosedServices.GetSection(ThemeOptions.SectionName);
        var sectionNetworking = sectionClosedServices.GetSection(NetworkingOptions.SectionName);
        var sectionDatabase = sectionClosedServices.GetSection(DatabaseOptions.SectionName);

        builder.Services
            .Configure<ClosedServicesOptions>(sectionClosedServices)
            .Configure<ApplicationInsightsOptions>(sectionApplicationInsights)
            .Configure<KeyVaultOptions>(sectionKeyVault)
            .Configure<ThemeOptions>(sectionTheme)
            .Configure<NetworkingOptions>(sectionNetworking)
            .Configure<DatabaseOptions>(sectionDatabase);

        return builder;
    }

    /// <summary>
    /// Add Application Insights telemetry if a connection string is provided
    /// </summary>
    internal static WebApplicationBuilder AddClosedServicesTelemetry(this WebApplicationBuilder builder)
    {
        var section = builder.Configuration
            .GetSection(ClosedServicesOptions.SectionName)
            .GetSection(ApplicationInsightsOptions.SectionName);

        var applicationInsightsOptions = section.Get<ApplicationInsightsOptions>();

        if (string.IsNullOrWhiteSpace(applicationInsightsOptions?.ConnectionString))
        {
            return builder;
        }

        builder.Services
            .AddOpenTelemetry()
            .UseAzureMonitor(options => {
                options.ConnectionString = applicationInsightsOptions.ConnectionString;
            });

        return builder;
    }

    /// <summary>
    /// Set up Azure Key Vault if a KeyVault name is provided in the configuration
    /// </summary>
    internal static WebApplicationBuilder AddClosedServicesAzureKeyVault(this WebApplicationBuilder builder)
    {
        var section = builder.Configuration
            .GetSection(ClosedServicesOptions.SectionName)
            .GetSection(KeyVaultOptions.SectionName);

        var keyVaultOptions = section.Get<KeyVaultOptions>();

        if (string.IsNullOrWhiteSpace(keyVaultOptions?.Name))
        {
            return builder;
        }

        using var x509Store = new X509Store(StoreLocation.LocalMachine);
        x509Store.Open(OpenFlags.ReadOnly);

        var x509Certificate = x509Store.Certificates
            .Find(X509FindType.FindByThumbprint, keyVaultOptions.AzureAd.CertificateThumbprint, validOnly: false)
            .OfType<X509Certificate2>()
            .Single();

        builder.Configuration.AddAzureKeyVault(
            new Uri($"https://{keyVaultOptions.Name}.vault.azure.net/"),
            new ClientCertificateCredential(keyVaultOptions.AzureAd.DirectoryId, keyVaultOptions.AzureAd.ApplicationId, x509Certificate));

        return builder;
    }

    /// <summary>
    /// Add the Microsoft Identity Web App authentication
    /// </summary>
    internal static WebApplicationBuilder AddClosedServicesAuthentication(this WebApplicationBuilder builder)
    {
        const string openIdConnectClientName = "OpenIDConnectResilient";
        const string graphClientName = "MicrosoftGraphResilient";

        var azureAdSection = builder.Configuration
            .GetSection(ClosedServicesOptions.SectionName)
            .GetSection(AzureAdOptions.SectionName);

        // Register named HttpClients for external auth calls with resilience
        builder.Services
            .AddHttpClient(openIdConnectClientName)
            .AddStandardResilienceHandler();

        builder.Services
            .AddHttpClient(graphClientName, httpClient =>
            {
                httpClient.BaseAddress = new Uri("https://graph.microsoft.com/");
            })
            .AddStandardResilienceHandler();

        // Add microsoft identity web app authentication
        builder.Services
            .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
            .AddMicrosoftIdentityWebApp(options =>
            {
                azureAdSection.Bind(options);
                options.ResponseType = "code";

                if (string.IsNullOrWhiteSpace(options.SignedOutCallbackPath))
                {
                    options.SignedOutCallbackPath = "/signout-callback-oidc";
                }

                options.ErrorPath = "/Error";
                options.SignedOutRedirectUri = "/account/signedout";
                options.AccessDeniedPath = "/account/accessdenied";
            });
        builder.Services.AddRazorPages();
        builder.Services.AddControllersWithViews().AddMicrosoftIdentityUI();

        // Configure OpenIdConnectOptions to use our resilient HttpClient
        builder.Services
            .AddOptions<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme)
            .Configure<IHttpClientFactory>((options, httpClientFactory) =>
            {
                options.Backchannel = httpClientFactory.CreateClient(openIdConnectClientName);
                options.SignedOutRedirectUri = "/account/signedout";
                options.AccessDeniedPath = "/account/accessdenied";
                options.Events ??= new OpenIdConnectEvents();
                var existingRedirectHandler = options.Events.OnRedirectToIdentityProvider;
                var existingOnRemoteFailureHandler = options.Events.OnRemoteFailure;
                var existingOnTokenValidatedHandler = options.Events.OnTokenValidated;

                // Workaround for Entra External ID stale session errors on first login of the day.
                // When a user's Entra session expires overnight, the first authentication attempt can fail
                // with AADSTS50133 (session invalid due to expiry) or AADSTS165000 (session context missing).
                // This handler retries authentication once to obtain a fresh session. If the retry also fails
                // (indicating a genuine issue like a required password change), the user is redirected to an
                // error page to avoid an infinite redirect loop.
                options.Events.OnRemoteFailure = async context =>
                {
                    if (context.Failure?.Message?.Contains("AADSTS50133", StringComparison.Ordinal) == true ||
                        context.Failure?.Message?.Contains("AADSTS165000", StringComparison.Ordinal) == true)
                    {
                        const string authRetryCookieName = "authretry";
                        var hasRetried = context.Request.Cookies.ContainsKey(authRetryCookieName);

                        if (!hasRetried)
                        {
                            context.Response.Cookies.Append(authRetryCookieName, "1", new CookieOptions
                            {
                                HttpOnly = true,
                                IsEssential = true,
                                Secure = true,
                                SameSite = SameSiteMode.Lax,
                                MaxAge = TimeSpan.FromMinutes(5)
                            });

                            var signInPath = context.Request.PathBase.Add("/MicrosoftIdentity/Account/SignIn");
                            context.Response.Redirect($"{signInPath}?returnUrl=%2F");
                            context.HandleResponse();
                        }
                        else
                        {
                            context.Response.Cookies.Delete(authRetryCookieName);
                            var loginFailedPath = context.Request.PathBase.Add("/Account/LoginFailed");
                            context.Response.Redirect(loginFailedPath);
                            context.HandleResponse();
                        }
                    }
                    else
                    {

                        if (existingOnRemoteFailureHandler != null)
                            await existingOnRemoteFailureHandler(context);
                    }
                };
            });

        builder.Services.Configure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, options =>
        {
            options.AccessDeniedPath = "/account/accessdenied";
        });

        return builder;
    }

    /// <summary>
    /// Add the Closed Services database
    /// </summary>
    internal static WebApplicationBuilder AddClosedServicesDatabase(this WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString(ClosedServicesOptions.ConnectionStringName);

        var databaseOptions = builder.Configuration
            .GetSection(ClosedServicesOptions.SectionName)
            .GetSection(DatabaseOptions.SectionName)
            .Get<DatabaseOptions>();

        builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(connectionString, x =>
            {
                x.MigrationsHistoryTable("__EFMigrationsHistory", databaseOptions?.Schema);
                x.UseNodaTime();
                x.UseNetTopologySuite();
                x.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
            })
            .UseSnakeCaseNamingConvention();

            options.EnableSensitiveDataLogging(builder.Environment.IsDevelopment());
        });

        return builder;
    }
}
