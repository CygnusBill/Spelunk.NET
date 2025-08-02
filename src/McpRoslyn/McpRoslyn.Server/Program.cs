using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using McpRoslyn.Server.Configuration;
// using McpRoslyn.Server.FSharp; // Disabled for diagnostic PoC

namespace McpRoslyn.Server;

class Program
{
    static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                // Load configuration from multiple sources in priority order
                config.SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true)
                    .AddJsonFile("mcp-roslyn.config.json", optional: true, reloadOnChange: true);
                
                // Support legacy environment variable for backward compatibility
                var legacyAllowedPaths = Environment.GetEnvironmentVariable("MCP_ROSLYN_ALLOWED_PATHS");
                if (!string.IsNullOrEmpty(legacyAllowedPaths))
                {
                    // Convert legacy format to new format
                    var paths = legacyAllowedPaths.Split(Path.PathSeparator);
                    var inMemoryConfig = new Dictionary<string, string?>();
                    for (int i = 0; i < paths.Length; i++)
                    {
                        inMemoryConfig[$"McpRoslyn:AllowedPaths:{i}"] = paths[i];
                    }
                    config.AddInMemoryCollection(inMemoryConfig);
                }
                
                // Support legacy workspace environment variable
                var legacyWorkspace = Environment.GetEnvironmentVariable("MCP_ROSLYN_WORKSPACE");
                if (!string.IsNullOrEmpty(legacyWorkspace))
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["McpRoslyn:InitialWorkspace"] = legacyWorkspace
                    });
                }
                
                // Add new-style environment variables (MCP_ROSLYN__ prefix)
                config.AddEnvironmentVariables("MCP_ROSLYN__");
                
                // Command line arguments have highest priority
                config.AddCommandLine(args, GetCommandLineMappings());
            })
            .ConfigureServices((context, services) =>
            {
                // Configure options with validation
                services.AddOptions<McpRoslynOptions>()
                    .Bind(context.Configuration.GetSection(McpRoslynOptions.SectionName))
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
                
                // Register services
                services.AddSingleton<RoslynWorkspaceManager>();
                // services.AddSingleton<FSharpWorkspaceManager>(); // Disabled for PoC
                services.AddSingleton<McpJsonRpcServer>();
                services.AddHostedService<McpRoslynHostedService>();
            })
            .ConfigureLogging((context, logging) =>
            {
                // Configure logging to stderr to keep stdout clean for JSON-RPC
                logging.ClearProviders();
                logging.AddConsole(options =>
                {
                    options.LogToStandardErrorThreshold = LogLevel.Trace;
                });
                
                // Apply log level from configuration
                var logLevel = context.Configuration
                    .GetSection("McpRoslyn:Logging:MinimumLevel")
                    .Get<LogLevel?>() ?? LogLevel.Information;
                logging.SetMinimumLevel(logLevel);
            })
            .UseConsoleLifetime()
            .Build();
        
        try
        {
            await host.RunAsync();
        }
        catch (OptionsValidationException ex)
        {
            Console.Error.WriteLine("Configuration validation failed:");
            foreach (var failure in ex.Failures)
            {
                Console.Error.WriteLine($"  - {failure}");
            }
            Environment.Exit(1);
        }
    }
    
    private static Dictionary<string, string> GetCommandLineMappings()
    {
        return new Dictionary<string, string>
        {
            // Map legacy command line arguments
            { "--workspace", "McpRoslyn:InitialWorkspace" },
            { "-w", "McpRoslyn:InitialWorkspace" },
            { "--allowed-path", "McpRoslyn:AllowedPaths:0" },  // Simple case for single path
            
            // New style arguments
            { "--config", "ConfigFile" },  // Special handling needed
            { "--log-level", "McpRoslyn:Logging:MinimumLevel" }
        };
    }
}