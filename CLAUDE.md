# CLAUDE.md - Project Overview for AI Assistants

## Quick Start for Context Recreation

If you're starting fresh with this codebase:
1. **Read this file first** - It's designed to give you all essential context
2. **Check Serena memories** - Use `mcp__serena__list_memories` and read relevant ones for project context
3. **Check recent changes** - See "Recent Changes" section below
4. **Review key files** - Listed in "Key Implementation Files" section
5. **Understand the architecture** - MCP server with 27 Roslyn-based tools (including diagnostics)
6. **Check pending tasks** - See implementation status sections

## Project Structure

This is Spelunk.NET (formerly MCP Roslyn/McpDotnet), which provides multi-language code analysis and manipulation tools via the Model Context Protocol (MCP). It supports C#, VB.NET, and F# with language-agnostic abstractions.

Spelunk.NET is distributed as a .NET global tool (`spelunk`) with unified stdio and SSE modes.

### Directory Layout

```
Spelunk/
├── src/
│   └── Spelunk.Server/         # Main server implementation (packaged as Spelunk.NET)
│       ├── SpelunkPath/          # SpelunkPath query engine
│       ├── LanguageHandlers/     # C# and VB.NET language handlers
│       ├── FSharp/               # F# support infrastructure
│       ├── Modes/                # IMode interface and implementations
│       │   ├── IMode.cs          # Mode abstraction
│       │   ├── StdioMode.cs      # Stdio mode implementation
│       │   └── SseMode.cs        # SSE server mode
│       ├── Process/              # Background process management
│       │   ├── ProcessManager.cs # SSE lifecycle (start/stop/status)
│       │   └── PidFileManager.cs # PID file I/O
│       ├── Tools/                # MCP tool implementations
│       └── Program.cs            # Entry point with System.CommandLine
├── docs/                         # Current documentation
│   ├── TOOL_SYNOPSIS.md          # Reference for all tools
│   ├── design/                   # Design documents
│   │   ├── STATEMENT_LEVEL_EDITING.md
│   │   └── EPHEMERAL_MARKER_DESIGN.md
│   ├── roslyn-path/              # SpelunkPath documentation
│   │   ├── SPELUNK_PATH_INSTRUCTIONS.md   # Quick reference
│   │   ├── SPELUNK_PATH_AGENT_GUIDE.md    # 5-minute guide
│   │   ├── SPELUNK_PATH_SYNTAX_DESIGN.md  # Full syntax spec
│   │   ├── SPELUNK_PATH_ANALYSIS_EXAMPLES.md
│   │   ├── SPELUNK_PATH_TEST_PACKAGE.md
│   │   └── examples/             # Demo code
│   └── stale/                    # Archived docs (historical only)
├── tests/                        # Test suites
│   ├── RoslynPath/               # XUnit tests for SpelunkPath
│   ├── tools/                    # Python integration tests
│   ├── protocol/                 # MCP protocol tests
│   ├── integration/              # Cross-cutting tests
│   ├── utils/                    # Test utilities
│   └── run-all-tests.py          # Python test runner
├── scripts/                      # Shell scripts for development
│   ├── run/                      # Server launch scripts
│   │   ├── run-stdio-server.sh   # Primary STDIO server
│   │   ├── run-server-debug.sh   # Debug mode server
│   │   └── run-sse-server.sh     # SSE server
│   └── test/                     # Test scripts
│       ├── test-server.sh        # Run with test-requests.jsonl
│       └── test-mcp-server.sh    # Interactive protocol test
├── test-workspace/               # Sample projects for testing (C#, VB.NET, F#)
├── README.md                     # Project readme
└── CLAUDE.md                     # This file
```

## Key Concepts

### 1. Statement-Level Operations
All code modifications work at the statement level - this is the optimal granularity for refactoring. See `docs/design/STATEMENT_LEVEL_EDITING.md`.

### 2. Multi-Language Support
- **C#**: Full Roslyn integration
- **VB.NET**: Full Roslyn integration with language-agnostic mapping
- **F#**: Basic support via FSharp.Compiler.Service (separate from Roslyn)

### 3. SpelunkPath
An XPath-inspired query language for .NET code that provides stable references surviving edits:
- Language-agnostic: works with C# and VB.NET
- Enhanced with low-level node types (binary-expression, if-statement, literal, etc.)
- Full XPath-style axes support (ancestor::, descendant::, following-sibling::, etc.)
- Example: `//binary-expression[@operator='==' and @right-text='null']`
- VB.NET mapping: `//method[@returns='void']` finds both C# void methods and VB.NET Subs
- See `docs/spelunk-path/` for full documentation

