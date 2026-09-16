using Azure.Identity;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using ClosedServices_Admin.Data;
using ClosedServices_Admin_Shared.Options;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
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
