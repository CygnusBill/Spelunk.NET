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

### 3. Server SIGKILL Termination Issue

**Issue ID**: `TEST-INFRA-003`  
**Severity**: Critical  
**Status**: Unresolved  

#### Description
The MCP Roslyn Server process is being terminated with SIGKILL (exit code 137) immediately after startup across all test approaches, making the entire test suite unusable.

#### Error Symptoms
```bash
Process terminated with code: 137
```

#### Technical Investigation
- **Exit Code 137**: SIGKILL (128 + 9) - process forcibly terminated
- **Timing**: Occurs within 1-3 seconds of process startup
- **Consistency**: Affects all test approaches (TestClient, direct subprocess, script-based)
- **Environment**: .NET 10.0 preview version on macOS

#### Affected Systems
- ✅ **Build**: Project builds successfully without errors
- ❌ **Runtime**: Server process cannot start/survive in test environment  
- ❌ **All test approaches**: TestClient, subprocess.Popen, shell scripts

#### Investigation Results
1. **Permissions**: Fixed executable permissions - no change
2. **Command args**: Tested various argument combinations - no change  
3. **Environment variables**: Tried MCP_ROSLYN_ALLOWED_PATHS - no change
4. **Project paths**: Fixed incorrect paths in scripts - no change
5. **Resource limits**: No obvious memory/CPU constraints visible

#### Potential Causes
1. **System-level resource limits** killing the process
2. **.NET preview version instability** on this system
3. **macOS security restrictions** on the process
4. **Dependency conflicts** causing immediate crashes
5. **Build artifacts corruption** (despite successful builds)

#### Workaround Status
- ❌ **TestClient approach**: Fails with SIGKILL
- ❌ **Script-based approach**: Fails with SIGKILL  
- ❌ **Direct dotnet run**: Fails with SIGKILL
- ⚠️ **Manual testing**: May work but not suitable for automated tests

#### Resolution Status
✅ **RESOLVED**: The SIGKILL issue was caused by subprocess timeout commands, not server problems
- **Root Cause**: Using `timeout` or `subprocess.run(timeout=...)` was sending SIGKILL to server processes
- **Solution**: Build first with `dotnet build`, then run with `--no-build --no-restore` for predictable startup
- **Result**: Server now starts reliably in ~1 second

### 4. Server Response Issue for Specific Tools

**Issue ID**: `TEST-INFRA-004`  
**Severity**: Medium  
**Status**: Unresolved  

#### Description
Some tools (particularly `dotnet-load-workspace`) process requests successfully but don't return JSON responses to stdout, causing client timeouts.

#### Error Symptoms
```
Server stderr: Loaded workspace TestProject_xyz from /path/to/project.csproj with 1 projects
TimeoutError: No response received within 60 seconds
```

#### Technical Details
- **Server Processing**: Logs show successful processing and completion
- **Response Missing**: No JSON response written to stdout despite successful operation
- **Affected Tools**: `dotnet-load-workspace` confirmed, potentially others
- **Working Tools**: `initialize` works correctly

#### Investigation Needed
- Check server response serialization for workspace tools
- Verify stdout writing is not being suppressed for certain operations  
- Test other tools to identify pattern of affected operations

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