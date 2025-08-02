# CLAUDE.md - Project Overview for AI Assistants

## Quick Start for Context Recreation

If you're starting fresh with this codebase:
1. **Read this file first** - It's designed to give you all essential context
2. **Check Serena memories** - Use `mcp__serena__list_memories` and read relevant ones for project context
3. **Check recent changes** - See "Recent Changes" section below
4. **Review key files** - Listed in "Key Implementation Files" section
5. **Understand the architecture** - MCP server with 25 Roslyn-based tools (including diagnostics)
6. **Check pending tasks** - See implementation status sections

## Project Structure

This is the MCP Roslyn Server project, which provides multi-language code analysis and manipulation tools via the Model Context Protocol (MCP). It supports C#, VB.NET, and F# with language-agnostic abstractions.

### Directory Layout

```
McpDotnet/
├── src/
│   └── McpRoslyn/
│       ├── McpRoslyn.Server/      # Main server implementation
│       │   ├── RoslynPath/        # RoslynPath query engine
│       │   ├── LanguageHandlers/  # C# and VB.NET language handlers
│       │   ├── FSharp/           # F# support infrastructure
│       │   └── *.cs              # Core server files
│       └── McpRoslyn.Server.Sse/  # SSE server (experimental)
│           ├── Program.cs         # SSE server entry point
│           └── Tools/            # SSE-specific tools
├── docs/                         # Current documentation
│   ├── TOOL_SYNOPSIS.md         # Reference for all 24 tools
│   ├── design/                  # Design documents
│   │   ├── STATEMENT_LEVEL_EDITING.md
│   │   └── EPHEMERAL_MARKER_DESIGN.md
│   ├── roslyn-path/             # RoslynPath documentation
│   │   ├── ROSLYN_PATH_INSTRUCTIONS.md   # Quick reference
│   │   ├── ROSLYN_PATH_AGENT_GUIDE.md    # 5-minute guide
│   │   ├── ROSLYN_PATH_SYNTAX_DESIGN.md  # Full syntax spec
│   │   ├── ROSLYN_PATH_ANALYSIS_EXAMPLES.md
│   │   ├── ROSLYN_PATH_TEST_PACKAGE.md
│   │   └── examples/            # Demo code
│   └── stale/                   # Archived docs (historical only)
├── tests/                       # Python integration tests
│   ├── tools/                   # Individual tool tests
│   ├── protocol/                # MCP protocol tests
│   ├── integration/             # Cross-cutting tests
│   ├── utils/                   # Test utilities
│   └── run-all-tests.py         # Test runner
├── scripts/                     # Shell scripts for running/testing
│   ├── run/                     # Server launch scripts
│   │   ├── run-stdio-server.sh  # Primary STDIO server
│   │   ├── run-server-debug.sh  # Debug mode server
│   │   └── run-sse-server.sh    # SSE server (experimental)
│   └── test/                    # Test scripts
│       ├── test-server.sh       # Run with test-requests.jsonl
│       └── test-mcp-server.sh   # Interactive protocol test
├── test-workspace/              # Sample projects for testing (C#, VB.NET, F#)
├── README.md                    # Project readme
└── CLAUDE.md                    # This file
```

## Key Concepts

### 1. Statement-Level Operations
All code modifications work at the statement level - this is the optimal granularity for refactoring. See `docs/design/STATEMENT_LEVEL_EDITING.md`.

### 2. Multi-Language Support
- **C#**: Full Roslyn integration
- **VB.NET**: Full Roslyn integration with language-agnostic mapping
- **F#**: Basic support via FSharp.Compiler.Service (separate from Roslyn)

### 3. RoslynPath
An XPath-inspired query language for .NET code that provides stable references surviving edits:
- Language-agnostic: works with C# and VB.NET
- Example: `//class[UserService]/method[GetUser]//statement[@contains='Console.WriteLine']`
- VB.NET mapping: `//method[@returns='void']` finds both C# void methods and VB.NET Subs
- See `docs/roslyn-path/` for full documentation

### 4. FSharpPath
An XPath-inspired query language specifically for F# AST:
- F#-specific constructs: `//function[@recursive]`, `//type[Union]`
- Pattern matching: `//function[@async and @inline]`
- Active patterns and computation expressions support

### 5. Tool Composition
Complex refactorings are built from simple, composable tools. The 27 implemented tools can be combined for powerful operations.

## Documentation Guide

### For Quick Reference
- **Tool usage**: `docs/TOOL_SYNOPSIS.md` - All tools with examples
- **RoslynPath syntax**: `docs/roslyn-path/ROSLYN_PATH_INSTRUCTIONS.md`

### For Understanding Design
- **Philosophy**: `docs/design/STATEMENT_LEVEL_EDITING.md`
- **RoslynPath rationale**: `docs/roslyn-path/ROSLYN_PATH_SYNTAX_DESIGN.md`

