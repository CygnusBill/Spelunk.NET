# Surgical Code Edit Interface Design

## Goal
Provide precise, safe code modifications using Roslyn's syntax tree manipulation capabilities.

## Proposed Interfaces

### 1. **Syntax-Based Edit Tool**
Edit code by targeting specific syntax nodes with precise selectors.

```
dotnet-edit-syntax
{
  "file": "path/to/file.cs",
  "edits": [
    {
      "find": {
        "type": "MethodDeclaration",
        "name": "ProcessUser",
        "container": "UserController"
      },
      "action": "rename",
      "newName": "ProcessUserAsync"
    },
    {
      "find": {
        "type": "Parameter", 
        "name": "id",
        "parent": "GetUser"
      },
      "action": "changeType",
      "newType": "Guid"
    }
  ]
}
```

### 2. **Code Action Tool**
Apply Roslyn code fixes and refactorings (like IDE quick fixes).

```
dotnet-apply-code-action
{
  "file": "path/to/file.cs",
  "line": 42,
  "column": 15,
  "action": "Add async modifier",  // or action ID
  "preview": true
}
```

Available actions:
- Add using directive
- Make method async
- Extract method
- Inline variable
- Convert to expression body
- Add null check
- Generate constructor
- Implement interface

### 3. **Template-Based Insertion**
Insert code at specific locations using templates.

```
dotnet-insert-code
{
  "file": "path/to/file.cs",
  "position": {
    "after": {
      "type": "Method",
      "name": "GetUser"
    }
  },
  "template": "method",
  "code": "public async Task<User> GetUserByEmailAsync(string email)\n{\n    return await _repository.FindByEmailAsync(email);\n}"
}
```

Position options:
- `before/after`: { type, name }
- `inside`: { type, name, position: "start"|"end" }
- `replace`: { type, name }

### 4. **Attribute & Annotation Tool**
Add/remove attributes and annotations.

```
dotnet-modify-attributes
{
  "file": "path/to/file.cs",
  "target": {
    "type": "Class|Method|Property",
    "name": "UserController"
  },
  "add": [
    "[Authorize(Roles = \"Admin\")]",
    "[ApiController]"
  ],
  "remove": ["Obsolete"]
}
```

### 5. **Member Management Tool**
Add, remove, or modify class members.

```
dotnet-modify-members
{
  "file": "path/to/file.cs",
  "class": "UserService",
  "actions": [
    {
      "add": "property",
      "name": "Logger",
      "type": "ILogger<UserService>",
      "accessors": "{ get; }",
      "modifiers": ["private", "readonly"]
    },
    {
      "add": "constructor-parameter",
      "name": "logger",
      "type": "ILogger<UserService>",
      "assignToField": "_logger"
    }
  ]
}
```

### 6. **Statement-Level Edits**
Modify code within method bodies.

```
dotnet-edit-statements
{
  "file": "path/to/file.cs",
  "method": "ProcessData",
  "class": "DataProcessor",
  "edits": [
    {
      "find": "var result = Calculate(x);",
      "replace": "var result = await CalculateAsync(x);"
    },
    {
      "after": "logger.LogInfo(\"Starting\");",
      "insert": "var stopwatch = Stopwatch.StartNew();"
    },
    {
      "wrap": {
        "from": "Connection.Open();",
        "to": "Connection.Close();",
        "with": "try-finally"
      }
    }
  ]
}
```

### 7. **Semantic Patch Tool**
Apply semantic transformations across multiple files.

```
dotnet-apply-semantic-patch
{
  "pattern": {
    "type": "MethodCall",
    "method": "string.Format",
    "arguments": ["literal", "*"]
  },
  "transform": "StringInterpolation",
  "scope": "Solution",
  "preview": true
}
```

Common transforms:
- String.Format → String interpolation
- Task.Run → await
- foreach → LINQ
- if-null → null-conditional
- Manual dispose → using statement

## Safety Features

1. **Preview Mode**: All tools support preview to show changes before applying
2. **Validation**: Ensure syntax remains valid after edits
3. **Rollback**: Track changes for easy undo
4. **Conflict Detection**: Detect overlapping edits
5. **Format Preservation**: Maintain code style and formatting

## Benefits

- **Precision**: Target exact syntax nodes, not text
- **Safety**: Roslyn ensures syntactic correctness
- **Semantic Awareness**: Understands code structure
- **Refactoring Power**: Access to Roslyn's refactoring engine
- **Batch Operations**: Multiple edits in single transaction

## Implementation Priority

1. **High Priority** (Most useful for agents):
   - edit-syntax (targeted node modifications)
   - apply-code-action (leverage existing Roslyn fixes)
   - insert-code (add new members/methods)

2. **Medium Priority**:
   - modify-attributes
   - modify-members
   - edit-statements

3. **Lower Priority** (Complex but powerful):
   - apply-semantic-patch

These tools would give agents precise control over code modifications while maintaining safety through Roslyn's syntax tree validation.