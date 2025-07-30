# RoslynPath Test Package for AI Agents

## Overview

RoslynPath is a query language for finding code elements in C# syntax trees. It provides stable references that survive code edits, unlike fragile line/column positions.

## Quick Example

**The Problem:** Line numbers break when code changes
```csharp
// Original code
public class UserService  // Line 1
{                        // Line 2
    public void Process() // Line 3
    {
        Console.WriteLine("Processing");  // Line 5 - Target this
    }
}

// After adding a comment...
public class UserService  // Line 1
{                        // Line 2  
    // New comment!      // Line 3 <-- Added
    public void Process() // Line 4 <-- Was line 3
    {
        Console.WriteLine("Processing");  // Line 6 <-- Was line 5!
    }
}
```

**The Solution:** RoslynPath stays stable
```
Path: //class[UserService]/method[Process]//statement[@contains='Console.WriteLine']
```
This path finds the Console.WriteLine regardless of line number changes!

## Installation

1. Add the RoslynPath files to your project:
   - `RoslynPathParser.cs`
   - `RoslynPathEvaluator.cs`
   - `RoslynPath.cs`

2. Add NuGet packages:
   ```xml
   <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.8.0" />
   ```

## Basic Usage

```csharp
using McpRoslyn.Server.RoslynPath;

// Find all async methods
var results = RoslynPath.Find(sourceCode, "//method[@async]");

foreach (var result in results)
{
    Console.WriteLine($"Found: {result.Text} at line {result.Location.StartLine}");
}
```

## Test Scenarios

### Scenario 1: Find and Replace Console.WriteLine

**Task**: Replace all Console.WriteLine calls with logger calls

**Input Code:**
```csharp
public class OrderProcessor
{
    public void ProcessOrder(Order order)
    {
        Console.WriteLine($"Processing order {order.Id}");
        
        if (order.Items.Count == 0)
        {
            Console.WriteLine("Warning: Order has no items");
            return;
        }
        
        foreach (var item in order.Items)
        {
            Console.WriteLine($"Processing item {item.Name}");
            ProcessItem(item);
        }
        
        Console.WriteLine("Order processing complete");
    }
}
```

**Step 1: Find all Console.WriteLine statements**
```csharp
var results = RoslynPath.Find(code, "//statement[@contains='Console.WriteLine']");
```

**Results:**
```
Found 4 matches:
1. Line 5: Console.WriteLine($"Processing order {order.Id}");
   Path: /class[OrderProcessor]/method[ProcessOrder]/block/statement[1]
   
2. Line 9: Console.WriteLine("Warning: Order has no items");
   Path: /class[OrderProcessor]/method[ProcessOrder]/block/statement[2]/block/statement[1]
   
3. Line 15: Console.WriteLine($"Processing item {item.Name}");
   Path: /class[OrderProcessor]/method[ProcessOrder]/block/statement[3]/block/statement[1]
   
4. Line 19: Console.WriteLine("Order processing complete");
   Path: /class[OrderProcessor]/method[ProcessOrder]/block/statement[4]
```

**Step 2: For each result, replace with logger**
```csharp
// result.Location gives line/column for traditional editing
// result.Path gives stable reference that survives edits
// result.Text gives the actual statement to transform
```

### Scenario 2: Add Null Checks

**Task**: Add null checks to all public methods with object parameters

```csharp
// Find public methods
var publicMethods = RoslynPath.Find(code, "//method[@public]");

// For each method, find its first statement
var firstStatement = RoslynPath.Find(code, $"{methodPath}/block/statement[1]");

// Insert null check before first statement
```

### Scenario 3: Find Anti-Patterns

**Task**: Find async methods without await

```csharp
var suspiciousMethods = RoslynPath.Find(code, 
    "//method[@async and not(.//expression[@contains='await'])]");
```

### Scenario 4: Complex Refactoring

**Task**: Find all methods that throw ArgumentException and add parameter validation

```csharp
// Find methods with ArgumentException
var methods = RoslynPath.Find(code, 
    "//method[.//statement[@type=ThrowStatement and @contains='ArgumentException']]");
```

## Common Patterns Reference

| Task | RoslynPath |
|------|------------|
| All methods | `//method` |
| Specific method | `//method[ProcessOrder]` |
| Methods with pattern | `//method[Get*]` |
| Async methods | `//method[@async]` |
| Public methods | `//method[@public]` |
| All if statements | `//statement[@type=IfStatement]` |
| Null checks | `//statement[@type=IfStatement and @contains='== null']` |
| | *(Note: Searches normalized syntax, so whitespace doesn't matter)* |
| Return statements | `//statement[@type=ReturnStatement]` |
| First statement | `//method/block/statement[1]` |
| Last statement | `//method/block/statement[last()]` |
| TODO comments | `//comment[@contains='TODO']` |
| Statements with text | `//statement[@contains='logger']` |
| Regex matching | `//statement[@matches='await.*Async']` |

## Test Code Sample

```csharp
namespace TestApp
{
    public class OrderService
    {
        private readonly ILogger _logger;
        
        public async Task<Order> GetOrderAsync(int orderId)
        {
            // TODO: Add caching here
            Console.WriteLine($"Getting order {orderId}");
            
            if (orderId <= 0)
            {
                throw new ArgumentException("Invalid order ID");
            }
            
            var order = await FetchOrderAsync(orderId);
            
            if (order == null)
            {
                Console.WriteLine("Order not found");
                return null;
            }
            
            return order;
        }
        
        public void ProcessOrder(Order order)
        {
            if (order == null) return;
            
            Console.WriteLine($"Processing order {order.Id}");
            // Process logic
        }
    }
}
```

## Test Queries to Try

1. **Find all Console.WriteLine calls**:
   ```
   //statement[@contains='Console.WriteLine']
   ```

2. **Find the TODO comment**:
   ```
   //comment[@contains='TODO']
   ```

3. **Find null checks**:
   ```
   //statement[@type=IfStatement and @contains='== null']
   ```
   > **Note**: Uses normalized syntax tree text - finds `x==null`, `x == null`, and `x  ==  null`.

4. **Find async methods**:
   ```
   //method[@async]
   ```

5. **Find return statements in GetOrderAsync**:
   ```
   //method[GetOrderAsync]//statement[@type=ReturnStatement]
   ```

6. **Find methods that throw exceptions**:
   ```
   //method[.//statement[@type=ThrowStatement]]
   ```

## Expected Benefits

1. **Stability**: Paths like `/class[OrderService]/method[GetOrderAsync]` survive edits
2. **Precision**: Can uniquely identify any syntax node
3. **Flexibility**: Supports wildcards, patterns, and complex predicates
4. **Composability**: Build complex queries from simple parts

## Integration Points

### With MCP Tools
Instead of:
```json
{
  "location": {
    "file": "OrderService.cs",
    "line": 25,
    "column": 9
  }
}
```

Use:
```json
{
  "roslynPath": "//class[OrderService]/method[GetOrderAsync]/block/statement[@contains='Console'][1]",
  "file": "OrderService.cs"
}
```

## Success Metrics

1. Can find specific statements without line numbers
2. Paths remain valid after adding/removing code above target
3. Can express complex patterns concisely
4. Results include both location and stable path

## Troubleshooting

**No results**: Simplify the path, check spelling
**Syntax error**: Check brackets, quotes, operators
**Wrong results**: Make the path more specific

## Questions for Testing Agents

1. How intuitive is the syntax?
2. What patterns are hard to express?
3. How does it compare to line/column navigation?
4. What additional features would help?

This package provides everything needed to test RoslynPath with different AI agents and gather feedback on its usability.