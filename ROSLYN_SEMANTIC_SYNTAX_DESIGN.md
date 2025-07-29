# Roslyn Semantic-Syntax Traversal Design

## Core Concept
Roslyn enables seamless traversal between syntactic and semantic representations of code. This allows agents to start with either representation and move to the other as needed for analysis or modification.

## Key Capabilities

### 1. **Syntax → Semantic**
From any syntax node, get its semantic meaning:
```yaml
SyntaxNode (user.Name) → IPropertySymbol (User.Name property)
SyntaxNode (GetUser()) → IMethodSymbol (GetUser method)
SyntaxNode (int x) → ILocalSymbol (local variable x)
```

### 2. **Semantic → Syntax**
From any symbol, find all its syntax references:
```yaml
IPropertySymbol (User.Name) → All MemberAccessExpression nodes
IMethodSymbol (GetUser) → All InvocationExpression nodes
ITypeSymbol (User) → All type references, declarations
```

### 3. **Context Navigation**
- From a syntax node, walk up to understand context
- From a semantic symbol, understand relationships
- Track data flow through both representations

## Proposed Tool Interface

### Discovery Tools
```yaml
dotnet/get-semantic-info:
  input: syntaxNodeId
  output: 
    - symbol info (type, name, containing type)
    - type info (compile-time type)
    - data flow info (where value comes from/goes)

dotnet/get-syntax-nodes:
  input: symbolId
  output:
    - all syntax nodes referencing this symbol
    - categorized by usage (declaration, read, write)

dotnet/analyze-context:
  input: nodeId or symbolId
  output:
    - syntactic context (parent statements, containing method)
    - semantic context (related symbols, type hierarchy)
```

### Navigation Tools
```yaml
dotnet/traverse-syntax:
  input: nodeId, direction (up/down/sibling)
  output: related syntax nodes with their types

dotnet/follow-data-flow:
  input: expressionNodeId
  output: 
    - where the value comes from
    - where it flows to
    - all intermediate assignments
```

### Modification Tools
```yaml
dotnet/modify-semantic:
  operations:
    - add-parameter-to-symbol
    - change-return-type
    - add-attribute-to-symbol

dotnet/modify-syntax:
  operations:
    - replace-node
    - insert-before/after
    - wrap-with-node
```

## Example Workflow

**Task**: Add SQL parameterization for all User.Name uses in SQL contexts

```yaml
1. Find all User.Name property accesses:
   → dotnet/find-references symbol="User.Name"
   
2. For each syntax node:
   → dotnet/analyze-context nodeId=<node>
   Returns: "Inside string concatenation, flows to SqlCommand"
   
3. Navigate to the containing statement:
   → dotnet/traverse-syntax nodeId=<node> direction="up" until="Statement"
   
4. Analyze the semantic flow:
   → dotnet/follow-data-flow expressionNodeId=<concat>
   Returns: "Flows to SqlCommand constructor parameter"
   
5. Modify using semantic understanding:
   → dotnet/modify-syntax
     operation: "replace-node"
     node: <concatenation>
     newCode: "@Name"
   
   → dotnet/modify-syntax  
     operation: "insert-after"
     afterNode: <SqlCommand creation>
     newCode: "cmd.Parameters.AddWithValue(\"@Name\", user.Name);"
```

## Benefits for Agents

1. **Precision**: No guessing - exact symbol matching
2. **Completeness**: Find ALL uses, not just text matches
3. **Context-Aware**: Understand the semantic meaning
4. **Safe Transformations**: Roslyn validates all changes
5. **Cross-Cutting**: Changes can span multiple files consistently

## Implementation Notes

- Each syntax node gets a stable ID for the session
- Each symbol gets a stable ID for the session  
- Agent can store and reference these IDs
- Roslyn tracks all changes in memory
- Changes are applied atomically

## Roslyn Navigation Mechanisms

### Transitions Between Representations

Roslyn provides 6 key transitions between position, syntax, and semantic representations:

