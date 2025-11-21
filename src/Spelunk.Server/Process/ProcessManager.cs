using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Spelunk.Server.Process;

/// <summary>
/// Manages background SSE server processes
/// </summary>
public class ProcessManager
{
    /// <summary>
    /// Start SSE server in background
    /// </summary>
    public static async Task<(bool success, string message)> StartSseServerAsync(int port)
    {
        // Check if already running
        var existing = PidFileManager.GetRunningProcess();
        if (existing != null)
        {
            return (false, $"SSE server is already running on port {existing.Port} (PID: {existing.Pid})");
        }

        // Get path to current executable
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath))
        {
            return (false, "Could not determine executable path");
        }

        // Spawn detached background process
        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = $"sse --background -p {port}",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true
        };

        try
        {
            var process = System.Diagnostics.Process.Start(startInfo);
            if (process == null)
            {
                return (false, "Failed to start process");
            }

            // Write PID file
            PidFileManager.WritePidFile(process.Id, port);

            // Redirect output to log file in background
            var logPath = PidFileManager.GetLogFilePath();
            var loggingTask = Task.Run(async () =>
            {
                try
                {
                    using var logFile = new StreamWriter(logPath, append: true) { AutoFlush = true };
                    await logFile.WriteLineAsync($"\n=== SSE Server started at {DateTime.Now} on port {port} (PID: {process.Id}) ===");

                    // Create tasks for both stdout and stderr
                    var stdoutTask = Task.Run(async () =>
                    {
                        try
                        {
                            string? line;
                            while ((line = await process.StandardOutput.ReadLineAsync()) != null)
                            {
                                await logFile.WriteLineAsync(line);
                            }
                        }
                        catch (Exception ex)
                        {
                            await logFile.WriteLineAsync($"Error reading stdout: {ex.Message}");
                        }
                    });

                    var stderrTask = Task.Run(async () =>
                    {
                        try
                        {
                            string? line;
                            while ((line = await process.StandardError.ReadLineAsync()) != null)
                            {
                                await logFile.WriteLineAsync(line);
                            }
                        }
                        catch (Exception ex)
                        {
                            await logFile.WriteLineAsync($"Error reading stderr: {ex.Message}");
                        }
                    });

                    // Wait for both streams to complete
                    await Task.WhenAll(stdoutTask, stderrTask);
                }
                catch (Exception ex)
                {
                    // Log to console since log file may not be accessible
                    Console.Error.WriteLine($"Error in logging task: {ex.Message}");
                }
            });

            // Give it a moment to start
            await Task.Delay(500);

            // Verify it's still running
            if (process.HasExited)
            {
                PidFileManager.DeletePidFile();
                return (false, $"Server process exited immediately with code {process.ExitCode}. Check logs at {logPath}");
            }

            return (true, $"SSE server started on port {port} (PID: {process.Id})\nLogs: {logPath}");
        }
        catch (Exception ex)
        {
            return (false, $"Failed to start server: {ex.Message}");
        }
    }

    /// <summary>
    /// Stop SSE server
    /// </summary>
    public static (bool success, string message) StopSseServer()
    {
        var existing = PidFileManager.GetRunningProcess();
        if (existing == null)
        {
            return (false, "No SSE server is running");
        }

        try
        {
            var process = System.Diagnostics.Process.GetProcessById(existing.Pid);

            // Try graceful shutdown first
            process.CloseMainWindow();

            // Wait up to 5 seconds for graceful shutdown
            if (!process.WaitForExit(5000))
            {
                // Force kill if still running
                process.Kill(entireProcessTree: true);
                // Wait up to 2 seconds for forced kill to complete
                if (!process.WaitForExit(2000))
                {
                    // Process is unkillable - very rare but possible
                    return (false, $"Failed to kill process {existing.Pid} - process may be unkillable");
                }
            }

            PidFileManager.DeletePidFile();
            return (true, $"SSE server stopped (was running on port {existing.Port})");
        }
        catch (ArgumentException)
        {
            // Process doesn't exist - clean up stale PID file
            PidFileManager.DeletePidFile();
            return (true, "Cleaned up stale PID file");
        }
        catch (Exception ex)
        {
            return (false, $"Failed to stop server: {ex.Message}");
        }
    }

    /// <summary>
    /// Get SSE server status
    /// </summary>
    public static (bool running, string message) GetSseServerStatus()
    {
        var existing = PidFileManager.GetRunningProcess();
        if (existing == null)
        {
            return (false, "SSE server is not running");
        }

        var uptime = DateTime.UtcNow - existing.StartTime;
        return (true, $"SSE server is running on port {existing.Port} (PID: {existing.Pid})\n" +
                     $"Uptime: {uptime.Days}d {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s\n" +
                     $"Logs: {PidFileManager.GetLogFilePath()}");
    }

    /// <summary>
    /// Restart SSE server
    /// </summary>
    public static async Task<(bool success, string message)> RestartSseServerAsync(int? newPort = null)
    {
        // Get current port if not specified
        var existing = PidFileManager.GetRunningProcess();
        var port = newPort ?? existing?.Port ?? 3333;

        // Stop if running
        if (existing != null)
        {
            var stopResult = StopSseServer();
            if (!stopResult.success)
            {
                return (false, $"Failed to stop existing server: {stopResult.message}");
            }

            // Wait a moment for port to be released
            await Task.Delay(1000);
        }

        // Start with specified port
        return await StartSseServerAsync(port);
    }

    /// <summary>
    /// Show logs (tail)
    /// </summary>
    public static async Task ShowLogsAsync(bool follow = false, CancellationToken cancellationToken = default)
    {
        var logPath = PidFileManager.GetLogFilePath();

        if (!File.Exists(logPath))
        {
            Console.WriteLine("No log file found");
            return;
        }

        if (!follow)
        {
            // Just dump the file
            var content = await File.ReadAllTextAsync(logPath, cancellationToken);
            Console.WriteLine(content);
            return;
        }

        // Follow mode - tail -f style
        using var reader = new StreamReader(new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));

        // Read existing content
        var existing = await reader.ReadToEndAsync(cancellationToken);
        Console.Write(existing);

        // Follow new content
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line != null)
            {
                Console.WriteLine(line);
            }
            else
            {
                await Task.Delay(100, cancellationToken);
            }
        }
    }
}