### For Testing
- **Integration tests**: `tests/` directory with Python test scripts
- **Test runner**: `python3 tests/run-all-tests.py`
- **Test workspace**: `test-workspace/` contains sample C# code

## Current Implementation Status

### Completed Features
- ✅ 27 MCP tools implemented (see TOOL_SYNOPSIS.md)
- ✅ Multi-language support (C#, VB.NET, F#)
- ✅ Statement-level operations (find, replace, insert, remove)
- ✅ Ephemeral marker system for tracking statements
- ✅ Language-agnostic RoslynPath query engine
- ✅ F# support via FSharp.Compiler.Service
- ✅ Comprehensive test suite with multi-language tests

### Recently Completed (Latest Session)
- ✅ Complete VB.NET support with language-agnostic mapping
- ✅ F# project detection and tracking
- ✅ FSharpPath query language implementation
- ✅ F# workspace manager and tools (dotnet-load-fsharp-project, dotnet-fsharp-find-symbols)
- ✅ Multi-language test coverage (VB.NET and F# integration tests)
- ✅ Documentation updates for multi-language support

### High Priority Pending
- 🔲 Implement get-statement-context tool (semantic info)

### Medium Priority Pending
- ✅ Update test files to use new project paths (completed - now using relative paths)
- 🔲 Refactor fix-pattern to use statement-level operations
- 🔲 Design generic syntax tree navigation tools
- 🔲 Implement get-data-flow tool

## Development Workflow

### Running the Server
```bash
# Using convenience scripts (recommended)
./scripts/run/run-stdio-server.sh      # Standard mode
./scripts/run/run-server-debug.sh      # Debug mode

# Or directly with dotnet
dotnet run --project src/McpRoslyn/McpRoslyn.Server
```

### Running Tests
```bash
# All tests
python3 tests/run-all-tests.py

# Specific test
python3 tests/tools/test-find-statements.py
```

### Testing RoslynPath
See examples in `docs/roslyn-path/examples/`:
- `demo-roslyn-path-complex.cs` - Complex query demonstrations
- `test-roslyn-path-simple.cs` - Simple standalone test

## Important Notes

1. **Line Numbers Are Fragile**: Always prefer RoslynPath over line/column positions
2. **Statement Granularity**: Operations work on complete statements, not arbitrary text ranges
3. **Markers Are Ephemeral**: They survive edits but not file reloads
4. **Tools Are Composable**: Complex operations should combine simple tools

## Common Tasks

### Find and Replace Pattern
```python
# 1. Find targets (now supports RoslynPath!)
results = find_statements(
    pattern="//statement[@contains='Console.WriteLine']",
    patternType="roslynpath"
)

# 2. Replace each
for result in results:
    replace_statement(location=result.location, newStatement="logger.LogInfo(...)")
```

### Add Validation to Methods
```python
# 1. Find methods
methods = find_method(pattern="Process*")

# 2. Insert validation at start
for method in methods:
    insert_statement(
        location=f"{method.path}/block/statement[1]",
        position="before",
        statement="ArgumentNullException.ThrowIfNull(input);"
    )
```

## Debugging Tips

1. Use `tests/utils/debug_test.py` for interactive testing
2. Check server logs for detailed Roslyn operations
3. RoslynPath queries can be tested standalone with examples
4. The marker system helps track statements through transformations

## Key Implementation Files

### Core Server Components
- `RoslynWorkspaceManager.cs` - Main workspace and tool implementations
- `McpJsonRpcServer.cs` - MCP protocol handling
- `Program.cs` - Server entry point and configuration

### RoslynPath Components
- `RoslynPath/RoslynPath.cs` - Main query engine
- `RoslynPath/RoslynPathParser.cs` - Query parser
- `RoslynPath/RoslynPathEvaluator.cs` - AST evaluator

### Recent Changes (as of commit 56c3c24)
- RoslynPath integrated into find-statements tool (see ProcessDocumentForStatements method)
- All nullable reference warnings fixed
- SSE server now shows clear error when port is in use

## Common Pitfalls & Gotchas

1. **Test Paths**: Updated - all test files now use relative paths
2. **Port Conflicts**: SSE server uses port 3333 - check with `lsof -i :3333`
3. **Nullable Warnings**: Project uses nullable reference types - initialize all properties
4. **Build Warnings**: Run clean builds to catch all warnings: `dotnet clean && dotnet build`
5. **RoslynPath Case**: Pattern type is case-insensitive but use lowercase "roslynpath"

## Contributing

When adding new features:
1. Follow statement-level granularity principle
2. Update TOOL_SYNOPSIS.md with new tools
3. Add integration tests in `tests/tools/`
4. Consider RoslynPath integration for stability
5. Document design decisions in `docs/design/`