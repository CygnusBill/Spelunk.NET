#!/usr/bin/env python3
"""Debug F# symbol finding"""
import json
import subprocess
import time
import os

# Start the server
os.environ["MCP_ROSLYN_ALLOWED_PATHS"] = "/Users/bill/Repos/McpDotnet"
cmd = ["/usr/local/share/dotnet/dotnet", "run", "--project", "src/McpRoslyn.Server", "--no-build"]
process = subprocess.Popen(cmd, stdin=subprocess.PIPE, stdout=subprocess.PIPE, 
                          stderr=subprocess.PIPE, text=True)

# Wait for server to start
time.sleep(2)

# Send initialize
init_request = {
    "jsonrpc": "2.0",
    "id": 1,
    "method": "initialize",
    "params": {
        "protocolVersion": "2024-11-05",
        "capabilities": {},
        "clientInfo": {"name": "test", "version": "1.0"}
    }
}
process.stdin.write(json.dumps(init_request) + "\n")
process.stdin.flush()
response = process.stdout.readline()
print("Initialized")

# First load the F# project
load_request = {
    "jsonrpc": "2.0",
    "id": 2,
    "method": "tools/call",
    "params": {
        "name": "dotnet-fsharp-load-project",
        "arguments": {
            "projectPath": "/Users/bill/Repos/McpDotnet/test-workspace/FSharpTestProject/FSharpTestProject.fsproj"
        }
    }
}
process.stdin.write(json.dumps(load_request) + "\n")
process.stdin.flush()
response = process.stdout.readline()
print("Project loaded")

# Test different patterns for symbol finding
patterns = ["*", "add*", "factorial", "Math*", ""]

for pattern in patterns:
    print(f"\n\nTesting pattern: '{pattern}'")
    symbols_request = {
        "jsonrpc": "2.0",
        "id": 3,
        "method": "tools/call",
        "params": {
            "name": "dotnet-fsharp-find-symbols",
            "arguments": {
                "pattern": pattern,
                "filePath": "/Users/bill/Repos/McpDotnet/test-workspace/FSharpTestProject/Library.fs"
            }
        }
    }
    process.stdin.write(json.dumps(symbols_request) + "\n")
    process.stdin.flush()
    response = process.stdout.readline()
    
    try:
        result = json.loads(response)
        if "result" in result:
            symbols = result["result"].get("symbols", [])
            print(f"Found {len(symbols)} symbols")
            for s in symbols[:5]:
                print(f"  - {s['kind']}: {s['name']} @ line {s['startLine']}")
    except Exception as e:
        print(f"Error: {e}")
        print(f"Response: {response[:200]}...")

# Also check stderr for any F# errors
stderr_output = process.stderr.read(1000) if process.stderr else ""
if stderr_output:
    print("\n\nServer stderr output:")
    print(stderr_output)

process.terminate()