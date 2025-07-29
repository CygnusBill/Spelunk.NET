using Microsoft.Build.Locator;
using ModelContextProtocol.AspNetCore;
using McpRoslyn.Server.Sse.Tools;

// Register MSBuild once at startup
MSBuildLocator.RegisterDefaults();

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to listen on a specific port
var port = args.FirstOrDefault(a => a.StartsWith("--port="))?.Split('=')[1] ?? "3333";
builder.WebHost.UseUrls($"http://localhost:{port}");

// Get allowed paths from command line or environment
var allowedPaths = new List<string>();
var envPaths = Environment.GetEnvironmentVariable("MCP_ROSLYN_ALLOWED_PATHS");
if (!string.IsNullOrEmpty(envPaths))
{
    allowedPaths.AddRange(envPaths.Split(';'));
}

// Parse command line for allowed paths
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--allowed-path" && i + 1 < args.Length)
    {
        allowedPaths.Add(args[i + 1]);
    }
}

// Default to current directory if no paths specified
if (allowedPaths.Count == 0)
{
    allowedPaths.Add(Environment.CurrentDirectory);
}

// Add logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Information);
});

// Register MCP server with tools
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

// Initialize the static tools with configuration
RoslynTools.Initialize(allowedPaths, app.Services.GetRequiredService<ILogger<Program>>());

// Map MCP endpoints
app.MapMcp();

app.Logger.LogInformation("MCP Roslyn SSE Server starting on port {Port}", port);
app.Logger.LogInformation("Allowed paths: {Paths}", string.Join(", ", allowedPaths));

app.Run();