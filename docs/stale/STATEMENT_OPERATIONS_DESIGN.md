# Statement-Level Operations Design

## Core Concepts

### Statement Identification
Each statement in a C# file will be identified using:
1. **Statement ID**: Ephemeral identifier using SyntaxAnnotations (e.g., "stmt-123")
2. **Location**: File path + line number as fallback
3. **Pattern**: Text pattern for finding statements

### Statement Types
- **Expression Statements**: Method calls, assignments
- **Declaration Statements**: Variable declarations
- **Control Flow**: if, while, for, return, throw
- **Block Statements**: Code blocks with braces

## Tool Definitions

### 1. dotnet-find-statements
Find statements matching a pattern within a scope.

```yaml
name: dotnet-find-statements
description: |
  Find statements in code matching a pattern. Returns statement IDs for use with other operations.
  Patterns can be partial text matches or regex patterns.
  Scope can limit search to specific methods, classes, or files.
  Can group results when multiple statements form a logical unit.
parameters:
  pattern:
    type: string
    description: Text or regex pattern to match in statements
    required: true
  scope:
    type: object
    properties:
      file:
        type: string
        description: File path to search in
      className:
        type: string
        description: Class name to search within
      methodName:
        type: string
        description: Method name to search within
  patternType:
    type: string
    enum: [text, regex]
    default: text
    description: Type of pattern matching
  includeNestedStatements:
    type: boolean
    default: false
    description: Include statements inside blocks (if/while/for bodies)
  groupRelated:
    type: boolean
    default: false
    description: Group statements that share data flow or are in sequence

returns:
  statements:
    - statementId: string
      type: string (ExpressionStatement, LocalDeclarationStatement, etc.)
      text: string (the actual statement text)
      location:
        file: string
        line: number
        column: number
      containingMethod: string
      containingClass: string
      syntaxTag: string (reference to syntax node)
      semanticTags: string[] (references to semantic symbols)
      groupId: string (if part of a group)
```

### 1a. dotnet-find-statements-in-span
Find all statements within a file span.

```yaml
name: dotnet-find-statements-in-span
description: |
  Find all statements within a span of lines in a file.
  Useful when working with a specific area of code.
  Can return results as individual statements or as a group.
parameters:
  file:
    type: string
    required: true
    description: File path to search in
  startLine:
    type: number
    required: true
    description: Starting line number (1-based)
  endLine:
    type: number
    required: true
    description: Ending line number (inclusive)
  groupResults:
    type: boolean
    default: false
    description: Return statements as a group with relationships

returns:
  # If groupResults is false:
  statements: (same as find-statements)
  
  # If groupResults is true:
  statementGroup:
    groupId: string
    reason: string
    statements:
      - id: string
        groupTag: string (start, middle, end)
        type: string
        text: string
        syntaxTag: string
        semanticTags: string[]
    crossReferences:
      syntaxNodes: []
      semanticSymbols: []
```

### 2. dotnet-replace-statement
Replace an entire statement with new code.

```yaml
name: dotnet-replace-statement
description: |
  Replace a statement with new code. The new code must be a valid statement.
  Preserves indentation and formatting context.
parameters:
  statementId:
    type: string
    description: Statement ID from find-statements
    required: true
  newStatement:
    type: string
    description: The new statement code (including semicolon)
    required: true
  preserveComments:
    type: boolean
    default: true
    description: Keep existing comments attached to the statement

returns:
  success: boolean
  modifiedFile: string
  preview: string (shows before/after context)
```

### 3. dotnet-insert-statement
Insert a statement before or after another statement.

```yaml
name: dotnet-insert-statement
description: |
  Insert a new statement relative to an existing statement.
  Automatically handles indentation and formatting.
parameters:
  position:
    type: string
    enum: [before, after]
    required: true
    description: Where to insert relative to reference
  referenceId:
    type: string
    description: Statement ID to insert relative to (use either this or location)
  location:
    type: object
    properties:
      file: string
      line: number
    description: Alternative to referenceId
  statement:
    type: string
    required: true
    description: The statement to insert (including semicolon)
  
returns:
  success: boolean
  modifiedFile: string
  insertedAt:
    line: number
    column: number
```

### 4. dotnet-remove-statement
Remove a statement from the code.

```yaml
name: dotnet-remove-statement
description: |
  Remove a statement and its associated trivia (comments, whitespace).
  Maintains proper formatting of surrounding code.
parameters:
  statementId:
    type: string
    required: true
    description: Statement ID from find-statements
  removeComments:
    type: boolean
    default: true
    description: Also remove comments associated with the statement

returns:
  success: boolean
  modifiedFile: string
  removedText: string
```