#### 1. **Position → Syntax Node**
```csharp
// From line/column to SyntaxNode
var position = sourceText.Lines.GetPosition(new LinePosition(line, column));
var token = root.FindToken(position);
var node = token.Parent; // or token.Parent.AncestorsAndSelf() to find specific node type

// Alternative: direct from position
var node = root.FindNode(new TextSpan(position, 0));
```

#### 2. **Position → Semantic Symbol**
```csharp
// From line/column to ISymbol
var position = sourceText.Lines.GetPosition(new LinePosition(line, column));
var symbolInfo = semanticModel.GetSymbolInfo(root.FindNode(new TextSpan(position, 0)));
var symbol = symbolInfo.Symbol;

// Or for declaration
var declaredSymbol = semanticModel.GetDeclaredSymbol(root.FindNode(new TextSpan(position, 0)));
```

#### 3. **Syntax Node → Position**
```csharp
// From SyntaxNode to line/column
var span = node.Span;
var lineSpan = sourceText.Lines.GetLinePositionSpan(span);
var startLine = lineSpan.Start.Line;
var startColumn = lineSpan.Start.Character;
```

#### 4. **Syntax Node → Semantic Symbol**
```csharp
// From SyntaxNode to ISymbol
var symbol = semanticModel.GetSymbolInfo(node).Symbol;

// For declarations
var declaredSymbol = semanticModel.GetDeclaredSymbol(node);

// For type information
var typeInfo = semanticModel.GetTypeInfo(node);
```

#### 5. **Semantic Symbol → Syntax Node(s)**
```csharp
// From ISymbol to SyntaxNodes (can be multiple across files)
var references = await SymbolFinder.FindReferencesAsync(symbol, solution);
foreach (var reference in references)
{
    foreach (var location in reference.Locations)
    {
        var node = root.FindNode(location.Location.SourceSpan);
    }
}

// For declaration
var declarationSyntax = symbol.DeclaringSyntaxReferences
    .Select(r => r.GetSyntax())
    .FirstOrDefault();
```

#### 6. **Semantic Symbol → Position**
```csharp
// From ISymbol to line/column
foreach (var syntaxRef in symbol.DeclaringSyntaxReferences)
{
    var syntax = await syntaxRef.GetSyntaxAsync();
    var span = syntax.Span;
    var sourceText = await syntaxRef.SyntaxTree.GetTextAsync();
    var lineSpan = sourceText.Lines.GetLinePositionSpan(span);
}

// For all references
var references = await SymbolFinder.FindReferencesAsync(symbol, solution);
foreach (var reference in references)
{
    foreach (var location in reference.Locations)
    {
        var lineSpan = location.Location.GetLineSpan();
    }
}
```

### Key Classes & Methods

**Position-related:**
- `SourceText.Lines.GetPosition(LinePosition)` - Convert line/column to absolute position
- `SourceText.Lines.GetLinePositionSpan(TextSpan)` - Convert span to line/column range
- `Location.GetLineSpan()` - Get line span from a location

**Syntax-related:**
- `SyntaxNode.FindToken(position)` - Find token at position
- `SyntaxNode.FindNode(TextSpan)` - Find node containing span
- `SyntaxNode.Span` - Get text span (excluding trivia)
- `SyntaxNode.FullSpan` - Get full span (including trivia)

**Semantic-related:**
- `SemanticModel.GetSymbolInfo(SyntaxNode)` - Get symbol at node
- `SemanticModel.GetDeclaredSymbol(SyntaxNode)` - Get symbol declared by node
- `SemanticModel.GetTypeInfo(SyntaxNode)` - Get type information
- `SymbolFinder.FindReferencesAsync(ISymbol, Solution)` - Find all references

### Important Notes

- Positions are 0-based in Roslyn
- Multiple syntax nodes can map to the same symbol (references)
- One syntax node maps to at most one symbol
- Some syntax nodes have no semantic meaning (e.g., punctuation)
- SemanticModel is tied to a specific compilation/document
- Symbol resolution may fail for incomplete or erroneous code

## Research Needed

1. How to best represent syntax trees for agent consumption
2. Optimal granularity for syntax node IDs
3. How to handle symbol resolution across assemblies
4. Best practices for semantic model caching
5. Transaction/rollback mechanisms for changes