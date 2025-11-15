# Ephemeral Marker System Design

## Overview

The ephemeral marker system allows agents to temporarily mark statements in code for later reference within a session. This enables complex multi-step refactoring operations where agents need to track specific statements across multiple transformations.

## Key Concepts

### SyntaxAnnotations
Roslyn's `SyntaxAnnotation` provides a way to attach metadata to syntax nodes that persists through syntax tree transformations. These annotations are:
- Not part of the actual code (don't affect compilation)
- Preserved when creating modified syntax trees
- Lightweight and efficient
- Perfect for tracking nodes through transformations

### Ephemeral Nature
Markers are:
- Session-scoped (exist only during the MCP session)
- Not persisted to disk
- Cleared when documents are reloaded
- Automatically cleaned up on session end

## Design

### Marker Storage
```csharp
public class MarkerManager
{
    // Map of marker ID to annotation
    private readonly Dictionary<string, SyntaxAnnotation> _markers = new();
    
    // Map of document path to annotated syntax tree
    private readonly Dictionary<string, SyntaxTree> _annotatedTrees = new();
    
    // Counter for generating unique marker IDs
    private int _markerCounter = 0;
}
```

### Marker Operations

#### 1. Mark Statement
```csharp
public class MarkStatementResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? MarkerId { get; set; }
    public string? MarkedStatement { get; set; }
    public Location? Location { get; set; }
}
```

**Tool**: `dotnet-mark-statement`
- Input: location (file/line/column), optional label
- Process:
  1. Find statement at location
  2. Create unique marker ID (e.g., "mark-1", "mark-2")
  3. Create SyntaxAnnotation with marker ID
  4. Annotate the statement node
  5. Store annotated tree in memory
- Output: Marker ID, marked statement preview

#### 2. Find Marked Statements
```csharp
public class MarkedStatement
{
    public string MarkerId { get; set; }
    public string? Label { get; set; }
    public string Statement { get; set; }
    public Location Location { get; set; }
    public string? Context { get; set; }
}
```

**Tool**: `dotnet-find-marked-statements`
- Input: optional marker ID filter, optional file filter
- Process:
  1. Search annotated trees for marked nodes
  2. Extract current location and statement text
  3. Include surrounding context
- Output: List of marked statements with current locations

#### 3. Unmark Statement
**Tool**: `dotnet-unmark-statement`
- Input: marker ID
- Process:
  1. Find nodes with the annotation
  2. Remove annotation
  3. Update stored tree
- Output: Success/failure

### Integration with Existing Tools

All statement manipulation tools will preserve markers:

1. **replace-statement**: When replacing a marked statement, transfer the marker to the new statement
2. **insert-statement**: Markers on reference statements are preserved
3. **remove-statement**: Warn if removing a marked statement
4. **edit-code**: Preserve markers when possible, warn when markers might be affected

### Implementation Strategy

1. Add `MarkerManager` to `DotnetWorkspaceManager`
2. Update all modification methods to work with annotated trees
3. Ensure `TryApplyChanges` preserves annotations
4. Add marker info to statement search results

## Use Cases

### 1. Multi-Step Refactoring
```
Agent: Mark all methods that need async
Agent: For each marked method:
  - Add async keyword
  - Change return type to Task
  - Update callers
```

### 2. Complex Pattern Replacement
```
Agent: Mark all statements matching pattern A
Agent: Mark all statements matching pattern B
Agent: Find relationships between marked statements
Agent: Apply coordinated changes
```

### 3. Tracking Through Transformations
```
Agent: Mark original statement
Agent: Apply transformation 1
Agent: Find marked statement (at new location)
Agent: Apply transformation 2
```

## Technical Considerations

### Memory Management
- Limit number of markers per session (e.g., 100)
- Clear markers when documents are closed
- Option to clear all markers

### Persistence
- Markers are NOT saved with the file
- Markers are lost on:
  - Session end
  - Document reload from disk
  - Workspace reload

### Annotation Format
```csharp
var annotation = new SyntaxAnnotation(
    "MCP.Marker",  // Kind
    $"{markerId}|{label}|{timestamp}"  // Data
);
```

## Benefits

1. **Stateful Operations**: Enables complex multi-step refactoring
2. **Resilience**: Markers survive through code transformations
3. **Flexibility**: Agents can implement custom tracking strategies
4. **Performance**: More efficient than repeatedly searching for statements
5. **Clarity**: Clear which statements are being operated on

## Future Enhancements

1. **Marker Groups**: Group related markers together
2. **Marker Metadata**: Attach additional data to markers
3. **Marker Visualization**: Export markers for IDE visualization
4. **Cross-File Markers**: Track related statements across files
5. **Marker Persistence**: Optional save/load of marker sessions