# CLAUDE.md - Project Overview for AI Assistants

## Project Structure

This is the MCP Roslyn Server project, which provides code analysis and manipulation tools for C# via the Model Context Protocol (MCP).

### Directory Layout

```
McpDotnet/
├── src/
│   └── McpRoslyn/
│       └── McpRoslyn.Server/      # Main server implementation
│           ├── RoslynPath/        # RoslynPath query engine
│           └── *.cs              # Core server files
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
├── test-workspace/              # Sample C# projects for testing
├── README.md                    # Project readme
└── CLAUDE.md                    # This file
```

## Key Concepts

### 1. Statement-Level Operations
All code modifications work at the statement level - this is the optimal granularity for refactoring. See `docs/design/STATEMENT_LEVEL_EDITING.md`.

### 2. RoslynPath
An XPath-inspired query language for C# code that provides stable references surviving edits:
- Example: `//class[UserService]/method[GetUser]//statement[@contains='Console.WriteLine']`
- See `docs/roslyn-path/` for full documentation

### 3. Tool Composition
Complex refactorings are built from simple, composable tools. The 24 implemented tools can be combined for powerful operations.

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
- ✅ 24 MCP tools implemented (see TOOL_SYNOPSIS.md)
- ✅ Statement-level operations (find, replace, insert, remove)
- ✅ Ephemeral marker system for tracking statements
- ✅ RoslynPath query engine with parser and evaluator
- ✅ Comprehensive test suite

### High Priority Pending
- 🔲 Integrate RoslynPath into find-statements tool
- 🔲 Implement get-statement-context tool (semantic info)

### Medium Priority Pending
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
# 1. Find targets
results = find_statements(pattern="Console.WriteLine")

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

## Contributing

When adding new features:
1. Follow statement-level granularity principle
2. Update TOOL_SYNOPSIS.md with new tools
3. Add integration tests in `tests/tools/`
4. Consider RoslynPath integration for stability
5. Document design decisions in `docs/design/`