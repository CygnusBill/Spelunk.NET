# RoslynPath Parser Redesign

## Current Problems

### 1. Linear Token Processing
The current parser tries to handle complex patterns linearly, but patterns like `[Get*User]` or `[*User*Id]` require understanding that wildcards and identifiers form a single pattern unit.

**Current behavior:**
- Lexer: `[`, `Get`, `*`, `User`, `]` → 5 tokens
- Parser: Expects `]` after `Get`, fails

**Needed behavior:**
- Pattern recognition: `[Get*User]` → predicate with wildcard pattern

### 2. AND/OR Creates Expression Trees
With boolean operators, predicates are no longer linear filters but expression trees:

```
[not(@private) and (@async or @static)]
```

This creates an expression tree:
```
        AND
       /   \
    NOT     OR
     |     /  \
@private @async @static
```

Current implementation tries to handle this with nested predicate objects but evaluation is still linear.

### 3. Nested Path Predicates
Patterns like `[.//throw-statement]` require:
1. Save current node context
2. Evaluate sub-path from that context  
3. Return true if sub-path has any matches

Current implementation treats this as a string, losing the path semantics.

## Proposed Solution

### Phase 1: Context-Aware Lexer
```csharp
public class RoslynPathLexer
{
    private enum LexerContext
    {
        Default,
        InPredicate,
        AfterAt,
        InWildcardPattern
    }
    
    public List<Token> Tokenize(string input)
    {
        var tokens = new List<Token>();
        var context = LexerContext.Default;
        
        while (!AtEnd())
        {
            switch (context)
            {
                case LexerContext.InPredicate:
                    tokens.Add(TokenizePredicateContent());
                    break;
                case LexerContext.InWildcardPattern:
                    tokens.Add(TokenizeWildcardPattern());
                    break;
                default:
                    tokens.Add(TokenizeDefault());
                    break;
            }
        }
        
        return tokens;
    }
    
    private Token TokenizeWildcardPattern()
    {
        // Consume entire pattern like "Get*User" or "*User*Id" as single token
        var pattern = new StringBuilder();
        while (!AtEnd() && IsPatternChar(Peek()))
        {
            pattern.Append(Read());
        }
        return new Token(TokenType.Pattern, pattern.ToString());
    }
}
```

### Phase 2: Proper AST Structure
```csharp
// Expression tree for predicates
public abstract class PredicateExpr
{
    public abstract bool Evaluate(SyntaxNode node, IEvaluationContext context);
}

public class AndExpr : PredicateExpr
{
    public PredicateExpr Left { get; set; }
    public PredicateExpr Right { get; set; }
    
    public override bool Evaluate(SyntaxNode node, IEvaluationContext context)
    {
        // Short-circuit evaluation
        return Left.Evaluate(node, context) && Right.Evaluate(node, context);
    }
}

public class OrExpr : PredicateExpr
{
    public PredicateExpr Left { get; set; }
    public PredicateExpr Right { get; set; }
    
    public override bool Evaluate(SyntaxNode node, IEvaluationContext context)
    {
        // Short-circuit evaluation
        return Left.Evaluate(node, context) || Right.Evaluate(node, context);
    }
}

public class NotExpr : PredicateExpr
{
    public PredicateExpr Inner { get; set; }
    
    public override bool Evaluate(SyntaxNode node, IEvaluationContext context)
    {
        return !Inner.Evaluate(node, context);
    }
}

public class PathPredicateExpr : PredicateExpr
{
    public PathExpression Path { get; set; }
    
    public override bool Evaluate(SyntaxNode node, IEvaluationContext context)
    {
        // Evaluate path starting from current node
        var subContext = context.CreateSubContext(node);
        var results = Path.Evaluate(subContext);
        return results.Any();
    }
}
```

### Phase 3: Evaluation Context
```csharp
public interface IEvaluationContext
{
    SyntaxNode CurrentNode { get; }
    SyntaxTree Tree { get; }
    IEvaluationContext CreateSubContext(SyntaxNode newRoot);
    IEnumerable<SyntaxNode> EvaluatePath(PathExpression path);
}

public class EvaluationContext : IEvaluationContext
{
    private readonly SyntaxNode _root;
    private readonly SyntaxTree _tree;
    
    public SyntaxNode CurrentNode => _root;
    public SyntaxTree Tree => _tree;
    
    public IEvaluationContext CreateSubContext(SyntaxNode newRoot)
    {
        return new EvaluationContext(newRoot, _tree);
    }
    
    public IEnumerable<SyntaxNode> EvaluatePath(PathExpression path)
    {
        // Evaluate path from current context root
        var evaluator = new PathEvaluator(this);
        return evaluator.Evaluate(path);
    }
}
```

## Benefits of This Architecture

1. **Correctness**: Proper handling of complex boolean expressions
2. **Extensibility**: Easy to add new operators or predicate types
3. **Performance**: Short-circuit evaluation, potential for compilation
4. **Debugging**: Clear AST structure makes debugging easier
5. **Testing**: Each component can be tested independently

## Implementation Plan

### Step 1: Keep Current API
```csharp
public class RoslynPathEvaluator
{
    public IEnumerable<SyntaxNode> Evaluate(string path)
    {
        // Use new implementation internally
        var ast = new RoslynPathParser().Parse(path);
        var context = new EvaluationContext(_tree.GetRoot(), _tree);
        return ast.Evaluate(context);
    }
}
```

### Step 2: Gradual Migration
1. Implement new lexer that handles wildcards correctly
2. Build AST structure for predicates
3. Implement graph-based evaluation
4. Add comprehensive tests
5. Switch implementation

### Step 3: Optimize Common Patterns
Cache and compile frequently used patterns:
```csharp
private static readonly ConcurrentDictionary<string, CompiledPath> _cache = new();

public IEnumerable<SyntaxNode> Evaluate(string path)
{
    var compiled = _cache.GetOrAdd(path, p => CompilePath(p));
    return compiled.Evaluate(_tree);
}
```

## Example: Complex Query Evaluation

Query: `//method[@async and not(@private) and [.//await-expression]]`

### AST Representation:
```
PathExpression
├─ Step: DescendantOrSelf
│  ├─ NodeTest: "method"
│  └─ Predicate: AND
│     ├─ Left: Attribute(@async)
│     └─ Right: AND
│        ├─ Left: NOT
│        │  └─ Attribute(@private)
│        └─ Right: PathPredicate
│           └─ Path: .//await-expression
```

### Evaluation Flow:
1. Find all method nodes (descendant-or-self::method)
2. For each method:
   - Check if @async is true
   - If true, check if NOT @private
   - If true, evaluate .//await-expression from method context
   - If path returns any nodes, predicate is true
3. Return methods where all predicates are true

## Conclusion

The current linear approach cannot properly handle the graph nature of boolean expressions and nested predicates. A proper AST-based implementation with context-aware evaluation is necessary for correctness and maintainability.