### 4. FSharpPath
An XPath-inspired query language specifically for F# AST:
- F#-specific constructs: `//function[@recursive]`, `//type[Union]`
- Pattern matching: `//function[@async and @inline]`
- Active patterns and computation expressions support

### 5. Tool Composition
Complex refactorings are built from simple, composable tools. The 33 implemented tools can be combined for powerful operations.

### 6. Semantic vs Syntactic Tools
The server provides two complementary tool categories:
- **Semantic tools** (find-* family): Task-focused, return rich type information using Roslyn's semantic model
- **Syntactic tools** (SpelunkPath-based): Query-focused, provide flexible pattern matching on syntax trees
- See `docs/design/SEMANTIC_VS_SYNTACTIC_TOOLS.md` for architectural philosophy

## Documentation Guide

### For Quick Reference
- **Tool usage**: `docs/TOOL_SYNOPSIS.md` - All tools with examples
- **SpelunkPath syntax**: `docs/spelunk-path/SPELUNK_PATH_INSTRUCTIONS.md`
- **Agent tool selection**: `docs/AGENT_TOOL_SELECTION_GUIDE.md` - Decision tree for AI agents
- **Agent examples**: `docs/AGENT_QUERY_EXAMPLES.md` - Concrete semantic vs syntactic examples

### For Understanding Design
- **Philosophy**: `docs/design/STATEMENT_LEVEL_EDITING.md`
- **SpelunkPath rationale**: `docs/spelunk-path/SPELUNK_PATH_SYNTAX_DESIGN.md`
- **F# architecture**: `docs/design/FSHARP_ARCHITECTURE.md`
- **FSharpPath syntax**: `docs/spelunk-path/FSHARP_PATH_SYNTAX.md`
- **Semantic vs Syntactic**: `docs/design/SEMANTIC_VS_SYNTACTIC_TOOLS.md`

### For Testing
- **Integration tests**: `tests/` directory with Python test scripts
- **Test runner**: `python3 tests/run-all-tests.py`
- **Test workspace**: `test-workspace/` contains sample C# code

## Current Implementation Status

