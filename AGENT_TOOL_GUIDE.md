# McpDotnet Tool Guide for AI Agents

## Overview
This document lists all 37 McpDotnet tools with their descriptions as seen by AI agents, along with guidance on when to use each tool.

---

## 1. Workspace Management Tools

### dotnet-load-workspace
**Agent Description**: "Load a .NET solution or project into the workspace"
**When to Use**: Always start here. Load the solution/project before any other operations.
**Note**: Requires absolute paths only.

### dotnet-workspace-status  
**Agent Description**: "Get loading progress and workspace info"
**When to Use**: Check if workspace is loaded, get workspace ID, monitor loading progress.

### dotnet-get-diagnostics
**Agent Description**: "Get compilation diagnostics (errors, warnings, info) from the workspace"
**When to Use**: Check for build errors, find compilation issues, understand project health.

---

## 2. Symbol Discovery Tools (Semantic)

### dotnet-find-class
**Agent Description**: "Find classes, interfaces, structs, or enums by name pattern (supports * and ? wildcards)"
**When to Use**: Discovering types in codebase, finding specific classes/interfaces.
**Strength**: Complete results using Roslyn's semantic model.

### dotnet-find-method
**Agent Description**: "Find methods by name pattern with optional class pattern filter (supports * and ? wildcards)"
**When to Use**: Locating specific methods, finding all methods matching a pattern.
**Returns**: Full method signatures with return types and parameters.

### dotnet-find-property
**Agent Description**: "Find properties and fields by name pattern with optional class pattern filter (supports * and ? wildcards)"
**When to Use**: Finding properties/fields, tracing data flow through object graph.

---

## 3. Relationship Analysis Tools (Semantic)

### dotnet-find-references
**Agent Description**: "Find all references to a type, method, property, or field"
**When to Use**: Impact analysis before changes, finding all usages of a symbol.
**Strength**: COMPLETE results - same as Visual Studio's "Find All References".
**Note**: Now accepts "class", "interface", "struct" etc. as symbolType.

### dotnet-find-implementations
**Agent Description**: "Find all implementations of an interface or abstract class (trace inheritance hierarchy downward)"
**When to Use**: Understanding inheritance chains, finding concrete implementations.
**Strength**: Finds ALL implementations across entire solution.

### dotnet-find-derived-types
**Agent Description**: "Find all types that derive from a base class (trace class hierarchy, find all subclasses)"
**When to Use**: Understanding type hierarchies, impact analysis for base class changes.

### dotnet-find-overrides
**Agent Description**: "Find all overrides of a virtual or abstract method across the inheritance chain"
**When to Use**: Tracking method customizations, ensuring consistent behavior across overrides.

### dotnet-find-method-calls
**Agent Description**: "Find all methods called by a specific method (call tree analysis)"
**When to Use**: Understanding method dependencies, analyzing what a method does.

### dotnet-find-method-callers
**Agent Description**: "Find all methods that call a specific method (caller tree analysis)"
**When to Use**: Impact analysis, understanding who uses a method.
**Strength**: COMPLETE results including through interfaces and delegates.

---

## 4. Statement-Level Operations

### dotnet-find-statements
**Agent Description**: "Find statements in code matching a pattern. Returns statement IDs for use with other operations. Uses Roslyn's syntax tree to enumerate all statements."
**When to Use**: Finding specific code patterns, preparing for multi-step refactoring.
**Returns**: Statement text + stable ID + location.
**Pattern Types**: text, regex, or roslynpath.

### dotnet-replace-statement
**Agent Description**: "Replace a statement with new code. The statement is identified by its location from find-statements. Preserves indentation and formatting context."
**When to Use**: Surgical code modifications, preserving code structure.
**Strength**: Maintains proper indentation and formatting automatically.

### dotnet-insert-statement
**Agent Description**: "Insert a new statement before or after an existing statement. The reference statement is identified by its location from find-statements. Preserves indentation and formatting context."
**When to Use**: Adding validation, logging, or new logic at specific points.

### dotnet-remove-statement
**Agent Description**: "Remove a statement from the code. The statement is identified by its location from find-statements. Can preserve comments attached to the statement."
**When to Use**: Cleaning up code, removing deprecated functionality.

---

## 5. Marker System (For Multi-Step Operations)

### dotnet-mark-statement
**Agent Description**: "Mark a statement with an ephemeral marker for later reference. Markers are session-scoped and not persisted."
**When to Use**: Multi-step refactoring where you need to track multiple locations.
**Strength**: Markers survive edits - they move with the code.

### dotnet-find-marked-statements
**Agent Description**: "Find all or specific marked statements. Returns current locations even if code has been edited."
**When to Use**: Returning to marked locations after other edits.

### dotnet-unmark-statement
**Agent Description**: "Remove a specific marker by its ID."
**When to Use**: Cleaning up specific markers during refactoring.

### dotnet-clear-markers
**Agent Description**: "Clear all markers in the current session."
**When to Use**: Cleanup after completing multi-step refactoring.

---

## 6. Code Modification Tools

### dotnet-rename-symbol
**Agent Description**: "Rename a symbol (type, method, property, field) and update all references"
**When to Use**: Safe renaming with automatic reference updates.
**Strength**: Updates ALL references across entire solution.

### dotnet-edit-code
**Agent Description**: "Perform surgical code edits using Roslyn. Operations: add-method, add-property, make-async, add-parameter, wrap-try-catch"
**When to Use**: Specific structural modifications.
**Note**: Agents often prefer statement-level operations or direct file editing.

