# Agent Query Examples - Semantic vs Syntactic

## Finding Methods

### Task: "Find all public methods"

**Semantic Approach:**
```json
{
  "tool": "dotnet-find-method",
  "pattern": "*",
  "accessibility": "public"
}
```
Returns: Full method signatures with types, parameters, attributes

**Syntactic Approach:**
```json
{
  "tool": "dotnet-query-syntax",
  "roslynPath": "//method[@public]"
}
```
Returns: Method syntax nodes with text and location

**When to use which:**
- Semantic: When you need parameter types, return types, or inheritance info
- Syntactic: When you just need to locate methods or check their structure

### Task: "Find async methods that return Task<T>"

**Semantic Approach:**
```json
{
  "tool": "dotnet-find-method",
  "pattern": "*",
  "includeAsync": true,
  "returnTypePattern": "Task<*>"
}
```

**Syntactic Approach:**
```json
{
  "tool": "dotnet-query-syntax",
  "roslynPath": "//method[@async and @returns-generic='Task']"
}
```

## Finding Patterns

### Task: "Find all try-catch blocks without finally"

**Only Syntactic (semantic tools can't do this):**
```json
{
  "tool": "dotnet-query-syntax",
  "roslynPath": "//try-statement[not(finally-clause)]"
}
```

### Task: "Find string concatenation in loops"

**Only Syntactic:**
```json
{
  "tool": "dotnet-query-syntax",
  "roslynPath": "//for-statement//binary-expression[@operator='+' and @involves-string]",
  "includeSemanticInfo": true
}
```
Note: includeSemanticInfo helps confirm string types

## Type Analysis

### Task: "Find all classes that implement multiple interfaces"

**Semantic Approach:**
```json
{
  "tool": "dotnet-find-class",
  "pattern": "*",
  "minimumInterfaceCount": 2
}
```

**Syntactic Approach:**
```json
{
  "tool": "dotnet-query-syntax",
  "roslynPath": "//class[count(base-list/simple-base-type) > 1]"
}
```

**Key Difference**: Semantic includes inherited interfaces, syntactic only sees declared ones

### Task: "Find all uses of generic types"

**Semantic (for instances):**
```json
{
  "tool": "dotnet-find-type-references",
  "typeName": "List<*>"
}
```

**Syntactic (for declarations):**
```json
{
  "tool": "dotnet-query-syntax",
  "roslynPath": "//generic-name"
}
```

## Code Quality Checks

### Task: "Find empty catch blocks"

**Syntactic Only:**
```json
{
  "tool": "dotnet-query-syntax",
  "roslynPath": "//catch-clause[block[not(statement)]]"
}
```

### Task: "Find methods longer than 50 lines"

**Syntactic Only:**
```json
{
  "tool": "dotnet-query-syntax",
  "roslynPath": "//method[block[@line-count > 50]]"
}
```

### Task: "Find TODO comments"

**Syntactic:**
```json
{
  "tool": "dotnet-query-syntax",
  "roslynPath": "//comment[@contains='TODO']"
}
```

## Cross-File Analysis

### Task: "Find all callers of a method"

**Semantic Only:**
```json
{
  "tool": "dotnet-find-references",
  "symbolName": "OrderService.ProcessOrder"
}
```

### Task: "Find unused private methods"

**Semantic Required:**
```python
# Step 1: Find private methods
methods = dotnet_find_method(pattern="*", accessibility="private")

# Step 2: Check references for each
for method in methods:
    refs = dotnet_find_references(symbolName=method.FullName)
    if not refs:
        print(f"Unused: {method}")
```

## Complex Refactoring Scenarios

### Task: "Find all places where null is returned from methods returning Task"

**Syntactic with Semantic Enrichment:**
```json
{
  "tool": "dotnet-query-syntax",
  "roslynPath": "//return-statement[expression[@text='null']]",
  "includeSemanticInfo": true
}
```
Then filter results where semantic info shows Task return type

### Task: "Find switch statements that could be pattern matching"

**Syntactic:**
```json
{
  "tool": "dotnet-query-syntax",
  "roslynPath": "//switch-statement[switch-section/case-label[pattern-syntax]]"
}
```

## Navigation Examples

### Task: "From a method call, find the method definition"

**Semantic:**
```json
{
  "tool": "dotnet-go-to-definition",
  "file": "Program.cs",
  "line": 42,
  "column": 15
}
```

**Syntactic Navigation:**
```json
{
  "tool": "dotnet-navigate",
  "from": {"file": "Program.cs", "line": 42, "column": 15},
  "path": "ancestor::method[1]",
  "includeSemanticInfo": true
}
```

## Performance Patterns

### Task: "Find all LINQ queries"

**Fast (Syntactic):**
```json
{
  "tool": "dotnet-query-syntax",
  "roslynPath": "//query-expression | //invocation[@name='Where' or @name='Select']"
}
```

**Accurate (Semantic):**
```json
{
  "tool": "dotnet-find-references",
  "symbolName": "System.Linq.Enumerable.*"
}
```

## Multi-Language Examples

### C# vs VB.NET - Same Query

**Finding Properties - Language Agnostic:**
```json
{
  "tool": "dotnet-find-property",
  "pattern": "*Price*"
}
```
Works for both: `public decimal Price { get; set; }` (C#) and `Public Property Price As Decimal` (VB)

**RoslynPath - Language Aware:**
```json
{
  "tool": "dotnet-query-syntax",
  "roslynPath": "//property[@name='*Price*']"
}
```
Handles both syntaxes transparently

### F# Specific Patterns

**F# Function Composition:**
```json
{
  "tool": "dotnet-query-syntax",
  "file": "Library.fs",
  "roslynPath": "//function[@name >> function]"
}
```

**F# Discriminated Unions:**
```json
{
  "tool": "dotnet-query-syntax", 
  "file": "Types.fs",
  "roslynPath": "//type[union-case]"
}
```

## Choosing Tools - Decision Examples

### ✅ Use Semantic Tools For:
1. "What type is this variable?" - Needs type resolution
2. "Find all implementations of ILogger" - Needs inheritance analysis  
3. "What methods does this class override?" - Needs base class info
4. "Find all references to this API" - Needs cross-file analysis
5. "What assembly is this type from?" - Needs compilation info

### ✅ Use Syntactic Tools For:
1. "Find if statements without else" - Pure structure
2. "Find string literals containing SQL" - Pattern in syntax
3. "Find methods with too many parameters" - Structural metric
4. "Find all await expressions" - Syntax pattern
5. "Find comments with specific tags" - Text pattern

### ✅ Use Syntactic + Semantic For:
1. "Find null returns from Task methods" - Pattern + type check
2. "Find unused usings directives" - Syntax + resolution check  
3. "Find calls to obsolete methods" - Pattern + attribute check
4. "Find boxing operations" - Syntax + type analysis
5. "Find implicit conversions" - Expression + type info