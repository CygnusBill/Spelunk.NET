# Semantic vs Syntactic Tools: Architectural Philosophy

## Overview

The MCP Roslyn Server provides two complementary approaches for code analysis and navigation:
1. **Semantic Tools** (find-* family) - High-level, task-focused tools using Roslyn's semantic model
2. **Syntactic Tools** (RoslynPath-based) - Flexible, query-based tools operating on syntax trees

This document explains the philosophy behind maintaining both approaches and guides developers on extending the system.

## Core Concepts

### Syntactic Analysis
- Operates on the **syntax tree** (how code is structured)
- Fast, doesn't require compilation
- Language-specific syntax nodes
- Can analyze malformed or incomplete code
- Examples: finding all if-statements, locating string literals, identifying method declarations

### Semantic Analysis
- Uses the **semantic model** (what code means)
- Requires successful compilation
- Provides type information, symbol resolution, and cross-file references
- Language-agnostic symbol concepts
- Examples: finding method overrides, resolving types, checking interface implementations

## Tool Categories

### Semantic Tools (find-* family)

**Characteristics:**
- Task-focused interfaces (`find-method`, `find-property`, `find-class`)
- Return rich metadata (types, fully qualified names, project context)
- Use simple wildcard patterns
- Optimized for common queries
- Provide cross-file information

**Example Output:**
```json
{
  "MemberName": "ProcessOrderAsync",
  "ReturnType": "System.Threading.Tasks.Task<Order>",
  "ClassName": "OrderService",
  "FullyQualifiedClassName": "MyApp.Services.OrderService",
  "IsAsync": true,
  "IsStatic": false,
  "OverridesMethod": "BaseService.ProcessOrderAsync",
  "ImplementsInterface": "IOrderService.ProcessOrderAsync"
}
```

### Syntactic Tools (RoslynPath-based)

**Characteristics:**
- Query-based interface using XPath-like syntax
- Operate at syntax node level
- Support complex predicates and navigation
- Language-agnostic queries (where possible)
- Return syntax information and location

**Example Output:**
```json
{
  "node": {
    "type": "MethodDeclaration",
    "text": "public async Task<Order> ProcessOrderAsync(int orderId)",
    "kind": "MethodDeclaration"
  },
  "location": { "file": "OrderService.cs", "line": 42, "column": 5 }
}
```

## Design Principles

### 1. Progressive Disclosure
- New users start with simple find-* tools
- Advanced users graduate to RoslynPath for complex queries
- Documentation shows progression path

### 2. Complementary Strengths
- Semantic tools for "what" questions (what type? what does it override?)
- Syntactic tools for "how" questions (how is it structured? what patterns exist?)

### 3. No Artificial Limitations
- Don't restrict semantic tools to force RoslynPath usage
- Don't overload RoslynPath with semantic complexity
- Each tool should excel at its purpose

### 4. Performance Optimization
- Semantic tools can cache compilation results
- Syntactic tools avoid compilation overhead
- Choose the right tool for performance-critical scenarios

## When to Use Each Approach

### Use Semantic Tools (find-*) When:
- You need type information or symbol resolution
- Queries are simple and well-defined
- Cross-file information is required
- You want rich metadata in results
- Performance is not critical (compilation required)

### Use Syntactic Tools (RoslynPath) When:
- You need complex pattern matching
- Queries involve structural patterns
- You're analyzing code style or conventions
- You need expression-level granularity
- Performance is critical (no compilation)
- Code might not compile

## Implementation Guidelines

### Adding New Semantic Tools
1. Identify a common, well-defined task
2. Use `ISymbol` and semantic model APIs
3. Return rich, structured results
4. Support simple pattern matching (wildcards)
5. Handle multiple languages via symbol abstraction

### Extending Syntactic Tools
1. Add new node type mappings in EnhancedNodeTypes
2. Support new predicates in RoslynPathEvaluator
3. Keep queries language-agnostic where possible
4. Focus on syntax tree navigation

### Bridging the Gap
Future enhancements will allow syntactic tools to optionally include semantic information:
```json
{
  "roslynPath": "//method[@name='Process*']",
  "includeSemanticInfo": true  // Enriches results with types, symbols
}
```

## Examples: Same Query, Different Approaches

### Finding All Async Methods

**Semantic Approach:**
```json
// Request
{ "tool": "spelunk-find-method", "pattern": "*", "includeAsync": true }

// Returns methods with full type info, knows about async Task returns
```

**Syntactic Approach:**
```json
// Request
{ "tool": "spelunk-query-syntax", "roslynPath": "//method[@async]" }

// Returns syntax nodes marked with async modifier
```

### Finding Interface Implementations

**Semantic Approach:**
```json
// Request
{ "tool": "spelunk-find-implementations", "interfaceName": "IDisposable" }

// Returns all types implementing IDisposable, even indirect implementations
```

**Syntactic Approach:**
```json
// Request
{ "tool": "spelunk-query-syntax", "roslynPath": "//class[@implements='IDisposable']" }

// Returns only classes with explicit interface declaration in syntax
```

## Future Directions

1. **Semantic Enrichment for RoslynPath**: Optional semantic information in query results
2. **Query Shortcuts in Semantic Tools**: Support RoslynPath predicates in find-* tools
3. **Unified Result Format**: Common schema with optional semantic fields
4. **Performance Profiling**: Guide tool selection based on query complexity

## F# Considerations

### Parallel Architecture

F# requires its own parallel implementation:
- **FSharpPath** for syntactic queries (equivalent to RoslynPath)
- **F# semantic tools** using FSharp.Compiler.Service
- Similar semantic/syntactic split within F# tooling

### Language-Specific Patterns

| Pattern | C#/VB.NET | F# |
|---------|-----------|-----|
| **Syntactic Query** | `//method[@async]` | `//function[@async]` |
| **Semantic Query** | Find overrides of IDisposable.Dispose | Find implementations of seq<'T> |
| **Cross-Language** | Find all references to System.String | Maps F# string to System.String |

### Unified Abstraction

Despite three different implementations (C# via Roslyn, VB.NET via Roslyn, F# via FSharp.Compiler.Service), we maintain consistent tool interfaces:

```
dotnet-find-method works across all three languages
├── Routes to Roslyn for .cs/.vb files  
├── Routes to F# engine for .fs files
└── Returns normalized results
```

### F# Type System Challenges

F# semantic analysis includes concepts not present in C#/VB.NET:
- Type inference without explicit annotations
- Discriminated unions and pattern matching
- Type providers generating types at compile-time
- Units of measure as part of the type system

These require F#-specific semantic tools while maintaining familiar interfaces.

## Conclusion

The dual approach of semantic and syntactic tools is not redundancy—it's complementary design. Like Git's porcelain/plumbing or SQL's views/queries, we provide both high-level convenience and low-level power. This enables users to start simple and grow into advanced usage while maintaining system coherence.