# RoslynPath Quick Guide for AI Agents

## What is RoslynPath?

RoslynPath is a way to precisely identify locations in C# code that **stays stable even when the code is edited**. Think of it like a GPS coordinate for code - instead of saying "line 25" (which breaks when lines are added), you say "the return statement in the CalculateTotal method" which always finds the right spot.

## The 5-Minute Crash Course

### Basic Navigation: The `/` Operator

Just like file paths, use `/` to navigate through code structure:

```
/class[OrderService]/method[ProcessOrder]/block/statement[1]
```

This means: "In the OrderService class, find the ProcessOrder method, go into its block, get the first statement"

### Finding Things Anywhere: The `//` Operator

Use `//` to search everywhere below the current position:

```
//method[CalculateTotal]
```

This means: "Find a method named CalculateTotal anywhere in the code"

### The Most Common Patterns You'll Use

#### 1. Find a specific method
```
//method[ProcessOrder]
```

#### 2. Find all return statements in a method
```
//method[CalculateTotal]//statement[@type=ReturnStatement]
```

#### 3. Find statements containing specific text
```
//statement[@contains='Console.WriteLine']
```

#### 4. Find all async methods
```
//method[@async]
```

#### 5. Find null checks
```
//statement[@type=IfStatement and @contains='== null']
```

## Understanding the Syntax

### Node Types (what you can search for)

Common nodes:
- `class` - Class declarations
- `method` - Method declarations  
- `property` - Properties
- `field` - Fields
- `statement` - Any statement (if, return, assignment, etc.)
- `expression` - Any expression
- `block` - Code blocks { }

### Selectors (the `[]` part)

#### By Name
```
method[GetUser]         - Exact name
method[Get*]           - Starts with "Get"
method[*Async]         - Ends with "Async"
```

#### By Type
```
statement[@type=IfStatement]      - If statements
statement[@type=ReturnStatement]  - Return statements
expression[@type=AwaitExpression] - Await expressions
```

#### By Content
```
statement[@contains='TODO']       - Contains text
statement[@matches='await.*Async'] - Regex match
```

#### By Properties
```
method[@async]                    - Async methods
method[@public]                   - Public methods
class[@abstract]                  - Abstract classes
```

#### By Position
```
statement[1]                      - First statement
statement[last()]                 - Last statement
```

## Common Tasks and Their Paths

### "Find where this method is called"
```
//expression[@type=InvocationExpression and @contains='MethodName']
```

### "Find all TODOs"
```
//comment[@contains='TODO']
```

### "Find methods that might throw exceptions"
```
//method[.//statement[@type=ThrowStatement]]
```

### "Find empty catch blocks"
```
//catch[block[count(statement)=0]]
```

### "Find long methods"
```
//method[count(.//statement) > 20]
```

### "Find classes with only one method"
```
//class[count(method) = 1]
```

## Combining Conditions

Use `and`, `or`, `not`:

```
method[@async and @public]                    - Public async methods
statement[@type=IfStatement and @contains='null']  - Null check ifs
method[@public and not(@async)]              - Public sync methods
```

## Navigation from Found Nodes

### Going Up: Parent/Ancestor
```
/ancestor::method[1]      - The containing method
/ancestor::class[1]       - The containing class  
/..                      - Direct parent
```

### Going Sideways: Siblings
```
/following-sibling::statement[1]  - Next statement
/preceding-sibling::statement[1]  - Previous statement
```

## Real-World Examples

### Task: "Add logging to all public methods"
1. Find targets: `//method[@public]`
2. For each, insert at: `/block/statement[1]`

### Task: "Replace Console.WriteLine with logger"
1. Find all: `//statement[@contains='Console.WriteLine']`
2. Each one has a stable path for replacement

### Task: "Find async methods without await"
```
//method[@async and not(.//expression[@type=AwaitExpression])]
```

### Task: "Add null checks to methods with User parameters"
1. Find methods: `//method[parameter[@type='User']]`
2. Insert at: `/block/statement[1]`

## Tips for Stable Paths

### ✅ GOOD: Use names and types
```
/class[UserService]/method[GetUserById]
```

### ❌ BAD: Use only positions
```
/class[1]/method[3]    // Breaks if methods are reordered
```

### ✅ GOOD: Combine multiple criteria
```
//method[GetUser and @public and parameter[@name='userId']]
```

### ❌ BAD: Too generic
```
//method[Get*]         // Might match GetUser, GetOrder, GetProduct...
```

## Common Pitfalls

1. **Forgetting the type**: `statement[return]` won't work, use `statement[@type=ReturnStatement]`

2. **Wrong axis**: `/method/statement` only finds direct children, use `//method//statement` for all statements

3. **1-based indexing**: First element is `[1]`, not `[0]`

4. **Case sensitivity**: Method names are case-sensitive

## Quick Decision Tree

- **Finding something specific?** → Use full path: `/class[X]/method[Y]`
- **Finding all occurrences?** → Use `//` with type: `//statement[@type=ReturnStatement]`
- **Finding by pattern?** → Use `@contains` or `@matches`
- **Need context?** → Use `/ancestor::method[1]`
- **Multiple criteria?** → Combine with `and`: `[@async and @public]`

## Error Recovery

If a path returns no results:
1. Simplify: Remove criteria until something matches
2. Check spelling: Names are case-sensitive
3. Check structure: Did you assume a structure that doesn't exist?
4. Use wildcards: `method[*User*]` instead of exact names

## Remember

- RoslynPath is about **stable** references that survive edits
- Start simple, add criteria as needed
- When in doubt, use `//` to search broadly
- Names are better than positions
- Multiple criteria are better than single criteria

This guide covers 90% of what you'll need. The full syntax has more features, but these patterns will handle most code navigation tasks.