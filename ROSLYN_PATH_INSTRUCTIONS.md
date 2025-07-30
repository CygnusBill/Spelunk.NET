# RoslynPath Instructions for Agents

## Quick Start

RoslynPath lets you find specific code elements using path expressions, similar to how XPath works for XML.

### Basic Usage

```csharp
// Find all methods
var methods = RoslynPath.Find(sourceCode, "//method");

// Find a specific method
var getUserMethod = RoslynPath.Find(sourceCode, "//method[GetUser]");

// Find all return statements in a method
var returns = RoslynPath.Find(sourceCode, "//method[ProcessOrder]//statement[@type=ReturnStatement]");
```

## Essential Patterns

### 1. Finding Methods
```
//method                     - All methods
//method[GetUser]           - Method named GetUser  
//method[Get*]              - Methods starting with Get
//method[@async]            - Async methods
//method[@public]           - Public methods
```

### 2. Finding Statements
```
//statement[@type=IfStatement]       - All if statements
//statement[@type=ReturnStatement]   - All return statements
//statement[@contains='Console']     - Statements containing "Console"
//statement[1]                       - First statement in block
```

### 3. Finding in Context
```
//class[UserService]/method          - All methods in UserService class
//method[GetUser]/block/statement    - All statements in GetUser method
//method[@async]//statement[@type=ReturnStatement]  - Returns in async methods
```

### 4. Common Tasks

**Find all TODO comments:**
```
//comment[@contains='TODO']
```

**Find null checks:**
```
//statement[@type=IfStatement and @contains='== null']
```

**Find methods that throw exceptions:**
```
//method[.//statement[@type=ThrowStatement]]
```

**Find async methods without await:**
```
//method[@async and not(.//expression[@contains='await'])]
```

## Key Concepts

1. **`/` means direct child**: `/class/method` finds methods directly inside a class
2. **`//` means any descendant**: `//method` finds methods anywhere
3. **`[]` adds filters**: `[GetUser]` filters by name, `[@async]` by attribute
4. **`@type=` matches node types**: `[@type=IfStatement]` for if statements
5. **`@contains=` searches text**: `[@contains='TODO']` finds text containing TODO

## Node Types

- `class`, `interface`, `struct`, `enum` - Type declarations
- `method`, `property`, `field` - Members
- `statement` - Any statement
- `expression` - Any expression
- `block` - Code blocks { }
- `namespace` - Namespace declarations

## Examples for Common Refactoring Tasks

### Replace Console.WriteLine with Logger
1. Find: `//statement[@contains='Console.WriteLine']`
2. Each result gives you the exact location to replace

### Add Null Checks to Public Methods
1. Find: `//method[@public]`
2. For each, insert at: `/block/statement[1]`

### Find Long Methods
1. Find: `//method[count(.//statement) > 20]`

### Find Empty Catch Blocks
1. Find: `//catch[block[count(statement)=0]]`

## Tips

- Start simple and add filters as needed
- Use `@contains` for fuzzy matching
- Combine conditions with `and`/`or`
- Use `//` when you don't know the exact structure
- The results include line numbers and stable paths

## Error Messages

- "No matches found" - Path is valid but nothing matches
- "Unexpected character" - Syntax error in path
- "Expected X but found Y" - Malformed expression

## Integration with MCP Tools

When using with statement-level operations:

```json
{
  "roslynPath": "//method[ProcessOrder]//statement[@type=IfStatement][1]",
  "file": "/path/to/file.cs"
}
```

This is more stable than line/column positions!