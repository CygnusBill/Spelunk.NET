namespace Spelunk.Server.Modes;

/// <summary>
/// Interface for Spelunk server modes (stdio or SSE)
/// </summary>
public interface IMode
{
    /// <summary>
    /// Run the mode
    /// </summary>
    Task RunAsync(CancellationToken cancellationToken = default);
}
