# Testing Infrastructure Issues

## Overview

This document records known issues with the testing infrastructure that affect test execution and provides solutions for future test development.

## Critical Issues

### 1. Allowed Paths Security Restriction

**Issue ID**: `TEST-INFRA-001`  
**Severity**: High  
**Status**: Unresolved  

#### Description
The MCP Roslyn Server enforces security restrictions on file system access through the `--allowed-path` parameter. Tests that create temporary directories outside the allowed paths fail with timeouts.

#### Technical Details
- **Server Parameter**: `--allowed-path` specifies which directories the server can access
- **Default Behavior**: TestClient starts server without specifying allowed paths, defaulting to project root
- **Temp Directory Issue**: `tempfile.mkdtemp()` creates directories in system temp (e.g., `/var/folders/...`) which are outside allowed paths

#### Error Symptoms
```
TimeoutError: No response received within 10 seconds
```

Server logs show successful start but workspace loading fails:
```
Server stderr: Allowed paths: /Users/bill/Repos/McpDotnet
...
Server stderr: Loading workspace from: /var/folders/vc/.../temp_workspace/
[Server hangs - no response]
```

#### Affected Tests
- `test-navigate-semantic.py` - Uses `tempfile.mkdtemp()`
- `test-get-ast-semantic.py` - Uses `tempfile.mkdtemp()`
- `test-query-syntax-semantic.py` - Uses `tempfile.mkdtemp()`
- Any future tests that create isolated temporary workspaces

#### Solution Options

**Option 1: Update TestClient (Recommended)**
```python
class TestClient:
    def __init__(self, server_path=None, allowed_paths=None):
        self.allowed_paths = allowed_paths or ["."]
        # In _start_server():
        cmd = ["dotnet", "run", "--project", self.server_path]
        for path in self.allowed_paths:
            cmd.extend(["--", "--allowed-path", path])
```

**Option 2: Use test-workspace Directory**
```python
# Instead of:
workspace_path = tempfile.mkdtemp(prefix="test_semantic_")

# Use:
workspace_path = os.path.join("test-workspace", "semantic-test")
os.makedirs(workspace_path, exist_ok=True)
```

**Option 3: Grant Global Access (Not Recommended)**
```python
cmd = ["dotnet", "run", "--project", self.server_path, "--", "--allowed-path", "/"]
```

#### Implementation Status
- **Discovered**: During semantic enrichment test execution
- **Documented**: ✅ 
- **Fixed**: ❌ (Pending)
- **Workaround**: Use test-workspace directory

### 2. Response Error Handling

**Issue ID**: `TEST-INFRA-002`  
**Severity**: Medium  
**Status**: Fixed  

#### Description
TestClient error handling failed when server responses contained `"error": null`, causing AttributeError.

#### Error
```python
AttributeError: 'NoneType' object has no attribute 'get'
```

#### Root Cause
```python
if "error" in response:  # True when error: null exists
    return response["error"].get("message")  # NoneType.get() fails
```

#### Solution (Applied)
```python
if "error" in response and response["error"] is not None:
    return response["error"].get("message", "Unknown error")
```

### 3. Broken Pipe Errors

**Issue ID**: `TEST-INFRA-003`  
**Severity**: Medium  
**Status**: Unresolved  

#### Description
Existing tests show broken pipe errors when communicating with the server process.

#### Error
```
BrokenPipeError: [Errno 32] Broken pipe
```

#### Affected Tests
- `test-find-class.py`
- Multiple protocol tests

#### Analysis Needed
- Server process termination timing
- Stdin/stdout buffer handling
- Process lifecycle management

## Testing Best Practices

### For New Tests

1. **Use Existing Workspace**
   ```python
   # Good: Use test-workspace
   workspace_path = os.path.join("test-workspace", "TestProject.csproj")
   
   # Avoid: Temporary directories (until TestClient is fixed)
   workspace_path = tempfile.mkdtemp()  # Will fail
   ```

2. **Handle Response Structure**
   ```python
   # F# detection returns nested success flag
   if "result" in result and result["result"].get("success") == False:
       message = result["result"].get("message", "")
   ```

3. **Test File Naming**
   ```python
   # Tests are auto-discovered by pattern
   test-*.py  # ✅ Picked up by run-all-tests.py
   semantic-test.py  # ❌ Not discovered
   ```

### Working Examples

1. **F# Detection Test**: Successfully works with existing files
2. **Query-Syntax Semantic Test**: Has comprehensive logic but needs path fix
3. **Server Startup Pattern**: All tests successfully start server and initialize

## Future Improvements

### High Priority
1. Fix TestClient allowed paths configuration
2. Investigate broken pipe errors in existing tests
3. Add timeout configuration options

### Medium Priority  
1. Add test workspace management utilities
2. Create test file cleanup mechanisms
3. Improve error reporting and debugging

### Low Priority
1. Add performance benchmarking for test execution
2. Create test categorization (unit, integration, performance)
3. Add parallel test execution support

## References

- **TestClient Location**: `tests/utils/test_client.py`
- **Working Test Example**: `tests/tools/test-fsharp-detection-simple.py`
- **Server Configuration**: `src/McpRoslyn.Server/Program.cs` (allowed paths handling)
- **Existing Test Patterns**: `tests/tools/test-find-class.py` and similar