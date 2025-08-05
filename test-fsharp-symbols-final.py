#!/usr/bin/env python3
"""Test F# symbol finding with correct response format"""
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
patterns = ["*", "add*", "factorial", "Math*"]

for pattern in patterns:
    print(f"\n\nTesting pattern: '{pattern}'")
    symbols_request = {
        "jsonrpc": "2.0",
        "id": 3,
        "method": "tools/call",
        "params": {
            "name": "dotnet-fsharp-find-symbols",
            "arguments": {
                "query": pattern,
                "filePath": "/Users/bill/Repos/McpDotnet/test-workspace/FSharpTestProject/Library.fs"
            }
        }
    }
    process.stdin.write(json.dumps(symbols_request) + "\n")
    process.stdin.flush()
    response = process.stdout.readline()
    
    try:
        result = json.loads(response)
        if "result" in result and "content" in result["result"]:
            # The response is in text format within content
            content = result["result"]["content"][0]["text"]
            # Extract symbol count from the text
            import re
            match = re.search(r'Found (\d+) F# symbols', content)
            if match:
                count = match.group(1)
                print(f"Found {count} symbols")
                # Print first few lines of the response
                lines = content.split('\n')[:10]
                for line in lines[2:7]:  # Skip header lines
                    if line.strip():
                        print(f"  {line}")
            else:
                print("No symbols found")
    except Exception as e:
        print(f"Error: {e}")
        print(f"Response: {response[:200]}...")

process.terminate()