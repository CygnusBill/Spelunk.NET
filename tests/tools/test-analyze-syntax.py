#!/usr/bin/env python3
"""
Test the analyze-syntax tool in MCP Roslyn Server
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
        print(f"<<< Response: {response_line.strip()[:500]}..." if len(response_line.strip()) > 500 else f"<<< Response: {response_line.strip()}")
        try:
            return json.loads(response_line)
        except:
            return response_line
    return None

def test_analyze_syntax():
    print("Starting analyze-syntax tool test...")
    
    # Get workspace directory
    test_dir = os.path.dirname(os.path.abspath(__file__))
    workspace_dir = os.path.join(test_dir, '..', '..')
    workspace_dir = os.path.abspath(workspace_dir)
    
    # Start the server
    server_path = os.path.join(workspace_dir, 'src', 'McpDotnet.Server')
    cmd = ['dotnet', 'run', '--project', server_path, '--no-build']
    
    # Set environment variable for allowed paths
    env = os.environ.copy()
    env['MCP_DOTNET_ALLOWED_PATHS'] = workspace_dir
    
    print(f"Starting server with command: {' '.join(cmd)}")
    print(f"MCP_DOTNET_ALLOWED_PATHS={workspace_dir}")
    process = subprocess.Popen(
        cmd,
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        bufsize=1,
        env=env
    )
    
    # Give server time to start
    time.sleep(3)
    
    # Check if server started properly
    if process.poll() is not None:
        stderr_output = process.stderr.read()
        print(f"Server failed to start. Exit code: {process.returncode}")
        print(f"Stderr: {stderr_output}")
        return
    
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
        
        # Test 1: Analyze syntax without trivia
        print("\n=== Test 1: Analyze syntax without trivia ===")
        response = send_request(process, "tools/call", {
            "name": "dotnet-analyze-syntax",
            "arguments": {
                "filePath": os.path.join(workspace_dir, "test-workspace", "Program.cs"),
                "includeTrivia": False
            }
        })
        
        # Test 2: Analyze syntax with trivia
        print("\n=== Test 2: Analyze syntax with trivia ===")
        response = send_request(process, "tools/call", {
            "name": "dotnet-analyze-syntax",
            "arguments": {
                "filePath": os.path.join(workspace_dir, "test-workspace", "Program.cs"),
                "includeTrivia": True
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
        try:
            process.wait(timeout=2)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait()

if __name__ == "__main__":
    test_analyze_syntax()