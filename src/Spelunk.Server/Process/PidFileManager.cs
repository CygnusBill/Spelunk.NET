using System.Diagnostics;
using System.Text.Json;

namespace Spelunk.Server.Process;

/// <summary>
/// Manages PID files for background SSE server processes
/// </summary>
public class PidFileManager
{
    private static readonly string SpelunkDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".spelunk");

    private static readonly string PidFilePath = Path.Combine(SpelunkDir, "sse.pid");
    private static readonly string LogFilePath = Path.Combine(SpelunkDir, "sse.log");

    public class ProcessInfo
    {
        public int Pid { get; set; }
        public int Port { get; set; }
        public DateTime StartTime { get; set; }
    }

    /// <summary>
    /// Ensure .spelunk directory exists
    /// </summary>
    public static void EnsureDirectoryExists()
    {
        if (!Directory.Exists(SpelunkDir))
        {
            Directory.CreateDirectory(SpelunkDir);
        }
    }

    /// <summary>
    /// Write process info to PID file
    /// </summary>
    public static void WritePidFile(int pid, int port)
    {
        EnsureDirectoryExists();

        var info = new ProcessInfo
        {
            Pid = pid,
            Port = port,
            StartTime = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(PidFilePath, json);
    }

    /// <summary>
    /// Read process info from PID file
    /// </summary>
    public static ProcessInfo? ReadPidFile()
    {
        if (!File.Exists(PidFilePath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(PidFilePath);
            return JsonSerializer.Deserialize<ProcessInfo>(json);
        }
        catch (Exception ex)
        {
            // Corrupted PID file - log error and return null
            Console.Error.WriteLine($"Failed to read PID file at {PidFilePath}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Delete PID file
    /// </summary>
    public static void DeletePidFile()
    {
        if (File.Exists(PidFilePath))
        {
            File.Delete(PidFilePath);
        }
    }

    /// <summary>
    /// Check if process is alive
    /// </summary>
    public static bool IsProcessAlive(int pid)
    {
        try
        {
            var process = System.Diagnostics.Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            // Process doesn't exist - this is expected behavior
            return false;
        }
        catch (Exception ex)
        {
            // Unexpected error checking process status
            Console.Error.WriteLine($"Error checking if process {pid} is alive: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Get running process info (checks if process is actually alive)
    /// </summary>
    public static ProcessInfo? GetRunningProcess()
    {
        var info = ReadPidFile();
        if (info == null)
        {
            return null;
        }

        if (!IsProcessAlive(info.Pid))
        {
            // Stale PID file
            DeletePidFile();
            return null;
        }

        return info;
    }

    /// <summary>
    /// Get log file path
    /// </summary>
    public static string GetLogFilePath()
    {
        EnsureDirectoryExists();
        return LogFilePath;
    }
}
