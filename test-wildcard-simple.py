#!/usr/bin/env python3
"""Simple test for the wildcard pattern fix"""

import json
import sys
import os
import subprocess
import time

# Create test file
test_file = "/tmp/test-wildcard.cs"
with open(test_file, "w") as f:
    f.write("""
namespace Test
{
    public class TestClass  
    {
        public void foo() { }
        public void bar() { }
        private string foo = "field";
    }
}
""")

# Start server
env = os.environ.copy()
env["MCP_ROSLYN_ALLOWED_PATHS"] = "/tmp"

proc = subprocess.Popen(
    ["dotnet", "run", "--project", "src/McpRoslyn.Server", "--no-build"],
    stdin=subprocess.PIPE,
    stdout=subprocess.PIPE,
    stderr=subprocess.PIPE,
    env=env,
    text=True
)

# Initialize
init_request = json.dumps({
    "jsonrpc": "2.0",
    "id": 0,
    "method": "initialize",
    "params": {"protocolVersion": "2024-11-05"}
}) + "\n"

proc.stdin.write(init_request)
proc.stdin.flush()

# Read init response
init_response = proc.stdout.readline()
print("Init response:", init_response[:100] + "...")

# Load workspace
load_request = json.dumps({
    "jsonrpc": "2.0",
    "id": 1,
    "method": "tools/call",
    "params": {
        "name": "dotnet-load-workspace",
        "arguments": {"path": test_file}
    }
}) + "\n"

proc.stdin.write(load_request)
proc.stdin.flush()

# Read load response
load_response = proc.stdout.readline()
print("Load response:", load_response[:100] + "...")

# Test the pattern
query_request = json.dumps({
    "jsonrpc": "2.0",
    "id": 2,
    "method": "tools/call",
    "params": {
        "name": "dotnet-query-syntax",
        "arguments": {
            "file": test_file,
            "roslynPath": "//*[@name='foo']"
        }
    }
}) + "\n"

print("\nSending query: //*[@name='foo']")
proc.stdin.write(query_request)
proc.stdin.flush()

# Read with timeout
import select
ready, _, _ = select.select([proc.stdout], [], [], 3)

if ready:
    query_response = proc.stdout.readline()
    print("Query response received!")
    
    try:
        response = json.loads(query_response)
        if "result" in response:
            content = response["result"]["content"][0]["text"]
            data = json.loads(content)
            matches = data.get("matches", [])
            print(f"Found {len(matches)} matches:")
            for match in matches:
                print(f"  - {match.get('nodeType')}: {match.get('preview', '')[:50]}")
        elif "error" in response:
            print(f"Error: {response['error']}")
    except Exception as e:
        print(f"Failed to parse response: {e}")
        print(f"Raw response: {query_response[:200]}")
else:
    print("TIMEOUT - likely infinite loop!")
    
proc.terminate()
proc.wait()