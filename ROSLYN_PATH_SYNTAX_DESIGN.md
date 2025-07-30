# Roslyn Path Syntax Design

## Executive Summary

This document proposes a path-based syntax for identifying and navigating nodes in Roslyn syntax trees. The design draws inspiration from XPath's success in XML navigation while adapting to the specific needs of C# code analysis. The syntax must be stable across code edits, expressive enough for complex queries, and intuitive for both human and AI users.

## Domain Analysis

### The Problem Space

Current approaches to identifying statements in code have significant limitations:

1. **Line/Column Positions**: Fragile, break with any edit above the target
2. **Statement IDs**: Session-only, don't persist across reloads
3. **Text Matching**: Ambiguous when patterns repeat
4. **Syntax Annotations**: Only work within a single editing session

### Requirements

A robust statement identification system must:
- **Stability**: Survive code edits that don't directly affect the target
- **Precision**: Uniquely identify nodes without ambiguity
- **Flexibility**: Support both specific paths and pattern matching
- **Composability**: Allow complex queries from simple primitives
- **Readability**: Be understandable by humans and AI agents
- **Performance**: Execute efficiently on large syntax trees

### Forces from XPath

XPath succeeded because it:
1. **Mirrors Structure**: Path syntax (`/root/child`) matches tree structure
2. **Supports Patterns**: Wildcards (`*`), descendants (`//`)
3. **Has Predicates**: Filters (`[@attribute='value']`)
4. **Provides Axes**: Navigation in all directions (parent, sibling, descendant)
5. **Is Compositional**: Complex paths from simple parts

## Roslyn Syntax Tree Structure

### Key Concepts

1. **SyntaxNode**: Base class for all syntax tree nodes
   - CompilationUnit (root)
   - NamespaceDeclaration
   - ClassDeclaration
   - MethodDeclaration
   - Statements (various types)
   - Expressions

2. **SyntaxToken**: Atomic elements (keywords, identifiers, operators)

3. **SyntaxTrivia**: Whitespace, comments, directives

### C# Specific Challenges

Unlike XML, C# syntax trees have:
- **Multiple node types** at each level
- **Optional elements** (access modifiers, type parameters)
- **Semantic meaning** beyond structure
- **Contextual significance** (same syntax, different meaning)

## Proposed Syntax: RoslynPath

### Basic Path Navigation

```
/compilation/namespace[MyApp]/class[UserService]/method[GetUser]/block/statement[1]
```

Components:
- `/` - Child navigation
- `[]` - Node filter/selector
- Numbers - Position (1-based)

### Node Type Specifiers

Primary node types:
```
compilation     - Root compilation unit
namespace       - Namespace declaration
class           - Class declaration
interface       - Interface declaration
struct          - Struct declaration
enum            - Enum declaration
method          - Method declaration
property        - Property declaration
field           - Field declaration
constructor     - Constructor declaration
block           - Block statement { }
statement       - Any statement
expression      - Any expression
```

### Selector Syntax

#### Name Selectors
```
/class[UserService]           - Class named "UserService"
/method[Get*]                 - Methods starting with "Get"
/namespace[*.Models]          - Namespace ending with ".Models"
```

#### Position Selectors
```
/statement[1]                 - First statement
/statement[last()]            - Last statement
/statement[last()-1]          - Second to last
```

#### Type Selectors
```
/statement[@type=IfStatement]         - If statements only
/statement[@type=*Assignment*]        - Any assignment statement
/expression[@type=MethodInvocation]   - Method call expressions
```

#### Content Selectors
```
/statement[@contains='Console.WriteLine']   - Contains text
/statement[@matches='await.*Async']         - Regex match
/method[@async]                             - Async methods
/class[@abstract]                           - Abstract classes
```

### Navigation Axes

#### Descendant Axis (`//`)
```
//method[ProcessOrder]           - Find method anywhere
/class[Order]//statement         - All statements in Order class
//statement[@type=ReturnStatement] - All return statements
```

#### Parent Axis (`..`)
```
//../method                      - Parent method of current node
//statement[@contains='TODO']/../block - Block containing TODO
```

#### Sibling Axes
```
/following-sibling::statement[1]  - Next statement
/preceding-sibling::statement[1]  - Previous statement
/sibling::statement              - All sibling statements
```

#### Ancestor Axes
```
/ancestor::method[1]             - Containing method
/ancestor::class[1]              - Containing class
/ancestor-or-self::block[1]      - Nearest block (or self)
```

### Complex Predicates

#### Combining Conditions
```
/method[@async and @public]      - Public async methods
/statement[@type=IfStatement and @contains='null']  - Null checks
/class[@implements='IDisposable' or @implements='IAsyncDisposable']
```

#### Nested Paths
```
/method[block/statement[@type=ReturnStatement]]  - Methods with return
/class[method[@name='Dispose']]                  - Classes with Dispose
```

