using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using McpRoslyn.Server.Configuration;

namespace McpRoslyn.Server;

/// <summary>
/// Hosted service for running the MCP Roslyn Server
/// </summary>
public class McpRoslynHostedService : BackgroundService
{
    private readonly McpJsonRpcServer _server;
    private readonly IOptionsMonitor<McpRoslynOptions> _optionsMonitor;
    private readonly ILogger<McpRoslynHostedService> _logger;
    private IDisposable? _optionsChangeToken;
    
    public McpRoslynHostedService(
        McpJsonRpcServer server,
        IOptionsMonitor<McpRoslynOptions> optionsMonitor,
        ILogger<McpRoslynHostedService> logger)
    {
        _server = server;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Log initial configuration
            var options = _optionsMonitor.CurrentValue;
            _logger.LogInformation("Starting MCP Roslyn Server with configuration:");
            _logger.LogInformation("Allowed paths: {Paths}", string.Join(", ", options.AllowedPaths));
            if (!string.IsNullOrEmpty(options.InitialWorkspace))
            {
                _logger.LogInformation("Initial workspace: {Workspace}", options.InitialWorkspace);
            }
            
            // Subscribe to configuration changes
            _optionsChangeToken = _optionsMonitor.OnChange(OnOptionsChanged);
            
            // Run the server
            await _server.RunAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start MCP Roslyn Server");
            throw;
        }
    }
    
    private void OnOptionsChanged(McpRoslynOptions options, string? name)
    {
        _logger.LogInformation("Configuration changed. New allowed paths: {Paths}", 
            string.Join(", ", options.AllowedPaths));
        
        // Update server configuration
        _server.UpdateConfiguration(options);
    }
    
    public override void Dispose()
    {
        _optionsChangeToken?.Dispose();
        base.Dispose();
    }
}