using Microsoft.CodeAnalysis;

namespace Spelunk.Server;

/// <summary>
/// Represents a loaded workspace with tracking information
/// </summary>
public class WorkspaceEntry
{
    public Workspace Workspace { get; set; } = null!;
    public string Path { get; set; } = "";
    public DateTime LastAccessTime { get; set; } = DateTime.UtcNow;
    public DateTime LoadedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Update the last access time to current UTC time
    /// </summary>
    public void Touch()
    {
        LastAccessTime = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Check if this workspace has been inactive for the specified duration
    /// </summary>
    public bool IsStale(TimeSpan timeout)
    {
        return DateTime.UtcNow - LastAccessTime > timeout;
    }
}