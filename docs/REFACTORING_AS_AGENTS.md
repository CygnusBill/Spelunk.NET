# Refactoring as Agent Workflows

## Architectural Insight

Refactorings are inherently **workflows** composed of multiple steps, not single operations. They require orchestration, decision-making, and often span multiple files. This makes them better suited for **agent orchestration** using primitive tools rather than monolithic MCP operations.

## Key Principle

> **MCP tools should be primitive building blocks. Agents should orchestrate these primitives into intelligent workflows.**

## Why Agents Are Better for Refactoring

### 1. **Context and Decision Making**
Agents can maintain context across multiple operations and make intelligent decisions based on intermediate results.

### 2. **Error Recovery**
Agents can handle failures gracefully, retry with different approaches, or ask for user guidance.

### 3. **Cross-File Operations**
Refactorings often affect multiple files. Agents can track changes across boundaries that single tools cannot.

### 4. **User Interaction**
Agents can explain what they're doing, preview changes, and get confirmation for risky operations.

### 5. **Flexibility**
Different codebases need different approaches. Agents can adapt their strategy based on what they discover.

## Current Refactorings as Agent Workflows

### 1. SQL Parameterization Refactoring

**User Request**: "Convert all SQL in this project to use Dapper"

**Agent Workflow**:

```yaml
SQL_PARAMETERIZATION_AGENT:
  description: Converts SQL queries to use parameterized queries with chosen library
  
  steps:
    1_discover:
      - tool: spelunk-find-statements
        args: 
          pattern: "SqlCommand"
          patternType: "text"
      - tool: spelunk-find-statements
        args:
          pattern: "//invocation[@contains='Query' or @contains='Execute']"
          patternType: "roslynpath"
      - tool: search_for_pattern
        args:
          pattern: "SELECT.*FROM|INSERT.*INTO|UPDATE.*SET|DELETE.*FROM"
    
    2_analyze:
      for_each_statement:
        - tool: spelunk-get-statement-context
          purpose: Get symbol info and types
        - decision: Determine SQL library (ADO.NET, Dapper, EF Core)
        - decision: Identify injection vulnerabilities
    
    3_transform:
      for_each_vulnerable_statement:
        - tool: spelunk-analyze-syntax
          purpose: Parse SQL construction pattern
        - generate: Create parameterized version
        - tool: spelunk-replace-statement
          with: Generated parameterized code
        - tool: spelunk-insert-statement
          what: Parameter addition statements
    
    4_update_signatures:
      if_using_dapper:
        - tool: spelunk-find-method
          containing: Transformed statement
        - decision: Determine new return type
        - tool: spelunk-edit-code
          operation: modify-method
          change: Update return type
        - tool: spelunk-find-method-callers
          purpose: Find all callers to update
        - for_each_caller:
          - tool: spelunk-get-statement-context
          - tool: spelunk-replace-statement
            with: Updated call pattern
    
    5_verify:
      - tool: spelunk-workspace-status
        check: Compilation still succeeds
      - report: Summary of changes made
```

### 2. Convert to Async Refactoring

**User Request**: "Make all database operations async"

**Agent Workflow**:

```yaml
ASYNC_CONVERSION_AGENT:
  description: Converts synchronous operations to async/await pattern
  
  steps:
    1_identify_targets:
      - tool: spelunk-find-method
        args:
          methodPattern: "*"
      - filter: Methods containing I/O operations
      - tool: spelunk-find-statements
        args:
          pattern: "//invocation[File.Read* or Stream.Read* or SqlCommand.Execute*]"
          patternType: "roslynpath"
    
    2_analyze_call_tree:
      for_each_method:
        - tool: spelunk-find-method-callers
          purpose: Build call dependency graph
        - decision: Determine conversion order (leaf to root)
    
    3_convert_methods:
      for_each_method_in_order:
        - tool: spelunk-edit-code
          operation: make-async
        - tool: spelunk-find-statements
          pattern: Async-capable calls
        - for_each_statement:
          - tool: spelunk-replace-statement
            with: Await version
    
    4_propagate_changes:
      for_each_converted_method:
        - tool: spelunk-find-method-callers
        - for_each_caller:
          - tool: spelunk-get-statement-context
          - decision: Add await or convert caller to async
          - tool: spelunk-replace-statement or spelunk-edit-code
    
    5_handle_interfaces:
      - tool: spelunk-find-implementations
      - tool: spelunk-find-overrides
      - update_all: Ensure consistency
```