### 5. dotnet-get-statement-context
Get semantic and syntactic context for a statement.

```yaml
name: dotnet-get-statement-context
description: |
  Get detailed context about a statement including semantic information,
  data flow, and relationships to other code elements.
parameters:
  statementId:
    type: string
    required: true
    description: Statement ID from find-statements

returns:
  syntactic:
    type: string (statement type)
    parentStatement: string (if nested)
    containingBlock: string
    containingMethod: string
    containingClass: string
  semantic:
    symbols:
      - name: string
        kind: string (method, property, variable, etc.)
        type: string
        definedAt: location
    dataFlow:
      inputs:
        - symbol: string
          source: string (where value comes from)
      outputs:
        - symbol: string
          usage: string (where value is used)
    dependencies:
      - calls: string[] (methods called)
      - accesses: string[] (fields/properties accessed)
  scope:
    localVariables: string[]
    parameters: string[]
    accessibleMembers: string[]
```

### 6. dotnet-replace-statement-group
Replace multiple statements atomically.

```yaml
name: dotnet-replace-statement-group
description: |
  Replace a group of statements with new code atomically.
  Preserves proper indentation and formatting.
  All statements must be replaced or none are.
parameters:
  groupId:
    type: string
    required: true
    description: Group ID from find-statements-in-span
  newStatements:
    type: string
    required: true
    description: The new statements to replace the group (multi-line string)
  preserveComments:
    type: boolean
    default: true
    description: Keep existing comments from the original statements

returns:
  success: boolean
  modifiedFile: string
  replacedCount: number
  preview: string (shows before/after context)
```

### 7. dotnet-extract-statements
Extract a group of statements into a new method.

```yaml
name: dotnet-extract-statements
description: |
  Extract selected statements into a new method.
  Automatically determines parameters and return values.
  Replaces original statements with method call.
parameters:
  groupId:
    type: string
    description: Group ID for multiple statements
  statementIds:
    type: string[]
    description: Array of statement IDs (alternative to groupId)
  newMethodName:
    type: string
    required: true
    description: Name for the extracted method
  accessibility:
    type: string
    enum: [private, protected, internal, public]
    default: private
    description: Access modifier for new method
  makeStatic:
    type: boolean
    default: false
    description: Whether the method should be static

returns:
  success: boolean
  extractedMethod:
    signature: string
    body: string
    location: object
  replacementCall:
    text: string
    location: object
  detectedParameters: string[]
  detectedReturnType: string
```

## Implementation Strategy

### Phase 1: Core Infrastructure
1. Create StatementInfo class to hold statement data
2. Implement SyntaxAnnotation-based ID generation
3. Create statement visitor to traverse syntax trees

### Phase 2: Find Operations
1. Implement pattern matching for text/regex
2. Add scope filtering (file/class/method)
3. Handle nested statements properly

### Phase 3: Modification Operations
1. Implement replace with formatting preservation
2. Implement insert with auto-indentation
3. Implement remove with trivia handling

### Phase 4: Context Analysis
1. Integrate semantic model for symbol info
2. Implement basic data flow analysis
3. Add dependency tracking

## Usage Examples

### Example 1: SQL Parameterization
```yaml
# Find SQL command creation
→ find-statements
    pattern: "new SqlCommand("
    
# Get context to understand variables
→ get-statement-context
    statementId: "stmt-123"
    # Returns: sql variable contains concatenation
    
# Find the concatenation
→ find-statements
    pattern: "\" + "
    scope: { methodName: "BuildQuery" }
    
# Replace with parameterized version
→ replace-statement
    statementId: "stmt-456"
    newStatement: "return \"SELECT * FROM Users WHERE Name = @name\";"
    
# Add parameter to command
→ insert-statement
    position: "after"
    referenceId: "stmt-123"
    statement: "cmd.Parameters.AddWithValue(\"@name\", userName);"
```

### Example 2: Add Logging
```yaml
# Find all database operations
→ find-statements
    pattern: "ExecuteNonQuery()"
    
# For each, insert logging before
→ insert-statement
    position: "before"
    referenceId: "stmt-789"
    statement: "_logger.LogDebug(\"Executing database command\");"
```

## Benefits

1. **Natural Granularity**: Statements are how developers think
2. **Safe Operations**: Can't create invalid syntax
3. **Preserves Context**: Maintains formatting and comments
4. **Composable**: Complex refactorings from simple operations
5. **Traceable**: Can track changes through IDs

## Technical Considerations

1. **Performance**: Cache annotated syntax trees during session
2. **Consistency**: Apply all changes through single workspace update
3. **Validation**: Ensure modified code compiles
4. **Preview**: Show before/after for all operations
5. **Undo**: Track changes for potential rollback