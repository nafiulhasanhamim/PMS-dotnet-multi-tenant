var builder = DistributedApplication.CreateBuilder(args);

// The API.
var webapi = builder.AddProject<Projects.PMS_WebApi>("webapi");
//builder.AddSqlServer("sql");

// The Razor frontend.
//
// It reads the API's address from Api:BaseUrl and holds no project reference to the API, so
// Aspire has to tell it where the API ended up — otherwise it falls back to the port baked
// into appsettings, which is exactly the sort of thing that works until Aspire picks a
// different one.
builder.AddProject<Projects.PMS_Web>("web")
    .WithEnvironment("Api__BaseUrl", webapi.GetEndpoint("https"))
    .WithReference(webapi);

builder.Build().Run();
