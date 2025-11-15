# Agent Implementation Example: Extract Method Refactoring

This document demonstrates how a refactoring that would traditionally be a complex monolithic operation can be implemented as an agent workflow using primitive MCP tools.

## Traditional Approach (Monolithic)

In a traditional refactoring tool, "Extract Method" would be a single complex operation:

```csharp
// Hypothetical monolithic tool call
ExtractMethod(
    file: "Service.cs",
    startLine: 25,
    endLine: 35,
    methodName: "ValidateInput",
    visibility: "private"
)
```

This approach requires the MCP server to:
- Parse the selection
- Analyze data flow
- Generate method signature
- Handle all edge cases
- Update references
- All in one atomic operation

## Agent Workflow Approach

Instead, an agent orchestrates primitive tools to achieve the same result intelligently:

### Step 1: Agent Receives Request

**User**: "Extract lines 25-35 into a method called ValidateInput"

**Agent Internal Planning**:
```yaml
Task: Extract Method
Steps:
  1. Analyze selected code region
  2. Determine required parameters
  3. Determine return values
  4. Generate method signature
  5. Create new method
  6. Replace original code with method call
  7. Verify compilation
```

### Step 2: Agent Implementation

```python
# AGENT: Extract Method Refactoring Agent
# This is how the agent would orchestrate the primitive tools

async def extract_method(file_path, start_line, end_line, method_name):
    """
    Extract selected lines into a new method using primitive tools.
    """
    
    # Step 1: Analyze the code region with data flow analysis
    print("Analyzing selected code region...")
    
    data_flow_result = await call_tool("spelunk-get-data-flow", {
        "file": file_path,
        "startLine": start_line,
        "startColumn": 1,
        "endLine": end_line,
        "endColumn": 100  # Use large column to include full line
    })
    
    # Extract what flows in and out
    parameters_needed = data_flow_result["DataFlow"]["DataFlowsIn"]
    returns_needed = data_flow_result["DataFlow"]["DataFlowsOut"]
    variables_written = data_flow_result["DataFlow"]["WrittenInside"]
    
    print(f"Found {len(parameters_needed)} parameters needed")
    print(f"Found {len(returns_needed)} values to return")
    
    # Step 2: Get the actual code to extract
    statement_result = await call_tool("spelunk-find-statements", {
        "filePath": file_path,
        "pattern": "//statement",  # Get all statements
        "patternType": "roslynpath"
    })
    
    # Filter to our line range
    statements_to_extract = [
        s for s in statement_result["Statements"]
        if start_line <= s["Line"] <= end_line
    ]
    
    # Step 3: Get context about the containing class
    context_result = await call_tool("spelunk-get-statement-context", {
        "file": file_path,
        "line": start_line,
        "column": 1
    })
    
    containing_class = context_result["Context"]["EnclosingType"]
    
    # Step 4: Generate the method signature
    print("Generating method signature...")
    
    # Determine return type
    if len(returns_needed) == 0:
        return_type = "void"
    elif len(returns_needed) == 1:
        # Get the type of the single return value
        return_type = get_variable_type(returns_needed[0], context_result)
    else:
        # Multiple returns - use tuple
        types = [get_variable_type(v, context_result) for v in returns_needed]
        return_type = f"({', '.join(types)})"
    
    # Build parameter list
    parameters = []
    for param in parameters_needed:
        param_type = get_variable_type(param, context_result)
        parameters.append(f"{param_type} {param}")
    
    parameter_list = ", ".join(parameters)
    
    # Step 5: Get the extracted code
    # Read the actual lines
    read_result = await call_tool("Read", {
        "file_path": file_path,
        "offset": start_line,
        "limit": end_line - start_line + 1
    })
    
    extracted_code = extract_code_from_read(read_result)
    
    # Step 6: Build the new method
    print(f"Creating method: {method_name}")
    
    # Handle returns if needed
    if return_type != "void":
        if len(returns_needed) == 1:
            extracted_code += f"\n    return {returns_needed[0]};"
        else:
            returns = ", ".join(returns_needed)
            extracted_code += f"\n    return ({returns});"
    
    new_method = f"""
    private {return_type} {method_name}({parameter_list})
    {{
{extracted_code}
    }}"""
    
    # Step 7: Find where to insert the method
    # Get the last method in the class to insert after
    class_methods = await call_tool("spelunk-find-method", {
        "classPattern": containing_class,
        "methodPattern": "*"
    })
    
    if class_methods["Matches"]:
        last_method = class_methods["Matches"][-1]
        insert_location = {
            "filePath": file_path,
            "line": last_method["EndLine"],
            "column": 1
        }
    else:
        # No methods, find class declaration
        class_result = await call_tool("spelunk-find-class", {
            "pattern": containing_class
        })
        insert_location = {
            "filePath": file_path,
            "line": class_result["Matches"][0]["Line"] + 1,
            "column": 1
        }
    
    # Step 8: Insert the new method
    print("Inserting new method into class...")
    
    await call_tool("spelunk-insert-statement", {
        "position": "after",
        "filePath": insert_location["filePath"],
        "line": insert_location["line"],
        "column": insert_location["column"],
        "statement": new_method
    })
    
    # Step 9: Build the method call to replace original code
    print("Replacing original code with method call...")
    
    # Build the call
    arguments = ", ".join(parameters_needed)
    method_call = f"{method_name}({arguments})"
    
    # Handle return value assignment if needed
    if returns_needed:
        if len(returns_needed) == 1:
            replacement = f"var {returns_needed[0]} = {method_call};"
        else:
            vars_list = ", ".join(returns_needed)
            replacement = f"var ({vars_list}) = {method_call};"
    else:
        replacement = f"{method_call};"
    
    # Step 10: Replace the original statements
    # We need to replace multiple statements with one
    # First, mark the statements to track them
    for i, stmt in enumerate(statements_to_extract):
        await call_tool("spelunk-mark-statement", {
            "filePath": file_path,
            "line": stmt["Line"],
            "column": stmt["Column"],
            "label": f"extract-{i}"
        })
    
    # Replace first statement with method call
    await call_tool("spelunk-replace-statement", {
        "filePath": file_path,
        "line": statements_to_extract[0]["Line"],
        "column": statements_to_extract[0]["Column"],
        "newStatement": replacement
    })
    
    # Remove the rest
    for i in range(1, len(statements_to_extract)):
        await call_tool("spelunk-remove-statement", {
            "filePath": file_path,
            "line": statements_to_extract[i]["Line"],
            "column": statements_to_extract[i]["Column"]
        })
    
    # Step 11: Verify compilation
    print("Verifying code compiles...")
    
    workspace_status = await call_tool("spelunk-workspace-status", {})
    
    if workspace_status["HasErrors"]:
        print("⚠️ Compilation errors detected:")
        for error in workspace_status["Errors"]:
            print(f"  - {error}")
        print("\nThe extraction completed but may need manual adjustments.")
    else:
        print("✅ Method extracted successfully!")
    
    # Step 12: Report results
    return {
        "success": True,
        "method_name": method_name,
        "parameters": parameters,
        "return_type": return_type,
        "lines_extracted": end_line - start_line + 1,
        "compilation_status": "success" if not workspace_status["HasErrors"] else "has_errors"
    }

# Helper functions the agent would use
def get_variable_type(variable_name, context):
    """Determine the type of a variable from context."""
    # Look through symbols in context
    for symbol in context["Context"]["Symbols"]:
        if symbol["Name"] == variable_name:
            return symbol["Type"]
    
    # Default to var for type inference
    return "var"

def extract_code_from_read(read_result):
    """Extract clean code from Read tool output."""
    lines = read_result.split('\n')
    # Remove line numbers and clean up
    clean_lines = []
    for line in lines:
        if '\t' in line:
            # Split on tab to get code after line number
            code = line.split('\t', 1)[1] if len(line.split('\t', 1)) > 1 else ""
            clean_lines.append(code)
    return '\n'.join(clean_lines)
```

