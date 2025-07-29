# MCP Roslyn Server

A Model Context Protocol (MCP) server that provides Roslyn-based code analysis tools for .NET projects.

## Overview

This MCP server exposes .NET code analysis capabilities through the MCP protocol, allowing AI assistants to:
- Analyze C# and VB.NET syntax trees
- Retrieve symbol information
- Navigate code relationships
- Manage and inspect workspaces

## Current Status

The server implements the MCP protocol with JSON-RPC over stdio and includes placeholder implementations for three core tools:

### Available Tools

1. **dotnet/analyze-syntax**
   - Analyzes the syntax tree of C# or VB.NET files
   - Parameters:
     - `filePath` (required): Path to the source file
     - `includeTrivia` (optional): Include whitespace and comments

2. **dotnet/get-symbols**
   - Retrieves symbol information from code
   - Parameters:
     - `filePath` (required): Path to the source file
     - `position` (optional): Line and column position
     - `symbolName` (optional): Specific symbol to find

3. **dotnet/workspace-status**
   - Get loading progress and workspace info
   - Parameters:
     - `workspaceId` (optional): Specific workspace ID

## Building and Running

### Prerequisites
- .NET 10.0 Preview or later
- Visual Studio 2022 or VS Code with C# extension

### Build
```bash
dotnet build
```

### Run
```bash
dotnet run
```

The server listens on stdin/stdout for JSON-RPC messages.

## Testing

Send JSON-RPC messages to test the server:

```bash
# Initialize
echo '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}' | dotnet run

# List tools
echo '{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}' | dotnet run

# Call a tool
echo '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"dotnet/workspace-status","arguments":{}}}' | dotnet run
```

## Next Steps

- [ ] Integrate Microsoft.CodeAnalysis (Roslyn) packages
- [ ] Implement actual syntax tree analysis
- [ ] Add semantic model support
- [ ] Implement symbol resolution
- [ ] Add workspace/solution loading
- [ ] Implement code navigation features
- [ ] Add refactoring capabilities

## Architecture

The server is designed as a long-running process that maintains loaded workspaces in memory for fast responses. It uses:
- JSON-RPC 2.0 for communication
- stdio transport (stdin/stdout)
- Microsoft.Extensions.Logging for diagnostics (stderr)
- Persistent workspace management for performance

## Contributing

This is an early implementation. Key areas for contribution:
1. Roslyn integration
2. Performance optimization
3. Additional tool implementations
4. Test coverage