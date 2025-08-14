#!/usr/bin/env python3
"""
Test the remove-statement tool in MCP Roslyn Server
"""

import json
import os
import subprocess
import time
import signal
import sys

def send_request(process, method, params=None):
    """Send a JSON-RPC request and get response"""
    request = {
        "jsonrpc": "2.0",
        "id": 1,
        "method": method,
        "params": params or {}
    }
    
    request_str = json.dumps(request)
    print(f"\n>>> Sending: {request_str}")
    
    process.stdin.write(request_str + "\n")
    process.stdin.flush()
    
    # Read response
    response_line = process.stdout.readline()
    if response_line:
        print(f"<<< Response: {response_line.strip()}")
        return json.loads(response_line)
    return None

def main():
    print("Starting MCP Roslyn Server test for remove-statement...")
    
    # Start the server
    server_cmd = [
        "dotnet", "run",
        "--project", "./src/McpRoslyn/McpRoslyn.Server/McpRoslyn.Server.csproj"]
    
    # Set environment variable for allowed paths

    
    env = os.environ.copy()

    
    env["MCP_ROSLYN_ALLOWED_PATHS"] = os.path.abspath(".")

    
    

    
    process = subprocess.Popen(
        server_cmd,
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        bufsize=0
    ,
        env=env)
    
    # Set up signal handler
    def signal_handler(sig, frame):
        print("\nShutting down...")
        process.terminate()
        sys.exit(0)
    
    signal.signal(signal.SIGINT, signal_handler)
    
    try:
        # Give server time to start
        time.sleep(2)
        
        # Initialize
        print("\n=== Initializing ===")
        response = send_request(process, "initialize", {
            "protocolVersion": "2024-11-05"
        })
        
        # Load workspace
        print("\n=== Loading workspace ===")
        response = send_request(process, "tools/call", {
            "name": "dotnet-load-workspace",
            "arguments": {
                "path": "./test-workspace/TestProject.csproj"
            }
        })
        
        # First, find some statements to remove
        print("\n=== Finding Console.WriteLine statements ===")
        response = send_request(process, "tools/call", {
            "name": "dotnet-find-statements",
            "arguments": {
                "pattern": "Console.WriteLine"
            }
        })
        
        # Test 1: Remove a simple statement
        print("\n=== Test 1: Remove a Console.WriteLine statement ===")
        response = send_request(process, "tools/call", {
            "name": "dotnet-remove-statement",
            "arguments": {
                "location": {
                    "file": "./test-workspace/Program.cs",
                    "line": 8,
                    "column": 9
                },
                "preserveComments": True
            }
        })
        
        # Test 2: Remove a variable declaration
        print("\n=== Test 2: Remove variable declaration ===")
        response = send_request(process, "tools/call", {
            "name": "dotnet-find-statements",
            "arguments": {
                "pattern": "int x = 10"
            }
        })
        
        response = send_request(process, "tools/call", {
            "name": "dotnet-remove-statement",
            "arguments": {
                "location": {
                    "file": "./test-workspace/Program.cs",
                    "line": 10,
                    "column": 9
                },
                "preserveComments": True
            }
        })
        
        # Test 3: Remove statement with comment
        print("\n=== Test 3: Find and remove return statement ===")
        response = send_request(process, "tools/call", {
            "name": "dotnet-find-statements",
            "arguments": {
                "pattern": "return a + b"
            }
        })
        
        response = send_request(process, "tools/call", {
            "name": "dotnet-remove-statement",
            "arguments": {
                "location": {
                    "file": "./test-workspace/Program.cs",
                    "line": 22,
                    "column": 9
                },
                "preserveComments": True
            }
        })
        
        # Test 4: Remove without preserving comments
        print("\n=== Test 4: Remove statement without preserving comments ===")
        response = send_request(process, "tools/call", {
            "name": "dotnet-remove-statement",
            "arguments": {
                "location": {
                    "file": "./test-workspace/Program.cs",
                    "line": 11,
                    "column": 9
                },
                "preserveComments": False
            }
        })
        
        # Test 5: Test error handling - invalid location
        print("\n=== Test 5: Error handling - invalid location ===")
        response = send_request(process, "tools/call", {
            "name": "dotnet-remove-statement",
            "arguments": {
                "location": {
                    "file": "./test-workspace/Program.cs",
                    "line": 999,
                    "column": 1
                }
            }
        })
        
        # Test 6: View the final result
        print("\n=== Test 6: View final file content ===")
        response = send_request(process, "tools/call", {
            "name": "dotnet-find-statements",
            "arguments": {
                "pattern": "Main",
                "scope": {
                    "file": "./test-workspace/Program.cs"
                }
            }
        })
        
        print("\n=== Tests completed successfully! ===")
        
    finally:
        print("\nShutting down server...")
        process.terminate()
        process.wait(timeout=5)

if __name__ == "__main__":
    main()