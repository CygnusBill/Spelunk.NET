# Backlog TODOs

## Improve LLM Guidance and Prompting
**Priority**: High
**Status**: Research Available - Implementation Pending

### Issue
Current tool descriptions and MCP protocol responses need better guidance for LLM agents to use Spelunk.NET effectively. LLMs need clearer instructions on:
- When to use which tools
- How to compose tool calls for complex refactoring tasks
- Best practices for SpelunkPath query construction
- Error recovery strategies
- Tool selection decision trees

### Proposed Solution
1. Enhance MCP tool descriptions with better examples and constraints
2. Add LLM-specific guidance sections to documentation
3. Include decision trees for tool selection
4. Provide concrete examples of multi-step refactoring patterns
5. Add prompt engineering best practices based on available research

### Research Available
User has research in prompt engineering and LLM guidance that should be incorporated into:
- Tool descriptions in `src/Spelunk.Server/McpJsonRpcServer.cs`
- Documentation in `docs/`
- MCP protocol responses
- Tool synopsis in `docs/TOOL_SYNOPSIS.md`

### Impact
- **User Experience**: Significant - Better LLM understanding means better results
- **Risk**: Low - Documentation and tool description improvements only
- **Priority**: High - Core to the value proposition of MCP tools

### Implementation Notes
This should be a comprehensive review of all user-facing strings that LLMs consume:
- MCP tool descriptions (JSON schema descriptions)
- Error messages returned to LLMs
- Documentation referenced by LLMs
- Examples in tool responses

### Next Steps
1. User to provide research on LLM prompting
2. Review current tool descriptions for clarity
3. Add decision trees and flowcharts for tool selection
4. Enhance error messages with actionable guidance
5. Test improvements with various LLM models

---

## PathPredicateExpr Optimization
**Location**: `src/Spelunk.Server/RoslynPath/RoslynPathParser.cs:812`
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