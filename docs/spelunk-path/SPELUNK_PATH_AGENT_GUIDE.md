# SpelunkPath Quick Guide for AI Agents - Version 0.1

**Version:** 0.1
**Date:** November 2025
**Status:** Production Ready

## What is SpelunkPath?

SpelunkPath is a way to precisely identify locations in C# code that **stays stable even when the code is edited**. Think of it like a GPS coordinate for code - instead of saying "line 25" (which breaks when lines are added), you say "the return statement in the CalculateTotal method" which always finds the right spot.

**Version 0.1** delivers all core navigation features and predicates. Advanced functions with arguments (like `contains('text')` or `count()`) are coming in v0.2.

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
> **Note**: The `@contains='== null'` comparison is robust - it searches the normalized syntax tree text, not raw source. This means it finds null checks regardless of whitespace: `x==null`, `x == null`, or `x  ==  null` all match.

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

**Input Code:**
```csharp
public class OrderService
{
    public void CreateOrder(Order order)
    {
        ValidateOrder(order);
        SaveOrder(order);
    }
    
    private void ValidateOrder(Order order)
    {
        // Validation logic
    }
}
```

**Step 1 - Find targets:** `//method[@public]`
```
Found: CreateOrder at line 3
Path: /class[OrderService]/method[CreateOrder]
```

**Step 2 - Insert at:** `/block/statement[1]`
```csharp
public void CreateOrder(Order order)
{
    _logger.LogInformation("CreateOrder called");  // <-- Inserted here
    ValidateOrder(order);
    SaveOrder(order);
}
```

### Task: "Replace Console.WriteLine with logger"

**Input Code:**
```csharp
public void ProcessData()
{
    Console.WriteLine("Starting process");
    // Do work
    Console.WriteLine($"Processed {count} items");
}
```

**Find all:** `//statement[@contains='Console.WriteLine']`
```
Found 2 statements:
1. Line 3: Console.WriteLine("Starting process");
   Path: /method[ProcessData]/block/statement[1]
2. Line 5: Console.WriteLine($"Processed {count} items");
   Path: /method[ProcessData]/block/statement[2]
```

**After replacement:**
```csharp
public void ProcessData()
{
    _logger.LogInformation("Starting process");
    // Do work
    _logger.LogInformation($"Processed {count} items");
}
```

### Task: "Find async methods without await"

**Input Code:**
```csharp
public class UserService
{
    // BAD: async without await
    public async Task<User> GetUserAsync(int id)
    {
        return _cache.GetUser(id);  // Synchronous call!
    }
    
    // GOOD: async with await  
    public async Task<List<User>> GetAllUsersAsync()
    {
        return await _repository.GetUsersAsync();
    }
}
```

**Query:** `//method[@async and not(.//expression[@type=AwaitExpression])]`
```
Found 1 problematic method:
- GetUserAsync at line 4
  Path: /class[UserService]/method[GetUserAsync]
  Issue: Async method with no await expressions
```

### Task: "Add null checks to methods with User parameters"

**Input Code:**
```csharp
public class UserManager
{
    public void UpdateUser(User user)
    {
        user.LastModified = DateTime.Now;
        SaveUser(user);
    }
    
    public string GetUserName(User user, bool formal)
    {
        return formal ? user.FullName : user.FirstName;
    }
}
```

**Step 1 - Find methods:** `//method[parameter[@type='User']]`
```
Found 2 methods:
1. UpdateUser at line 3
   Path: /class[UserManager]/method[UpdateUser]
2. GetUserName at line 9  
   Path: /class[UserManager]/method[GetUserName]
```

**Step 2 - Insert at:** `/block/statement[1]`
```csharp
public void UpdateUser(User user)
{
    if (user == null) throw new ArgumentNullException(nameof(user));
    user.LastModified = DateTime.Now;
    SaveUser(user);
}

public string GetUserName(User user, bool formal)  
{
    if (user == null) throw new ArgumentNullException(nameof(user));
    return formal ? user.FullName : user.FirstName;
}
```

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

### 1. Forgetting the type attribute
❌ **Wrong:** `statement[return]`
✅ **Right:** `statement[@type=ReturnStatement]`

**Example:**
```csharp
public int Calculate()
{
    return 42;  // This is a ReturnStatement
}
```

### 2. Wrong axis for nested elements
❌ **Wrong:** `/method/statement` (only direct children)
✅ **Right:** `//method//statement` (all descendants)

**Example:**
```csharp
public void Process()
{
    if (condition)  // This statement is /method/block/statement[1]
    {
        DoWork();   // This is deeper: /method/block/statement[1]/block/statement[1]
    }              // Need // to find both
}
```

### 3. Zero vs One-based indexing  
❌ **Wrong:** `[0]` (like arrays)
✅ **Right:** `[1]` (first element)

**Example:**
```csharp
public void Example()
{
    FirstStatement();   // This is statement[1], not statement[0]
    SecondStatement();  // This is statement[2]
}
```

### 4. Case sensitivity
❌ **Wrong:** `//method[getuser]`
✅ **Right:** `//method[GetUser]`

**Example:**
```csharp
public User GetUser(int id) { }  // Capital G, capital U
public User getuser(int id) { }  // Different method!
```

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

- SpelunkPath is about **stable** references that survive edits
- Start simple, add criteria as needed
- When in doubt, use `//` to search broadly
- Names are better than positions
- Multiple criteria are better than single criteria

This guide covers 90% of what you'll need. The full syntax has more features, but these patterns will handle most code navigation tasks.

## Version 0.1 - What's Available

**✅ Fully Working:**
- All navigation operators (`/`, `//`, `..`)
- All XPath axes (ancestor::, descendant::, sibling::, parent::, etc.)
- Name matching with wildcards (`Get*`, `*Service`)
- Position functions: `position()`, `last()`, `first()`, `[N]`
- Attribute predicates: `@async`, `@public`, `@contains='text'`
- Boolean operators: `and`, `or`, `not`
- Enhanced node types: binary-expression, if-statement, literal, etc.
- Multi-language: C# and VB.NET

**⏳ Coming in v0.2:**
- XPath functions with arguments: `contains('text')`, `substring(@name, 0, 4)`
- `count()` function: `//method[count(statement) > 10]`
- String functions: `string-length()`, `concat()`, `normalize-space()`

**💡 v0.1 Tip:** Use `@contains='text'` instead of `contains('text')` function - it works the same way!