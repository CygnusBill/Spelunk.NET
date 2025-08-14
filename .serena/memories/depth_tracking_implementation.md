# Depth Tracking Implementation for Statement Searches

## Overview
Added depth tracking to statement search results to help users and agents understand the nesting level of statements and make informed decisions about which matches to use.

## Implementation Details

### 1. Added Depth Property
- Added `public int Depth { get; set; }` to the `StatementInfo` class in RoslynWorkspaceManager.cs (line 5206)
- This property tracks the nesting depth of each statement relative to its containing method or class

### 2. Depth Calculation Logic
The depth is calculated in the `AddStatementToResult` method by:
- Counting statement ancestors up to the containing method or class boundary
- For top-level statements, using CompilationUnit as the boundary
- Both statements and BlockSyntax nodes count as depth levels

```csharp
// Calculate depth: count statement ancestors up to containing method or class
int depth = 0;
SyntaxNode? currentParent = statement.Parent;
SyntaxNode? boundary = containingMethod ?? containingClass;

// For top-level statements, use CompilationUnit as boundary
if (isTopLevel && boundary == null)
{
    boundary = statement.Ancestors().FirstOrDefault(n => n is CS.CompilationUnitSyntax);
}

// Count how many statement nodes are between this statement and the boundary
while (currentParent != null && currentParent != boundary)
{
    // Count statements and blocks as depth levels
    if (handler.IsStatement(currentParent) || currentParent is CS.BlockSyntax)
    {
        depth++;
    }
    currentParent = currentParent.Parent;
}
```

### 3. API Improvements
- Fixed `LoadWorkspaceToolAsync` to accept both "workspacePath" and "path" parameters for consistency
- Fixed `FindStatementsAsync` to accept direct "file" parameter in addition to scope object
- Added depth to the output format in McpJsonRpcServer.cs

## Why This Matters

### The Nesting Problem
When searching for statements (especially with patterns like `//statement` or text patterns), the results can include:
1. The actual statement containing the pattern
2. Parent statements that contain child statements with the pattern
3. Block statements that only match because their children match

### How Depth Helps
With depth information:
- Depth 0: Top-level statements in a method
- Depth 1: Statements inside one level of nesting (e.g., inside an if-statement)
- Depth 2+: Deeper nesting levels

Users and agents can:
- Filter for leaf statements (highest depth values)
- Understand the structure without parsing AST
- Make informed decisions about which matches to modify

## Testing
Created test scripts to verify depth tracking:
- `test-depth-with-workspace.py`: Comprehensive test with workspace loading
- `test-depth-main-code.py`: Tests on actual project source files
- Confirmed depth values are correctly calculated and displayed

## Future Considerations
1. **BlockSyntax treatment**: Currently BlockSyntax contributes to depth but isn't returned as a statement itself
2. **Leaf-only option**: Could add `leafStatementsOnly` parameter to only return deepest matches
3. **Parent filtering**: Could add option to exclude parent statements that only match due to children

## Related Files Modified
- `/src/McpRoslyn.Server/RoslynWorkspaceManager.cs` - Core depth calculation
- `/src/McpRoslyn.Server/McpJsonRpcServer.cs` - API parameter handling and output formatting
- `/src/McpRoslyn.Server/LanguageHandlers/CSharpLanguageHandler.cs` - Statement identification logic