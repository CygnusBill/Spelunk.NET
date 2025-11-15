# Roslyn Node Identification Mechanisms

## Built-in Roslyn Tracking Systems

### 1. TextSpan
- Every SyntaxNode has a `Span` property with Start and Length
- Used by diagnostics to provide precise line/column locations
- Survives minor edits as long as node position can be recalculated
- Used by editors for error highlighting and navigation

### 2. SyntaxAnnotation
- Roslyn's primary mechanism for tracking nodes through edits
- Annotations survive syntax tree transformations
- Can be used to "tag" nodes and find them later even after edits
- **Our MarkerManager uses this system internally**

### 3. TrackNodes
- `SyntaxTree.GetChanges()` can track how nodes move during edits
- Used by IDEs to maintain cursor position and error locations during refactoring
- Automatically updates line/column positions as code changes

## Integration with Our System

**Key Finding**: Our "ephemeral" marker system is actually built on Roslyn's robust SyntaxAnnotation infrastructure.

**Diagnostic → Marker → Fix Workflow**:
1. `dotnet-get-diagnostics` provides TextSpan location (line:column)
2. Agent uses `mark-statement` at that location → creates SyntaxAnnotation
3. Agent performs hierarchical context walking using markers
4. Agent applies fixes using marker references (not line numbers)
5. Markers survive the edit operations needed to fix errors

This elegantly combines Roslyn's native location tracking with our edit-resilient marker system.

## Implementation Details

From `DotnetWorkspaceManager.cs`:
```csharp
private const string MarkerAnnotationKey = "EphemeralMarker";

public string AddMarker(SyntaxNode node, string markerId)
{
    var annotation = new SyntaxAnnotation(MarkerAnnotationKey, markerId);
    // ... annotation gets attached to node
}
```

The markers are not truly "ephemeral" - they're persistent through edits via SyntaxAnnotation, which is Roslyn's recommended approach for tracking nodes through transformations.