var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent)
    .WithImage("postgis/postgis")
    .WithPgAdmin();
var postgresdb = postgres.AddDatabase("closedservices-admin");

var migrations = builder.AddProject<Projects.ClosedServices_Admin_MigrationService>("migrations")
    .WithReference(postgresdb)
    .WaitFor(postgresdb);

builder.AddProject<Projects.ClosedServices_Admin_Web>("closedservices-admin-web")
    .WithReference(postgresdb)
    .WithReference(migrations)
    .WaitForCompletion(migrations);

builder.Build().Run();
