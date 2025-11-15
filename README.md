# Spelunk.NET

A Model Context Protocol (MCP) server that provides powerful code analysis and modification tools for .NET projects using Microsoft's Roslyn compiler platform. Spelunk.NET is available as a .NET global tool for easy installation and use.

## Architecture Philosophy

**Primitive Tools, Intelligent Agents**

Spelunk.NET follows a **primitive tools** philosophy:
- Tools are simple, focused, and composable building blocks
- Complex refactorings are **agent workflows** that orchestrate these primitives  
- This approach provides flexibility, transparency, and adaptability

> **Important**: Refactoring operations (like SQL parameterization, async conversion, etc.) are not implemented as monolithic tools but as agent workflows. See [REFACTORING_AS_AGENTS.md](docs/REFACTORING_AS_AGENTS.md) for details.

## Overview

This MCP server exposes Roslyn's advanced code analysis capabilities, allowing AI agents to:
- **Analyze** C#, VB.NET, and F# code at both syntactic and semantic levels
- **Search** for symbols, references, and patterns across entire solutions
- **Navigate** code relationships and AST structure with XPath-style queries
- **Modify** code safely using Roslyn's syntax tree manipulation
- **Refactor** with confidence using semantic understanding
- **Query** abstract syntax trees with enhanced SpelunkPath expressions

## Key Capabilities

### Statement-Level Editing
The server provides precise statement-level operations, treating statements as the fundamental unit of code modification. This approach:
- **Preserves code structure** - Maintains proper indentation and formatting
- **Handles edge cases** - Correctly manages comments, trivia, and block structures
- **Enables composability** - Complex refactorings built from simple operations
- **Provides stability** - Operations work reliably across different code styles

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

### Statement-Level Operations
- `dotnet/find-statements` - Find statements matching patterns with stable IDs
- `dotnet/replace-statement` - Replace any statement precisely
- `dotnet/insert-statement` - Insert statements before/after existing ones
- `dotnet/remove-statement` - Remove statements while preserving comments

### SpelunkPath Query Tools
- `dotnet/query-syntax` - Query AST with XPath-style expressions
- `dotnet/navigate` - Navigate from a position using axes
- `dotnet/get-ast` - Get AST structure at any level
- `dotnet/mark-statement` - Mark statements with ephemeral markers
- `dotnet/find-marked-statements` - Find previously marked statements
- `dotnet/unmark-statement` - Remove specific markers
- `dotnet/clear-markers` - Clear all markers

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
  
- `dotnet/fix-pattern` - Find and fix code patterns across the codebase

### Workspace Management
- `dotnet/load-workspace` - Load a .NET solution or project
- `dotnet/analyze-syntax` - Get syntax tree information
- `dotnet/workspace-status` - Check workspace loading status

### F# Support
- `dotnet-fsharp-load-project` - Load an F# project (.fsproj)
- `dotnet-fsharp-find-symbols` - Find F# symbols with pattern matching
- `dotnet-fsharp-query` - Query F# AST using FSharpPath expressions
- `dotnet-fsharp-get-ast` - Get F# abstract syntax tree structure

### Advanced AST Navigation
- `dotnet/query-syntax` - Query AST using enhanced SpelunkPath expressions
  - Support for expression-level nodes (binary-expression, literal, identifier)
  - Advanced predicates (@operator, @literal-value, @contains)
  - Find specific patterns like null checks, string comparisons

- `dotnet/navigate` - Navigate from any position using XPath-style axes
  - Navigate to parent, ancestor, child, descendant nodes
  - Find siblings with following-sibling:: and preceding-sibling::
  - Chain navigation paths like "parent::method/following-sibling::method"

- `dotnet/get-ast` - Get abstract syntax tree structure
  - Visualize code hierarchy and node relationships
  - Debug SpelunkPath queries
  - Understand AST node types for pattern matching

## Installation

### As a .NET Global Tool

Install Spelunk.NET as a global tool (recommended):

```bash
# Install from NuGet (coming soon)
dotnet tool install --global Spelunk.NET

# Or install from local build
dotnet pack src/McpDotnet.Server/McpDotnet.Server.csproj
dotnet tool install --global --add-source ./src/McpDotnet.Server/nupkg Spelunk.NET
```

### Prerequisites
- .NET 10.0 SDK (or .NET 8.0 or later)
- MSBuild (comes with .NET SDK)
- **OR** Docker (if running in container)

## Usage

Once installed, use the `spelunk` command:

```bash
# Run in stdio mode (for MCP clients like Claude Desktop)
spelunk stdio

# Run SSE server on default port (3333)
spelunk sse

# Run SSE server on custom port
spelunk sse -p 8080

# Check SSE server status
spelunk sse status

# View SSE server logs
spelunk sse logs
spelunk sse logs -f  # Follow mode

# Stop SSE server
spelunk sse stop

# Restart SSE server
spelunk sse restart
```

