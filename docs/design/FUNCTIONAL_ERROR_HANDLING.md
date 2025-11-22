# Functional Error Handling in Spelunk.NET

This document describes the functional error handling patterns used in Spelunk.NET, leveraging LanguageExt's `Either<L, R>` and `Option<T>` types.

## Overview

Spelunk.NET uses functional error handling instead of exceptions for predictable, composable error management:

- **`Either<SpelunkError, T>`** - For operations that can fail with an error
- **`Option<T>`** - For values that may or may not exist (not-found scenarios)
- **Exceptions** - Reserved for truly exceptional/unexpected circumstances

## Type Semantics

### Either<SpelunkError, T>

Represents an operation that either succeeds with a value (`Right`) or fails with an error (`Left`).

```csharp
Either<SpelunkError, T>
├── Right(T value)    // Success
└── Left(SpelunkError) // Failure
```

### Option<T>

Represents a value that may or may not exist. Used for "not found" scenarios that aren't errors.

```csharp
Option<T>
├── Some(T value)  // Value exists
└── None           // Value doesn't exist
```

## Return Type Guidelines

| Scenario | Return Type | Example |
|----------|-------------|---------|
| Collection lookup | `Either<SpelunkError, List<T>>` | Empty list = not found |
| Single item lookup | `Either<SpelunkError, Option<T>>` | None = not found |
| Required value | `Either<SpelunkError, T>` | Error if missing |
| Validation/Operation errors | `Left(SpelunkError)` | Invalid input, unsupported operation |

### Collections

For methods returning collections, an **empty list naturally represents "not found"**. No need for Option wrapper.

```csharp
// Good: Empty list = no references found
public async Task<Either<SpelunkError, List<ReferenceInfo>>> FindReferencesAsync(...)
{
    var results = new List<ReferenceInfo>();
    // ... populate results ...
    return results;  // Empty list if nothing found
}

// Caller
result.Match(
    Right: refs => refs.Any()
        ? $"Found {refs.Count} references"
        : "No references found",
    Left: error => $"Error: {error.Message}"
);
```

### Single Items

For methods returning a single item that might not exist, use **Option<T>** inside Either.

```csharp
// Good: Option for single item lookup
public async Task<Either<SpelunkError, Option<Statement>>> GetStatementByIdAsync(string id)
{
    var statement = FindStatement(id);
    return statement != null
        ? Option<Statement>.Some(statement)
        : Option<Statement>.None;
}

// Caller
result.Match(
    Right: optStmt => optStmt.Match(
        Some: stmt => $"Found: {stmt}",
        None: () => "Statement not found"
    ),
    Left: error => $"Error: {error.Message}"
);
```

### Required Values

For methods where the value must exist (caller provided coordinates, etc.), return the value directly or an error.

```csharp
// Good: Error if not found (caller gave specific location)
public async Task<Either<SpelunkError, StatementContextResult>> GetStatementContextAsync(
    string filePath, int line, int column)
{
    // If no statement at this exact location, that's an error
    if (statement == null)
        return CodeEditFailed.NoStatement(filePath, line, column);

    return new StatementContextResult { ... };
}
```

## Error Types

All errors inherit from `SpelunkError`:

```csharp
public abstract record SpelunkError(string Message)
{
    public abstract string Code { get; }
}

// Specific error types
public sealed record WorkspaceNotFound(...) : SpelunkError(Message)
public sealed record SymbolNotFound(...) : SpelunkError(Message)
public sealed record InvalidPattern(...) : SpelunkError(Message)
public sealed record OperationNotSupported(...) : SpelunkError(Message)
public sealed record CodeEditFailed(...) : SpelunkError(Message)
public sealed record MarkerFailed(...) : SpelunkError(Message)
public sealed record UnexpectedError(...) : SpelunkError(Message)
```

Each error type has factory methods for common cases:

```csharp
WorkspaceNotFound.NoWorkspace(workspaceId)
WorkspaceNotFound.LoadFailed(path, reason)
CodeEditFailed.FileNotFound(filePath)
CodeEditFailed.NoStatement(filePath, line, column)
SymbolNotFound.Create(symbolName, symbolType, containerName)
```

## MCP Tool Boundary

At the MCP tool boundary (DotnetTools.cs), errors are converted to JSON responses:

```csharp
// Helper for error responses
file static class ToolError
{
    public static string Create(string code, string message) =>
        JsonSerializer.Serialize(new { error = new { code, message } });
}

// Usage in tool methods
return result.Match(
    Right: data => JsonSerializer.Serialize(data),
    Left: error => ToolError.Create(error.Code, error.Message)
);
```

## Best Practices

### Do

- Use `Either<SpelunkError, T>` for operations that can fail
- Use empty collections to represent "nothing found"
- Use `Option<T>` for single item lookups where absence is normal
- Create specific error types with context (file path, line number, etc.)
- Use factory methods for common error cases

### Don't

- Don't wrap collections in Option - empty list is sufficient
- Don't throw exceptions for expected error conditions
- Don't use generic error messages - include context
- Don't catch and re-throw as different exception types

## Example: Complete Method

```csharp
public async Task<Either<SpelunkError, List<ReferenceInfo>>> FindReferencesAsync(
    string symbolName,
    string? symbolType = null)
{
    // Validation errors use Either
    if (symbolType == "local")
        return OperationNotSupported.Create("find-references",
            "Local variables require method context");

    var results = new List<ReferenceInfo>();

    // ... find references ...

    // Empty list = not found (not an error)
    return results;
}
```

## Migration Guide

When converting from exception-based to functional error handling:

1. Change return type from `Task<T>` to `Task<Either<SpelunkError, T>>`
2. Replace `throw new XException(...)` with `return XError.Create(...)`
3. Remove try-catch blocks that just rethrow
4. Update callers to use `.Match()` instead of try-catch
5. For collections, return empty list instead of throwing "not found"
6. For single items, consider if `Option<T>` is appropriate

## See Also

- [LanguageExt Documentation](https://github.com/louthy/language-ext)
- `src/Spelunk.Server/SpelunkErrors.cs` - Error type definitions
