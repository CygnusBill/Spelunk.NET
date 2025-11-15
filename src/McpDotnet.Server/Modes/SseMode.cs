using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Connections;
using Microsoft.Build.Locator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.AspNetCore;
using Spelunk.Server.Configuration;
using Spelunk.Server.Tools;

namespace Spelunk.Server.Modes;

/// <summary>
/// SSE mode - runs the MCP server over HTTP with Server-Sent Events
/// </summary>
public class SseMode : IMode
{
    private readonly int _port;
    private readonly bool _isBackgroundProcess;

    public SseMode(int port, bool isBackgroundProcess = false)
    {
        _port = port;
        _isBackgroundProcess = isBackgroundProcess;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        // Register MSBuild once at startup
        if (!MSBuildLocator.IsRegistered)
        {
            MSBuildLocator.RegisterDefaults();
        }

        var builder = WebApplication.CreateBuilder();

        // Configure Kestrel to listen on specified port
        builder.WebHost.UseUrls($"http://localhost:{_port}");

        // User config path
        var userConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".spelunk", "config.json");

        // Add user configuration source
        if (File.Exists(userConfigPath))
        {
            builder.Configuration.AddJsonFile(userConfigPath, optional: true, reloadOnChange: true);
        }

        // Support legacy environment variable
        var legacyAllowedPaths = Environment.GetEnvironmentVariable("SPELUNK_ALLOWED_PATHS");
        if (!string.IsNullOrEmpty(legacyAllowedPaths))
        {
            var paths = legacyAllowedPaths.Split(Path.PathSeparator);
            var inMemoryConfig = new Dictionary<string, string?>();
            for (int i = 0; i < paths.Length; i++)
            {
                inMemoryConfig[$"Spelunk:AllowedPaths:{i}"] = paths[i];
            }
            builder.Configuration.AddInMemoryCollection(inMemoryConfig);
        }

        builder.Configuration.AddEnvironmentVariables("SPELUNK__");

        // Configure and validate options
        builder.Services.AddOptions<SpelunkOptions>()
            .Bind(builder.Configuration.GetSection(SpelunkOptions.SectionName))
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

        // Initialize the static tools with configuration
        var optionsMonitor = app.Services.GetRequiredService<IOptionsMonitor<SpelunkOptions>>();
        var logger = app.Services.GetRequiredService<ILogger<SseMode>>();
        DotnetTools.Initialize(optionsMonitor, logger);

        // Map MCP endpoints
        app.MapMcp();

        app.Logger.LogInformation("Spelunk SSE Server starting on port {Port}", _port);
        app.Logger.LogInformation("Allowed paths: {Paths}", string.Join(", ", optionsMonitor.CurrentValue.AllowedPaths));

        try
        {
            await app.RunAsync(cancellationToken);
        }
        catch (IOException ioEx) when (ioEx.InnerException is AddressInUseException)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\n=================================================================");
            Console.WriteLine($"ERROR: Port {_port} is already in use!");
            Console.WriteLine("=================================================================");
            Console.WriteLine("\nAnother process is already using this port. This is likely:");
            Console.WriteLine("  - Another instance of the SSE server running");
            Console.WriteLine("  - A different application using the same port");
            Console.WriteLine("\nTo fix this issue:");
            Console.WriteLine($"  1. Find the process: lsof -i :{_port}");
            Console.WriteLine($"  2. Kill the process: kill <PID>");
            Console.WriteLine($"  3. Or use a different port: spelunk sse -p <PORT>");
            Console.WriteLine("=================================================================\n");
            Console.ResetColor();
            Environment.Exit(1);
        }
    }
}
