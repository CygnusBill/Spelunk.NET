#!/usr/bin/env python3
import json
import subprocess
import time
import os

def send_request(proc, request):
    """Send a JSON-RPC request and get response"""
    json_str = json.dumps(request)
    print(f"Sending: {json_str}")
    proc.stdin.write(json_str + '\n')
    proc.stdin.flush()
    
    # Read response
    response = proc.stdout.readline()
    if response:
        print(f"Received: {response.strip()}")
        return json.loads(response)
    return None

def test_mcp_protocol():
    # Start the MCP server
    cmd = [
        "dotnet", "run", 
        "--project", "./src/McpRoslyn/McpRoslyn.Server/McpRoslyn.Server.csproj"]
    
    print("Starting MCP server...")
    proc = subprocess.Popen(
        cmd,
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        bufsize=0
    ,
        env={"MCP_ROSLYN_ALLOWED_PATHS": os.path.abspath(.)})
    
    # Give server time to start
    time.sleep(2)
    
    try:
        # 1. Initialize
        print("\n1. Testing initialize:")
        init_request = {
            "jsonrpc": "2.0",
            "method": "initialize",
            "params": {
                "protocolVersion": "0.1.0",
                "capabilities": {
                    "sampling": {}
                },
                "clientInfo": {
                    "name": "test-client",
                    "version": "1.0.0"
                }
            },
            "id": 1
        }
        response = send_request(proc, init_request)
        
        # 2. List tools
        print("\n2. Testing tools/list:")
        tools_request = {
            "jsonrpc": "2.0",
            "method": "tools/list",
            "id": 2
        }
        response = send_request(proc, tools_request)
        
        # 3. Load workspace
        print("\n3. Testing dotnet-load-workspace:")
        load_request = {
            "jsonrpc": "2.0",
            "method": "tools/call",
            "params": {
                "name": "dotnet-load-workspace",
                "arguments": {
                    "path": "./src/McpRoslyn/McpRoslyn.sln"
                }
            },
            "id": 3
        }
        response = send_request(proc, load_request)
        
        # 4. Get workspace status
        print("\n4. Testing dotnet-workspace-status:")
        status_request = {
            "jsonrpc": "2.0",
            "method": "tools/call",
            "params": {
                "name": "dotnet-workspace-status",
                "arguments": {}
            },
            "id": 4
        }
        response = send_request(proc, status_request)
        
        # 5. Find classes with pattern
        print("\n5. Testing dotnet-find-class with pattern '*Manager':")
        find_request = {
            "jsonrpc": "2.0",
            "method": "tools/call",
            "params": {
                "name": "dotnet-find-class",
                "arguments": {
                    "pattern": "*Manager"
                }
            },
            "id": 5
        }
        response = send_request(proc, find_request)
        
        # 6. Find interfaces
        print("\n6. Testing dotnet-find-class with pattern 'I*':")
        find_request2 = {
            "jsonrpc": "2.0",
            "method": "tools/call",
            "params": {
                "name": "dotnet-find-class",
                "arguments": {
                    "pattern": "I*"
                }
            },
            "id": 6
        }
        response = send_request(proc, find_request2)
        
    finally:
        proc.terminate()
        proc.wait()

if __name__ == "__main__":
    test_mcp_protocol()