var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.ClosedServices_Admin>("closedservices-admin");

builder.Build().Run();
