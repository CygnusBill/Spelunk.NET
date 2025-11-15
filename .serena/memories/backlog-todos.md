# Backlog TODOs

## PathPredicateExpr Optimization
**Location**: `src/McpDotnet.Server/RoslynPath/RoslynPathParser.cs:812`
**Current**: `public string PathString { get; set; } = "";`
**Suggested**: `public PathExpression Path { get; set; }`

### Issue
The `PathPredicateExpr` class currently stores the path as a string which gets re-parsed every time it's evaluated in `EvaluatePathPredicate`. This is inefficient as the same path string is parsed multiple times.

### Proposed Solution
1. Change `PathString` property to `Path` of type `PathExpression`
2. Parse the path once in `ParsePathPredicate` method
3. Update `EvaluatePathPredicate` to use the pre-parsed expression

### Impact
- **Performance**: Minor improvement - avoids re-parsing
- **Risk**: Low - purely internal optimization
- **Priority**: Low - current implementation works correctly

### Implementation Notes
```csharp
// Current (inefficient)
public class PathPredicateExpr : PredicateExpr
{
    public string PathString { get; set; } = "";
}

// Proposed (efficient)
public class PathPredicateExpr : PredicateExpr
{
    public PathExpression Path { get; set; } = new();
}
```

This would require updates to:
- `ParsePathPredicate()` in RoslynPathParser.cs
- `EvaluatePathPredicate()` in RoslynPathEvaluator.cs

### Reason for Backlog
Unlike the 3 TODOs fixed today which were blocking functionality, this is a performance optimization with no user-facing impact. The current implementation works correctly and the performance impact is minimal since path predicates are not commonly used.