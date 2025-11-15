#!/usr/bin/env python3
"""
Test the find-statements tool in MCP Roslyn Server
"""

import json
import subprocess
import time
import signal
import sys
import os

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
    print("Starting MCP Roslyn Server test for find-statements...")
    
    # Start the server
    server_cmd = [
        "dotnet", "run",
        "--project", os.path.join(os.path.dirname(__file__), "..", "..", "src", "McpDotnet.Server", "McpDotnet.Server.csproj"),
        "--no-build"]
    
    # Set environment variable for allowed paths
    env = os.environ.copy()
    env["MCP_DOTNET_ALLOWED_PATHS"] = os.path.abspath(".")
    
    process = subprocess.Popen(
        server_cmd,
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        bufsize=0,
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
        
        # Check if server started properly
        if process.poll() is not None:
            stderr_output = process.stderr.read()
            print(f"Server failed to start. Exit code: {process.returncode}")
            print(f"Stderr: {stderr_output}")
            return
        
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
        
        # Test 1: Find Console.WriteLine statements
        print("\n=== Test 1: Find Console.WriteLine statements ===")
        response = send_request(process, "tools/call", {
            "name": "spelunk-find-statements",
            "arguments": {
                "pattern": "Console.WriteLine"
            }
        })
        
        # Test 2: Find statements in specific method
        print("\n=== Test 2: Find statements in Main method ===")
        response = send_request(process, "tools/call", {
            "name": "spelunk-find-statements",
            "arguments": {
                "pattern": "",
                "scope": {
                    "methodName": "Main"
                }
            }
        })
        
        # Test 3: Find variable declarations
        print("\n=== Test 3: Find variable declarations (var keyword) ===")
        response = send_request(process, "tools/call", {
            "name": "spelunk-find-statements",
            "arguments": {
                "pattern": "var ",
                "patternType": "text"
            }
        })
        
        # Test 4: Find if statements using regex
        print("\n=== Test 4: Find if statements using regex ===")
        response = send_request(process, "tools/call", {
            "name": "spelunk-find-statements",
            "arguments": {
                "pattern": "^\\s*if\\s*\\(",
                "patternType": "regex"
            }
        })
        
        # Test 5: Find nested statements
        print("\n=== Test 5: Find statements including nested ones ===")
        response = send_request(process, "tools/call", {
            "name": "spelunk-find-statements",
            "arguments": {
                "pattern": ".",
                "patternType": "regex",
                "includeNestedStatements": True,
                "scope": {
                    "methodName": "Main"
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