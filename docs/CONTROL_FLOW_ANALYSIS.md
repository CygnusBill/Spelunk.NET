# Control Flow Analysis in McpDotnet

## Overview

The `dotnet-get-data-flow` tool includes optional control flow analysis via the `includeControlFlow` parameter. As of January 2025, this feature has been enhanced to use Roslyn's control flow analysis API with clear error reporting when analysis isn't possible.

## Current Implementation (Enhanced)

### What It Does

The control flow analysis uses **Roslyn's AnalyzeControlFlow API** exclusively, providing:

1. **Accurate reachability analysis** - Determines if code paths are reachable
2. **Proper return detection** - Knows if all paths return a value
3. **Entry/exit point tracking** - Identifies how control enters and exits regions
4. **Yield statement detection** - For iterator methods
5. **Clear error reporting** - Returns null with informative error when region is invalid

### Data Structure Returned

When successful:
```json
{
  "ControlFlow": {
    "AlwaysReturns": false,          // True if all paths return
    "EndPointIsReachable": true,     // Can control reach the end?
    "StartPointIsReachable": true,   // Can control reach the start?
    "ReturnStatements": 3,           // Count of return statements
    "HasYieldStatements": false,     // Presence of yield statements
    "ExitPoints": ["Return", "Throw", "Break"],  // Types of exit statements found
    "EntryPoints": 0                 // Number of entry points (e.g., goto targets)
  }
}
```

When analysis fails:
```json
{
  "ControlFlow": null,
  "Warnings": [
    {
      "Type": "ControlFlowError",
      "Message": "Control flow analysis requires complete, consecutive statements. The selected region either contains partial statements or statements from different scopes. Try selecting complete statement(s) within a single block."
    }
  ]
}
```

## Improvements Made (January 2025)

### ✅ Now Using Roslyn's Control Flow API
The implementation properly uses `SemanticModel.AnalyzeControlFlow()`, providing:
- Accurate reachability analysis
- Proper "always returns" detection across all code paths
- Entry and exit point tracking
- Understanding of control flow semantics

### ✅ Clear Error Reporting
When Roslyn's API cannot analyze a region:
- Returns `null` instead of misleading fallback data
- Provides clear error message explaining the issue
- Guides users on how to fix the problem
- No more confusion from inaccurate fallback analysis

## Remaining Limitations

### 1. Region Requirements
- Only works with complete, consecutive statements
- Partial control structures return null with error
- Statements must be within the same scope/block

### 2. No Control Flow Graph
- Doesn't provide the full control flow graph
- No basic block information
- No branch condition details

## Examples

### When It Works

```csharp
// Complete if statement - gets control flow data
if (x > 10)
{
    return true;
}
```

Result:
```json
{
  "ControlFlow": {
    "ReturnStatements": 1,
    "AlwaysReturns": false,
    "EndPointIsReachable": true,
    "StartPointIsReachable": true
  }
}
```

### When It Fails

```csharp
// Partial statement - only the condition selected
if (x > 10)  // <- Only this line selected
```

Result:
```json
{
  "ControlFlow": null,
  "Warnings": [
    {
      "Type": "ControlFlowError", 
      "Message": "Control flow analysis requires complete, consecutive statements..."
    }
  ]
}
```

## Data Flow Analysis (Always Works)

In contrast, the **data flow analysis** is robust and uses Roslyn's `AnalyzeDataFlow()` API properly:

```json
{
  "DataFlow": {
    "DataFlowsIn": ["x", "this"],
    "DataFlowsOut": ["result"],
    "ReadInside": ["x", "y"],
    "WrittenInside": ["result", "temp"],
    "AlwaysAssigned": ["result"],
    "Captured": [],
    "UnsafeAddressTaken": []
  }
}
```

## Recommendations

### For Users

1. **Don't rely on control flow analysis** for critical decisions
2. **Use data flow analysis instead** - it's accurate and reliable
3. **Set `includeControlFlow: false`** to avoid confusion
4. **Use other tools** for control flow needs:
   - `dotnet-find-statements` with RoslynPath for finding control structures
   - `dotnet-get-statement-context` for understanding code structure

### For Future Development

To properly implement control flow analysis:

1. **Use Roslyn's ControlFlowAnalysis API**:
   ```csharp
   var cfg = ControlFlowAnalysis.Create(method, semanticModel);
   ```

2. **Provide control flow graph** with:
   - Entry/exit points
   - Basic blocks
   - Branch conditions
   - Reachability information

3. **Add dedicated control flow tool** instead of mixing with data flow

## Current Status

✅ **Control flow analysis uses Roslyn API exclusively**
✅ **Clear error messages when analysis cannot be performed**
✅ **No misleading fallback data - returns null with explanation**
✅ **Data flow analysis remains production-ready and robust**

## Related Documentation

- [Data Flow Analysis](./DATA_FLOW_ANALYSIS.md)
- [Tool Synopsis](./TOOL_SYNOPSIS.md)
- [Statement Level Operations](./design/STATEMENT_LEVEL_EDITING.md)