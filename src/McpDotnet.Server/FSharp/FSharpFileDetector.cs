using System;
using System.IO;

namespace McpDotnet.Server.FSharp;

/// <summary>
/// Provides F# file detection and routing decisions for tool implementations.
/// This is a minimal implementation to demonstrate F# support architecture.
/// </summary>
public static class FSharpFileDetector
{
    /// <summary>
    /// Determines if a file is an F# source file based on its extension.
    /// </summary>
    public static bool IsFSharpFile(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return false;

        var extension = Path.GetExtension(filePath);
        return extension?.Equals(".fs", StringComparison.OrdinalIgnoreCase) == true ||
               extension?.Equals(".fsi", StringComparison.OrdinalIgnoreCase) == true ||
               extension?.Equals(".fsx", StringComparison.OrdinalIgnoreCase) == true ||
               extension?.Equals(".fsscript", StringComparison.OrdinalIgnoreCase) == true;
    }

    /// <summary>
    /// Determines if a project file is an F# project.
    /// </summary>
    public static bool IsFSharpProject(string? projectPath)
    {
        if (string.IsNullOrEmpty(projectPath))
            return false;

        return projectPath.EndsWith(".fsproj", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets a user-friendly message for unsupported F# operations.
    /// </summary>
    public static string GetFSharpNotSupportedMessage(string toolName, string? filePath = null)
    {
        var fileInfo = !string.IsNullOrEmpty(filePath) ? $" for file '{Path.GetFileName(filePath)}'" : "";
        return $"F# support is not yet implemented for '{toolName}'{fileInfo}. " +
               "F# files require FSharp.Compiler.Service integration which is planned but not yet available. " +
               "See docs/design/FSHARP_ROADMAP.md for implementation timeline.";
    }

    /// <summary>
    /// Checks if a document collection contains any F# files.
    /// </summary>
    public static bool ContainsAnyFSharpFiles(IEnumerable<string>? filePaths)
    {
        return filePaths?.Any(IsFSharpFile) == true;
    }
}