# Agent Tool Selection Guide

## Quick Decision Tree

```
Need type information or cross-file analysis?
├── YES → Use Semantic Tools (find-*)
└── NO → Need complex pattern matching?
    ├── YES → Use Syntactic Tools (query-syntax, navigate, get-ast)
    └── NO → Use Semantic Tools for simplicity
```

## Tool Categories at a Glance

### Semantic Tools (find-* family)
**Best for**: Type information, symbol resolution, cross-file references, inheritance relationships
**Speed**: Slower (requires compilation)
**Pattern matching**: Simple wildcards only
**Returns**: Rich metadata with types and relationships

### Syntactic Tools (RoslynPath-based)
**Best for**: Code patterns, structural queries, expression-level analysis, code style checks
**Speed**: Fast (no compilation needed)
**Pattern matching**: Complex XPath-style queries
**Returns**: AST nodes with optional semantic enrichment

## Common Scenarios

### 1. "Find all methods that override a base class method"
**Use**: `dotnet-find-method` (semantic)
```json
{
  "tool": "dotnet-find-method",
  "pattern": "*",
  "includeOverrides": true
}
```
**Why**: Requires understanding inheritance relationships across files

### 2. "Find all null comparisons in if statements"
**Use**: `dotnet-query-syntax` (syntactic)
```json
{
  "tool": "dotnet-query-syntax",
  "roslynPath": "//if-statement//binary-expression[@operator='==' and @right-text='null']"
}
```
**Why**: Pattern-based search within syntax structure

### 3. "Find all implementations of IDisposable"
**Use**: `dotnet-find-implementations` (semantic)
```json
{
  "tool": "dotnet-find-implementations",
  "interfaceName": "IDisposable"
}
```
**Why**: Requires type resolution and inheritance analysis

### 4. "Find all async methods without ConfigureAwait"
**Use**: `dotnet-query-syntax` (syntactic)
```json
{
  "tool": "dotnet-query-syntax",
  "roslynPath": "//method[@async]//await-expression[not(invocation[@name='ConfigureAwait'])]"
}
```
**Why**: Structural pattern within method bodies

### 5. "Find what type a variable is"
**Use**: `dotnet-query-syntax` with semantic enrichment
```json
{
  "tool": "dotnet-query-syntax",
  "roslynPath": "//variable[@name='customer']",
  "includeSemanticInfo": true
}
```
**Why**: Need syntax location but also type information

## Performance Considerations

### Use Semantic Tools When:
- First-time analysis of a project
- Type information is essential
- Cross-file relationships matter
- Working with compiled, stable code

### Use Syntactic Tools When:
- Repeated queries on same codebase
- Analyzing code style or patterns
- Working with code that might not compile
- Need sub-second response times

## Multi-Language Considerations

### C# and VB.NET
Both languages work with all tools through Roslyn:
- Semantic tools abstract language differences
- RoslynPath queries use language-agnostic node types where possible

### F# 
Currently requires special handling:
- Semantic tools route to F#-specific implementations
- Use FSharpPath syntax instead of RoslynPath
- Some cross-language features limited

## Advanced Patterns

### Combining Tools
1. Use semantic tool to find symbols
2. Use syntactic tool to analyze usage patterns

Example: Find all ToString() overrides that don't call base
```json
// Step 1: Find overrides
{
  "tool": "dotnet-find-method",
  "pattern": "ToString",
  "includeOverrides": true
}

// Step 2: Check implementation
{
  "tool": "dotnet-query-syntax",
  "file": "result.file",
  "roslynPath": "//method[ToString]/block[not(.//base-expression)]"
}
```

### Semantic Enrichment
When you need both syntax patterns and type info:
```json
{
  "tool": "dotnet-query-syntax",
  "roslynPath": "//invocation[@name='Process']",
  "includeSemanticInfo": true
}
```

Returns syntax nodes with added semantic data:
```json
{
  "node": { "type": "InvocationExpression", "text": "Process(order)" },
  "semanticInfo": {
    "symbol": "OrderService.Process(Order)",
    "returnType": "ProcessResult",
    "isAsync": false
  }
}
```

## Decision Matrix

| Task | Recommended Tool | Reasoning |
|------|------------------|-----------|
| Find method by name | find-method | Simple, returns rich metadata |
| Find complex patterns | query-syntax | XPath flexibility needed |
| Find type references | find-type-references | Cross-file semantic analysis |
| Check code style | query-syntax | Pattern-based, no compilation |
| Find unused code | find-references + analysis | Semantic understanding required |
| Navigate from position | navigate | Position-based with XPath nav |
| Understand code structure | get-ast | Syntax tree visualization |
| Find inheritance chain | find-type | Semantic relationships |
| Find string literals | query-syntax | Simple syntax pattern |
| Find API usage | find-references | Semantic symbol resolution |

## Error Handling

### Semantic Tool Failures
- **Compilation errors**: Tool returns partial results or fails
- **Missing references**: May not resolve external types
- **Solution**: Fix compilation or use syntactic tools

### Syntactic Tool Limitations  
- **No type info**: Can't distinguish overloads
- **No cross-file**: Can't follow references
- **Solution**: Use includeSemanticInfo or switch to semantic tools

## Best Practices for Agents

1. **Start with intent**: What information does the user actually need?
2. **Consider compilation state**: Is the code likely to compile?
3. **Optimize for speed**: Use syntactic when semantic isn't required
4. **Cache patterns**: Reuse RoslynPath queries for similar requests
5. **Fail gracefully**: Have fallback from semantic to syntactic

## Quick Reference

### Semantic Tools
- `dotnet-find-method` - Methods with full signatures
- `dotnet-find-property` - Properties with types  
- `dotnet-find-class` - Classes with inheritance
- `dotnet-find-interface` - Interfaces with members
- `dotnet-find-type` - Any type definition
- `dotnet-find-implementations` - Interface/abstract implementations
- `dotnet-find-references` - Symbol usage across files
- `dotnet-find-type-references` - Type usage locations

### Syntactic Tools  
- `dotnet-query-syntax` - XPath queries on AST
- `dotnet-navigate` - Navigate from position using axes
- `dotnet-get-ast` - Get syntax tree structure
- `dotnet-find-in-project` - Text search (fallback)

### Hybrid Approach
All syntactic tools support `includeSemanticInfo: true` for enriched results combining syntax patterns with semantic data.