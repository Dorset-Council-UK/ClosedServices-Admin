using ClosedServices_Admin.Components;
using ClosedServices_Admin_Shared.Options;

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
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var options = builder.Configuration.GetSection(ClosedServicesOptions.SectionName).Get<ClosedServicesOptions>();


var app = builder.Build();

app.UsePathBase($"/{options?.PathBase}");

app.UseForwardedHeaders();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

if (options is not null && options.UseHttpsRedirection)
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
