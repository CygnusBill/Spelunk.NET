# FSharpPath Syntax Guide

## Overview

FSharpPath is an XPath-inspired query language for navigating F# abstract syntax trees. It provides a powerful way to search and analyze F# code structure.

## Basic Syntax

### Node Selection
- `//nodetype` - Select all nodes of a specific type anywhere in the tree
- `/nodetype` - Select nodes at the root level
- `nodetype` - Select direct children of the current context

### Node Types
- `let` - Let bindings (both values and functions)
- `function` - Functions (let bindings with parameters)
- `value` - Values (let bindings without parameters)
- `type` - Type definitions
- `module` - Modules (including nested modules)
- `match` - Pattern match expressions
- `pattern` - Pattern nodes
- `union` - Discriminated unions
- `record` - Record types
- `class` - Class types
- `interface` - Interface types

### Predicates

#### Name Predicates
- `[@name='factorial']` - Exact name match
- `[@name]` - Has any name

#### Boolean Attributes
- `[@recursive]` - Recursive functions
- `[@async]` - Async functions
- `[@mutable]` - Mutable values
- `[@inline]` - Inline functions

#### Type Predicates
- `[Union]` - Union types
- `[Record]` - Record types
- `[Class]` - Class types
- `[Interface]` - Interface types

### Axes
- `descendant::` - All descendants (default for //)
- `child::` - Direct children (default for /)
- `descendant-or-self::` - Node and all descendants
- `parent::` - Parent node
- `ancestor::` - All ancestors
- `ancestor-or-self::` - Node and all ancestors
- `following-sibling::` - Siblings after this node
- `preceding-sibling::` - Siblings before this node

## Examples

### Basic Queries
```
//let                    - All let bindings
//function               - All functions
//value                  - All values (non-function lets)
//type                   - All type definitions
//module                 - All modules
```

### Name-Based Queries
```
//let[@name='add']                     - Function named 'add'
//function[@name='factorial']          - Function named 'factorial'
//type[@name='Person']                 - Type named 'Person'
//module[@name='MathOperations']       - Module named 'MathOperations'
```

### Attribute Queries
```
//function[@recursive]                 - All recursive functions
//function[@async]                     - All async functions
//let[@inline]                        - All inline functions
//value[@mutable]                     - All mutable values
```

### Type-Specific Queries
```
//type[Union]                         - All discriminated unions
//type[Record]                        - All record types
//type[Class]                         - All class types
//type[Interface]                     - All interface types
```

### Complex Queries
```
//module[MathOperations]//function[@recursive]    - Recursive functions in MathOperations module
//let[@name='factorial']/match                    - Match expressions in factorial function
descendant::function[@async]                      - All async functions (explicit axis)
//type[Record][@name='Person']                    - Record type named Person
```

### Positional Queries
```
//let[1]                              - First let binding
//module/let[2]                       - Second let in each module
//function[last()]                    - Last function
```

## Wildcards in FSharpPath

Currently, FSharpPath uses exact matching. For pattern-based matching, use the `spelunk-fsharp-find-symbols` tool which supports:
- `*` - Matches any sequence of characters
- `?` - Matches any single character

## Query Context

When using `spelunk-fsharp-query`, you can include context:
```json
{
  "fsharpPath": "//let[@name='factorial']",
  "includeContext": true,
  "contextLines": 3
}
```

## Common Use Cases

### Finding Specific Constructs
- Find all recursive functions: `//function[@recursive]`
- Find all async workflows: `//function[@async]`
- Find all pattern matches: `//match`
- Find all discriminated unions: `//type[Union]`

### Analyzing Code Structure
- Functions in a module: `//module[Utils]//function`
- Nested modules: `//module//module`
- All named constructs: `//*[@name]`

### Navigation
- Parent of a match: `//match/parent::*`
- All ancestors of a function: `//function[@name='foo']/ancestor::*`
- Siblings of a type: `//type[@name='Person']/following-sibling::*`

## Limitations

1. No wildcard support in names (use exact matches)
2. No string predicates besides name
3. No numeric comparisons
4. No semantic information (types, references)
5. Single-file scope only

## Tips

1. Use `//` for deep searches when you don't know the exact path
2. Use `/` for performance when you know the structure
3. Combine node types with predicates for precise queries
4. Use axes explicitly for complex navigation
5. Test queries with `spelunk-fsharp-get-ast` to understand structure