# MCP Roslyn Server

A Model Context Protocol (MCP) server that provides powerful code analysis and modification tools for .NET projects using Microsoft's Roslyn compiler platform.

## Overview

This MCP server exposes Roslyn's advanced code analysis capabilities, allowing AI agents to:
- **Analyze** C# code at both syntactic and semantic levels
- **Search** for symbols, references, and patterns across entire solutions
- **Navigate** code relationships and type hierarchies
- **Modify** code safely using Roslyn's syntax tree manipulation
- **Refactor** with confidence using semantic understanding

## Key Capabilities

### Semantic-Syntax Traversal
The server leverages Roslyn's unique ability to seamlessly move between syntactic representation (how code looks) and semantic representation (what code means). This enables:
- Finding ALL uses of a property, even through method calls and assignments
- Understanding type relationships and inheritance
- Tracking data flow through the codebase
- Making context-aware modifications

## Available Tools

### Search & Discovery
- `dotnet/find-class` - Find classes by name with wildcards
- `dotnet/find-method` - Find methods by name with wildcards
- `dotnet/find-property` - Find properties by name with wildcards
- `dotnet/find-references` - Find all references to any symbol
- `dotnet/find-implementations` - Find all implementations of an interface
- `dotnet/find-overrides` - Find all overrides of a virtual/abstract method
- `dotnet/find-derived-types` - Find all types that inherit from a base class

### Call Analysis
- `dotnet/find-method-calls` - Find what methods a given method calls
- `dotnet/find-method-callers` - Find what methods call a given method

### Code Modification
- `dotnet/rename-symbol` - Safely rename any symbol with full validation
  - Prevents renaming system types
  - Validates identifiers (including @keyword support)
  - Shows impact analysis
  - Detects naming conflicts
  
- `dotnet/edit-code` - Perform surgical code edits
  - `add-method` - Add methods to classes
  - `add-property` - Add properties to classes
  - `make-async` - Convert synchronous methods to async

### Workspace Management
- `dotnet/load-workspace` - Load a .NET solution or project
- `dotnet/analyze-syntax` - Get syntax tree information
- `dotnet/workspace-status` - Check workspace loading status

## Building and Running

### Prerequisites
- .NET 8.0 or later (tested with .NET 10.0 preview)
- MSBuild (comes with .NET SDK)

### Build
```bash
cd src/McpRoslyn
dotnet build
```

### Run
```bash
# Standard stdio mode
dotnet run --project McpRoslyn.Server/McpRoslyn.Server.csproj -- --allowed-path /path/to/code

# SSE mode (for HTTP-based clients)
dotnet run --project McpRoslyn.Server.Sse/McpRoslyn.Server.Sse.csproj
```

## Usage Example

```python
# Load a workspace
{
  "tool": "dotnet/load-workspace",
  "arguments": {
    "path": "/path/to/MySolution.sln"
  }
}

# Find all references to a method
{
  "tool": "dotnet/find-references", 
  "arguments": {
    "symbolName": "GetUserAsync",
    "symbolType": "method",
    "containerName": "UserController"
  }
}

# Safely rename a symbol
{
  "tool": "dotnet/rename-symbol",
  "arguments": {
    "oldName": "GetUser",
    "newName": "FetchUser",
    "symbolType": "method",
    "containerName": "UserService",
    "preview": true
  }
}

# Add a method to a class
{
  "tool": "dotnet/edit-code",
  "arguments": {
    "file": "Services/UserService.cs",
    "operation": "add-method",
    "className": "UserService",
    "code": "public async Task<bool> IsValidUser(int id) { return await GetUser(id) != null; }"
  }
}
```

## Architecture

The server uses:
- **Roslyn** for all code analysis and manipulation
- **MSBuild** for loading and understanding project structure  
- **JSON-RPC 2.0** over stdio for MCP communication
- **In-memory workspace** management for performance
- **Atomic operations** with preview support

## Safety Features

- All rename operations validate against reserved keywords and system types
- Code modifications go through Roslyn's syntax validation
- Preview mode available for all modifications
- Impact analysis shows affected files before changes
- Semantic validation ensures type safety

## Future Capabilities

The architecture supports advanced scenarios like:
- Pattern-based code transformations
- Cross-file refactoring
- Semantic code search
- Data flow analysis
- Custom code fixes

## Contributing

Key areas for enhancement:
1. Additional edit-code operations (add-parameter, wrap-try-catch, etc.)
2. Generic syntax tree manipulation tools
3. Semantic analysis tools (data flow, type relationships)
4. Performance optimizations for large solutions