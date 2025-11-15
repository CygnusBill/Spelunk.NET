# MCP Tool Descriptions

This document contains the exact descriptions returned by the MCP server for each tool. These descriptions are what agents see when discovering available tools.

## Important Note

The McpDotnet project has two server implementations:
1. **STDIO Server** (`McpDotnet.Server`) - Primary implementation using JSON-RPC over stdio
2. **SSE Server** (`McpDotnet.Server.Sse`) - Experimental Server-Sent Events implementation

Both servers share the same underlying `DotnetWorkspaceManager` but have slightly different tool descriptions.

## STDIO Server Tool Descriptions

These are defined in `McpJsonRpcServer.cs` in the tool definitions array:

### dotnet-load-workspace
**MCP Description**: "Load a .NET solution or project into the workspace"

### dotnet-analyze-syntax
**MCP Description**: "Analyzes the syntax tree of a C# or VB.NET file"

### dotnet-get-symbols
**MCP Description**: "Get symbols at a specific position in a file"

### dotnet-workspace-status
**MCP Description**: "Get loading progress and workspace info"

### dotnet-find-class
**MCP Description**: "Find classes, interfaces, structs, or enums by name pattern (supports * and ? wildcards)"

### dotnet-find-method
**MCP Description**: "Find methods by name pattern with optional class pattern filter (supports * and ? wildcards)"

### dotnet-find-property
**MCP Description**: "Find properties and fields by name pattern with optional class pattern filter (supports * and ? wildcards)"

### dotnet-find-method-calls
**MCP Description**: "Find all methods called by a specific method (call tree analysis)"

### dotnet-find-method-callers
**MCP Description**: "Find all methods that call a specific method (caller tree analysis)"

### dotnet-find-references
**MCP Description**: "Find all references to a type, method, property, or field"

### dotnet-find-implementations
**MCP Description**: "Find all implementations of an interface or abstract class"

### dotnet-find-overrides
**MCP Description**: "Find all overrides of a virtual or abstract method"

### dotnet-find-derived-types
**MCP Description**: "Find all types that derive from a base class"

### dotnet-rename-symbol
**MCP Description**: "Rename a symbol (type, method, property, field) and update all references"

### dotnet-edit-code
**MCP Description**: "Perform surgical code edits using Roslyn. Operations: add-method, add-property, make-async, add-parameter, wrap-try-catch"

### dotnet-fix-pattern
**MCP Description**: "Find code matching a pattern and transform it to a new pattern"

### dotnet-find-statements
**MCP Description**: "Find statements in code matching a pattern. Returns statement IDs for use with other operations. Uses Roslyn's syntax tree to enumerate all statements."

### dotnet-replace-statement
**MCP Description**: "Replace a statement with new code. The statement is identified by its location from find-statements. Preserves indentation and formatting context."

### dotnet-insert-statement
**MCP Description**: "Insert a new statement before or after an existing statement. The reference statement is identified by its location from find-statements. Preserves indentation and formatting context."

### dotnet-remove-statement
**MCP Description**: "Remove a statement from the code. The statement is identified by its location from find-statements. Can preserve comments attached to the statement."

### dotnet-mark-statement
**MCP Description**: "Mark a statement with an ephemeral marker for later reference. Markers are session-scoped and not persisted."

### dotnet-find-marked-statements
**MCP Description**: "Find all or specific marked statements. Returns current locations even if code has been edited."

### dotnet-unmark-statement
**MCP Description**: "Remove a specific marker by its ID."

### dotnet-clear-markers
**MCP Description**: "Clear all markers in the current session."

### dotnet-get-diagnostics
**MCP Description**: "Get compilation diagnostics (errors, warnings, info) from the workspace"

### spelunk-fsharp-projects
**MCP Description**: "Get information about F# projects in the workspace (detected but not loaded by MSBuild)"

### dotnet-load-fsharp-project
**MCP Description**: "Load an F# project using FSharp.Compiler.Service (separate from MSBuild workspaces)"

### spelunk-fsharp-find-symbols
**MCP Description**: "Find symbols in F# code using FSharpPath queries"

## SSE Server Tool Descriptions

The SSE server uses attribute-based tool definitions in `DotnetTools.cs`. Here are the descriptions from the SSE server:

### dotnet-load-workspace
**SSE Description**: "Load a .NET solution or project into the workspace"

(Note: The SSE server uses the same descriptions as the STDIO server for most tools)

## Key Differences

1. **Definition Method**:
   - STDIO Server: Defines tools in a JSON-like array structure in `McpJsonRpcServer.cs`
   - SSE Server: Uses `[McpServerTool]` and `[Description]` attributes on static methods

2. **Tool Registration**:
   - STDIO Server: Manually lists all tools in `GetAvailableTools()` method
   - SSE Server: Uses `.WithToolsFromAssembly()` to auto-discover tools via reflection

3. **Descriptions**:
   - Both servers now use the centralized `ToolDescriptions` class defined in `McpDotnet.Server/ToolDescriptions.cs`
   - This ensures consistency and prevents duplication bugs
   - STDIO server descriptions are what agents see when using the primary server

## Implementation Note

As of the latest update, all tool descriptions have been centralized in the `ToolDescriptions` static class:
- Located at: `src/McpDotnet/McpDotnet.Server/ToolDescriptions.cs`
- Both STDIO and SSE servers reference this shared source
- Future tool additions should update the centralized class

## Notes

- The descriptions are returned to MCP clients during tool discovery
- Each tool also includes an `inputSchema` that defines its parameters
- The descriptions are designed to be concise but informative for agents
- The STDIO server is the primary implementation and its descriptions are authoritative