#!/usr/bin/env python3
"""
Test the get-symbols tool in MCP Roslyn Server
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
        try:
            return json.loads(response_line)
        except:
            return response_line
    return None

def test_get_symbols():
    print("Starting get-symbols tool test...")
    
    # Get workspace directory
    test_dir = os.path.dirname(os.path.abspath(__file__))
    workspace_dir = os.path.join(test_dir, '..', '..')
    workspace_dir = os.path.abspath(workspace_dir)
    
    # Start the server
    server_path = os.path.join(workspace_dir, 'src', 'McpRoslyn', 'McpDotnet.Server')
    cmd = ['dotnet', 'run', '--project', server_path, '--no-build', '--', '--allowed-path', workspace_dir]
    
    print(f"Starting server with command: {' '.join(cmd)}")
    
    # Set environment variable for allowed paths
    env = os.environ.copy()
    env["MCP_DOTNET_ALLOWED_PATHS"] = os.path.abspath(".")
    
    process = subprocess.Popen(
        cmd,
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        bufsize=1,
        env=env)
    
    # Give server time to start
    time.sleep(2)
    
    try:
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
                "path": os.path.join(workspace_dir, "test-workspace", "TestProject.csproj")
            }
        })
        
        # Test 1: Get symbol by name
        print("\n=== Test 1: Get symbol by name (Calculator) ===")
        response = send_request(process, "tools/call", {
            "name": "dotnet-get-symbols",
            "arguments": {
                "filePath": os.path.join(workspace_dir, "test-workspace", "Program.cs"),
                "symbolName": "Calculator"
            }
        })
        
        # Test 2: Get symbol at position
        print("\n=== Test 2: Get symbol at position ===")
        response = send_request(process, "tools/call", {
            "name": "dotnet-get-symbols",
            "arguments": {
                "filePath": os.path.join(workspace_dir, "test-workspace", "Program.cs"),
                "position": {
                    "line": 5,
                    "column": 20
                }
            }
        })
        
        # Test 3: Get all symbols in file
        print("\n=== Test 3: Get all symbols in file ===")
        response = send_request(process, "tools/call", {
            "name": "dotnet-get-symbols",
            "arguments": {
                "filePath": os.path.join(workspace_dir, "test-workspace", "Calculator.cs")
            }
        })
        
        print("\n=== Test completed successfully! ===")
        
    except Exception as e:
        print(f"Error: {e}")
        import traceback
        traceback.print_exc()
    finally:
        print("\nShutting down server...")
        process.terminate()
        process.wait(timeout=5)

if __name__ == "__main__":
    test_get_symbols()