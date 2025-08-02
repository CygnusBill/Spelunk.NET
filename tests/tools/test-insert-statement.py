#!/usr/bin/env python3
"""
Test the insert-statement tool in MCP Roslyn Server
"""

import json
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
    print("Starting MCP Roslyn Server test for insert-statement...")
    
    # Start the server
    server_cmd = [
        "dotnet", "run",
        "--project", "./src/McpRoslyn/McpRoslyn.Server/McpRoslyn.Server.csproj",
        "--",
        "--allowed-path", "."
    ]
    
    process = subprocess.Popen(
        server_cmd,
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        bufsize=0
    )
    
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
        
        # First, find a statement to use as reference
        print("\n=== Finding Main method for reference ===")
        response = send_request(process, "tools/call", {
            "name": "dotnet-find-statements",
            "arguments": {
                "pattern": "Console.WriteLine"
            }
        })
        
        # Test 1: Insert before Console.WriteLine
        print("\n=== Test 1: Insert before first Console.WriteLine ===")
        response = send_request(process, "tools/call", {
            "name": "dotnet-insert-statement",
            "arguments": {
                "position": "before",
                "location": {
                    "file": "./test-workspace/Program.cs",
                    "line": 7,
                    "column": 9
                },
                "statement": 'Console.WriteLine("Starting program...");'
            }
        })
        
        # Test 2: Insert after Console.WriteLine  
        print("\n=== Test 2: Insert after Console.WriteLine ===")
        response = send_request(process, "tools/call", {
            "name": "dotnet-insert-statement",
            "arguments": {
                "position": "after",
                "location": {
                    "file": "./test-workspace/Program.cs",
                    "line": 7,
                    "column": 9
                },
                "statement": 'Console.WriteLine("Program started!");'
            }
        })
        
        # Test 3: Insert variable declaration
        print("\n=== Test 3: Insert variable before calculation ===")
        response = send_request(process, "tools/call", {
            "name": "dotnet-insert-statement",
            "arguments": {
                "position": "before",
                "location": {
                    "file": "./test-workspace/Program.cs",
                    "line": 10,
                    "column": 9
                },
                "statement": 'int x = 10; // Test value'
            }
        })
        
        # Test 4: Insert in method body
        print("\n=== Test 4: Insert logging in Add method ===")
        response = send_request(process, "tools/call", {
            "name": "dotnet-find-statements",
            "arguments": {
                "pattern": "return a + b"
            }
        })
        
        response = send_request(process, "tools/call", {
            "name": "dotnet-insert-statement",
            "arguments": {
                "position": "before",
                "location": {
                    "file": "./test-workspace/Program.cs",
                    "line": 19,
                    "column": 9
                },
                "statement": 'Console.WriteLine($"Adding {a} + {b}");'
            }
        })
        
        # Test 5: Test error handling - invalid syntax
        print("\n=== Test 5: Error handling - invalid syntax ===")
        response = send_request(process, "tools/call", {
            "name": "dotnet-insert-statement",
            "arguments": {
                "position": "after",
                "location": {
                    "file": "./test-workspace/Program.cs",
                    "line": 7,
                    "column": 9
                },
                "statement": 'Console.WriteLine("Missing semicolon"'  # Invalid - no semicolon
            }
        })
        
        # Test 6: Test error handling - invalid position
        print("\n=== Test 6: Error handling - invalid position ===")
        response = send_request(process, "tools/call", {
            "name": "dotnet-insert-statement",
            "arguments": {
                "position": "middle",  # Invalid position
                "location": {
                    "file": "./test-workspace/Program.cs",
                    "line": 7,
                    "column": 9
                },
                "statement": 'Console.WriteLine("Test");'
            }
        })
        
        print("\n=== Tests completed successfully! ===")
        
    finally:
        print("\nShutting down server...")
        process.terminate()
        process.wait(timeout=5)

if __name__ == "__main__":
    main()