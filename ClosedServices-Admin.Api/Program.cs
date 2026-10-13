using ClosedServices_Admin.Api.Endpoints;
using ClosedServices_Admin.Data;
using ClosedServices_Admin.Data.PublicStatus;
using ClosedServices_Admin_Shared.Options;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using System.Net;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddProblemDetails();
builder.Services.AddPublicStatusQueries();
builder.Services.AddPublicStatusRateLimiting(builder.Configuration);
var section = builder.Configuration.GetSection(ClosedServicesOptions.SectionName);
builder.Services.Configure<DatabaseOptions>(section.GetSection(DatabaseOptions.SectionName));
var databaseOptions = section.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>();
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString(ClosedServicesOptions.ConnectionStringName), postgres =>
    {
        postgres.MigrationsHistoryTable("__EFMigrationsHistory", databaseOptions?.Schema);
        postgres.UseNodaTime();
        postgres.UseNetTopologySuite();
        postgres.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
    }).UseSnakeCaseNamingConvention());

var networking = section.GetSection(NetworkingOptions.SectionName).Get<NetworkingOptions>();
if (networking?.UseForwardedHeadersMiddleware == true)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor;
        if (networking.KnownProxies is not null)
        {
            foreach (var proxy in networking.KnownProxies)
            {
                if (IPAddress.TryParse(proxy, out var address))
                {
                    options.KnownProxies.Add(address);
                }
            }
        }
    });
}

var app = builder.Build();
app.UseForwardedHeaders();
app.UsePathBase(ClosedServicesOptions.NormalisePathBase(section["PathBase"]));
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
        return Task.CompletedTask;
    });
    await next(context);
});
app.UseExceptionHandler(handler => handler.Run(context => Results.Problem(
    statusCode: StatusCodes.Status500InternalServerError,
    title: "Service status is temporarily unavailable.").ExecuteAsync(context)));
app.UseStatusCodePages(context => Results.Problem(statusCode: context.HttpContext.Response.StatusCode)
    .ExecuteAsync(context.HttpContext));
if (section.GetValue<bool>("UseHttpsRedirection"))
{
    app.UseHttpsRedirection();
}
app.UseRouting();
app.UseRateLimiter();
app.MapDefaultEndpoints();
app.MapPublicStatusEndpoints();
app.Run();

namespace ClosedServices_Admin.Api
{
    public sealed class ApiAssemblyMarker;
}
