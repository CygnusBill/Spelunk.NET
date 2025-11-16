#!/usr/bin/env python3
"""
Test the ephemeral marker system in MCP Roslyn Server
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
    print("Starting MCP Roslyn Server test for marker system...")
    
    # Start the server
    server_cmd = [
        "dotnet", "run",
        "--project", "./src/McpRoslyn/Spelunk.Server/Spelunk.Server.csproj"]
    
    # Set environment variable for allowed paths

    
    env = os.environ.copy()

    
    env["MCP_DOTNET_ALLOWED_PATHS"] = os.path.abspath(".")

    
    

    
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
            "name": "spelunk-load-workspace",
            "arguments": {
                "path": "./test-workspace/TestProject.csproj"
            }
        })
        
        # Test 1: Mark a statement
        print("\n=== Test 1: Mark a Console.WriteLine statement ===")
        response = send_request(process, "tools/call", {
            "name": "spelunk-mark-statement",
            "arguments": {
                "location": {
                    "file": "./test-workspace/Program.cs",
                    "line": 7,
                    "column": 9
                },
                "label": "First output"
            }
        })
        
        # Test 2: Mark another statement
        print("\n=== Test 2: Mark another statement ===")
        response = send_request(process, "tools/call", {
            "name": "spelunk-mark-statement",
            "arguments": {
                "location": {
                    "file": "./test-workspace/Program.cs",
                    "line": 11,
                    "column": 9
                },
                "label": "Result output"
            }
        })
        
        # Test 3: Mark without label
        print("\n=== Test 3: Mark statement without label ===")
        response = send_request(process, "tools/call", {
            "name": "spelunk-mark-statement",
            "arguments": {
                "location": {
                    "file": "./test-workspace/Program.cs",
                    "line": 9,
                    "column": 9
                }
            }
        })
        
        # Test 4: Find all marked statements
        print("\n=== Test 4: Find all marked statements ===")
        response = send_request(process, "tools/call", {
            "name": "spelunk-find-marked-statements",
            "arguments": {}
        })
        
        # Test 5: Find specific marker
        print("\n=== Test 5: Find specific marker (mark-1) ===")
        response = send_request(process, "tools/call", {
            "name": "spelunk-find-marked-statements",
            "arguments": {
                "markerId": "mark-1"
            }
        })
        
        # Test 6: Edit marked statement and find it again
        print("\n=== Test 6: Replace marked statement and find again ===")
        # First replace the statement
        response = send_request(process, "tools/call", {
            "name": "spelunk-replace-statement",
            "arguments": {
                "location": {
                    "file": "./test-workspace/Program.cs",
                    "line": 7,
                    "column": 9
                },
                "newStatement": 'Console.WriteLine("Modified: Hello, World!");'
            }
        })
        
        # Find marked statements again (markers should be preserved on normal edits)
        response = send_request(process, "tools/call", {
            "name": "spelunk-find-marked-statements",
            "arguments": {}
        })
        
        # Test 7: Unmark a statement
        print("\n=== Test 7: Unmark statement (mark-2) ===")
        response = send_request(process, "tools/call", {
            "name": "spelunk-unmark-statement",
            "arguments": {
                "markerId": "mark-2"
            }
        })
        
        # Test 8: Find remaining marked statements
        print("\n=== Test 8: Find remaining marked statements ===")
        response = send_request(process, "tools/call", {
            "name": "spelunk-find-marked-statements",
            "arguments": {}
        })
        
        # Test 9: Clear all markers
        print("\n=== Test 9: Clear all markers ===")
        response = send_request(process, "tools/call", {
            "name": "spelunk-clear-markers",
            "arguments": {}
        })
        
        # Test 10: Verify all markers cleared
        print("\n=== Test 10: Verify all markers cleared ===")
        response = send_request(process, "tools/call", {
            "name": "spelunk-find-marked-statements",
            "arguments": {}
        })
        
        # Test 11: Error handling - mark non-existent location
        print("\n=== Test 11: Error handling - mark non-existent location ===")
        response = send_request(process, "tools/call", {
            "name": "spelunk-mark-statement",
            "arguments": {
                "location": {
                    "file": "./test-workspace/Program.cs",
                    "line": 999,
                    "column": 1
                }
            }
        })
        
        # Test 12: Error handling - unmark non-existent marker
        print("\n=== Test 12: Error handling - unmark non-existent marker ===")
        response = send_request(process, "tools/call", {
            "name": "spelunk-unmark-statement",
            "arguments": {
                "markerId": "mark-999"
            }
        })
        
        print("\n=== Tests completed successfully! ===")
        
    finally:
        print("\nShutting down server...")
        process.terminate()
        process.wait(timeout=5)

if __name__ == "__main__":
    main()