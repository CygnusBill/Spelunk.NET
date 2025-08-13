# RoslynPath Parser Redesign Needed

## Date: 2025-08-13

## Current State
- Created comprehensive test suite with 42 tests
- 13 tests failing due to parser limitations
- Attempted ad-hoc fixes but fundamental architecture issues remain

## Core Problems Identified

### 1. Linear Token Processing
- Wildcard patterns like `[Get*User]` are tokenized as separate tokens
- Parser expects closing bracket after first token, fails
- Need context-aware lexer that combines pattern tokens

### 2. Boolean Expression Trees
- AND/OR operators create expression trees, not linear filters
- Current implementation tries linear evaluation
- Example: `[not(@private) and (@async or @static)]` needs tree evaluation

### 3. Nested Path Predicates
- Patterns like `[.//throw-statement]` need sub-query evaluation
- Current parser treats these as strings
- Need to evaluate path from current node context

## Solution Approach

### Phase 1: Context-Aware Lexer
- Track lexer context (in predicate, after @, in pattern)
- Combine tokens appropriately (wildcards + identifiers)
- Handle operators like `~=` as single tokens

### Phase 2: Proper AST
- PredicateExpr base class with Evaluate method
- AndExpr, OrExpr, NotExpr for boolean logic
- PathPredicateExpr for nested path evaluation
- Short-circuit evaluation for performance

### Phase 3: Evaluation Context
- IEvaluationContext to track current node
- Support for sub-contexts in nested predicates
- Graph-based evaluation instead of linear

## Files Created
- docs/roslyn-path/ROSLYN_PATH_GRAMMAR.md - Formal BNF grammar
- docs/roslyn-path/ROSLYN_PATH_REDESIGN.md - Detailed redesign proposal

## Next Steps
1. Implement context-aware lexer
2. Build proper AST structure  
3. Create graph-based evaluator
4. Migrate incrementally while keeping API stable

## Test Status
- 29 passing tests demonstrate core functionality works
- 13 failing tests identify specific parser limitations
- Tests provide excellent regression suite for redesign