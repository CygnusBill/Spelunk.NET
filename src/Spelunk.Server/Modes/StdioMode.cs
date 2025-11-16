using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spelunk.Server.Configuration;
using Spelunk.Server.FSharp;

namespace Spelunk.Server.Modes;

/// <summary>
/// Stdio mode - runs the MCP JSON-RPC server over standard input/output
/// </summary>
public class StdioMode : IMode
{
    private readonly string[] _args;

    public StdioMode(string[] args)
    {
        _args = args;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var host = Host.CreateDefaultBuilder(_args)
            .ConfigureAppConfiguration((context, config) =>
            {
                // Load configuration from multiple sources in priority order
                config.SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true)
                    .AddJsonFile("spelunk.config.json", optional: true, reloadOnChange: true);

                // Add user-level configuration file from home directory
                var userConfigPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".spelunk", "config.json");
                if (File.Exists(userConfigPath))
                {
                    config.AddJsonFile(userConfigPath, optional: true, reloadOnChange: true);
                }

                // Support legacy environment variable for backward compatibility
                var legacyAllowedPaths = Environment.GetEnvironmentVariable("SPELUNK_ALLOWED_PATHS");
                if (!string.IsNullOrEmpty(legacyAllowedPaths))
                {
                    // Convert legacy format to new format
                    var paths = legacyAllowedPaths.Split(Path.PathSeparator);
                    var inMemoryConfig = new Dictionary<string, string?>();
                    for (int i = 0; i < paths.Length; i++)
                    {
                        inMemoryConfig[$"Spelunk:AllowedPaths:{i}"] = paths[i];
                    }
                    config.AddInMemoryCollection(inMemoryConfig);
                }

                // Support legacy workspace environment variable
                var legacyWorkspace = Environment.GetEnvironmentVariable("SPELUNK_WORKSPACE");
                if (!string.IsNullOrEmpty(legacyWorkspace))
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Spelunk:InitialWorkspace"] = legacyWorkspace
                    });
                }

                // Add new-style environment variables (SPELUNK__ prefix)
                config.AddEnvironmentVariables("SPELUNK__");

                // Command line arguments have highest priority
                config.AddCommandLine(_args, GetCommandLineMappings());
            })
            .ConfigureServices((context, services) =>
            {
                // Configure options with validation
                services.AddOptions<SpelunkOptions>()
                    .Bind(context.Configuration.GetSection(SpelunkOptions.SectionName))
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
                services.AddSingleton<DotnetWorkspaceManager>();
                services.AddSingleton<FSharpWorkspaceManager>();
                services.AddSingleton<McpJsonRpcServer>();
                services.AddHostedService<SpelunkHostedService>();
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
                    .GetSection("Spelunk:Logging:MinimumLevel")
                    .Get<LogLevel?>() ?? LogLevel.Information;
                logging.SetMinimumLevel(logLevel);
            })
            .UseConsoleLifetime()
            .Build();

        try
        {
            await host.RunAsync(cancellationToken);
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
            { "--workspace", "Spelunk:InitialWorkspace" },
            { "-w", "Spelunk:InitialWorkspace" },
            { "--allowed-path", "Spelunk:AllowedPaths:0" },  // Simple case for single path

            // New style arguments
            { "--config", "ConfigFile" },  // Special handling needed
            { "--log-level", "Spelunk:Logging:MinimumLevel" }
        };
    }
}