### Completed Features
- ✅ 37 MCP tools implemented (33 Roslyn + 4 F#)
- ✅ Multi-language support (C#, VB.NET, F#)
- ✅ Statement-level operations (find, replace, insert, remove)
- ✅ Ephemeral marker system for tracking statements
- ✅ Language-agnostic SpelunkPath query engine with enhanced navigation
- ✅ F# support via FSharp.Compiler.Service with all tools functional
- ✅ Comprehensive test suite with multi-language tests (XUnit + Python)
- ✅ Advanced AST navigation and querying capabilities
- ✅ .NET Global Tool packaging (Spelunk.NET)
- ✅ Unified CLI with stdio and SSE modes

### Recently Completed (Latest Session - November 2025)
- ✅ Complete rebrand from Spelunk.NET to Spelunk.NET
- ✅ Renamed RoslynPath to SpelunkPath throughout codebase
- ✅ Unified CLI architecture with System.CommandLine
  - `spelunk stdio` - Run in stdio mode for MCP clients
  - `spelunk sse` - Run SSE server (with start/stop/status/logs/restart)
- ✅ Background process management for SSE server
  - PID file tracking at `~/.spelunk/sse.pid`
  - Log file at `~/.spelunk/sse.log`
  - Cross-platform process spawning
- ✅ Packaged as .NET global tool
  - PackageId: Spelunk.NET
  - Command: `spelunk`
  - Version: 1.0.0-alpha-01
- ✅ Migrated SSE server into main project (no longer separate)
- ✅ Updated all documentation and tests
- ✅ XUnit test suite (46/55 tests passing, 9 tests for unimplemented function argument parsing)

### Previously Completed (Earlier 2025)
- ✅ Fixed field symbol detection in `spelunk-get-symbols` (special handling for FieldDeclarationSyntax)
- ✅ Fixed workspace parameter handling (now accepts both workspace IDs and paths)
- ✅ Fixed SpelunkPath parser to handle `//method[Name]//statement` patterns correctly
- ✅ Removed artificial restrictions on SpelunkPath patterns
- ✅ All torture test failures resolved (field detection, data flow, statement context)
- ✅ Enhanced control flow analysis to use Roslyn's AnalyzeControlFlow API exclusively
- ✅ Removed misleading fallback - now returns null with clear error when analysis fails
- ✅ Comprehensive data flow analysis testing and documentation (DATA_FLOW_ANALYSIS.md)
- ✅ Updated CONTROL_FLOW_ANALYSIS.md to reflect error-first approach

### Previously Completed
- ✅ Implemented XPath-style statement search with structural paths
- ✅ Added Path property showing full AST location from solution to statement
- ✅ Standardized depth calculation (always from method/class boundary)
- ✅ Removed "smart" container filtering - returns all matches in document order
- ✅ Fixed parameter consistency (file parameter, workspacePath/path parameters)
- ✅ Documented XPath conventions in docs/design/XPATH_STATEMENT_SEARCH.md

### High Priority Pending
- None currently

### Medium Priority Pending
- None currently

## Development Workflow

### Running the Server

#### Using the Global Tool (Recommended)
```bash
# Install the global tool
dotnet pack src/Spelunk.Server/Spelunk.Server.csproj
dotnet tool install --global --add-source ./src/Spelunk.Server/nupkg Spelunk.NET

# Run in stdio mode (for MCP clients)
spelunk stdio

# Run SSE server
spelunk sse                  # Start on port 3333
spelunk sse -p 8080          # Start on custom port
spelunk sse status           # Check status
spelunk sse logs             # View logs
spelunk sse logs -f          # Follow logs
spelunk sse stop             # Stop server
spelunk sse restart          # Restart server
```

#### Development Mode
```bash
# Using convenience scripts
./scripts/run/run-stdio-server.sh      # Standard mode
./scripts/run/run-server-debug.sh      # Debug mode

# Or directly with dotnet
dotnet run --project src/Spelunk.Server -- stdio
dotnet run --project src/Spelunk.Server -- sse
```

### Running Tests
```bash
# XUnit tests
dotnet test

# Python integration tests
python3 tests/run-all-tests.py

# Specific test
python3 tests/tools/test-find-statements.py
```

### Testing SpelunkPath
See examples in `docs/spelunk-path/examples/`:
- `demo-spelunk-path-complex.cs` - Complex query demonstrations
- `test-spelunk-path-simple.cs` - Simple standalone test

### Configuration

#### User-Level Configuration
The server supports a user-level configuration file for setting allowed directories and other options.

**Location**: `~/.config/spelunk/config.json`

**Example**:
```json
{
  "Spelunk": {
    "AllowedPaths": [
      "/Users/bill/Repos",
      "/Users/bill/Desktop",
      "/Users/bill/Documents"
    ],
    "Logging": {
      "MinimumLevel": "Information"
    },
    "Server": {
      "RequestTimeoutSeconds": 120,
      "MaxWorkspaces": 10
    }
  }
}
```

**Configuration Priority** (highest to lowest):
1. Command line arguments
2. Environment variables (e.g., `SPELUNK_ALLOWED_PATHS`)
3. User config (`~/.config/spelunk/config.json`)
4. Project config (`spelunk.config.json` in working directory)
5. Default settings

See `~/.config/spelunk/README.md` for detailed configuration documentation.

## Important Notes

1. **Line Numbers Are Fragile**: Always prefer SpelunkPath over line/column positions
2. **Statement Granularity**: Operations work on complete statements, not arbitrary text ranges
3. **Markers Are Ephemeral**: They survive edits but not file reloads
4. **Tools Are Composable**: Complex operations should combine simple tools

## Common Tasks

### Find and Replace Pattern
```python
# 1. Find targets (now supports SpelunkPath!)
results = find_statements(
    pattern="//statement[@contains='Console.WriteLine']",
    patternType="spelunkpath"
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

### Advanced AST Navigation
```python
# 1. Find null comparisons using enhanced SpelunkPath
null_checks = query_syntax(
    spelunkPath="//if-statement//binary-expression[@operator='==' and @right-text='null']"
)

# 2. Navigate to parent method from any position
result = navigate(
    from={"file": "/path/file.cs", "line": 42, "column": 10},
    path="ancestor::method[1]"
)

# 3. Get AST structure to understand code
ast = get_ast(
    file="/path/file.cs",
    root="//method[ProcessOrder]",
    depth=3
)
```

## Debugging Tips

1. Use `tests/utils/debug_test.py` for interactive testing
2. Check server logs for detailed Roslyn operations
3. SpelunkPath queries can be tested standalone with examples
4. The marker system helps track statements through transformations

## Key Implementation Files

### Core Server Components
- `src/Spelunk.Server/DotnetWorkspaceManager.cs` - Main workspace and tool implementations
- `src/Spelunk.Server/McpJsonRpcServer.cs` - MCP protocol handling
- `src/Spelunk.Server/Program.cs` - Server entry point with System.CommandLine

### Mode Infrastructure
- `src/Spelunk.Server/Modes/IMode.cs` - Mode abstraction interface
- `src/Spelunk.Server/Modes/StdioMode.cs` - Stdio mode implementation
- `src/Spelunk.Server/Modes/SseMode.cs` - SSE server mode

### Process Management
- `src/Spelunk.Server/Process/ProcessManager.cs` - Background SSE lifecycle
- `src/Spelunk.Server/Process/PidFileManager.cs` - PID file I/O

### SpelunkPath Components
- `src/Spelunk.Server/SpelunkPath/SpelunkPath.cs` - Main query engine
- `src/Spelunk.Server/SpelunkPath/SpelunkPathParser.cs` - Query parser
- `src/Spelunk.Server/SpelunkPath/SpelunkPathEvaluator.cs` - AST evaluator

### Test Suites
- `tests/RoslynPath/` - XUnit tests for SpelunkPath
- `tests/tools/` - Python integration tests for MCP tools

### Recent Changes (Latest Architecture Refactor - November 2025)
- Complete rebrand to Spelunk.NET with unified CLI
- SpelunkPath replaces RoslynPath throughout codebase
- SSE server merged into main project with mode pattern
- Background process management for SSE lifecycle
- Packaged as .NET global tool

## Common Pitfalls & Gotchas

1. **Test Paths**: Updated - all test files now use relative paths
2. **Port Conflicts**: SSE server uses port 3333 by default - check with `lsof -i :3333`
3. **Nullable Warnings**: Project uses nullable reference types - initialize all properties
4. **Build Warnings**: Run clean builds to catch all warnings: `dotnet clean && dotnet build`
5. **SpelunkPath Case**: Pattern type is case-insensitive but use lowercase "spelunkpath"
6. **Background SSE**: Use `spelunk sse stop` before uninstalling/reinstalling the tool
7. **PID Files**: Located at `~/.spelunk/sse.pid` - clean up manually if needed

## Contributing

When adding new features:
1. Follow statement-level granularity principle
2. Update TOOL_SYNOPSIS.md with new tools
3. Add integration tests in `tests/tools/` (Python) and/or `tests/RoslynPath/` (XUnit)
4. Consider SpelunkPath integration for stability
5. Document design decisions in `docs/design/`
6. Test both stdio and SSE modes
7. Update version in Spelunk.NET.Server.csproj before packaging

## F# Architecture

### Why F# is Different

F# requires separate handling from C#/VB.NET because:
- **Different Compiler**: Uses FSharp.Compiler.Service instead of Roslyn
- **Different AST**: Expression-based functional AST vs statement-based OO AST  
- **MSBuild Limitation**: MSBuildWorkspace cannot load F# projects
- **Type System**: Advanced type inference, discriminated unions, type providers

### Implementation Strategy

We maintain parallel infrastructure while providing a unified interface:

```
MCP Client Request → Unified Tool Interface → Language Router
                                                 ├── Roslyn Engine (C#/VB.NET)
                                                 └── F# Engine (FSharp.Compiler.Service)
```

### Key F# Components

1. **FSharpWorkspaceManager**: Manages F# projects outside MSBuildWorkspace
2. **FSharpProjectTracker**: Tracks F# projects that couldn't load in Roslyn
3. **FSharpPath**: XPath-like query language for F# AST (like SpelunkPath for C#/VB)
4. **Symbol Mapper**: Translates between F# and Roslyn symbol formats

### F# Development Guides

- **Architecture**: `docs/design/FSHARP_ARCHITECTURE.md` - Why and how F# differs
- **Implementation**: `docs/design/FSHARP_IMPLEMENTATION_GUIDE.md` - Technical details
- **Roadmap**: `docs/design/FSHARP_ROADMAP.md` - Planned features and timeline

### Current F# Status

F# support is currently disabled (commented out) pending full implementation. The infrastructure is designed but not active. To work with F#:
1. Project detection works (reports skipped F# projects)
2. Full support requires uncommenting FSharpWorkspaceManager
3. See roadmap for implementation timeline