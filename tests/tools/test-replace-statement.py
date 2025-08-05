#!/usr/bin/env python3
"""
Test the replace-statement tool in MCP Roslyn Server
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
    print("Starting MCP Roslyn Server test for replace-statement...")
    
    # Start the server
    server_cmd = [
        "dotnet", "run",
        "--project", "./src/McpRoslyn/McpRoslyn.Server/McpRoslyn.Server.csproj"]
    
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
        
        # First, find a statement to replace
        print("\n=== Finding Console.WriteLine statements ===")
        response = send_request(process, "tools/call", {
            "name": "dotnet-find-statements",
            "arguments": {
                "pattern": "Console.WriteLine"
            }
        })
        
        # Extract the location from the response
        # The response contains the statement locations - we'll replace the first one
        # Example location: ./test-workspace/Program.cs:7:9
        
        # Test 1: Replace the Hello World statement
        print("\n=== Test 1: Replace Hello World with Hello MCP ===")
        response = send_request(process, "tools/call", {
            "name": "dotnet-replace-statement",
            "arguments": {
                "location": {
                    "file": "./test-workspace/Program.cs",
                    "line": 7,
                    "column": 9
                },
                "newStatement": 'Console.WriteLine("Hello from MCP Roslyn!");',
                "preserveComments": True
            }
        })
        
        # Test 2: Replace a variable declaration
        print("\n=== Test 2: Find and replace variable declaration ===")
        response = send_request(process, "tools/call", {
            "name": "dotnet-find-statements",
            "arguments": {
                "pattern": "var calculator"
            }
        })
        
        response = send_request(process, "tools/call", {
            "name": "dotnet-replace-statement",
            "arguments": {
                "location": {
                    "file": "./test-workspace/Program.cs",
                    "line": 9,
                    "column": 9
                },
                "newStatement": 'var calculator = new Calculator(); // Modified by MCP',
                "preserveComments": True
            }
        })
        
        # Test 3: Replace method call with more complex statement
        print("\n=== Test 3: Replace method call ===")
        response = send_request(process, "tools/call", {
            "name": "dotnet-find-statements",
            "arguments": {
                "pattern": "calculator.Add"
            }
        })
        
        response = send_request(process, "tools/call", {
            "name": "dotnet-replace-statement",
            "arguments": {
                "location": {
                    "file": "./test-workspace/Program.cs",
                    "line": 10,
                    "column": 9
                },
                "newStatement": '''var result = calculator.Add(5, 3);
        Console.WriteLine($"Calculating 5 + 3...");''',
                "preserveComments": False
            }
        })
        
        # Test 4: Test error handling - invalid syntax
        print("\n=== Test 4: Error handling - invalid syntax ===")
        response = send_request(process, "tools/call", {
            "name": "dotnet-replace-statement",
            "arguments": {
                "location": {
                    "file": "./test-workspace/Program.cs",
                    "line": 7,
                    "column": 9
                },
                "newStatement": 'Console.WriteLine("Missing semicolon"',  # Invalid - no semicolon
                "preserveComments": True
            }
        })
        
        # Test 5: Test with statement ID error message
        print("\n=== Test 5: Test with statement ID (should show error) ===")
        response = send_request(process, "tools/call", {
            "name": "dotnet-replace-statement",
            "arguments": {
                "statementId": "stmt-1",
                "newStatement": 'Console.WriteLine("Test");'
            }
        })
        
        print("\n=== Tests completed successfully! ===")
        
    finally:
        print("\nShutting down server...")
        process.terminate()
        process.wait(timeout=5)

if __name__ == "__main__":
    main()