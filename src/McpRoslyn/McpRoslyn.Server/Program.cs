using Microsoft.Extensions.Logging;

namespace McpRoslyn.Server;

class Program
{
    static async Task Main(string[] args)
    {
        // Configure logging to stderr to keep stdout clean for JSON-RPC
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .SetMinimumLevel(LogLevel.Information)
                .AddConsole(options =>
                {
                    options.LogToStandardErrorThreshold = LogLevel.Trace;
                });
        });
        
        var logger = loggerFactory.CreateLogger<Program>();
        logger.LogInformation("Starting MCP Roslyn Server...");
        
        // Parse command-line arguments for initial workspace
        string? initialWorkspace = null;
        var allowedPaths = new List<string>();
        
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--workspace":
                case "-w":
                    if (i + 1 < args.Length)
                        initialWorkspace = args[++i];
                    break;
                case "--allowed-path":
                    if (i + 1 < args.Length)
                        allowedPaths.Add(args[++i]);
                    break;
            }
        }
        
        // Also check environment variables
        if (string.IsNullOrEmpty(initialWorkspace))
            initialWorkspace = Environment.GetEnvironmentVariable("MCP_ROSLYN_WORKSPACE");
        
        var allowedPathsEnv = Environment.GetEnvironmentVariable("MCP_ROSLYN_ALLOWED_PATHS");
        if (!string.IsNullOrEmpty(allowedPathsEnv))
            allowedPaths.AddRange(allowedPathsEnv.Split(Path.PathSeparator));
        
        // Default to current directory if no allowed paths specified
        if (allowedPaths.Count == 0)
            allowedPaths.Add(Directory.GetCurrentDirectory());
        
        logger.LogInformation("Allowed paths: {Paths}", string.Join(", ", allowedPaths));
        
        try
        {
            var server = new McpJsonRpcServer(logger, allowedPaths, initialWorkspace);
            await server.RunAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start MCP Roslyn Server");
            Environment.Exit(1);
        }
    }
}

// JSON-RPC types