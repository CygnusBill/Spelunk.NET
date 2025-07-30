# Simple Surgical Edit Tool Design

## `dotnet/edit-code` - Precise code modifications using Roslyn

### Basic Operations

1. **Add Member**
```json
{
  "file": "UserService.cs",
  "operation": "add-member",
  "className": "UserService",
  "member": {
    "type": "method",
    "signature": "public async Task<bool> ValidateUserAsync(int userId)",
    "body": "{\n    var user = await GetUserAsync(userId);\n    return user != null && user.IsActive;\n}"
  }
}
```

2. **Modify Method**
```json
{
  "file": "UserService.cs", 
  "operation": "modify-method",
  "className": "UserService",
  "methodName": "GetUser",
  "changes": {
    "makeAsync": true,
    "addParameter": { "name": "includeDeleted", "type": "bool", "defaultValue": "false" },
    "renameParameter": { "from": "id", "to": "userId" }
  }
}
```

3. **Add Using/Import**
```json
{
  "file": "UserService.cs",
  "operation": "add-using",
  "namespace": "System.Linq"
}
```

4. **Wrap in Try-Catch**
```json
{
  "file": "DataProcessor.cs",
  "operation": "wrap-statements",
  "method": "ProcessData",
  "fromLine": 25,
  "toLine": 30,
  "wrapper": "try-catch",
  "catchType": "SqlException",
  "catchBody": "_logger.LogError(ex, \"Database error\");"
}
```

5. **Add Attribute**
```json
{
  "file": "UserController.cs",
  "operation": "add-attribute", 
  "target": { "type": "class", "name": "UserController" },
  "attribute": "[Authorize(Roles = \"Admin\")]"
}
```

6. **Extract Method**
```json
{
  "file": "OrderService.cs",
  "operation": "extract-method",
  "fromLine": 45,
  "toLine": 52,
  "newMethodName": "CalculateDiscount",
  "makePrivate": true
}
```

### Benefits
- Simple, focused operations
- Each operation maps to common refactoring needs
- Easy for agents to understand and use
- Leverages Roslyn for safety
- No need to understand complex syntax trees

### Safety
- All operations validate syntax
- Preview mode available
- Preserves formatting
- Atomic operations (all or nothing)