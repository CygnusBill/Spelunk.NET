# MCP Dotnet Refactoring Capabilities

## Overview

The MCP Dotnet toolset provides comprehensive refactoring capabilities through a combination of specialized tools. These refactorings range from simple renames to complex structural transformations, all maintaining semantic correctness through Roslyn's analysis.

## Core Refactoring Tools

### 1. Symbol Renaming

#### `spelunk-rename-symbol`
Safely renames any symbol (type, method, property, field) across the entire codebase with all references updated.

**Capabilities:**
- Rename classes, interfaces, structs, enums
- Rename methods with all call sites updated
- Rename properties and fields
- Rename parameters within methods
- Updates all references including:
  - Direct usage
  - Interface implementations
  - Override declarations
  - XML documentation comments

**Example:**
```json
{
  "oldName": "GetUser",
  "newName": "GetUserById",
  "symbolType": "method",
  "containerName": "UserService"
}
```

### 2. Structural Code Modifications

#### `spelunk-edit-code`
Performs complex structural edits using Roslyn's syntax transformation capabilities.

**Supported Operations:**

##### `add-method`
Add a complete method to a class:
```json
{
  "operation": "add-method",
  "className": "UserService",
  "code": "public async Task<bool> ValidateUser(User user)\n{\n    return user != null && user.IsActive;\n}"
}
```

##### `add-property`
Add properties to classes:
```json
{
  "operation": "add-property",
  "className": "User",
  "code": "public DateTime LastModified { get; set; }"
}
```

##### `make-async`
Convert synchronous methods to async:
- Changes return type from `T` to `Task<T>` or `void` to `Task`
- Updates method signature with `async` keyword
- Identifies and updates async calls within the method

##### `add-parameter`
Add parameters to existing methods:
```json
{
  "operation": "add-parameter",
  "methodName": "ProcessUser",
  "parameters": {
    "parameterName": "cancellationToken",
    "parameterType": "CancellationToken",
    "defaultValue": "default"
  }
}
```

##### `wrap-try-catch`
Add exception handling to method bodies:
```json
{
  "operation": "wrap-try-catch",
  "methodName": "SaveData",
  "parameters": {
    "catchType": "Exception",
    "logError": true
  }
}
```

### 3. Pattern-Based Transformations

#### `spelunk-fix-pattern`
Apply semantic-aware transformations across the codebase using pattern matching.

**Transformation Types:**

##### Code Modernization
- **`convert-to-async`**: Convert synchronous I/O to async
  - `File.ReadAllText` → `File.ReadAllTextAsync`
  - `stream.Read` → `await stream.ReadAsync`
  - Adds necessary `await` keywords
  - Updates method signatures

- **`convert-to-interpolation`**: Modernize string formatting
  - `string.Format("{0} items", count)` → `$"{count} items"`
  - `"Hello " + name + "!"` → `$"Hello {name}!"`

- **`add-await`**: Fix missing await keywords
  - Detects async methods called without await
  - Adds `await` keyword appropriately
  - Updates containing method to async if needed

##### Null Safety
- **`add-null-check`**: Add defensive null checks
  - Adds `ArgumentNullException.ThrowIfNull(param)` at method start
  - Inserts null checks before dereferencing
  - Uses modern C# null-checking patterns

- **`simplify-conditional`**: Use null-conditional operators
  - `if (x != null) x.Method()` → `x?.Method()`
  - `x != null ? x.Property : null` → `x?.Property`

##### Code Quality
- **`extract-variable`**: Extract complex expressions
  - Identifies complex expressions
  - Creates meaningful variable names
  - Replaces expression with variable reference

- **`parameterize-query`**: Secure SQL queries
  - Convert string concatenation to parameterized queries
  - Prevent SQL injection vulnerabilities

### 4. Statement-Level Refactoring

#### `spelunk-replace-statement`
Replace specific statements while preserving context and formatting:
```json
{
  "filePath": "/path/to/file.cs",
  "line": 42,
  "column": 5,
  "newStatement": "logger.LogInformation(message);"
}
```

#### `spelunk-insert-statement`
Insert new statements with proper context:
```json
{
  "position": "before",
  "statement": "ArgumentNullException.ThrowIfNull(input);",
  "filePath": "/path/to/file.cs",
  "line": 10,
  "column": 5
}
```

#### `spelunk-remove-statement`
Clean removal of statements:
```json
{
  "filePath": "/path/to/file.cs",
  "line": 15,
  "column": 5,
  "preserveComments": true
}
```

## Advanced Refactoring Patterns

### 1. Multi-Step Refactoring with Markers

The ephemeral marker system enables complex multi-step refactorings:

```python
# Step 1: Find all Console.WriteLine statements
statements = find_statements("Console.WriteLine")

# Step 2: Mark each for tracking
for stmt in statements:
    mark_statement(stmt.location, label=f"console-{stmt.id}")

# Step 3: Transform each marked statement
for marker in find_marked_statements():
    replace_statement(
        marker.location,
        "logger.LogInformation(message);"
    )

# Step 4: Clean up markers
clear_markers()
```

### 2. Safe Large-Scale Refactoring

Combine discovery and modification tools for safe refactoring:

