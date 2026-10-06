using ClosedServices_Admin.Components;
using ClosedServices_Admin.Data.Services;
using ClosedServices_Admin_Shared.Options;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder
    .AddClosedServicesNetworking()
    .AddClosedServicesOptions()
    .AddClosedServicesAzureKeyVault()
    .AddClosedServicesDatabase()
    .AddClosedServicesTelemetry()
    .AddAuthentication();

// Add services to the container.

builder.Services.AddScoped<IServicesService, ServicesService>();
builder.Services.AddScoped<IServiceStatusService, ServiceStatusService>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Resolve via DI (not builder.Configuration.Get<>) so the PostConfigure normalisation
// registered in AddClosedServicesOptions has been applied to PathBase.
var options = app.Services.GetRequiredService<IOptions<ClosedServicesOptions>>().Value;

app.UsePathBase(options.PathBase);

app.UseForwardedHeaders();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

if (options.UseHttpsRedirection)
{
    app.UseHttpsRedirection();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAntiforgery();

app.UseRouting();

app.MapStaticAssets();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapAuthenticationEndpoints();

app.Run();
