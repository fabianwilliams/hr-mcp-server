using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using HRMCPServer;
using HRMCPServer.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure the HR MCP Server settings
builder.Services.Configure<HRMCPServerConfiguration>(
    builder.Configuration.GetSection(HRMCPServerConfiguration.SectionName));

// Register the candidate service with Table Storage
builder.Services.AddScoped<ICandidateService, TableStorageCandidateService>();

// Register the data seeding service
builder.Services.AddScoped<DataSeedingService>();

// Add the MCP services: the transport to use (HTTP) and the tools to register.
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();
    
var app = builder.Build();

// Seed initial data if Table Storage is empty
using (var scope = app.Services.CreateScope())
{
    var dataSeedingService = scope.ServiceProvider.GetRequiredService<DataSeedingService>();
    await dataSeedingService.SeedDataAsync();
}

// Configure the application to use the MCP server
app.MapMcp();

// Run the application
// This will start the MCP server and listen for incoming requests.
app.Run();