```python
# Step 1: Impact analysis
references = find_references("OldMethodName")
callers = find_method_callers("OldMethodName")

# Step 2: Preview changes
rename_symbol("OldMethodName", "NewMethodName", preview=True)

# Step 3: Apply if safe
if len(references) < 100:  # Threshold check
    rename_symbol("OldMethodName", "NewMethodName", preview=False)
```

### 3. Inheritance-Aware Refactoring

Handle inheritance hierarchies correctly:

```python
# Find all implementations to update
implementations = find_implementations("IRepository")
overrides = find_overrides("SaveChanges", "BaseRepository")

# Update each implementation
for impl in implementations:
    add_method(impl.class, "async Task SaveChangesAsync()")
```

### 4. Data Flow-Driven Refactoring

Use data flow analysis to guide refactoring:

```python
# Analyze variable usage
data_flow = get_data_flow(region)

# Extract method with correct parameters
params = data_flow["DataFlowsIn"]
returns = data_flow["DataFlowsOut"]

# Generate method signature
create_extracted_method(params, returns)
```

## Refactoring Capabilities by Category

### Basic Refactorings ✅
- **Rename** - All symbols with reference updates
- **Extract Variable** - Complex expressions to variables
- **Inline Variable** - Via statement replacement
- **Add Parameter** - To existing methods
- **Remove Parameter** - Via method signature editing

### Method Refactorings ✅
- **Extract Method** - Via data flow analysis and code generation
- **Inline Method** - Via statement replacement
- **Make Method Async** - Complete async transformation
- **Add Method** - New method generation
- **Change Method Signature** - Via edit operations

### Class Refactorings ✅
- **Add Property** - Generate properties
- **Add Method** - Generate methods
- **Implement Interface** - Via pattern detection and generation
- **Extract Interface** - Via member analysis

### Code Quality Refactorings ✅
- **Add Null Checks** - Defensive programming
- **Convert to Modern Patterns** - String interpolation, async/await
- **Simplify Conditionals** - Null-conditional operators
- **Add Exception Handling** - Try-catch wrapping
- **Remove Dead Code** - Via statement removal

### Cross-Cutting Refactorings ✅
- **Update Logging** - Replace all Console.WriteLine with logger
- **Add Validation** - Insert validation at method starts
- **Modernize Async** - Convert entire codebase to async patterns
- **Security Fixes** - Parameterize SQL queries

## Limitations and Workarounds

### Current Limitations

1. **Multi-Statement Replacement**: `spelunk-replace-statement` only uses the first statement when given multiple
   - **Workaround**: Use multiple replace operations or insert + remove

2. **No Direct Extract Method**: Not a single-operation refactoring
   - **Workaround**: Use data flow analysis + add-method + replace-statement

3. **No Move Class/Method**: Not directly supported
   - **Workaround**: Copy + delete operations

4. **Limited Change Signature**: Only add parameter supported
   - **Workaround**: Create new method + update callers + remove old

### Best Practices

1. **Always Preview First**: Use preview mode for large changes
2. **Use Markers for Multi-Step**: Track statements through transformations
3. **Analyze Impact**: Check references before renaming
4. **Preserve Semantics**: Verify with compilation after changes
5. **Incremental Changes**: Small, verified steps over large transformations

## Examples of Complex Refactorings

### Example 1: Convert Repository to Async

```python
# Find all repository methods
methods = find_method("*", classPattern="*Repository")

# Make each async
for method in methods:
    # Make the method async
    edit_code("make-async", method)
    
    # Find all calls to this method
    callers = find_method_callers(method.name)
    
    # Add await to each call
    for caller in callers:
        fix_pattern(f"{method.name}(", "add-await")
```

### Example 2: Add Logging to All Public Methods

```python
# Find all public methods
methods = find_method("*")

# Add logging to each
for method in methods:
    if "public" in method.modifiers:
        insert_statement(
            position="after",
            statement=f'logger.LogDebug("Entering {method.name}");',
            location=method.body_start
        )
```

### Example 3: Implement IDisposable Pattern

```python
# Add IDisposable to class
class_name = "ResourceManager"
edit_code("add-interface", class_name, "IDisposable")

# Add Dispose method
edit_code("add-method", class_name, """
public void Dispose()
{
    Dispose(true);
    GC.SuppressFinalize(this);
}
""")

# Add protected Dispose
edit_code("add-method", class_name, """
protected virtual void Dispose(bool disposing)
{
    if (disposing)
    {
        // Dispose managed resources
    }
}
""")

# Add finalizer
edit_code("add-method", class_name, """
~ResourceManager()
{
    Dispose(false);
}
""")
```

## Summary

The MCP Dotnet toolset provides comprehensive refactoring capabilities through:

1. **Safe Symbol Renaming** - With all references updated
2. **Structural Modifications** - Add methods, properties, parameters
3. **Pattern-Based Transformations** - Modernize code patterns
4. **Statement-Level Operations** - Precise code modifications
5. **Marker System** - Track changes through multi-step refactorings

These tools combine to enable virtually any refactoring scenario, from simple renames to complex architectural transformations, all while maintaining semantic correctness through Roslyn's analysis capabilities.