### Configuration

Configure allowed directories in `~/.config/mcp-dotnet/config.json`:

```json
{
  "McpDotnet": {
    "AllowedPaths": [
      "/Users/yourname/Repos",
      "/path/to/your/projects"
    ]
  }
}
```

Or use environment variables:

```bash
export MCP_DOTNET_ALLOWED_PATHS="/path/to/code:/another/path"
spelunk stdio
```

### Development Build

Build from source:

```bash
dotnet build
dotnet run --project src/McpDotnet.Server -- stdio
```

### Docker
The server can also run in a Docker container:

```bash
# Build the image
docker build -t mcp-dotnet:latest .

# Run with mounted code directory
docker run -i \
  -v /path/to/your/code:/workspace \
  -e MCP_DOTNET_ALLOWED_PATHS=/workspace \
  mcp-dotnet:latest
```

For detailed Docker usage, see [DOCKER.md](DOCKER.md).

## Usage Examples

### Basic Operations
```json
// Load a workspace
{
  "tool": "dotnet/load-workspace",
  "arguments": {
    "path": "/path/to/MySolution.sln"
  }
}

// Find all references to a method
{
  "tool": "dotnet/find-references", 
  "arguments": {
    "symbolName": "GetUserAsync",
    "symbolType": "method",
    "containerName": "UserController"
  }
}
```

### Statement-Level Editing
```json
// Find all Console.WriteLine statements
{
  "tool": "dotnet/find-statements",
  "arguments": {
    "pattern": "Console.WriteLine",
    "patternType": "text"
  }
}

// Replace a specific statement
{
  "tool": "dotnet/replace-statement",
  "arguments": {
    "location": {
      "file": "/path/to/Program.cs",
      "line": 25,
      "column": 9
    },
    "newStatement": "_logger.LogInformation(\"Application started\");"
  }
}

// Insert validation at the start of a method
{
  "tool": "dotnet/insert-statement",
  "arguments": {
    "position": "before",
    "location": {
      "file": "/path/to/UserService.cs",
      "line": 30,
      "column": 9
    },
    "statement": "ArgumentNullException.ThrowIfNull(user);"
  }
}

// Mark statements for multi-step refactoring
{
  "tool": "dotnet/mark-statement",
  "arguments": {
    "location": {
      "file": "/path/to/OrderService.cs",
      "line": 45,
      "column": 13
    },
    "label": "validation-point"
  }
}
```

### Advanced Refactoring
```json
// Add a method to a class
{
  "tool": "dotnet/edit-code",
  "arguments": {
    "file": "Services/UserService.cs",
    "operation": "add-method",
    "className": "UserService",
    "code": "public async Task<bool> IsValidUser(int id) { return await GetUser(id) != null; }"
  }
}

// Fix a pattern across the codebase
{
  "tool": "dotnet/fix-pattern",
  "arguments": {
    "findPattern": "DateTime.Now",
    "replacePattern": "DateTime.UtcNow",
    "patternType": "property-access"
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

## Documentation

### For AI Agents
- **[Agent Tool Selection Guide](docs/AGENT_TOOL_SELECTION_GUIDE.md)** - Decision tree for choosing between semantic and syntactic tools
- **[Agent Query Examples](docs/AGENT_QUERY_EXAMPLES.md)** - Concrete examples comparing approaches for common tasks

### Architecture & Design
- **[Tool Synopsis](docs/TOOL_SYNOPSIS.md)** - Complete reference for all available tools
- **[Semantic vs Syntactic Philosophy](docs/design/SEMANTIC_VS_SYNTACTIC_TOOLS.md)** - Architectural philosophy behind tool categories
- **[Statement-Level Editing](docs/design/STATEMENT_LEVEL_EDITING.md)** - Design principles for code modification
- **[F# Architecture](docs/design/FSHARP_ARCHITECTURE.md)** - Understanding F# support implementation

### SpelunkPath Documentation
- **[SpelunkPath Instructions](docs/roslyn-path/ROSLYN_PATH_INSTRUCTIONS.md)** - Quick reference for query syntax
- **[SpelunkPath Agent Guide](docs/roslyn-path/ROSLYN_PATH_AGENT_GUIDE.md)** - 5-minute introduction for AI agents
- **[SpelunkPath Syntax Design](docs/roslyn-path/ROSLYN_PATH_SYNTAX_DESIGN.md)** - Complete syntax specification

## Contributing

Key areas for enhancement:
1. Additional edit-code operations (add-parameter, wrap-try-catch, etc.)
2. Generic syntax tree manipulation tools
3. Semantic analysis tools (data flow, type relationships)
4. Performance optimizations for large solutions