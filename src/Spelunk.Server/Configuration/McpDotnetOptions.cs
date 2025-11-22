using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;

namespace Spelunk.Server.Configuration;

/// <summary>
/// Configuration options for the MCP Dotnet Server
/// </summary>
public class SpelunkOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "Spelunk";
    
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
    /// Workspace idle timeout in minutes before unloading
    /// </summary>
    [Range(1, 1440, ErrorMessage = "Workspace timeout must be between 1 and 1440 minutes (24 hours)")]
    public int WorkspaceTimeoutMinutes { get; set; } = 15;

    /// <summary>
    /// History retention timeout in hours
    /// </summary>
    [Range(1, 168, ErrorMessage = "History timeout must be between 1 and 168 hours (1 week)")]
    public int HistoryTimeoutHours { get; set; } = 1;

    /// <summary>
    /// Maximum number of ephemeral markers per session
    /// </summary>
    [Range(1, 10000, ErrorMessage = "Max markers must be between 1 and 10000")]
    public int MaxMarkers { get; set; } = 100;

    /// <summary>
    /// Cleanup timer interval in minutes for removing stale workspaces and markers
    /// </summary>
    [Range(1, 60, ErrorMessage = "Cleanup interval must be between 1 and 60 minutes")]
    public int CleanupIntervalMinutes { get; set; } = 10;

    /// <summary>
    /// Enable experimental features
    /// </summary>
    public bool EnableExperimentalFeatures { get; set; } = false;
}