var builder = DistributedApplication.CreateBuilder(args);

// Add WebApi project
builder.AddProject<Projects.PMS_WebApi>("webapi");
//builder.AddSqlServer("sql");

builder.Build().Run();
