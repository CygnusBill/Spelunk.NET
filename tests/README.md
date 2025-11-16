# MCP Roslyn Server Tests

This directory contains integration tests for the MCP Roslyn Server. These tests use Python to simulate MCP clients and verify server functionality.

## Directory Structure

- `integration/` - End-to-end integration tests
- `tools/` - Individual tool tests
- `protocol/` - MCP protocol tests

## Running Tests

### Individual Test
```bash
python3 tests/tools/test-find-class.py
```

### All Tests
```bash
for test in tests/**/*.py; do
    echo "Running $test..."
    python3 "$test"
done
```

## Test Categories

### Tool Tests
- `test-find-class.py` - Tests class discovery
- `test-find-statements.py` - Tests statement-level searching
- `test-replace-statement.py` - Tests statement replacement
- `test-insert-statement.py` - Tests statement insertion
- `test-remove-statement.py` - Tests statement removal
- `test-marker-system.py` - Tests ephemeral markers
- `test-rename-validation.py` - Tests symbol renaming
- `test-edit-code.py` - Tests code editing operations
- `test-method-calls.py` - Tests call graph analysis
- `test-reference-tools.py` - Tests find-references functionality

### Protocol Tests
- `test-protocol.py` - Basic MCP protocol validation
- `test-client.py` - Client connection tests
- `test-enhanced-calls.py` - Advanced protocol features

### Integration Tests
- `test-member-search.py` - Cross-cutting member search
- `test-rename-safety.py` - Safe renaming across files
- `test-at-keyword.py` - Special syntax handling

### Utilities
- `run-test-with-cleanup.py` - Test runner with cleanup
- `debug_test.py` - Debugging helper

## Writing New Tests

Tests follow this pattern:

```python
import subprocess
import json

# Start server
server = subprocess.Popen(["dotnet", "run", "--project", "src/Spelunk/Spelunk.Server"],
                         stdin=subprocess.PIPE, stdout=subprocess.PIPE)

# Send initialize
request = {"jsonrpc": "2.0", "id": 1, "method": "initialize", 
           "params": {"protocolVersion": "2024-11-05"}}
send_request(server, request)

# Test specific functionality
# ...

# Cleanup
server.terminate()
```

## Test Data

Tests use the `test-workspace/` directory which contains sample C# projects for testing.