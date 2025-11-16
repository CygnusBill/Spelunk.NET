# XPath-Style Statement Search Implementation

## Overview
Implemented XPath-style conventions for statement search results, following the principle of "return what you ask for" - no hidden filtering or "smart" behavior.

## Key Changes

### 1. Added Path Property
Added `Path` property to `StatementInfo` class that shows the full structural path from solution to statement:
```
/McpDotnet/Spelunk.Server/McpJsonRpcServer.cs/McpJsonRpcServer/RunAsync/block[1]/expression[1]
```

### 2. XPath Conventions
- **Document Order**: Results always returned in source file order (parents before children)
- **No Filtering**: All matching statements returned (both parents and children if both match)
- **Position Tracking**: `[1]`, `[2]` show position among siblings of same type
- **Consistent Depth**: Always measures from method/class root (not relative to search)

### 3. Path Structure
```
/solution/project/file/class/method/node[position]/node[position]...
```

Components:
- Solution name (from .sln file)
- Project name  
- File name (just the name, not full path)
- Class/method names if present
- Node types with positions (e.g., `if[1]`, `block[2]`, `expression[3]`)

### 4. Depth Calculation
- Depth = nesting level from method/class boundary
- Depth 0 = direct child of method body
- Depth 1 = one level nested (inside if/while/try etc.)
- Same calculation for all search types (text, regex, RoslynPath)

### 5. Location vs Path
- **Path**: Structural position in AST (what contains this?)
- **Location**: Physical position in file (where is this?)
- Line and column are relative to the containing file (1-based, editor-compatible)

## Benefits

1. **Predictable**: Tool does exactly what it says, no surprises
2. **Complete Context**: Full path shows exactly where each statement lives
3. **Unambiguous**: Each node has a unique path
4. **Enables Navigation**: Could use path with navigate tool to jump to parent/siblings
5. **Cross-file Clarity**: Same method name in different files/classes is clear

## Example Output
```
Statement ID: stmt-6
Path: /McpDotnet/Spelunk.Server/McpJsonRpcServer.cs/McpJsonRpcServer/RunAsync/block[1]/expression[1]
Type: ExpressionStatementSyntax
Depth: 1
Location: /Users/bill/Repos/McpDotnet/src/Spelunk.Server/McpJsonRpcServer.cs:75:9
Code: _logger.LogInformation("MCP Roslyn Server started - listening on stdio");
```

## Philosophy
Following XPath's philosophy:
- Explicit queries get explicit results
- Users filter with more specific queries if needed
- Depth and path provide information, not filtering
- The tool is a reliable, predictable primitive that can be composed

## Files Modified
- `DotnetWorkspaceManager.cs`: Added path building logic, removed container filtering
- `McpJsonRpcServer.cs`: Updated output format to show paths
- `StatementInfo` class: Added Path property

## Testing
Created `test-xpath-paths.py` to verify:
- Paths are correctly formatted
- Document order is preserved  
- Depth calculation is consistent
- All matching statements are returned (no filtering)