using Microsoft.AspNetCore.Connections;
using Microsoft.Build.Locator;
using ModelContextProtocol.AspNetCore;
using McpDotnet.Server.Sse.Tools;
using McpDotnet.Server.Configuration;
using Microsoft.Extensions.Options;

// Register MSBuild once at startup
MSBuildLocator.RegisterDefaults();

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to listen on a specific port
var port = args.FirstOrDefault(a => a.StartsWith("--port="))?.Split('=')[1] ?? "3333";
builder.WebHost.UseUrls($"http://localhost:{port}");

// User config path
var userConfigPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    ".config", "mcp-dotnet", "config.json");

// Add user configuration source (after default sources but before command line)
if (File.Exists(userConfigPath))
{
    builder.Configuration.AddJsonFile(userConfigPath, optional: true, reloadOnChange: true);
}

// Support legacy environment variable
var legacyAllowedPaths = Environment.GetEnvironmentVariable("MCP_ROSLYN_ALLOWED_PATHS");
if (!string.IsNullOrEmpty(legacyAllowedPaths))
{
    var paths = legacyAllowedPaths.Split(Path.PathSeparator);
    var inMemoryConfig = new Dictionary<string, string?>();
    for (int i = 0; i < paths.Length; i++)
    {
        inMemoryConfig[$"McpRoslyn:AllowedPaths:{i}"] = paths[i];
    }
    builder.Configuration.AddInMemoryCollection(inMemoryConfig);
}

builder.Configuration.AddEnvironmentVariables("MCP_ROSLYN__");

// Configure and validate options
builder.Services.AddOptions<McpDotnetOptions>()
    .Bind(builder.Configuration.GetSection(McpDotnetOptions.SectionName))
    .Configure(options =>
    {
        // Default to current directory if no allowed paths specified
        if (options.AllowedPaths.Count == 0)
        {
            options.AllowedPaths.Add(Directory.GetCurrentDirectory());
        }
    })
    .ValidateDataAnnotations()
    .ValidateOnStart();

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

// Initialize the static tools with configuration from IOptionsMonitor
var optionsMonitor = app.Services.GetRequiredService<IOptionsMonitor<McpDotnetOptions>>();
var logger = app.Services.GetRequiredService<ILogger<Program>>();
DotnetTools.Initialize(optionsMonitor, logger);

// Map MCP endpoints
app.MapMcp();

app.Logger.LogInformation("MCP Roslyn SSE Server starting on port {Port}", port);
app.Logger.LogInformation("Allowed paths: {Paths}", string.Join(", ", optionsMonitor.CurrentValue.AllowedPaths));

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