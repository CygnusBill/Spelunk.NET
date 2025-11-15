# Statement-Level Editing Design

## Core Principle
The most effective way to edit code is at the **statement level**. This provides the right balance between precision and practicality.

## Why Statement-Level?

### Too Fine-Grained: Expression Level
- Editing `user.Name` inside a larger expression is complex
- Hard to maintain context
- Too many small operations needed

### Too Coarse: Method/Class Level  
- Replacing entire methods loses precision
- Hard to preserve unchanged code
- Risk of losing formatting/comments

### Just Right: Statement Level
- Natural unit of code logic
- Clear boundaries (semicolons, braces)
- Preserves context while allowing precise edits
- Maps to how developers think about code

## Statement Types in C#

1. **Expression Statements**
   ```csharp
   Console.WriteLine("Hello");
   user.Name = "John";
   ProcessData();
   ```

2. **Declaration Statements**
   ```csharp
   int count = 0;
   var user = new User();
   ```

3. **Control Flow Statements**
   ```csharp
   if (condition) { ... }
   while (running) { ... }
   return result;
   ```

4. **Block Statements**
   ```csharp
   {
       // Multiple statements
   }
   ```

## Proposed Statement-Level Operations

### 1. Find Statements
```yaml
spelunk-find-statements:
  pattern: "= new SqlCommand("  # Find SQL command creation
  scope: "method:ProcessData"   # Optional scope
  returns:
    - statementId: "stmt-123"
      type: "LocalDeclarationStatement"
      text: "var cmd = new SqlCommand(sql, connection);"
      location: { file: "DataAccess.cs", line: 45 }
```

### 2. Replace Statement
```yaml
spelunk-replace-statement:
  statementId: "stmt-123"
  newStatement: |
    var cmd = new SqlCommand(sql, connection);
    cmd.Parameters.AddWithValue("@name", userName);
```

### 3. Insert Statement
```yaml
spelunk-insert-statement:
  position: "after"
  referenceId: "stmt-123"  # or location
  statement: "logger.LogDebug(\"Command created\");"
```

### 4. Remove Statement
```yaml
spelunk-remove-statement:
  statementId: "stmt-123"
```

### 5. Get Statement Context
```yaml
spelunk-get-statement-context:
  statementId: "stmt-123"
  returns:
    containingMethod: "ProcessData"
    containingClass: "DataProcessor"
    localVariables: ["sql", "connection", "userName"]
    accessibleMembers: ["_logger", "ExecuteQuery"]
    dataFlow:
      inputs: ["sql parameter from BuildQuery()"]
      outputs: ["cmd used in ExecuteCommand()"]
```

## Example Workflow: SQL Parameterization

**Goal**: Find SQL concatenation and add parameters

```yaml
# 1. Find all SQL command creations
→ find-statements 
    pattern: "new SqlCommand"
    
# 2. For each statement, check the SQL string
→ get-statement-context
    statementId: "stmt-123"
    # Returns that 'sql' variable contains concatenation
    
# 3. Mark the statement for modification
→ mark-statement
    statementId: "stmt-123"
    markerId: "needs-params"
    
# 4. Find where the SQL is built
→ find-statements
    pattern: "\" + "  # String concatenation
    scope: "method:BuildQuery"
    
# 5. Replace the concatenation
→ replace-statement
    statementId: "stmt-456"
    newStatement: "return \"SELECT * FROM Users WHERE Name = @name\";"
    
# 6. Find the marked command creation
→ find-marked-statement
    markerId: "needs-params"
    
# 7. Insert parameter addition after it
→ insert-statement
    position: "after"
    referenceId: "stmt-123"
    statement: "cmd.Parameters.AddWithValue(\"@name\", userName);"
```

## Benefits for Agents

1. **Clear Boundaries**: Statements have definitive start/end
2. **Semantic Completeness**: Each statement is a complete thought
3. **Preserves Structure**: Maintains code organization
4. **Composable**: Complex refactorings from simple operations
5. **Traceable**: Can track effects through data flow

## Finding Nearest Enclosing Statement

### From Any Starting Point

Roslyn provides straightforward traversal to find the nearest enclosing statement:

```csharp
// From any starting point, get to SyntaxNode first
SyntaxNode startNode = null;

// From position
startNode = root.FindNode(new TextSpan(position, 0));

// From span  
startNode = root.FindNode(span);

// From semantic symbol
var syntaxRef = symbol.DeclaringSyntaxReferences.FirstOrDefault();
startNode = syntaxRef?.GetSyntax();

// Then find enclosing statement
var statement = startNode?.AncestorsAndSelf()
    .OfType<StatementSyntax>()
    .FirstOrDefault();
```

### Statement Presentation for Agents

The most effective presentation includes context and metadata:

