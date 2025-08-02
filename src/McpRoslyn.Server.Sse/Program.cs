using Microsoft.AspNetCore.Connections;
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

try
{
    app.Run();
}
catch (IOException ioEx) when (ioEx.InnerException is AddressInUseException)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("\n=================================================================");
    Console.WriteLine($"ERROR: Port {port} is already in use!");
    Console.WriteLine("=================================================================");
    Console.WriteLine("\nAnother process is already using this port. This is likely:");
    Console.WriteLine("  - Another instance of the SSE server running");
    Console.WriteLine("  - A different application using the same port");
    Console.WriteLine("\nTo fix this issue:");
    Console.WriteLine($"  1. Find the process: lsof -i :{port}");
    Console.WriteLine($"  2. Kill the process: kill <PID>");
    Console.WriteLine($"  3. Or use a different port: --port=<PORT>");
    Console.WriteLine("=================================================================\n");
    Console.ResetColor();
    Environment.Exit(1);
}