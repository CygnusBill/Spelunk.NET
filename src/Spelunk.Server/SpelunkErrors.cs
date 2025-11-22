using LanguageExt;
using static LanguageExt.Prelude;

namespace Spelunk.Server;

/// <summary>
/// Base error type for all Spelunk.NET operations using functional error handling.
/// Uses discriminated union pattern via records.
/// </summary>
public abstract record SpelunkError(string Message)
{
    public abstract string Code { get; }

    /// <summary>
    /// Convert to Either Left
    /// </summary>
    public Either<SpelunkError, T> ToLeft<T>() => Left<SpelunkError, T>(this);
}

/// <summary>
/// Workspace not found or failed to load
/// </summary>
public sealed record WorkspaceNotFound(
    string Message,
    string? WorkspaceId = null,
    string? WorkspacePath = null
) : SpelunkError(Message)
{
    public override string Code => "WORKSPACE_NOT_FOUND";

    public static WorkspaceNotFound NoWorkspace(string? workspaceId = null) =>
        new("No workspace loaded", workspaceId, null);

    public static WorkspaceNotFound LoadFailed(string path, string reason) =>
        new($"Failed to load workspace: {reason}", null, path);
}

/// <summary>
/// Symbol not found in workspace
/// </summary>
public sealed record SymbolNotFound(
    string Message,
    string? SymbolName = null,
    string? SymbolType = null,
    string? ContainerName = null
) : SpelunkError(Message)
{
    public override string Code => "SYMBOL_NOT_FOUND";

    public static SymbolNotFound Create(string symbolName, string? symbolType = null, string? containerName = null) =>
        new(
            containerName != null
                ? $"Symbol '{symbolName}' of type '{symbolType}' not found in container '{containerName}'"
                : $"Symbol '{symbolName}' of type '{symbolType}' not found",
            symbolName, symbolType, containerName
        );
}

/// <summary>
/// Invalid pattern or query
/// </summary>
public sealed record InvalidPattern(
    string Message,
    string? Pattern = null,
    string? PatternType = null
) : SpelunkError(Message)
{
    public override string Code => "INVALID_PATTERN";

    public static InvalidPattern Create(string pattern, string reason, string? patternType = null) =>
        new($"Invalid pattern '{pattern}': {reason}", pattern, patternType);
}

/// <summary>
/// Operation not supported
/// </summary>
public sealed record OperationNotSupported(
    string Message,
    string? OperationName = null,
    string? Reason = null
) : SpelunkError(Message)
{
    public override string Code => "OPERATION_NOT_SUPPORTED";

    public static OperationNotSupported Create(string operation, string reason) =>
        new($"Operation '{operation}' not supported: {reason}", operation, reason);
}

/// <summary>
/// Code edit operation failed
/// </summary>
public sealed record CodeEditFailed(
    string Message,
    string? FilePath = null,
    int? Line = null,
    int? Column = null
) : SpelunkError(Message)
{
    public override string Code => "CODE_EDIT_FAILED";

    public static CodeEditFailed FileNotFound(string filePath) =>
        new($"File not found: {filePath}", filePath);

    public static CodeEditFailed ParseFailed(string filePath) =>
        new("Could not parse syntax tree", filePath);

    public static CodeEditFailed SemanticModelFailed(string filePath) =>
        new("Could not get semantic model", filePath);

    public static CodeEditFailed NoStatement(string filePath, int line, int column) =>
        new($"No statement at position {line}:{column}", filePath, line, column);

    public static CodeEditFailed NoStatements(string filePath, int line, int column) =>
        new("No statements found in the specified region", filePath, line, column);
}

/// <summary>
/// Marker operation failed
/// </summary>
public sealed record MarkerFailed(
    string Message,
    string? MarkerId = null
) : SpelunkError(Message)
{
    public override string Code => "MARKER_FAILED";

    public static MarkerFailed NotFound(string markerId) =>
        new($"Marker '{markerId}' not found", markerId);

    public static MarkerFailed LimitExceeded(int max) =>
        new($"Maximum marker limit ({max}) exceeded");
}

/// <summary>
/// Unexpected error (wraps exceptions during transition)
/// </summary>
public sealed record UnexpectedError(
    string Message,
    Exception? Exception = null
) : SpelunkError(Message)
{
    public override string Code => "UNEXPECTED_ERROR";

    public static UnexpectedError FromException(Exception ex) =>
        new(ex.Message, ex);
}

/// <summary>
/// Extension methods for working with SpelunkError and Either
/// </summary>
public static class SpelunkResult
{
    /// <summary>
    /// Create a success result
    /// </summary>
    public static Either<SpelunkError, T> Success<T>(T value) =>
        Right<SpelunkError, T>(value);

    /// <summary>
    /// Create a failure result
    /// </summary>
    public static Either<SpelunkError, T> Fail<T>(SpelunkError error) =>
        Left<SpelunkError, T>(error);

    /// <summary>
    /// Create a failure result from an error
    /// </summary>
    public static Either<SpelunkError, T> Fail<T>(string message) =>
        Left<SpelunkError, T>(new UnexpectedError(message));

    /// <summary>
    /// Try to execute an operation, catching exceptions as UnexpectedError
    /// </summary>
    public static Either<SpelunkError, T> Try<T>(Func<T> f)
    {
        try
        {
            return Success(f());
        }
        catch (Exception ex)
        {
            return Fail<T>(UnexpectedError.FromException(ex));
        }
    }

    /// <summary>
    /// Try to execute an async operation, catching exceptions as UnexpectedError
    /// </summary>
    public static async Task<Either<SpelunkError, T>> TryAsync<T>(Func<Task<T>> f)
    {
        try
        {
            return Success(await f());
        }
        catch (Exception ex)
        {
            return Fail<T>(UnexpectedError.FromException(ex));
        }
    }

    /// <summary>
    /// Convert Either to a JSON-friendly result object
    /// </summary>
    public static object ToResult<T>(this Either<SpelunkError, T> either) =>
        either.Match(
            Right: value => (object)new { success = true, data = value },
            Left: error => (object)new { success = false, error = new { code = error.Code, message = error.Message } }
        );
}
