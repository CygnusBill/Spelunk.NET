#!/usr/bin/env python3
"""Debug F# symbol finding with more aggressive stderr capture"""
import json
import subprocess
import time
import os
import threading

# Store stderr output
stderr_lines = []

def read_stderr(process):
    """Read stderr in a separate thread to prevent blocking"""
    for line in process.stderr:
        stderr_lines.append(line.strip())

# Start the server
os.environ["MCP_ROSLYN_ALLOWED_PATHS"] = "/Users/bill/Repos/McpDotnet"
cmd = ["/usr/local/share/dotnet/dotnet", "run", "--project", "src/McpRoslyn.Server", "--no-build"]
process = subprocess.Popen(cmd, stdin=subprocess.PIPE, stdout=subprocess.PIPE, 
                          stderr=subprocess.PIPE, text=True)

# Start stderr reader thread
stderr_thread = threading.Thread(target=read_stderr, args=(process,))
stderr_thread.daemon = True
stderr_thread.start()

# Wait for server to start
time.sleep(3)

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

# Test just one pattern
print("\n\nTesting pattern: '*'")
symbols_request = {
    "jsonrpc": "2.0",
    "id": 3,
    "method": "tools/call",
    "params": {
        "name": "dotnet-fsharp-find-symbols",
        "arguments": {
            "query": "*",
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

# Wait a bit more for logging
time.sleep(2)

process.terminate()

# Print all stderr
print("\n\nAll server stderr output:")
for line in stderr_lines:
    print(line)