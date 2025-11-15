using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;

namespace McpDotnet.Server.Configuration;

/// <summary>
/// Configuration options for the MCP Roslyn Server
/// </summary>
public class McpDotnetOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "McpRoslyn";
    
    /// <summary>
    /// List of allowed paths that the server can access
    /// </summary>
    [Required(ErrorMessage = "At least one allowed path must be specified")]
    [MinLength(1, ErrorMessage = "At least one allowed path must be specified")]
    public List<string> AllowedPaths { get; set; } = new();
    
    /// <summary>
    /// Initial workspace to load on startup
    /// </summary>
    public string? InitialWorkspace { get; set; }
    
    /// <summary>
    /// Logging configuration
    /// </summary>
    public LoggingOptions Logging { get; set; } = new();
    
    /// <summary>
    /// Server configuration
    /// </summary>
    public ServerOptions Server { get; set; } = new();
}

/// <summary>
/// Logging configuration options
/// </summary>
public class LoggingOptions
{
    /// <summary>
    /// Minimum log level
    /// </summary>
    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;
    
    /// <summary>
    /// Enable debug logging for specific components
    /// </summary>
    public bool EnableDebugLogging { get; set; } = false;
}

/// <summary>
/// Server-specific configuration options
/// </summary>
public class ServerOptions
{
    /// <summary>
    /// Request timeout in seconds
    /// </summary>
    [Range(1, 3600, ErrorMessage = "Request timeout must be between 1 and 3600 seconds")]
    public int RequestTimeoutSeconds { get; set; } = 120;
    
    /// <summary>
    /// Maximum number of concurrent workspaces
    /// </summary>
    [Range(1, 100, ErrorMessage = "Maximum workspaces must be between 1 and 100")]
    public int MaxWorkspaces { get; set; } = 10;
    
    /// <summary>
    /// Enable experimental features
    /// </summary>
    public bool EnableExperimentalFeatures { get; set; } = false;
}