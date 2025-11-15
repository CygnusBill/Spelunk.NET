# Powerful SpelunkPath Analysis Examples

## 1. Security Analysis

### Find SQL Injection Vulnerabilities

**Query:**
```xpath
//expression[@type=InvocationExpression and 
    @contains='ExecuteSql' and 
    .//expression[@type=InterpolatedStringExpression or @type=BinaryExpression]]
```

**What it finds:** SQL commands built with string concatenation or interpolation instead of parameters.

**Example Input:**
```csharp
public class UserRepository
{
    public User GetUser(string username)
    {
        // VULNERABLE: String interpolation in SQL
        var sql = $"SELECT * FROM Users WHERE Username = '{username}'";
        return db.ExecuteSql<User>(sql);
    }
    
    public User GetUserSafe(string username)
    {
        // SAFE: Parameterized query
        var sql = "SELECT * FROM Users WHERE Username = @username";
        return db.ExecuteSql<User>(sql, new { username });
    }
}
```

**Output:**
```
Found 1 potential SQL injection:
- Line 6: db.ExecuteSql<User>(sql) where sql uses string interpolation
  Path: /class[UserRepository]/method[GetUser]/statement[2]
  Risk: User input 'username' is directly interpolated into SQL
```

### Find Hardcoded Secrets

**Query:**
```xpath
//field[@contains='password' or @contains='key' or @contains='secret'] |
//variable[@contains='password' or @contains='key' or @contains='secret']
    [.//expression[@type=StringLiteralExpression]]
```

**What it finds:** Hardcoded passwords, API keys, and secrets.

**Example Input:**
```csharp
public class ApiClient
{
    // BAD: Hardcoded secret
    private string apiKey = "sk-1234567890abcdef";
    private const string DatabasePassword = "P@ssw0rd123";
    
    public void Connect()
    {
        // BAD: Hardcoded in local variable
        var connectionString = "Server=prod;Password=admin123";
        
        // GOOD: From configuration
        var safeKey = Configuration["ApiKey"];
    }
}
```

**Output:**
```
Found 3 hardcoded secrets:
1. Field 'apiKey' at line 4
   Value: "sk-1234567890abcdef"
   Path: /class[ApiClient]/field[apiKey]
   
2. Field 'DatabasePassword' at line 5
   Value: "P@ssw0rd123"
   Path: /class[ApiClient]/field[DatabasePassword]
   
3. Variable 'connectionString' at line 10
   Contains: "Password=admin123"
   Path: /class[ApiClient]/method[Connect]/statement[1]
```

## 2. Performance Analysis

### Find N+1 Query Patterns

**Query:**
```xpath
//foreach[
    .//expression[@type=MemberAccessExpression and 
        (@contains='.First' or @contains='.Single' or @contains='.ToList')]
]
```

**What it finds:** Loops that might be making database calls for each iteration.

**Example Input:**
```csharp
public class OrderService
{
    public void ProcessOrders(List<Order> orders)
    {
        // BAD: N+1 query pattern
        foreach (var order in orders)
        {
            // This hits the database for each order!
            var customer = _customers.First(c => c.Id == order.CustomerId);
            var items = _orderItems.Where(i => i.OrderId == order.Id).ToList();
            ProcessOrderWithDetails(order, customer, items);
        }
    }
    
    public void ProcessOrdersEfficient(List<Order> orders)
    {
        // GOOD: Load all data upfront
        var customerIds = orders.Select(o => o.CustomerId).Distinct();
        var customers = _customers.Where(c => customerIds.Contains(c.Id)).ToList();
        // Process without additional queries
    }
}
```

**Output:**
```
Found N+1 query pattern:
- foreach loop at line 6
  Path: /class[OrderService]/method[ProcessOrders]/statement[1]
  Issues:
    - Line 9: _customers.First() called inside loop
    - Line 10: _orderItems...ToList() called inside loop
  Performance impact: For 100 orders, this makes 201 database queries instead of 3
```

### Find Synchronous I/O in Async Context

**Query:**
```xpath
//method[@async]//expression[
    @type=InvocationExpression and 
    (@contains='.Read(' or @contains='.Write(' or @contains='.Get(') and
    not(@contains='Async')
]
```

**What it finds:** Blocking I/O operations in async methods.

