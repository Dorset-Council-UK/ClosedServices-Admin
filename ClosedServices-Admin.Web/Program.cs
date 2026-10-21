using ClosedServices_Admin.Components;
using ClosedServices_Admin.Components.Pages.Services;
using ClosedServices_Admin.Data.PublicStatus;
using ClosedServices_Admin.Data.Services;
using ClosedServices_Admin.Embedding;
using ClosedServices_Admin.Validators;
using ClosedServices_Admin_Shared.Options;
using FluentValidation;
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
builder.Services.AddScoped<IServiceOperatingHoursService, ServiceOperatingHoursService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IValidator<UpdateServiceStatusFormModel>, UpdateServiceStatusFormModelValidator>();
builder.Services.AddPublicStatusQueries();
builder.Services.AddPublicStatusRateLimiting(
    builder.Configuration,
    (context, cancellationToken) => WidgetResponses.WriteErrorAsync(
        context,
        StatusCodes.Status429TooManyRequests,
        "Too many requests. Please try again shortly.",
        cancellationToken));
builder.Services.AddRazorPages();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Resolve via DI (not builder.Configuration.Get<>) so the PostConfigure normalisation
// registered in AddClosedServicesOptions has been applied to PathBase.
var options = app.Services.GetRequiredService<IOptions<ClosedServicesOptions>>().Value;

app.UsePathBase(options.PathBase);

app.UseForwardedHeaders();
app.UseWidgetResponses();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
app.UseWhen(context => !WidgetResponses.IsWidgetRequest(context), branch =>
{
    if (!app.Environment.IsDevelopment())
    {
        branch.UseExceptionHandler("/Error", createScopeForErrors: true);
    }

    branch.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
});

if (options.UseHttpsRedirection)
{
    app.UseHttpsRedirection();
}

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.UseRateLimiter();
app.MapStaticAssets();
app.MapRazorPages();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapAuthenticationEndpoints();

app.Run();
