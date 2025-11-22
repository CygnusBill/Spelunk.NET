using System;

namespace Spelunk.Server;

/// <summary>
/// Base exception for all Spelunk.NET operations
/// </summary>
public class SpelunkException : Exception
{
    public SpelunkException() : base() { }

    public SpelunkException(string message) : base(message) { }

    public SpelunkException(string message, Exception innerException)
        : base(message, innerException) { }
}

/// <summary>
/// Thrown when a workspace cannot be found or loaded
/// </summary>
public class WorkspaceNotFoundException : SpelunkException
{
    public string? WorkspaceId { get; }
    public string? WorkspacePath { get; }

    public WorkspaceNotFoundException(string message) : base(message) { }

    public WorkspaceNotFoundException(string message, string? workspaceId, string? workspacePath)
        : base(message)
    {
        WorkspaceId = workspaceId;
        WorkspacePath = workspacePath;
    }

    public WorkspaceNotFoundException(string message, Exception innerException)
        : base(message, innerException) { }
}

/// <summary>
/// Thrown when a symbol cannot be found in the workspace
/// </summary>
public class SymbolNotFoundException : SpelunkException
{
    public string? SymbolName { get; }
    public string? SymbolType { get; }
    public string? ContainerName { get; }

    public SymbolNotFoundException(string message) : base(message) { }

    public SymbolNotFoundException(string message, string? symbolName, string? symbolType = null, string? containerName = null)
        : base(message)
    {
        SymbolName = symbolName;
        SymbolType = symbolType;
        ContainerName = containerName;
    }

    public SymbolNotFoundException(string message, Exception innerException)
        : base(message, innerException) { }
}

/// <summary>
/// Thrown when a pattern is invalid or cannot be parsed
/// </summary>
public class InvalidPatternException : SpelunkException
{
    public string? Pattern { get; }
    public string? PatternType { get; }

    public InvalidPatternException(string message) : base(message) { }

    public InvalidPatternException(string message, string? pattern, string? patternType = null)
        : base(message)
    {
        Pattern = pattern;
        PatternType = patternType;
    }

    public InvalidPatternException(string message, Exception innerException)
        : base(message, innerException) { }
}

/// <summary>
/// Thrown when an operation is not supported for a given context
/// </summary>
public class UnsupportedOperationException : SpelunkException
{
    public string? OperationName { get; }
    public string? Reason { get; }

    public UnsupportedOperationException(string message) : base(message) { }

    public UnsupportedOperationException(string message, string? operationName, string? reason = null)
        : base(message)
    {
        OperationName = operationName;
        Reason = reason;
    }

    public UnsupportedOperationException(string message, Exception innerException)
        : base(message, innerException) { }
}

/// <summary>
/// Thrown when a code edit operation fails
/// </summary>
public class CodeEditException : SpelunkException
{
    public string? FilePath { get; }
    public int? Line { get; }
    public int? Column { get; }

    public CodeEditException(string message) : base(message) { }

    public CodeEditException(string message, string? filePath, int? line = null, int? column = null)
        : base(message)
    {
        FilePath = filePath;
        Line = line;
        Column = column;
    }

    public CodeEditException(string message, Exception innerException)
        : base(message, innerException) { }
}

/// <summary>
/// Thrown when a marker operation fails
/// </summary>
public class MarkerException : SpelunkException
{
    public string? MarkerId { get; }

    public MarkerException(string message) : base(message) { }

    public MarkerException(string message, string? markerId)
        : base(message)
    {
        MarkerId = markerId;
    }

    public MarkerException(string message, Exception innerException)
        : base(message, innerException) { }
}
