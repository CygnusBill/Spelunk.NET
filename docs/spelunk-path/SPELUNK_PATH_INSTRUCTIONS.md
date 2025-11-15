# SpelunkPath Instructions for Agents

## Quick Start

SpelunkPath lets you find specific code elements using path expressions, similar to how XPath works for XML.

### Basic Usage

```csharp
// Find all methods
var methods = SpelunkPath.Find(sourceCode, "//method");

// Find a specific method
var getUserMethod = SpelunkPath.Find(sourceCode, "//method[GetUser]");

// Find all return statements in a method
var returns = SpelunkPath.Find(sourceCode, "//method[ProcessOrder]//statement[@type=ReturnStatement]");
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
> **Note**: Searches normalized syntax tree text, so `x==null`, `x == null`, and `x  ==  null` all match.

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

**Before:**
```csharp
public void ProcessOrder(Order order)
{
    Console.WriteLine($"Processing order {order.Id}");
    ValidateOrder(order);
    Console.WriteLine("Order validated");
    SaveOrder(order);
    Console.WriteLine($"Order {order.Id} saved");
}
```

**Step 1 - Find:** `//statement[@contains='Console.WriteLine']`
```
Found 3 statements:
- Line 3: Console.WriteLine($"Processing order {order.Id}");
- Line 5: Console.WriteLine("Order validated");
- Line 7: Console.WriteLine($"Order {order.Id} saved");
```

**Step 2 - Replace each with logger calls**

**After:**
```csharp
public void ProcessOrder(Order order)
{
    _logger.LogInformation($"Processing order {order.Id}");
    ValidateOrder(order);
    _logger.LogInformation("Order validated");
    SaveOrder(order);  
    _logger.LogInformation($"Order {order.Id} saved");
}
```

### Add Null Checks to Public Methods

**Before:**
```csharp
public class CustomerService
{
    public void UpdateCustomer(Customer customer)
    {
        customer.ModifiedDate = DateTime.UtcNow;
        _repository.Update(customer);
    }
    
    private void ValidateCustomer(Customer customer)
    {
        // Private method - no null check needed
    }
}
```

**Step 1 - Find:** `//method[@public]`
```
Found: UpdateCustomer at line 3
```

**Step 2 - Insert at:** `{method-path}/block/statement[1]`

**After:**
```csharp
public void UpdateCustomer(Customer customer)
{
    ArgumentNullException.ThrowIfNull(customer);
    customer.ModifiedDate = DateTime.UtcNow;
    _repository.Update(customer);
}
```

### Find Long Methods

**Input:**
```csharp
public class ReportGenerator
{
    public void GenerateReport()  // 25 statements - too long!
    {
        var data = LoadData();
        ValidateData(data);
        // ... 20 more statements ...
        FormatReport();
        SaveReport();
    }
    
    public void PrintSummary()  // 5 statements - OK
    {
        var summary = GetSummary();
        Format(summary);
        Console.WriteLine(summary);
        LogCompletion();
        return;
    }
}
```

**Find:** `//method[count(.//statement) > 20]`
```
Found 1 long method:
- GenerateReport at line 3
  Statement count: 25
  Path: /class[ReportGenerator]/method[GenerateReport]
  Recommendation: Consider breaking into smaller methods
```

### Find Empty Catch Blocks

**Input:**
```csharp
public void RiskyOperation()
{
    try
    {
        DoSomethingDangerous();
    }
    catch (IOException)
    {
        // Empty - swallowing exception!
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Operation failed");
        throw;
    }
}
```

**Find:** `//catch[block[count(statement)=0]]`
```
Found 1 empty catch block:
- IOException handler at line 7
  Path: /method[RiskyOperation]/statement[1]/catch[1]
  Warning: Exception being swallowed without logging or rethrowing
```

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