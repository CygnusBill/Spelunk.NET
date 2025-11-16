# SpelunkPath Syntax Design - Version 0.1

**Version:** 0.1
**Date:** November 2025
**Status:** Initial Release

## Version History

### Version 0.1 (November 2025) - Initial Release

**Implemented Features:**
- ✅ Basic path navigation (`/`, `//`, `..`)
- ✅ Node type specifiers (class, method, statement, etc.)
- ✅ Name selectors with wildcards (`Get*`, `*Service`)
- ✅ Position selectors (`[1]`, `[last()]`, `[last()-N]`)
- ✅ Attribute predicates (`[@async]`, `[@public]`, `[@contains='text']`)
- ✅ Complex predicates with `and`/`or`/`not`
- ✅ All XPath axes (ancestor, descendant, sibling, parent)
- ✅ Enhanced node types (binary-expression, if-statement, literal, etc.)
- ✅ Multi-language support (C#, VB.NET with language-agnostic mapping)

**Limitations/Future:**
- ⏳ XPath-style functions with arguments (e.g., `contains('text')`, `substring(@name, 0, 4)`)
  - Parser can parse function arguments but evaluator has incomplete implementation
  - Functions without arguments work: `position()`, `last()`, `first()`
- ⏳ `count()` function for node counting
- ⏳ Additional string functions (`string-length()`, `concat()`, `normalize-space()`)

### Planned for Future Versions

**Version 0.2 (Planned):**
- Complete XPath function implementations (contains, substring, string-length, etc.)
- Function nesting support
- Enhanced semantic predicates

**Version 1.0 (Planned):**
- F# support with FSharpPath
- Performance optimizations and caching
- Advanced type parameter handling

## Executive Summary

This document specifies SpelunkPath, a path-based syntax for identifying and navigating nodes in Roslyn syntax trees. The design draws inspiration from XPath's success in XML navigation while adapting to the specific needs of C# code analysis. The syntax must be stable across code edits, expressive enough for complex queries, and intuitive for both human and AI users.

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

## Proposed Syntax: SpelunkPath

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

> **Important**: `@contains` searches the normalized syntax tree text, not raw source code. Whitespace variations don't affect matching: `x==null`, `x == null`, and `x  ==  null` all match `@contains='== null'`. This makes queries robust against code formatting differences.

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

#### Counting (⏳ Planned for v0.2)
```
/method[count(block/statement) > 10]    - Methods with >10 statements (⏳ v0.2)
/class[count(method) = 1]               - Classes with single method (⏳ v0.2)
```

**Note:** The `count()` function is not yet implemented in v0.1.

### Special Functions

**Version 0.1 - Implemented:**
```
position()      - Position in parent's child list (✅ v0.1)
last()          - Last position (✅ v0.1)
first()         - First position (✅ v0.1)
```

**Future Versions - Planned:**
```
text()          - Text content of node (⏳ v0.2)
name()          - Name of declaration (⏳ v0.2)
type()          - Node type (⏳ v0.2)
line()          - Line number (⏳ v0.2)
column()        - Column number (⏳ v0.2)
span()          - Text span (⏳ v0.2)
parent()        - Parent node (⏳ v0.2)
children()      - Child nodes (⏳ v0.2)
contains(text)  - Text containment check (⏳ v0.2)
starts-with(text) - Prefix check (⏳ v0.2)
ends-with(text) - Suffix check (⏳ v0.2)
substring(text, start, length) - Substring extraction (⏳ v0.2)
string-length(text) - String length (⏳ v0.2)
concat(str1, str2, ...) - String concatenation (⏳ v0.2)
```

**Note:** Function argument parsing is partially implemented. While the parser can handle function calls with arguments, the evaluator currently only fully supports position functions (`position()`, `last()`, `first()`).

### Practical Examples

#### Find all TODO comments
**Input Code:**
```csharp
public class UserService
{
    public async Task<User> GetUserAsync(int id)
    {
        // TODO: Add caching here
        var user = await _repository.GetUserAsync(id);
        // TODO: Add validation
        return user;
    }
}
```

**Query:** `//comment[@contains='TODO']`

**Output:**
```
Found 2 matches:
1. Line 5: // TODO: Add caching here
   Path: /class[UserService]/method[GetUserAsync]/comment[1]
2. Line 7: // TODO: Add validation
   Path: /class[UserService]/method[GetUserAsync]/comment[2]
```

#### Find methods that throw exceptions
**Input Code:**
```csharp
public class OrderService
{
    public void ProcessOrder(Order order)
    {
        if (order == null)
            throw new ArgumentNullException(nameof(order));
        
        if (order.Items.Count == 0)
            throw new InvalidOperationException("Order has no items");
            
        // Process order...
    }
    
    public void SaveOrder(Order order)
    {
        // No exceptions thrown here
        _repository.Save(order);
    }
}
```

**Query:** `//method[.//statement[@type=ThrowStatement]]`

**Output:**
```
Found 1 match:
1. Method: ProcessOrder at line 3
   Path: /class[OrderService]/method[ProcessOrder]
   Contains 2 throw statements
```

#### Find async methods not using await
**Input Code:**
```csharp
public class DataService
{
    // BAD: async without await
    public async Task<string> GetDataAsync()
    {
        return _cache.GetData();  // Missing await!
    }
    
    // GOOD: async with await
    public async Task<string> FetchDataAsync()
    {
        var result = await _api.GetDataAsync();
        return result;
    }
}
```

**Query:** `//method[@async and not(.//expression[@contains='await'])]`

**Output:**
```
Found 1 suspicious async method:
1. Method: GetDataAsync at line 4
   Path: /class[DataService]/method[GetDataAsync]
   Issue: Async method contains no await expressions
```

#### Find classes implementing multiple interfaces
**Input Code:**
```csharp
public interface IService { }
public interface IDisposable { }
public interface ILogger { }

public class BasicService : IService { }

public class ComplexService : IService, IDisposable, ILogger
{
    // Implementation
}
```

**Query:** `//class[count(@implements) > 1]`

**Output:**
```
Found 1 match:
1. Class: ComplexService at line 7
   Path: /class[ComplexService]
   Implements: IService, IDisposable, ILogger (3 interfaces)
```

#### Find switch expressions (C# 8+)
**Input Code:**
```csharp
public class PriceCalculator
{
    public decimal CalculateDiscount(CustomerType type)
    {
        // Old style switch statement
        switch (type)
        {
            case CustomerType.Regular: return 0.05m;
            case CustomerType.Premium: return 0.10m;
            default: return 0m;
        }
    }
    
    public decimal CalculateBonus(CustomerType type) =>
        // New switch expression
        type switch
        {
            CustomerType.Regular => 0.02m,
            CustomerType.Premium => 0.05m,
            _ => 0m
        };
}
```

**Query:** `//expression[@type=SwitchExpression]`

**Output:**
```
Found 1 match:
1. Switch expression at line 15
   Path: /class[PriceCalculator]/method[CalculateBonus]/expression[1]
   Pattern: type switch { ... }
```

#### Find all null-forgiving operators
**Input Code:**
```csharp
public class UserManager
{
    public void ProcessUser(User? user)
    {
        // Using null-forgiving operator (dangerous!)
        var name = user!.Name;
        var email = user!.Email!.ToLower();
        
        // Safe null check
        if (user?.Address != null)
        {
            Console.WriteLine(user.Address);
        }
    }
}
```

**Query:** `//expression[@type=PostfixUnaryExpression and @operator='!']`

**Output:**
```
Found 3 matches:
1. Line 6: user!.Name
   Path: /class[UserManager]/method[ProcessUser]/statement[1]/expression[1]
2. Line 7: user!.Email
   Path: /class[UserManager]/method[ProcessUser]/statement[2]/expression[1]
3. Line 7: user!.Email!
   Path: /class[UserManager]/method[ProcessUser]/statement[2]/expression[2]
   Warning: Multiple null-forgiving operators in chain
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

## Implementation Status & Roadmap

### Version 0.1 - Current Release (Completed)
- ✅ Path parser with full tokenizer and AST builder
- ✅ All navigation axes (/, //, .., ancestor::, descendant::, sibling::)
- ✅ Name and position selectors with wildcards
- ✅ Complex predicates with boolean operators (and/or/not)
- ✅ Enhanced node types for low-level AST navigation
- ✅ Multi-language support (C#, VB.NET)
- ✅ Position functions (position(), last(), first())

### Version 0.2 - Next Release (Planned)
- ⏳ Complete XPath function implementations
  - contains(), starts-with(), ends-with()
  - substring(), string-length(), concat()
  - normalize-space(), translate()
- ⏳ count() function for node counting
- ⏳ Function nesting and composition
- ⏳ Expression-level navigation and querying
  - Find object creation expressions: `//expression[@type=ObjectCreationExpression and @text='new Item()']`
  - Return containing statement for expressions: `expression-statement()` function
  - Enhanced expression predicates
- ⏳ Performance optimization and query caching

### Version 1.0 - Future (Planned)
- ⏳ F# support with FSharpPath query language
- ⏳ Advanced type parameter handling for generics
- ⏳ IDE integration and IntelliSense
- ⏳ Path builder UI
- ⏳ Stability analyzer and migration tools

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

SpelunkPath Version 0.1 provides a robust, expressive syntax for navigating C# and VB.NET syntax trees. By adapting XPath's proven concepts to Roslyn's specific needs, we have created a powerful tool for code analysis and transformation that remains stable across edits while providing the precision needed for automated refactoring.

The syntax balances power with simplicity, making it accessible to both human developers and AI agents. Version 0.1 delivers core functionality including full XPath-style navigation, complex predicates, and multi-language support. Future versions will add advanced functions and F# support to become the comprehensive code navigation solution for the .NET ecosystem.

**Current Status:** Version 0.1 is production-ready for basic to intermediate code navigation tasks. Advanced function-based queries await Version 0.2.