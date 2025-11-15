# Data Flow Analysis in McpDotnet

## Overview

The `spelunk-get-data-flow` tool provides comprehensive data flow analysis using Roslyn's `AnalyzeDataFlow` API. This feature is production-ready and highly reliable, providing accurate information about variable usage, assignments, and flow through code regions.

## What It Does

Data flow analysis tracks how data moves through your code:

1. **Variable Flow Tracking** - Which variables flow into and out of regions
2. **Read/Write Analysis** - Which variables are read or written
3. **Definite Assignment** - Which variables are always assigned
4. **Capture Detection** - Which variables are captured by lambdas/closures
5. **Unsafe Analysis** - Which variables have their address taken
6. **Cross-boundary Analysis** - How variables interact with surrounding code

## Data Structure Returned

```json
{
  "DataFlow": {
    "DataFlowsIn": ["this", "input", "x"],      // Variables that flow into the region
    "DataFlowsOut": ["result", "modified"],      // Variables that flow out
    "ReadInside": ["x", "y", "field1"],         // Variables read within region
    "WrittenInside": ["result", "temp"],        // Variables written within region
    "AlwaysAssigned": ["result"],               // Variables always assigned before exit
    "ReadOutside": ["result"],                  // Variables read outside but declared inside
    "WrittenOutside": ["this", "input"],        // Variables written outside but used inside
    "Captured": ["outer"],                      // Variables captured by lambdas
    "CapturedInside": ["local"],                // Variables captured inside the region
    "UnsafeAddressTaken": ["buffer"]           // Variables with address taken (unsafe)
  },
  "VariableFlows": [                           // Optional detailed variable information
    {
      "Name": "result",
      "Type": "int",
      "FirstRead": {"Line": 10, "Column": 15},
      "LastWrite": {"Line": 12, "Column": 9}
    }
  ]
}
```

## Capabilities

### ✅ What Works Well

1. **Partial Regions** - Can analyze incomplete code fragments
2. **Complex Flow** - Handles loops, conditionals, exceptions
3. **Parameter Analysis** - Correctly tracks ref/out parameters
4. **Closure Analysis** - Detects captured variables in lambdas
5. **Field Access** - Tracks instance and static field usage
6. **Unsafe Code** - Detects pointer operations and address-taking

### 🎯 Key Use Cases

1. **Extract Method Refactoring**
   - Identify which variables need to be parameters
   - Determine what needs to be returned
   - Find captured variables that complicate extraction

2. **Variable Usage Analysis**
   - Find unused variables (written but never read)
   - Detect uninitialized variables (read but never written)
   - Identify write-only variables

3. **Definite Assignment Analysis**
   - Ensure variables are assigned before use
   - Verify all code paths assign required values
   - Check ref/out parameter compliance

4. **Side Effect Detection**
   - Identify which fields are modified
   - Track which parameters are changed
   - Find hidden dependencies

5. **Safety Analysis**
   - Find unsafe pointer usage
   - Detect variables with address taken
   - Identify potential memory safety issues

## Examples

### Basic Variable Flow

```csharp
// Region to analyze
int x = 10;
int y = x * 2;
return y;
```

Result:
```json
{
  "DataFlow": {
    "DataFlowsOut": ["y"],
    "ReadInside": ["x"],
    "WrittenInside": ["x", "y"],
    "AlwaysAssigned": ["x", "y"]
  }
}
```

### Captured Variables in Lambda

```csharp
// Region to analyze
int outer = 10;
Action lambda = () => Console.WriteLine(outer);
lambda();
```

Result:
```json
{
  "DataFlow": {
    "WrittenInside": ["outer", "lambda"],
    "Captured": ["outer"],
    "AlwaysAssigned": ["outer", "lambda"]
  }
}
```

### Ref and Out Parameters

```csharp
// Method signature: void Process(ref int x, out int y)
// Region to analyze
x = x * 2;
y = 100;
```

Result:
```json
{
  "DataFlow": {
    "DataFlowsIn": ["x"],
    "DataFlowsOut": ["x", "y"],
    "ReadInside": ["x"],
    "WrittenInside": ["x", "y"],
    "AlwaysAssigned": ["x", "y"]
  }
}
```

### Unsafe Pointer Operations

```csharp
// Region to analyze
int value = 42;
int* ptr = &value;
*ptr = 100;
```

Result:
```json
{
  "DataFlow": {
    "WrittenInside": ["value", "ptr"],
    "UnsafeAddressTaken": ["value"],
    "AlwaysAssigned": ["value", "ptr"]
  }
}
```

## Comparison with Control Flow

| Aspect | Data Flow | Control Flow |
|--------|-----------|--------------|
| **Focus** | Variable usage and assignments | Execution paths and branches |
| **Robustness** | Works on partial regions | Requires complete statements |
| **Roslyn API** | Always uses AnalyzeDataFlow | Uses AnalyzeControlFlow when possible |
| **Production Ready** | ✅ Yes | ⚠️ Limited (now with clear errors) |
| **Use Cases** | Refactoring, variable analysis | Path analysis, dead code detection |

## Limitations

1. **No Interprocedural Analysis** - Doesn't follow method calls
2. **No Alias Analysis** - Can't track through complex references
3. **Local Scope** - Only analyzes within the specified region
4. **No Type Flow** - Doesn't track type changes or casts

## Best Practices

1. **Start with Complete Statements** - While partial regions work, complete statements give best results
2. **Use for Refactoring** - Excellent for extract method and variable analysis
3. **Combine with Other Tools** - Use with `spelunk-find-statements` for comprehensive analysis
4. **Check AlwaysAssigned** - Useful for ensuring variables are initialized

## API Usage

```bash
# Basic usage
dotnet-get-data-flow \
  --file "/path/to/file.cs" \
  --startLine 10 --startColumn 5 \
  --endLine 20 --endColumn 10

# Without control flow (faster)
dotnet-get-data-flow \
  --file "/path/to/file.cs" \
  --startLine 10 --startColumn 5 \
  --endLine 20 --endColumn 10 \
  --includeControlFlow false

# With workspace context
dotnet-get-data-flow \
  --file "/path/to/file.cs" \
  --startLine 10 --startColumn 5 \
  --endLine 20 --endColumn 10 \
  --workspacePath "/path/to/solution.sln"
```

## Implementation Details

The tool uses Roslyn's `SemanticModel.AnalyzeDataFlow()` API which provides:
- Accurate semantic analysis
- Understanding of variable scopes
- Type-aware flow tracking
- Language-specific semantics (C#, VB.NET)

## Related Documentation

- [Control Flow Analysis](./CONTROL_FLOW_ANALYSIS.md)
- [Tool Synopsis](./TOOL_SYNOPSIS.md)
- [Statement Context](./design/STATEMENT_CONTEXT.md)