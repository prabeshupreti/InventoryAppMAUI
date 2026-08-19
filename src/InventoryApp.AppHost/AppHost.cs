var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.InventoryApp_Api>("inventoryapp-api");

builder.AddProject<Projects.InventoryApp_Client>("inventoryapp-client");

builder.Build().Run();