### 3. Add Logging Refactoring

**User Request**: "Replace all Console.WriteLine with proper logging"

**Agent Workflow**:

```yaml
LOGGING_CONVERSION_AGENT:
  description: Converts console output to structured logging
  
  steps:
    1_analyze_current_state:
      - tool: spelunk-find-statements
        args:
          pattern: "Console.WriteLine"
      - tool: spelunk-find-class
        args:
          pattern: "*"
      - decision: Determine if logger is already available
    
    2_setup_logging:
      if_no_logger:
        - tool: spelunk-find-class
          purpose: Find appropriate classes for logger injection
        - for_each_class:
          - tool: spelunk-edit-code
            operation: add-property
            code: "private readonly ILogger<ClassName> _logger;"
          - tool: spelunk-edit-code
            operation: add-parameter
            to: Constructor
            parameter: "ILogger<ClassName> logger"
          - tool: spelunk-insert-statement
            position: after
            statement: "_logger = logger;"
    
    3_convert_statements:
      for_each_console_writeline:
        - tool: spelunk-get-statement-context
        - analyze: Determine log level from context
        - parse: Extract message and parameters
        - tool: spelunk-replace-statement
          with: "_logger.LogInformation(...)"
    
    4_add_using:
      if_needed:
        - tool: spelunk-insert-before-symbol
          statement: "using Microsoft.Extensions.Logging;"
```

### 4. Extract Method Refactoring

**User Request**: "Extract this code block into a separate method"

**Agent Workflow**:

```yaml
EXTRACT_METHOD_AGENT:
  description: Extracts code into a new method with proper parameters
  
  steps:
    1_analyze_selection:
      - tool: spelunk-get-data-flow
        args:
          file: Selected file
          startLine: Selection start
          endLine: Selection end
      - extract: Variables flowing in (parameters)
      - extract: Variables flowing out (return values)
      - extract: Variables used locally
    
    2_generate_signature:
      - decision: Determine return type (void, single value, tuple)
      - decision: Generate meaningful method name
      - generate: Method signature with parameters
    
    3_create_method:
      - tool: spelunk-find-class
        containing: Original code
      - tool: spelunk-edit-code
        operation: add-method
        code: Generated method
    
    4_replace_original:
      - generate: Method call with arguments
      - tool: spelunk-replace-statement
        args:
          newStatement: Method call
          
    5_handle_returns:
      if_returns_values:
        - tool: spelunk-insert-statement
          before: Method call
          statement: Variable declarations for returns
```

### 5. Implement Interface Refactoring

**User Request**: "Implement IDisposable pattern"

**Agent Workflow**:

```yaml
IMPLEMENT_INTERFACE_AGENT:
  description: Properly implements an interface with all required members
  
  steps:
    1_analyze_interface:
      - tool: spelunk-get-symbols
        args:
          symbolName: "IDisposable"
      - extract: Required methods and properties
      - decision: Determine implementation pattern
    
    2_add_interface:
      - tool: spelunk-edit-code
        operation: add-interface
        interface: "IDisposable"
    
    3_implement_members:
      - tool: spelunk-edit-code
        operation: add-method
        code: "public void Dispose() { ... }"
      - tool: spelunk-edit-code
        operation: add-method
        code: "protected virtual void Dispose(bool disposing) { ... }"
      - tool: spelunk-edit-code
        operation: add-method
        code: "~ClassName() { ... }"
    
    4_find_resources:
      - tool: spelunk-find-property
        pattern: "*"
      - filter: IDisposable types
      - for_each_resource:
        - tool: spelunk-insert-statement
          in: Dispose method
          statement: "resource?.Dispose();"
```

## How Agents Use Primitive Tools

### Pattern: Discover → Analyze → Transform → Verify

1. **Discovery Phase** - Use search tools to find targets:
   - `spelunk-find-statements` with SpelunkPath
   - `spelunk-find-method`, `spelunk-find-class`
   - `search_for_pattern` for flexible searches

2. **Analysis Phase** - Understand the code:
   - `spelunk-get-statement-context` for semantic info
   - `spelunk-get-data-flow` for dependencies
   - `spelunk-find-references` for impact analysis

3. **Transform Phase** - Make changes:
   - `spelunk-replace-statement` for precise edits
   - `spelunk-insert-statement` for additions
   - `spelunk-edit-code` for structural changes