## Example Execution

### Given this code:

```csharp
public class OrderService
{
    public void ProcessOrder(Order order)
    {
        // Lines 25-35 to extract
        if (order == null)
            throw new ArgumentNullException(nameof(order));
        
        if (order.Items == null || order.Items.Count == 0)
            throw new InvalidOperationException("Order must have items");
        
        if (order.TotalAmount <= 0)
            throw new InvalidOperationException("Order amount must be positive");
        
        if (string.IsNullOrEmpty(order.CustomerEmail))
            throw new InvalidOperationException("Customer email is required");
        // End of extraction
        
        // Process the order...
        SendConfirmationEmail(order);
        UpdateInventory(order);
    }
}
```

### Agent Execution:

```
User: Extract lines 25-35 into a method called ValidateOrder

Agent: I'll extract those lines into a new ValidateOrder method.

Analyzing selected code region...
Found 1 parameter needed: order
Found 0 values to return

Generating method signature...
Creating method: ValidateOrder

Inserting new method into class...
✅ Added method: private void ValidateOrder(Order order)

Replacing original code with method call...
✅ Replaced validation logic with: ValidateOrder(order);

Verifying code compiles...
✅ Method extracted successfully!

The code now looks like:

public class OrderService
{
    public void ProcessOrder(Order order)
    {
        ValidateOrder(order);
        
        // Process the order...
        SendConfirmationEmail(order);
        UpdateInventory(order);
    }
    
    private void ValidateOrder(Order order)
    {
        if (order == null)
            throw new ArgumentNullException(nameof(order));
        
        if (order.Items == null || order.Items.Count == 0)
            throw new InvalidOperationException("Order must have items");
        
        if (order.TotalAmount <= 0)
            throw new InvalidOperationException("Order amount must be positive");
        
        if (string.IsNullOrEmpty(order.CustomerEmail))
            throw new InvalidOperationException("Customer email is required");
    }
}
```