### dotnet-fix-pattern
**Agent Description**: "Transform code using semantic-aware patterns with RoslynPath queries and statement-level operations. Supports transformations: add-null-check, convert-to-async, extract-variable, simplify-conditional, parameterize-query, convert-to-interpolation, add-await, custom"
**When to Use**: Bulk pattern-based transformations.
**Note**: May be too specific - agents often prefer general tools.

---

## 7. Advanced Analysis Tools

### dotnet-get-statement-context
**Agent Description**: "Get comprehensive semantic context for a statement including symbols, types, diagnostics, and basic data flow"
**When to Use**: Deep analysis of specific code, understanding complex statements.
**Returns**: Full semantic information including types, symbols, data flow.

### dotnet-get-data-flow
**Agent Description**: "Get comprehensive data flow analysis for a code region showing variable usage, dependencies, and control flow"
**When to Use**: Understanding variable lifecycle, tracking data dependencies.

---

## 8. AST Navigation Tools (Syntactic)

### dotnet-query-syntax
**Agent Description**: "Query any syntax node using enhanced RoslynPath with full AST navigation"
**When to Use**: Complex structural queries, pattern matching on AST.
**Strength**: XPath-like queries for precise AST navigation.
**Example**: `//if-statement//binary-expression[@operator='==']`

### dotnet-navigate
**Agent Description**: "Navigate from a position using RoslynPath axes (ancestor::, following-sibling::, etc.)"
**When to Use**: Moving through AST from a known position.

### dotnet-get-ast
**Agent Description**: "Get AST structure for understanding code hierarchy"
**When to Use**: Understanding code structure, exploring unfamiliar code.

### dotnet-analyze-syntax
**Agent Description**: "Analyzes the syntax tree of a C# or VB.NET file"
**When to Use**: Low-level syntax analysis.
**Note**: Other tools usually more useful.

### dotnet-get-symbols
**Agent Description**: "Get symbols at a specific position in a file"
**When to Use**: Understanding what's at a specific location.
**Note**: get-statement-context often more comprehensive.

---

## 9. F# Specific Tools

### spelunk-fsharp-load-project
**Agent Description**: "Load an F# project using FSharp.Compiler.Service (separate from MSBuild workspaces)"
**When to Use**: Working with F# projects (required before other F# operations).

### spelunk-fsharp-find-symbols
**Agent Description**: "Find symbols in F# code using FSharpPath queries"
**When to Use**: Searching F# code for functions, types, patterns.

### spelunk-fsharp-query
**Agent Description**: "Query F# AST using FSharpPath (XPath-like syntax for F#)"
**When to Use**: Pattern matching on F# code structure.

### spelunk-fsharp-get-ast
**Agent Description**: "Get F# AST structure"
**When to Use**: Understanding F# code organization.

---

## Key Differentiators for Agents

### Why Choose McpDotnet Tools Over Text-Based Search:

1. **COMPLETENESS**: Find-references, find-implementations, etc. find ALL occurrences, not just text matches
2. **ACCURACY**: No false positives from similar names, no false negatives from aliases
3. **SEMANTIC UNDERSTANDING**: Knows about inheritance, interfaces, overloads, generics
4. **STRUCTURE PRESERVATION**: Statement-level edits maintain proper formatting automatically
5. **STABLE REFERENCES**: Statement IDs and markers survive edits

### When to Use McpDotnet vs Other Tools (like Serena):

**Use McpDotnet for**:
- .NET-specific operations (C#, VB.NET, F#)
- When you need COMPLETE results (all references, all implementations)
- Structure-preserving edits (statement-level operations)
- Semantic understanding (type relationships, inheritance)
- Multi-step refactoring with markers

**Use Other Tools for**:
- Simple text replacement across many files
- Non-.NET languages
- File operations outside code
- When semantic understanding isn't needed

---

## Tool Selection Decision Tree

```
Need to analyze .NET code?
├─ Yes → Load workspace first
│   ├─ Finding symbols? → Use find-* tools (complete results)
│   ├─ Understanding relationships? → Use find-references/implementations/derived
│   ├─ Modifying code?
│   │   ├─ Single statement? → Use replace/insert/remove-statement
│   │   ├─ Multiple related statements? → Use markers + statement operations
│   │   ├─ Renaming? → Use rename-symbol (updates all references)
│   │   └─ Complex pattern? → Use RoslynPath with query-syntax
│   └─ Analyzing code?
│       ├─ Need semantic info? → Use get-statement-context
│       └─ Need structure? → Use get-ast or query-syntax
└─ No → Consider other tools
```

---

## Important Notes for Agents

1. **Always Load Workspace First**: Most tools require a loaded workspace
2. **Use Absolute Paths**: Relative paths are not supported for workspace loading
3. **Statement IDs are Stable**: They survive edits, use them for multi-step operations
4. **Results are Complete**: When we say "all references", we mean ALL - same as Visual Studio
5. **Prefer Semantic Tools for Accuracy**: Find-* tools use Roslyn's semantic model
6. **Use RoslynPath for Complex Patterns**: More powerful than regex for AST queries
7. **Statement-Level is Optimal**: Not too fine (tokens), not too coarse (methods)

---

*Generated from McpDotnet tool descriptions and documentation*
*Last updated: 2024*