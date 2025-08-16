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
- Enhanced with low-level node types (binary-expression, if-statement, literal, etc.)
- Full XPath-style axes support (ancestor::, descendant::, following-sibling::, etc.)
- Example: `//binary-expression[@operator='==' and @right-text='null']`
- VB.NET mapping: `//method[@returns='void']` finds both C# void methods and VB.NET Subs
- See `docs/roslyn-path/` for full documentation

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
- **Syntactic tools** (RoslynPath-based): Query-focused, provide flexible pattern matching on syntax trees
- See `docs/design/SEMANTIC_VS_SYNTACTIC_TOOLS.md` for architectural philosophy

## Documentation Guide

### For Quick Reference
- **Tool usage**: `docs/TOOL_SYNOPSIS.md` - All tools with examples
- **RoslynPath syntax**: `docs/roslyn-path/ROSLYN_PATH_INSTRUCTIONS.md`
- **Agent tool selection**: `docs/AGENT_TOOL_SELECTION_GUIDE.md` - Decision tree for AI agents
- **Agent examples**: `docs/AGENT_QUERY_EXAMPLES.md` - Concrete semantic vs syntactic examples

### For Understanding Design
- **Philosophy**: `docs/design/STATEMENT_LEVEL_EDITING.md`
- **RoslynPath rationale**: `docs/roslyn-path/ROSLYN_PATH_SYNTAX_DESIGN.md`
- **F# architecture**: `docs/design/FSHARP_ARCHITECTURE.md`
- **FSharpPath syntax**: `docs/roslyn-path/FSHARP_PATH_SYNTAX.md`
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
- ✅ Language-agnostic RoslynPath query engine with enhanced navigation
- ✅ F# support via FSharp.Compiler.Service with all tools functional
- ✅ Comprehensive test suite with multi-language tests
- ✅ Advanced AST navigation and querying capabilities

### Recently Completed (Latest Session - January 2025)
- ✅ Fixed field symbol detection in `dotnet-get-symbols` (special handling for FieldDeclarationSyntax)
- ✅ Fixed workspace parameter handling (now accepts both workspace IDs and paths)
- ✅ Fixed RoslynPath parser to handle `//method[Name]//statement` patterns correctly
- ✅ Removed artificial restrictions on RoslynPath patterns
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

### Advanced AST Navigation
```python
# 1. Find null comparisons using enhanced RoslynPath
null_checks = query_syntax(
    roslynPath="//if-statement//binary-expression[@operator='==' and @right-text='null']"
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
3. **FSharpPath**: XPath-like query language for F# AST (like RoslynPath for C#/VB)
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