#### Counting
```
/method[count(block/statement) > 10]    - Methods with >10 statements
/class[count(method) = 1]               - Classes with single method
```

### Special Functions

```
text()          - Text content of node
name()          - Name of declaration
type()          - Node type
line()          - Line number
column()        - Column number
span()          - Text span
parent()        - Parent node
children()      - Child nodes
```

### Practical Examples

#### Find all TODO comments
```
//comment[@contains='TODO']
```

#### Find methods that throw exceptions
```
//method[block//statement[@type=ThrowStatement]]
```

#### Find async methods not using await
```
//method[@async and not(block//expression[@contains='await'])]
```

#### Find classes implementing multiple interfaces
```
//class[count(@implements) > 1]
```

#### Find switch expressions (C# 8+)
```
//expression[@type=SwitchExpression]
```

#### Find all null-forgiving operators
```
//expression[@type=PostfixUnaryExpression and @operator='!']
```

## Stability Strategies

### Path Resilience

Paths remain stable when:
1. **Name-based selection**: `/class[UserService]/method[GetUser]`
2. **Semantic markers**: `/method[@async]/statement[@type=AwaitExpression][1]`
3. **Relative navigation**: From a known stable point

Paths break when:
1. Positional only: `/statement[5]` (fragile)
2. Names change: `/method[OldName]` 
3. Structure changes: Adding/removing intermediate nodes

### Best Practices

1. **Prefer names over positions**
   ```
   Good:  /class[UserService]/method[ProcessOrder]
   Bad:   /class[1]/method[3]
   ```

2. **Use semantic markers**
   ```
   Good:  /method[@async and @returns='Task<User>']
   Bad:   /method[2]
   ```

3. **Combine multiple criteria**
   ```
   Good:  /class[UserService]/method[GetUser and @parameter='userId']
   Bad:   //method[GetUser]  (might match multiple)
   ```

## Implementation Considerations

### Parser Requirements
- Tokenizer for path syntax
- AST builder for path expressions
- Predicate evaluator
- Node matcher

### Roslyn Integration
- SyntaxNode visitor pattern
- Efficient tree traversal
- Caching for repeated queries
- Lazy evaluation

### Error Handling
- Invalid syntax → Clear error messages
- No matches → Empty result, not error
- Multiple matches → Return all or error based on context
- Partial matches → Closest match hints

## Comparison with Alternatives

### vs. Pure XPath
- **Simpler**: Fewer axes, focused on code navigation
- **Richer**: Code-specific predicates (@async, @implements)
- **Adapted**: 1-based indexing to match IDE line numbers

### vs. CSS Selectors
- **More powerful**: Full tree navigation, not just descendants
- **More explicit**: Clear parent/child relationships
- **Better predicates**: Richer filtering capabilities

### vs. JSONPath
- **Better navigation**: Multiple axes, not just child/descendant
- **Type-aware**: Understands C# syntax node types
- **More stable**: Names and semantic properties, not just structure

## Migration Path

### Phase 1: Basic Implementation
- Path parser
- Basic navigation (/, //, ..)
- Name and position selectors
- Simple predicates

### Phase 2: Advanced Features
- All axes
- Complex predicates
- Special functions
- Performance optimization

### Phase 3: Tooling
- IDE integration
- Path builder UI
- Stability analyzer
- Migration tools

## Example Tool Integration

### Current (Fragile)
```json
{
  "location": {
    "file": "/path/file.cs",
    "line": 25,
    "column": 9
  }
}
```

### New (Stable)
```json
{
  "path": "/class[UserService]/method[ProcessOrder]/block/statement[@type=IfStatement and @contains='null'][1]",
  "file": "/path/file.cs"
}
```

### Hybrid (Migration)
```json
{
  "path": "//method[ProcessOrder]",
  "location": {
    "file": "/path/file.cs", 
    "line": 25  // Hint for disambiguation
  }
}
```

## Benefits

1. **Stability**: Survives non-structural edits
2. **Precision**: Can uniquely identify any node
3. **Power**: Complex queries in concise syntax
4. **Familiarity**: Developers know XPath concepts
5. **Tooling**: Enables powerful code analysis tools

## Open Questions

1. **Namespace syntax**: `/namespace[A.B.C]` or `/namespace[A]/namespace[B]/namespace[C]`?
2. **Type parameters**: How to handle generics in paths?
3. **Lambda expressions**: Special syntax for anonymous functions?
4. **Pattern matching**: Extend to C# pattern syntax?
5. **Performance**: Index frequently used paths?

## Conclusion

RoslynPath provides a robust, expressive syntax for navigating C# syntax trees. By adapting XPath's proven concepts to Roslyn's specific needs, we can create a powerful tool for code analysis and transformation that remains stable across edits while providing the precision needed for automated refactoring.

The syntax balances power with simplicity, making it accessible to both human developers and AI agents. With proper implementation, this could become the standard way to reference code elements in the .NET ecosystem.