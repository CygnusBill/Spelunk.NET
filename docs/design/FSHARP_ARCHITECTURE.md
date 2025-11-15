# F# Architecture in Spelunk.NET

## Overview

The Spelunk.NET provides unified code analysis and manipulation tools for .NET languages. While C# and VB.NET share the Roslyn compiler platform, F# requires a fundamentally different architectural approach. This document explains how we integrate F# support alongside Roslyn-based languages to provide a consistent developer experience.

## Why F# is Different

### Roslyn vs FSharp.Compiler.Service

**Roslyn** (used by C# and VB.NET):
- Microsoft's unified compiler platform
- Provides rich APIs for syntax trees, semantic analysis, and workspaces
- Deep integration with MSBuild and Visual Studio
- Designed for imperative, object-oriented languages

**FSharp.Compiler.Service** (used by F#):
- Separate compiler infrastructure specific to F#
- Different AST structure optimized for functional programming
- Own type checking and inference system
- Limited MSBuildWorkspace compatibility

### Key Architectural Differences

| Aspect | C#/VB.NET (Roslyn) | F# |
|--------|-------------------|-----|
| **Compiler Platform** | Microsoft.CodeAnalysis | FSharp.Compiler.Service |
| **AST Structure** | Object-oriented, statement-based | Functional, expression-based |
| **Workspace Loading** | MSBuildWorkspace | Custom project loading |
| **Syntax Nodes** | SyntaxNode hierarchy | FSharpSyntaxTree |
| **Semantic Model** | SemanticModel API | FSharpCheckFileResults |
| **Type System** | Nominal typing | Structural typing with inference |

## Architecture Design

### Dual Infrastructure Approach

```
┌─────────────────────────────────────────┐
│          MCP Protocol Layer             │
├─────────────────────────────────────────┤
│         Unified Tool Interface          │
│  (spelunk-find-method, spelunk-rename...) │
├─────────────┬───────────────────────────┤
│   Roslyn    │      F# Infrastructure    │
│   Engine    │                           │
├─────────────┼───────────────────────────┤
│ C# Handler  │    F# Handler             │
│ VB Handler  │    (FSharpWorkspace-      │
│             │     Manager)               │
├─────────────┼───────────────────────────┤
│  Roslyn     │  FSharp.Compiler.         │
│  APIs       │  Service APIs             │
└─────────────┴───────────────────────────┘
```

### Component Responsibilities

#### 1. **Unified Tool Interface**
- Presents consistent API to MCP clients
- Routes requests to appropriate handler
- Manages response format normalization

#### 2. **Language Detection**
```csharp
// Simplified routing logic
if (fileExtension == ".fs" || fileExtension == ".fsi")
    return FSharpHandler.Process(request);
else
    return RoslynHandler.Process(request);
```

#### 3. **F# Infrastructure Components**

**FSharpWorkspaceManager**:
- Manages F# project loading outside MSBuildWorkspace
- Maintains F# compiler context
- Handles incremental compilation

**FSharpProjectTracker**:
- Detects F# projects in mixed solutions
- Tracks project state and dependencies
- Reports why projects can't load in MSBuildWorkspace

**FSharpPath Query Engine**:
- XPath-style queries for F# AST
- Handles F#-specific constructs (discriminated unions, computation expressions)
- Maps F# concepts to unified tool interface

## Implementation Details

### F# Project Detection

When loading a solution, we:
1. Attempt MSBuildWorkspace loading (works for C#/VB.NET)
2. Detect failed F# projects
3. Track them separately for manual loading

```csharp
public class FSharpProjectTracker
{
    private readonly Dictionary<string, FSharpProjectInfo> _projects = new();
    
    public void TrackSkippedProject(string projectPath, string reason)
    {
        _projects[projectPath] = new FSharpProjectInfo
        {
            Path = projectPath,
            SkipReason = reason,
            DetectedAt = DateTime.Now
        };
    }
    
    public IReadOnlyList<FSharpProjectInfo> GetSkippedProjects()
    {
        return _projects.Values.ToList();
    }
}
```

### F# AST Navigation

F# AST differs significantly from Roslyn's:

**Roslyn AST Example** (C#):
```
MethodDeclaration
├── Modifiers: [public, async]
├── ReturnType: Task<string>
├── Identifier: ProcessAsync
├── Parameters: [(int, id)]
└── Body: Block
    └── Statements: [...]
```

**F# AST Example**:
```
LetBinding
├── Accessibility: Public
├── Identifier: processAsync
├── Expression: Lambda
    ├── Parameter: id
    └── Body: Computation
        └── AsyncBuilder: [...]
```

### FSharpPath Design

FSharpPath provides XPath-style queries for F# AST:

```fsharp
// F# code
let rec fibonacci n =
    match n with
    | 0 | 1 -> n
    | _ -> fibonacci (n-1) + fibonacci (n-2)

// FSharpPath queries
"//function[@recursive]"          // Finds recursive functions
"//match-expression"              // Finds pattern matches
"//function[contains(@name, 'fib')]"  // Finds functions with 'fib' in name
```

### Type System Mapping

F# and C#/VB.NET have different type representations:

| F# Type | C#/VB.NET Equivalent | Notes |
|---------|---------------------|-------|
| `int list` | `List<int>` | F# lists are immutable |
| `int option` | `int?` | Option types vs nullable |
| `Result<'T,'E>` | No direct equivalent | Discriminated unions |
| `int -> string` | `Func<int, string>` | Function types |
| `Async<'T>` | `Task<T>` | Different async models |

## Unified Interface Design

### Tool Response Normalization

All tools return consistent responses regardless of language:

```json
{
  "symbol": {
    "name": "processOrder",
    "kind": "Function",    // Normalized from F# "LetBinding"
    "type": "int -> Async<Order>",
    "location": { "file": "Orders.fs", "line": 42 }
  }
}
```

### Cross-Language Symbol Resolution

When finding references across languages:
1. Normalize F# symbols to Roslyn-compatible format
2. Search in both Roslyn and F# workspaces
3. Merge and deduplicate results

## Challenges and Solutions

### Challenge 1: Project System Integration

**Problem**: MSBuildWorkspace can't load F# projects
**Solution**: 
- Detect and track F# projects separately
- Provide explicit `spelunk-load-fsharp-project` tool
- Report skipped projects with reasons

### Challenge 2: Different AST Structures

**Problem**: SpelunkPath doesn't work with F# AST
**Solution**: 
- Created separate FSharpPath query language
- Similar syntax for developer familiarity
- F#-specific predicates and axes

### Challenge 3: Type System Differences

**Problem**: F# types don't map 1:1 to C#/VB.NET
**Solution**: 
- Best-effort mapping for common types
- Preserve F# type notation in responses
- Document mapping limitations

### Challenge 4: Async Model Differences

**Problem**: F# Async vs .NET Task
**Solution**: 
- Detect async patterns in both models
- Normalize in tool responses
- Preserve original semantics in code generation

## Performance Considerations

### Separate Compilation Contexts

- Roslyn and F# compiler maintain separate contexts
- No shared caching between language engines
- Memory overhead for mixed-language solutions

### Optimization Strategies

1. **Lazy Loading**: Only load F# projects when accessed
2. **Incremental Compilation**: Leverage F# compiler's incremental features
3. **Shared File Watching**: Unified file system monitoring
4. **Result Caching**: Cache query results at tool interface level

## Future Enhancements

### Planned Improvements

1. **Unified Project System**: Abstract over MSBuild limitations
2. **Cross-Language Refactoring**: Rename across F#/C# boundaries
3. **Type Provider Support**: Handle F# type providers in analysis
4. **Computation Expression Analysis**: Special support for F# workflows

### Long-term Vision

Eventually provide truly unified experience where language differences are transparent to users, while preserving language-specific features and idioms.

## Summary

F# support in the Spelunk.NET requires a parallel infrastructure to Roslyn, but careful design allows us to present a unified interface. By understanding the fundamental differences between F# and C#/VB.NET, we can build appropriate abstractions that serve developers working in mixed-language .NET solutions.

The key insight is that while the implementations differ significantly, the developer intent (find symbols, rename, refactor) remains consistent across languages. Our architecture bridges these differences at the tool interface level while respecting each language's unique characteristics.