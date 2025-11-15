# Semantic Enrichment Specification

## Overview

This specification defines how semantic information (type info, symbol details, project context) is added to syntax-based tools in the Spelunk.NET. The enrichment provides deeper code understanding by combining syntax analysis with Roslyn's semantic model.

## Affected Tools

The following tools will support semantic enrichment via an `includeSemanticInfo` parameter:

1. **spelunk-query-syntax** - Enhanced SpelunkPath query results
2. **spelunk-navigate** - Navigation with semantic context
3. **spelunk-get-ast** - AST visualization with type information

## API Design

### Input Parameter

Each tool will accept an optional parameter:

```json
{
  "includeSemanticInfo": boolean  // Default: false
}
```

When `true`, the response will include additional semantic information for each syntax node.

### Output Format

#### Standard Semantic Info Structure

Each enriched node will include a `semanticInfo` object with the following potential fields:

```typescript
interface SemanticInfo {
  // Type Information
  type?: string;           // Fully qualified type (e.g., "System.String")
  returnType?: string;     // For methods/properties
  elementType?: string;    // For arrays/collections
  
  // Symbol Information
  symbolKind?: string;     // "Method", "Property", "Field", "Variable", etc.
  symbolName?: string;     // Fully qualified symbol name
  accessibility?: string;  // "public", "private", "protected", "internal"
  modifiers?: string[];    // ["static", "async", "virtual", "override", etc.]
  
  // Method/Constructor Specific
  parameters?: Array<{
    name: string;
    type: string;
    isOptional: boolean;
    defaultValue?: string;
  }>;
  typeParameters?: string[];  // Generic type parameters
  
  // Context Information
  containingType?: string;    // Parent class/interface
  containingNamespace?: string;
  assembly?: string;          // Assembly name
  project?: string;           // Project name
  
  // Usage Information
  isExtensionMethod?: boolean;
  isGeneric?: boolean;
  isAsync?: boolean;
  
  // Data Flow (for expressions/variables)
  dataFlowIn?: string[];     // Variables flowing into expression
  dataFlowOut?: string[];    // Variables flowing out
  definiteAssignment?: boolean;
  
  // Diagnostics
  hasErrors?: boolean;
  diagnostics?: Array<{
    severity: string;        // "Error", "Warning", "Info"
    code: string;           // e.g., "CS0103"
    message: string;
  }>;
}
```

### Tool-Specific Enrichment

#### spelunk-query-syntax

**Request:**
```json
{
  "roslynPath": "//method[@name='ProcessOrder']",
  "includeSemanticInfo": true
}
```

**Response with semantic info:**
```json
{
  "nodes": [
    {
      "nodeType": "MethodDeclaration",
      "path": "//class[OrderService]/method[ProcessOrder]",
      "location": { "file": "OrderService.cs", "startLine": 25, "startColumn": 5 },
      "text": "public async Task<OrderResult> ProcessOrder(Order order)",
      "semanticInfo": {
        "symbolKind": "Method",
        "symbolName": "OrderService.ProcessOrder",
        "accessibility": "public",
        "modifiers": ["async"],
        "returnType": "System.Threading.Tasks.Task<OrderService.OrderResult>",
        "parameters": [
          {
            "name": "order",
            "type": "OrderService.Order",
            "isOptional": false
          }
        ],
        "containingType": "OrderService",
        "containingNamespace": "MyApp.Services",
        "project": "MyApp.Core",
        "isAsync": true
      }
    }
  ]
}
```

#### spelunk-navigate

**Request:**
```json
{
  "from": { "file": "OrderService.cs", "line": 30, "column": 10 },
  "path": "ancestor::method[1]",
  "includeSemanticInfo": true
}
```

