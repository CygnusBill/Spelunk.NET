#!/usr/bin/env python3
"""Debug AST structure to fix tests"""

import subprocess
import json
import os

def send_request(process, method, params=None):
    request = {
        "jsonrpc": "2.0",
        "id": 1,
        "method": method,
        "params": params or {}
    }
    
    process.stdin.write(json.dumps(request) + '\n')
    process.stdin.flush()
    
    response_line = process.stdout.readline()
    
    try:
        response = json.loads(response_line)
        if "error" in response and response["error"] is not None:
            print(f"ERROR: {response['error']}")
            return None
        result = response.get("result", {})
        if isinstance(result, dict) and "content" in result:
            content = result["content"]
            if content and len(content) > 0 and content[0].get("type") == "text":
                try:
                    return json.loads(content[0]["text"])
                except:
                    return content[0]["text"]
        return result
    except json.JSONDecodeError as e:
        print(f"Failed to parse response: {e}")
        return None

# Start server
process = subprocess.Popen(
    ["dotnet", "run", "--project", "src/McpRoslyn.Server/McpRoslyn.Server.csproj"],
    stdin=subprocess.PIPE,
    stdout=subprocess.PIPE,
    stderr=subprocess.PIPE,
    text=True,
    bufsize=1
)

import time
time.sleep(2)

# Initialize
send_request(process, "initialize", {
    "protocolVersion": "0.1.0",
    "capabilities": {"tools": {}},
    "clientInfo": {"name": "debug-ast", "version": "1.0.0"}
})

# Create test file
test_code = """namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod()
        {
            var x = 1 + 2;
        }
    }
}"""

with open("test-ast-debug.cs", "w") as f:
    f.write(test_code)

# Load workspace (use test file)
send_request(process, "tools/call", {
    "name": "spelunk-load-workspace",
    "arguments": {
        "path": os.path.abspath("test-workspace/TestProject.csproj")
    }
})

# Get AST at the position
print("\nGetting AST structure around '1' in '1 + 2'...")
result = send_request(process, "tools/call", {
    "name": "spelunk-get-ast",
    "arguments": {
        "file": os.path.abspath("test-ast-debug.cs"),
        "depth": 5
    }
})

if result:
    print("\nAST Structure:")
    print("Result type:", type(result))
    if isinstance(result, dict) and "ast" in result:
        print(json.dumps(result["ast"], indent=2))
    else:
        print("Unexpected result:", result)

# Navigate from position
print("\nNavigating from position of '1'...")
result = send_request(process, "tools/call", {
    "name": "spelunk-navigate",
    "arguments": {
        "from": {
            "file": os.path.abspath("test-ast-debug.cs"),
            "line": 7,
            "column": 21  # Position of "1"
        },
        "path": "parent",
        "returnPath": True
    }
})

if result and "navigatedTo" in result:
    print("\nParent of '1':")
    print(json.dumps(result["navigatedTo"], indent=2))

# Clean up
process.terminate()
os.remove("test-ast-debug.cs")