4. **Verification Phase** - Ensure correctness:
   - `spelunk-workspace-status` for compilation
   - `spelunk-find-marked-statements` to track changes
   - Generate summary reports

## Agent Instructions Template

```markdown
## [AGENT_NAME] - [Description]

You are a specialized refactoring agent that [specific purpose].

### Your Workflow:

1. **Discovery**: First, find all instances of [pattern] using:
   - Use `spelunk-find-statements` with pattern "[specific pattern]"
   - If working with methods, use `spelunk-find-method`
   - For broad searches, use `search_for_pattern`

2. **Analysis**: For each discovered item:
   - Use `spelunk-get-statement-context` to understand the code
   - Check for [specific conditions]
   - Determine [specific decisions]

3. **Transformation**: Apply these changes:
   - [Specific transformation rules]
   - Use `spelunk-replace-statement` for line-level changes
   - Use `spelunk-insert-statement` for additions
   - Use `spelunk-edit-code` for structural changes

4. **Propagation**: Handle ripple effects:
   - Find affected code with `spelunk-find-references`
   - Update callers with `spelunk-find-method-callers`
   - Ensure consistency across interfaces/implementations

5. **Verification**: Ensure success:
   - Check compilation with `spelunk-workspace-status`
   - Report what was changed
   - Suggest manual review points

### Important Considerations:
- [Specific gotchas for this refactoring]
- [When to ask for user input]
- [How to handle errors]

### Example Usage:
When user says: "[example request]"
You should: [specific steps]
```

## Benefits of the Agent Approach

### 1. **Composability**
Agents can combine primitive tools in unlimited ways, enabling refactorings we haven't explicitly programmed.

### 2. **Adaptability**
Agents can adjust their approach based on what they discover, handling edge cases intelligently.

### 3. **Transparency**
Users can see exactly what the agent is doing at each step, building trust and enabling learning.

### 4. **Interruptibility**
Complex refactorings can be paused, reviewed, and resumed, giving users control.

### 5. **Evolution**
New refactoring patterns emerge naturally as agents discover new ways to combine tools.

## Migration Strategy

### Phase 1: Document Patterns (Current)
- Document each refactoring as an agent workflow
- Create clear instructions for agents

### Phase 2: Create Agent Specifications
- Formalize agent descriptions
- Test with Claude Code subagents

### Phase 3: Deprecate Monolithic Tools
- Mark transformation tools as deprecated
- Guide users to agent approach

### Phase 4: Enhance Primitive Tools
- Add any missing primitive operations
- Improve error messages for agent use

## Example: SQL Parameterization Agent in Action

```yaml
User: "Convert all SQL in this file to use Dapper"

Agent: I'll help you convert SQL queries to use Dapper. Let me analyze your code first.

Step 1 - Finding SQL usage:
[Calls spelunk-find-statements with "SqlCommand"]
Found 3 SQL command instances

Step 2 - Analyzing each query:
[Calls spelunk-get-statement-context for each]
- Line 25: Simple SELECT with 2 parameters
- Line 45: INSERT with 3 parameters  
- Line 67: Complex JOIN with dynamic WHERE

Step 3 - Converting to Dapper:
[For each statement:]
  - Extracting SQL and parameters
  - Generating Dapper query call
  - Creating anonymous parameter object
  [Calls spelunk-replace-statement]

Step 4 - Updating method signatures:
[Calls spelunk-find-method for containing methods]
Method GetUser needs to return User instead of DataTable
[Calls spelunk-edit-code to update return type]

Step 5 - Updating callers:
[Calls spelunk-find-method-callers]
Found 2 callers that need updates
[Updates each caller appropriately]

✅ Conversion complete! 
- 3 SQL queries converted to Dapper
- 2 method signatures updated
- 2 callers updated
- All changes maintain type safety

Would you like me to proceed with other files?
```

## Conclusion

Refactorings are workflows, not operations. By moving refactoring logic from monolithic MCP tools to agent orchestration, we:

1. **Simplify the MCP server** - Focus on robust primitive tools
2. **Empower agents** - Let them combine primitives creatively
3. **Improve user experience** - Transparent, interruptible workflows
4. **Enable evolution** - New refactoring patterns emerge naturally

The future of refactoring is intelligent agents orchestrating simple tools, not complex monolithic operations.