**Example Input:**
```csharp
public class FileService
{
    public async Task<string> ProcessFileAsync(string path)
    {
        // BAD: Synchronous I/O in async method
        var content = File.ReadAllText(path);  // Blocks thread!
        
        // Some async operation
        await Task.Delay(100);
        
        // BAD: Another blocking call
        File.WriteAllText(path + ".bak", content);
        
        return content;
    }
    
    public async Task<string> ProcessFileCorrectlyAsync(string path)
    {
        // GOOD: Async all the way
        var content = await File.ReadAllTextAsync(path);
        await File.WriteAllTextAsync(path + ".bak", content);
        return content;
    }
}
```

**Output:**
```
Found 2 synchronous I/O operations in async context:
1. Method: ProcessFileAsync (async) at line 3
   - Line 6: File.ReadAllText(path)
     Should use: File.ReadAllTextAsync(path)
   - Line 11: File.WriteAllText(path + ".bak", content)
     Should use: File.WriteAllTextAsync(...)
   Path: /class[FileService]/method[ProcessFileAsync]
```

## 3. Code Quality Patterns

### Find God Classes (Too Many Responsibilities)
```xpath
//class[
    count(method[@public]) > 20 or
    count(property[@public]) > 15 or
    count(.//field[@private]) > 30
]
```
Identifies classes that might be doing too much.

### Find Methods That Should Be Async
```xpath
//method[not(@async) and 
    .//expression[@type=InvocationExpression and @contains='Async('] and
    not(@returns='void')
]
```
Methods calling async operations but not properly awaiting them.

### Find Dead Code (Unreachable After Return)
```xpath
//statement[@type=ReturnStatement]/following-sibling::statement
```
Statements that appear after a return statement in the same block.

## 4. Architectural Violations

### Find Direct Database Access in Controllers
```xpath
//class[*Controller]//expression[
    @contains='DbContext' or 
    @contains='SqlConnection' or
    @contains='ExecuteSql'
]
```
Controllers that bypass the repository pattern.

### Find Business Logic in Data Layer
```xpath
//class[*Repository or *Context]//method[
    count(.//statement[@type=IfStatement]) > 3 or
    .//statement[@type=SwitchStatement]
]
```
Repository methods with too much logic.

## 5. Exception Handling Anti-Patterns

### Find Catch Blocks That Hide Exceptions
```xpath
//catch[
    not(.//statement[@type=ThrowStatement]) and
    not(.//expression[@contains='Log']) and
    count(.//statement) > 0
]
```
Catch blocks that neither rethrow nor log the exception.

### Find Generic Exception Catches
```xpath
//catch[@type='Exception' or not(@type)]
```
Catches that are too broad and might hide specific errors.

## 6. Modern C# Feature Adoption

### Find Places to Use Pattern Matching
```xpath
//statement[@type=IfStatement and 
    @contains='!= null' and 
    following-sibling::statement[1][@type=IfStatement and @contains='.GetType()']
]
```
Traditional type checking that could use pattern matching.

### Find Places to Use Null-Conditional Operator
```xpath
//statement[@type=IfStatement and 
    @contains='!= null' and 
    count(.//statement) = 1 and
    .//statement[@type=ReturnStatement or @type=ExpressionStatement]
]
```
Simple null checks that could use ?. operator.

## 7. Test Quality Analysis

### Find Tests Without Assertions
```xpath
//method[@contains='Test' or @contains='Should']
    [not(.//expression[@contains='Assert' or @contains='Verify' or @contains='Should'])]
```
Test methods that don't actually test anything.

### Find Tests With Multiple Assertions (Violating Single Assert Principle)
```xpath
//method[@contains='Test']
    [count(.//expression[@contains='Assert']) > 3]
```
Tests that might be testing too many things at once.

## 8. Resource Management

### Find IDisposable Not in Using Statement
```xpath
//variable[
    @type='*:IDisposable' and 
    not(ancestor::using) and
    not(ancestor::method//expression[@contains='.Dispose()'])
]
```
Resources that might not be properly disposed.

## Real-World Impact

These queries can find:
- **Security vulnerabilities** before they reach production
- **Performance bottlenecks** that are hard to spot in code review
- **Architectural violations** that accumulate over time
- **Modernization opportunities** for cleaner, more maintainable code

The power comes from combining:
1. **Structural awareness** (understanding C# syntax)
2. **Pattern matching** (finding code shapes)
3. **Context navigation** (understanding relationships)
4. **Boolean logic** (complex conditions)

This makes SpelunkPath not just a navigation tool, but a powerful static analysis engine that can enforce coding standards, find bugs, and guide refactoring efforts.