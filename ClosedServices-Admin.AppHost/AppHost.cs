var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin();
var postgresdb = postgres.AddDatabase("closedservices-admin");

builder.AddProject<Projects.ClosedServices_Admin_Web>("closedservices-admin-web")
    .WithReference(postgresdb);

builder.Build().Run();
