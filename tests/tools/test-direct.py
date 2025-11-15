#!/usr/bin/env python3
"""
Direct test of subprocess communication
"""

import subprocess
import json
import os
import sys
import time

# Build first
print("Building...")
build_cmd = ["dotnet", "build", "src/McpDotnet.Server", "--configuration", "Debug"]
subprocess.run(build_cmd, check=True)

# Start server
cmd = ["dotnet", "run", "--project", "src/McpDotnet.Server", "--no-build", "--no-restore"]
env = os.environ.copy()
env["MCP_DOTNET_ALLOWED_PATHS"] = os.path.abspath("test-workspace")

print("Starting server...")
process = subprocess.Popen(
    cmd,
    stdin=subprocess.PIPE,
    stdout=subprocess.PIPE,
    stderr=subprocess.PIPE,
    text=True,
    bufsize=1  # Line buffered
)

# Give it time to start
time.sleep(2)

# Send initialize
init_request = {
    "jsonrpc": "2.0",
    "id": 1,
    "method": "initialize",
    "params": {
        "protocolVersion": "2024-11-05",
        "capabilities": {},
        "clientInfo": {
            "name": "test-client",
            "version": "1.0.0"
        }
    }
}

print("Sending initialize...")
process.stdin.write(json.dumps(init_request) + '\n')
process.stdin.flush()

# Read response
print("Reading response...")
response_line = process.stdout.readline()
print(f"Got response: {response_line.strip()}")

# Send tool call
tool_request = {
    "jsonrpc": "2.0",
    "id": 2,
    "method": "tools/call",
    "params": {
        "name": "spelunk-load-workspace",
        "arguments": {
            "path": os.path.abspath("test-workspace/TestProject.csproj")
        }
    }
}

print("\nSending tool call...")
process.stdin.write(json.dumps(tool_request) + '\n')
process.stdin.flush()

# Try to read response
print("Reading tool response...")
response_line = process.stdout.readline()
print(f"Got tool response: {response_line.strip()}")

# Clean up
process.terminate()
process.wait()