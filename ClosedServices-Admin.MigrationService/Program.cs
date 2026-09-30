using ClosedServices_Admin.MigrationService;
using ClosedServices_Admin.Data;
using ClosedServices_Admin_Shared.Options;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddHostedService<Worker>();

builder.Services.Configure<DatabaseOptions>(builder.Configuration
    .GetSection(ClosedServicesOptions.SectionName)
    .GetSection(DatabaseOptions.SectionName));


var databaseOptions = builder.Configuration
    .GetSection(ClosedServicesOptions.SectionName)
    .GetSection(DatabaseOptions.SectionName)
    .Get<DatabaseOptions>();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString(ClosedServicesOptions.ConnectionStringName)
        ?? throw new InvalidOperationException($"Connection string '{ClosedServicesOptions.ConnectionStringName}' was not found.");

        options.UseNpgsql(connectionString, x =>
        {
            x.MigrationsHistoryTable("__EFMigrationsHistory", databaseOptions?.Schema);
            x.UseNodaTime();
            x.UseNetTopologySuite();
            x.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
        })
        .UseSnakeCaseNamingConvention();
});

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(Worker.ActivitySourceName));

var host = builder.Build();
host.Run();