```yaml
statement:
  id: "stmt-123"  # Ephemeral ID via SyntaxAnnotation
  type: "ExpressionStatement"
  text: "var cmd = new SqlCommand(sql, connection);"
  location:
    file: "DataAccess.cs"
    line: 45
    column: 8
  context:
    before: |
      var sql = BuildQuery(userName);
      logger.LogDebug("Executing query");
    after: |
      return cmd.ExecuteReader();
  indentLevel: 2
  containingMethod: "GetUserData"
  containingClass: "DataRepository"
```

### Handling Multi-Statement Spans

When an area of interest crosses multiple statements, we group them with tags:

```yaml
statementGroup:
  groupId: "group-456"  # Group identifier
  reason: "Span from line 45 to 48 covers 3 statements"
  statements:
    - id: "stmt-123"
      groupTag: "start"  # First statement in span
      type: "LocalDeclarationStatement"
      text: "var sql = BuildQuery(userName);"
      syntaxTag: "syntax-123"  # Link to syntax node
      semanticTags: ["symbol-sql-var", "symbol-BuildQuery"]  # Semantic symbols
      
    - id: "stmt-124"
      groupTag: "middle"
      type: "ExpressionStatement"  
      text: "logger.LogDebug($\"Query: {sql}\");"
      syntaxTag: "syntax-124"
      semanticTags: ["symbol-logger", "symbol-LogDebug", "symbol-sql-var"]
      
    - id: "stmt-125"
      groupTag: "end"  # Last statement in span
      type: "LocalDeclarationStatement"
      text: "var cmd = new SqlCommand(sql, connection);"
      syntaxTag: "syntax-125"
      semanticTags: ["symbol-cmd-var", "symbol-SqlCommand", "symbol-sql-var"]
  
  containingBlock:
    type: "MethodBody"
    method: "GetUserData"
  
  crossReferences:
    syntaxNodes:
      - tag: "syntax-123"
        type: "LocalDeclarationStatementSyntax"
        span: { start: 1024, length: 35 }
      - tag: "syntax-124"
        type: "ExpressionStatementSyntax"
        span: { start: 1060, length: 40 }
      - tag: "syntax-125"
        type: "LocalDeclarationStatementSyntax"
        span: { start: 1101, length: 45 }
    
    semanticSymbols:
      - tag: "symbol-sql-var"
        kind: "Local"
        type: "string"
        declaredAt: "stmt-123"
        usedAt: ["stmt-124", "stmt-125"]
      - tag: "symbol-BuildQuery"
        kind: "Method"
        containingType: "DataRepository"
```

### Operations on Statement Groups

```yaml
# Find statements in a span
spelunk-find-statements-in-span:
  file: "DataAccess.cs"
  startLine: 45
  endLine: 48
  groupResults: true  # Return as a group
  returns: statementGroup (as above)

# Replace multiple statements atomically
spelunk-replace-statement-group:
  groupId: "group-456"
  newStatements: |
    var sql = BuildQuery(userName);
    var cmd = new SqlCommand(sql, connection);
    cmd.CommandTimeout = 30;
    logger.LogDebug("Command created");

# Extract statement group to method
spelunk-extract-statements:
  groupId: "group-456"
  newMethodName: "CreateCommand"
  parameters: ["userName", "connection"]
```

This approach:
- Preserves individual statement identity
- Shows relationships between statements
- Links to both syntactic and semantic representations
- Enables group operations while maintaining granularity

## Semantic vs Syntactic Manipulation

Agents will use a **hybrid approach** combining both representations:

### Semantic for Discovery/Analysis:
- Finding all references to a symbol
- Understanding type relationships  
- Checking if a method is virtual/override
- Tracing data flow
- Validating correctness

### Syntactic for Modification:
- Actually editing code text
- Preserving formatting and style
- Simple replacements
- Adding new statements
- Managing comments and whitespace

### Why Syntactic for Edits:
1. **Familiarity**: Agents are trained on text, so string manipulation is natural
2. **Precision**: Can control exact formatting, spacing, style
3. **Simplicity**: "Replace this text with that text" is straightforward
4. **Visibility**: The agent can "see" what the code will look like

### Example Hybrid Workflow:
```yaml
# Semantic analysis first
1. Find all references to User.Name property (semantic)
2. Check each reference's context (semantic)
3. Identify which are in SQL concatenation (semantic + syntactic)

# Then syntactic modification  
4. Replace the concatenation statement (syntactic)
5. Insert parameter statement after (syntactic)
6. Preserve indentation and style (syntactic)
```

This combination leverages semantic understanding for correctness while using syntactic manipulation for practical code editing.

## Implementation Notes

- Use Roslyn's `StatementSyntax` hierarchy
- Generate stable IDs using SyntaxAnnotations
- Preserve trivia (comments, whitespace)
- Validate statement completeness after edits
- Track statement relationships for data flow
- Provide both semantic and syntactic context to agents