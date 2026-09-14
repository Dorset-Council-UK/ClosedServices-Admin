var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.ClosedServices_Admin_Web>("closedservices-admin-web");

builder.Build().Run();