**Response with semantic info:**
```json
{
  "target": {
    "nodeType": "MethodDeclaration",
    "location": { "file": "OrderService.cs", "startLine": 25, "startColumn": 5 },
    "path": "//class[OrderService]/method[ProcessOrder]",
    "text": "public async Task<OrderResult> ProcessOrder(Order order)",
    "semanticInfo": {
      "symbolKind": "Method",
      "returnType": "System.Threading.Tasks.Task<OrderService.OrderResult>",
      "containingType": "OrderService",
      "isAsync": true,
      "parameters": [...]
    }
  }
}
```

#### spelunk-get-ast

**Request:**
```json
{
  "file": "OrderService.cs",
  "root": "//method[ProcessOrder]",
  "depth": 2,
  "includeSemanticInfo": true
}
```

**Response with semantic info:**
```json
{
  "ast": [
    {
      "nodeType": "MethodDeclaration",
      "name": "ProcessOrder",
      "semanticInfo": {
        "symbolKind": "Method",
        "returnType": "System.Threading.Tasks.Task<OrderService.OrderResult>",
        "isAsync": true
      },
      "children": [
        {
          "nodeType": "Parameter",
          "name": "order",
          "semanticInfo": {
            "symbolKind": "Parameter",
            "type": "OrderService.Order"
          }
        },
        {
          "nodeType": "Block",
          "children": [
            {
              "nodeType": "LocalDeclarationStatement",
              "semanticInfo": {
                "variables": [
                  {
                    "name": "result",
                    "type": "OrderService.OrderResult",
                    "definiteAssignment": true
                  }
                ]
              }
            }
          ]
        }
      ]
    }
  ]
}
```

## Implementation Requirements

### 1. Semantic Model Access
- Must have access to Roslyn's `SemanticModel` for the file
- Requires the file to be part of a loaded workspace/project
- Falls back gracefully when semantic info is unavailable

### 2. Performance Considerations
- Semantic analysis is more expensive than syntax-only analysis
- Should cache semantic models when processing multiple nodes
- Only compute requested semantic information (lazy evaluation)

### 3. Error Handling
- If semantic model is unavailable, return syntax-only results
- Include partial semantic info when some fields cannot be computed
- Never fail the entire request due to semantic analysis errors

### 4. Type Name Formatting
- Use fully qualified type names by default
- Include generic type arguments (e.g., `List<System.String>`)
- Handle nullable types appropriately (e.g., `System.String?`)

## Use Cases

### 1. Type-Aware Code Navigation
Find all async methods returning a specific type:
```
//method[@async and @returns='Task<OrderResult>']
```

### 2. Symbol Resolution
Navigate from a method call to its declaration with full type context.

### 3. Code Analysis
Identify variables with specific types or unresolved symbols.

### 4. Refactoring Assistance
Understand type hierarchies and method signatures for safe refactoring.

### 5. IDE-like Features
Provide hover information, go-to-definition context, and type checking.

## Testing Strategy

### Unit Tests
1. Test each tool with `includeSemanticInfo: false` (default behavior)
2. Test each tool with `includeSemanticInfo: true`
3. Verify graceful fallback when semantic model unavailable
4. Test with various C# constructs (generics, async, nullable, etc.)

### Integration Tests
1. Load a real project and verify semantic info accuracy
2. Test cross-file symbol resolution
3. Verify project and assembly information
4. Test with VB.NET files for language-agnostic behavior

### Performance Tests
1. Measure overhead of semantic enrichment
2. Verify caching effectiveness
3. Test with large files/projects

## Future Extensions

1. **Semantic-aware SpelunkPath predicates**
   - `@implements='IDisposable'`
   - `@derives-from='BaseClass'`
   - `@calls='MethodName'`

2. **Rich diagnostics integration**
   - Include compiler errors/warnings
   - Suggest fixes based on diagnostics

3. **Cross-file semantic queries**
   - Find all implementations across solution
   - Track data flow across method boundaries

4. **Language service features**
   - Completion suggestions
   - Signature help
   - Quick info

---

**PAUSING HERE FOR REVIEW** - Please review this specification before I proceed with implementation.