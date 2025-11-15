# SpelunkPath Grammar Specification

## Overview
SpelunkPath is an XPath-inspired query language for navigating and querying .NET syntax trees.
This document provides the formal BNF grammar and architectural considerations.

## BNF Grammar

```bnf
# Top-level expression
path_expression     ::= absolute_path | relative_path
absolute_path       ::= "/" relative_path
relative_path       ::= step_expression ("/" step_expression)*

# Step expressions
step_expression     ::= axis_step | abbreviated_step
axis_step          ::= axis "::" node_test predicate_list
abbreviated_step   ::= ".." | "//" node_test predicate_list | node_test predicate_list

# Axes
axis               ::= "ancestor" | "ancestor-or-self" | "child" | "descendant" 
                     | "descendant-or-self" | "following" | "following-sibling" 
                     | "parent" | "preceding" | "preceding-sibling" | "self"

# Node tests
node_test          ::= wildcard | node_type | node_name
wildcard           ::= "*"
node_type          ::= enhanced_node_type | standard_node_type
enhanced_node_type ::= "if-statement" | "while-statement" | "for-statement" 
                     | "foreach-statement" | "do-statement" | "switch-statement"
                     | "try-statement" | "throw-statement" | "return-statement"
                     | "using-statement" | "lock-statement" | "binary-expression"
                     | "unary-expression" | "literal" | "invocation" | "member-access"
                     | "assignment" | "conditional" | "lambda" | "await-expression"
                     | "object-creation" | "array-creation" | "element-access"
                     | "cast-expression" | "typeof-expression" | "query-expression"
                     | "local-declaration" | "expression-statement" | "block"
standard_node_type ::= "namespace" | "class" | "interface" | "struct" | "enum"
                     | "method" | "property" | "field" | "constructor" | "statement"
                     | "expression" | "parameter" | "attribute"
node_name          ::= identifier | wildcard_pattern
wildcard_pattern   ::= (identifier | "*" | "?")+ 

# Predicates
predicate_list     ::= ("[" predicate_expr "]")*
predicate_expr     ::= or_expr
or_expr            ::= and_expr ("or" and_expr)*
and_expr           ::= not_expr ("and" not_expr)*
not_expr           ::= "not" "(" predicate_expr ")" | primary_expr
primary_expr       ::= position_expr | attribute_expr | path_expr | name_expr | "(" predicate_expr ")"

# Position predicates
position_expr      ::= number | "last()" | "last()" "-" number

# Attribute predicates  
attribute_expr     ::= "@" attribute_name (operator attribute_value)?
attribute_name     ::= identifier
operator           ::= "=" | "~=" | "!=" | "<" | ">" | "<=" | ">="
attribute_value    ::= string_literal | number | identifier

# Path predicates (nested paths)
path_expr          ::= "." relative_path | relative_path

# Name predicates
name_expr          ::= node_name

# Lexical elements
identifier         ::= [a-zA-Z_][a-zA-Z0-9_-]*
string_literal     ::= '"' [^"]* '"' | "'" [^']* "'"
number             ::= [0-9]+
```

## Architectural Issues with Current Implementation

### 1. Evaluation Strategy Problem
The current evaluator processes predicates linearly, but with AND/OR operators, we need:
- **Expression tree evaluation**: Build an AST for predicates and evaluate recursively
- **Short-circuit evaluation**: OR should stop at first true, AND at first false
- **Context preservation**: Each predicate needs access to the current node context

### 2. Predicate Context Issues
Nested path predicates like `[.//throw-statement]` require:
- **Sub-expression evaluation**: Run a full path query within the predicate context
- **Relative path resolution**: The `.` should refer to the current node being tested
- **Result interpretation**: Path predicates are true if they return any matches

### 3. Tokenization Challenges
Current issues:
- **Wildcard patterns**: `Get*User` should be treated as a single pattern, not three tokens
- **Operator ambiguity**: `~=` vs `~` followed by `=`
- **Context-sensitive tokens**: `-` can be minus operator or part of identifier

### 4. Parser Architecture Limitations
The current recursive descent parser has issues with:
- **Lookahead requirements**: Need multiple token lookahead for disambiguation
- **Error recovery**: No backtracking or error recovery
- **Precedence handling**: AND/OR/NOT precedence not clearly defined

## Proposed Architecture Improvements

### 1. Two-Phase Parsing
```csharp
// Phase 1: Tokenize with context awareness
public class ContextAwareLexer
{
    // Track context (in predicate, after @, etc.)
    // Combine tokens when appropriate (wildcards, operators)
}

// Phase 2: Build proper AST
public class SpelunkPathAst
{
    public abstract class PredicateExpression { }
    public class BinaryPredicate : PredicateExpression 
    {
        public PredicateExpression Left { get; set; }
        public string Operator { get; set; } // "and", "or"
        public PredicateExpression Right { get; set; }
    }
    public class UnaryPredicate : PredicateExpression
    {
        public string Operator { get; set; } // "not"
        public PredicateExpression Operand { get; set; }
    }
    public class PathPredicate : PredicateExpression
    {
        public PathExpression Path { get; set; }
    }
}
```

### 2. Graph-Based Evaluation
```csharp
public class PredicateEvaluator
{
    public bool Evaluate(SyntaxNode context, PredicateExpression expr)
    {
        return expr switch
        {
            BinaryPredicate binary => binary.Operator switch
            {
                "and" => Evaluate(context, binary.Left) && Evaluate(context, binary.Right),
                "or" => Evaluate(context, binary.Left) || Evaluate(context, binary.Right),
                _ => false
            },
            UnaryPredicate unary => unary.Operator == "not" 
                ? !Evaluate(context, unary.Operand)
                : false,
            PathPredicate path => EvaluatePath(context, path.Path).Any(),
            _ => false
        };
    }
}
```

### 3. Predicate Compilation (Optional Optimization)
For frequently used queries, compile predicates to delegates:
```csharp
public class CompiledPredicate
{
    private readonly Func<SyntaxNode, bool> _evaluator;
    
    public static CompiledPredicate Compile(PredicateExpression expr)
    {
        // Generate expression tree and compile to delegate
        // Cache for reuse
    }
}
```

## Implementation Priority

1. **Critical**: Fix tokenization of wildcard patterns and operators
2. **High**: Implement proper AST for predicates with AND/OR/NOT
3. **High**: Support nested path predicates `[.//pattern]`
4. **Medium**: Add proper error messages with position info
5. **Low**: Optimize with predicate compilation

## Testing Strategy

1. **Unit tests**: Each grammar production rule
2. **Integration tests**: Complex real-world patterns
3. **Performance tests**: Large syntax trees with complex predicates
4. **Cross-language tests**: C# and VB.NET compatibility

## Migration Path

1. Keep existing API surface
2. Rewrite lexer with context awareness
3. Build proper AST structure
4. Implement graph-based evaluator
5. Gradually deprecate old implementation