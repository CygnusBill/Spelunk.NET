# XPath-Style Statement Search Design

## Overview

The statement search functionality follows XPath conventions, providing predictable, composable results without hidden filtering or "smart" behavior. This document describes the design philosophy and implementation details.

## Design Philosophy

Following XPath's principle of "return what you ask for":

1. **No Hidden Filtering**: All matching statements are returned, including both parent and child statements when both match the criteria
2. **Document Order**: Results are always returned in source file order (parents before children)
3. **Explicit Queries**: Users get exactly what they ask for - use more specific queries to narrow results
4. **Information, Not Filtering**: Depth and path provide context but don't filter results

## Result Structure

Each statement result includes:

### Path (Structural Position)
Shows the complete path from solution root to the statement:
```
/solution/project/file/class/method/node[position]/node[position]...
```

Example:
```
/McpDotnet/McpDotnet.Server/McpJsonRpcServer.cs/McpJsonRpcServer/RunAsync/block[1]/expression[1]
```

Components:
- **Solution**: Name from .sln file
- **Project**: Project name within solution
- **File**: Just the filename (not full path)
- **Class/Method**: Semantic containers if present
- **Nodes**: Statement types with positions among siblings

### Depth (Nesting Level)
- Consistent calculation from method/class boundary
- Depth 0 = Direct child of method body
- Depth 1 = One level nested (e.g., inside if-statement)
- Same calculation for all search types (text, regex, RoslynPath)

### Location (Physical Position)
- File path, line, and column
- Line/column are relative to the containing file
- 1-based numbering (standard for editors)
- Kept separate from Path (different concerns)

## Node Type Abbreviations

For readability, paths use short node type names:

| C# Node Type | Path Name |
|-------------|-----------|
| IfStatementSyntax | if |
| WhileStatementSyntax | while |
| ForStatementSyntax | for |
| ForEachStatementSyntax | foreach |
| BlockSyntax | block |
| ExpressionStatementSyntax | expression |
| LocalDeclarationStatementSyntax | local |
| ReturnStatementSyntax | return |
| TryStatementSyntax | try |

## Position Numbering

Positions (e.g., `[1]`, `[2]`) indicate:
- 1-based index among siblings of the same type
- Only counts siblings with the same node type
- Enables unique identification within parent

## Example Results

### Text Search for "WriteLine"
```
Statement ID: stmt-1
Path: /McpDotnet/TestProject/Program.cs/Test/Method/block[1]/expression[1]
Type: ExpressionStatementSyntax
Depth: 0
Location: /path/to/Program.cs:6:9
Code: Console.WriteLine("outer");

Statement ID: stmt-2
Path: /McpDotnet/TestProject/Program.cs/Test/Method/block[1]/if[1]
Type: IfStatementSyntax  
Depth: 0
Location: /path/to/Program.cs:8:9
Code: if (true) { Console.WriteLine("nested"); }

Statement ID: stmt-3
Path: /McpDotnet/TestProject/Program.cs/Test/Method/block[1]/if[1]/block[1]/expression[1]
Type: ExpressionStatementSyntax
Depth: 1
Location: /path/to/Program.cs:10:13
Code: Console.WriteLine("nested");
```

Note that all three statements are returned:
- stmt-1: Contains "WriteLine" directly
- stmt-2: The if-statement contains "WriteLine" in its body
- stmt-3: The nested expression contains "WriteLine"

Users can:
- Use depth to filter for leaf statements (highest depth)
- Use path to understand containment relationships
- Process in document order (parents before children)

### RoslynPath Search
```
Pattern: //method[Main]//statement
```

Returns ALL statement descendants of Main method, with paths showing exact position.

## Benefits

1. **Predictable**: No surprises, tool does exactly what documented
2. **Composable**: Simple primitive that can be combined for complex operations
3. **Complete Context**: Full path shows exact location in solution hierarchy
4. **Cross-file Clarity**: Same method names in different files are unambiguous
5. **Navigation Ready**: Paths could be used with navigation tools

## Implementation Notes

- Uses Roslyn's `DescendantNodes()` which naturally provides document order
- Path building walks ancestors from statement to root
- Position counting examines siblings of same type
- No filtering of "container" statements - all matches returned

## Comparison with Previous Approach

| Aspect | Previous | XPath-Style |
|--------|----------|-------------|
| Parent statements | Filtered out | Returned |
| Depth meaning | Varied by search type | Consistent |
| Result order | Not guaranteed | Document order |
| Philosophy | "Smart" filtering | Return what asked |

## Usage Guidelines

1. **For specific statements**: Use more specific patterns or node types
2. **For leaf statements only**: Sort by depth and take highest
3. **To avoid duplicates**: Process in order, skip children of processed parents
4. **For navigation**: Use path to identify parent/sibling relationships

## Future Considerations

While not implemented (following XPath's minimalist approach), possible extensions could include:
- Path-based filtering in queries (e.g., `//project[Tests]//statement`)
- Sibling navigation using paths
- Integration with navigate tool using paths

However, the current implementation intentionally remains simple and predictable, allowing users to build their own filtering logic as needed.