## Benefits of the Agent Approach

### 1. **Flexibility**
The agent can adapt based on what it discovers. If the extraction is complex, it can ask for user input or suggest alternatives.

### 2. **Transparency**
Each step is visible and explainable. Users can see exactly what the agent is doing.

### 3. **Composability**
The same primitive tools used here can be combined differently for other refactorings.

### 4. **Error Recovery**
If a step fails, the agent can try alternative approaches or provide helpful error messages.

### 5. **Incremental Progress**
The refactoring can be paused, reviewed, and resumed at any step.

### 6. **Learning and Improvement**
Agents can learn from successful patterns and improve over time.

## Primitive Tools Used

This example demonstrates how these primitive tools combine:

1. **spelunk-get-data-flow** - Understand variable dependencies
2. **spelunk-find-statements** - Locate code to extract
3. **spelunk-get-statement-context** - Get type information
4. **spelunk-find-method** - Find insertion point
5. **spelunk-insert-statement** - Add new method
6. **spelunk-mark-statement** - Track statements
7. **spelunk-replace-statement** - Replace with method call
8. **spelunk-remove-statement** - Clean up extra lines
9. **spelunk-workspace-status** - Verify compilation

## Comparison

| Aspect | Monolithic Tool | Agent Workflow |
|--------|----------------|----------------|
| **Flexibility** | Fixed algorithm | Adaptive approach |
| **Transparency** | Black box | Step-by-step visible |
| **Error Handling** | All or nothing | Graceful degradation |
| **User Control** | Limited | Full control at each step |
| **Extensibility** | Requires code changes | New combinations emerge |
| **Debugging** | Difficult | Each step observable |

## Conclusion

This example shows that complex refactorings don't need complex tools. They need intelligent orchestration of simple, reliable primitives. The agent approach provides:

- **Better user experience** through transparency
- **More reliable outcomes** through adaptability  
- **Easier maintenance** through simplicity
- **Natural evolution** through emergent patterns

The future of refactoring is intelligent agents orchestrating simple tools, not monolithic operations trying to handle every edge case.