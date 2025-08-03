# Enhanced RoslynPath Design

## Overview

This document outlines enhancements to the existing RoslynPath system to provide more comprehensive syntax tree navigation without creating a new query language.

## Current State

RoslynPath already supports:

### Axes
- `ancestor::` - All ancestors
- `ancestor-or-self::` - Node and its ancestors  
- `following-sibling::` - Siblings after current node
- `preceding-sibling::` - Siblings before current node
- `/` - Child axis
- `//` - Descendant axis
- `..` - Parent axis

### Node Types (High-Level)
- `class`, `interface`, `struct`, `enum`
- `method`, `property`, `field`, `constructor`
- `namespace`, `block`, `statement`, `expression`

### Predicates
- Name matching: `[ProcessOrder]`, `[Get*]`
- Position: `[1]`, `[last()]`
- Attributes: `[@type=]`, `[@contains=]`, `[@matches=]`
- Boolean: `[@async]`, `[@static]`, `[@public]`

## Proposed Enhancements

### 1. Additional Axes

Add missing XPath-standard axes:
- `descendant-or-self::` - Node and its descendants
- `child::` - Explicit child axis (currently implicit)
- `parent::` - Explicit parent axis (currently `..`)
- `self::` - Current node

### 2. Expanded Node Types

#### Expression-Level Nodes
```
binary-expression    - Binary operations (==, !=, +, -, etc.)
unary-expression     - Unary operations (!, -, ++, etc.)
literal             - Literals (42, "string", true, null)
identifier          - Variable/member names
invocation          - Method calls
member-access       - Property/field access
assignment          - Assignment expressions
conditional         - Ternary operator
lambda             - Lambda expressions
```

#### Statement-Level Nodes
```
if-statement        - If statements
for-statement       - For loops
foreach-statement   - Foreach loops
while-statement     - While loops
do-statement        - Do-while loops
switch-statement    - Switch statements
try-statement       - Try-catch-finally
throw-statement     - Throw statements
return-statement    - Return statements
using-statement     - Using statements
```

#### Declaration-Level Nodes
```
parameter          - Method parameters
type-parameter     - Generic type parameters
variable           - Local variables
argument           - Method arguments
attribute          - Attributes/annotations
```

### 3. Enhanced Attributes

#### Node Properties
```
@name              - Node name (if applicable)
@type              - Specific syntax node type
@kind              - Roslyn SyntaxKind
@text              - Node text content
@line              - Line number
@column            - Column number
@span-start        - Text span start
@span-length       - Text span length
```

#### Semantic Properties (requires SemanticModel)
```
@symbol-type       - Symbol type (method, field, etc.)
@return-type       - Return type for methods
@value-type        - Type of expressions
@is-async          - Is async method/lambda
@is-static         - Is static member
@is-virtual        - Is virtual/override
@accessibility     - public, private, protected, internal
```

#### Expression Properties
```
@operator          - Operator for binary/unary expressions
@literal-value     - Value of literal expressions
@method-name       - Name of invoked method
@member-name       - Name of accessed member
```

#### Metrics
```
@complexity        - Cyclomatic complexity
@line-count        - Number of lines
@statement-count   - Number of statements
@depth             - Nesting depth
```

### 4. New Tools

#### dotnet-query-syntax

Query any node type with full RoslynPath:

```json
{
  "roslynPath": "//binary-expression[@operator='==' and @right-text='null']",
  "file": "/path/to/file.cs",
  "includeContext": true,
  "contextLines": 2
}
```

Response:
```json
{
  "matches": [
    {
      "node": {
        "type": "BinaryExpression",
        "kind": "EqualsExpression",
        "text": "user == null",
        "location": { "line": 42, "column": 12 }
      },
      "context": {
        "before": ["    public void Process(User user)", "    {"],
        "after": ["        {", "            throw new ArgumentNullException();"]
      },
      "path": "//class[UserService]/method[Process]/block/if-statement[1]/condition"
    }
  ]
}
```

#### dotnet-navigate

Navigate from a position using RoslynPath:

```json
{
  "from": { "file": "/path/to/file.cs", "line": 42, "column": 15 },
  "path": "ancestor::method[1]/following-sibling::method[1]",
  "returnPath": true
}
```

Response:
```json
{
  "navigatedTo": {
    "type": "MethodDeclaration",
    "name": "ProcessNext",
    "location": { "line": 50, "column": 5 },
    "path": "//class[UserService]/method[ProcessNext]"
  }
}
```

#### dotnet-get-ast

Get AST structure for learning:

```json
{
  "file": "/path/to/file.cs",
  "root": "//method[Process]",
  "depth": 3,
  "includeTokens": false,
  "format": "tree"
}
```

Response:
```json
{
  "ast": {
    "type": "MethodDeclaration",
    "name": "Process",
    "children": [
      {
        "type": "ParameterList",
        "children": [
          {
            "type": "Parameter",
            "name": "user",
            "type": "User"
          }
        ]
      },
      {
        "type": "Block",
        "children": [
          {
            "type": "IfStatement",
            "children": [
              {
                "type": "BinaryExpression",
                "operator": "==",
                "left": { "type": "Identifier", "name": "user" },
                "right": { "type": "Literal", "value": "null" }
              }
            ]
          }
        ]
      }
    ]
  }
}
```

## Implementation Plan

### Phase 1: Enhance Node Type Support
1. Add expression-level node types to GetNodeTypeName
2. Add statement-specific node types
3. Add declaration node types
4. Update MatchesNodeTest to handle new types

### Phase 2: Add New Attributes
1. Extend AttributePredicate handling
2. Add semantic property support (when SemanticModel available)
3. Add expression-specific properties
4. Add metrics calculation

### Phase 3: Implement New Tools
1. Create dotnet-query-syntax tool
2. Create dotnet-navigate tool  
3. Create dotnet-get-ast tool
4. Add tests for each tool

### Phase 4: Documentation
1. Update RoslynPath documentation
2. Add examples for new node types
3. Create tutorial for AST navigation
4. Update TOOL_SYNOPSIS.md

## Benefits

1. **No New Syntax** - Extends existing RoslynPath
2. **Powerful Navigation** - Can reach any node in the AST
3. **Semantic Awareness** - Can query based on types and symbols
4. **Learning Tool** - get-ast helps understand code structure
5. **Precise Targeting** - Can target specific expressions, not just statements

## Examples

### Find Null Comparisons in Specific Context
```
//method[@async]/block//if-statement/condition/binary-expression[@operator='==' and @right-text='null']
```

### Navigate to Variable Declaration
```
//identifier[@name='userName']/ancestor::block[1]//variable[@name='userName']
```

### Find Complex Expressions
```
//binary-expression[@operator='&&' or @operator='||'][@depth>3]
```

### Get All Async Method Calls Without Await
```
//invocation[@method-async=true and not(ancestor::await-expression)]
```

This enhancement makes RoslynPath a complete AST query language while maintaining backward compatibility and consistency with XPath standards.