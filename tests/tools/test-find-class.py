#!/usr/bin/env python3
import json
import subprocess
import time
import os

def send_request(proc, request):
    """Send a JSON-RPC request and get response"""
    json_str = json.dumps(request)
    print(f"\n📤 Request: {request['params']['arguments']['pattern']}")
    proc.stdin.write(json_str + '\n')
    proc.stdin.flush()
    
    # Read response
    response = proc.stdout.readline()
    if response:
        data = json.loads(response)
        if 'result' in data and 'content' in data['result']:
            text = data['result']['content'][0]['text']
            print(f"📥 Response:\n{text}")
        return data
    return None

def test_find_patterns():
    # Start the MCP server
    cmd = [
        "dotnet", "run", 
        "--project", "./src/McpRoslyn/McpDotnet.Server/McpDotnet.Server.csproj",
        "--no-build"
    ]
    
    # Set environment variable for allowed paths
    env = os.environ.copy()
    env['MCP_DOTNET_ALLOWED_PATHS'] = os.path.abspath(".")
    
    print("Starting MCP server...")
    proc = subprocess.Popen(
        cmd,
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        bufsize=0,
        env=env
    )
    
    # Give server time to start
    time.sleep(2)
    
    try:
        # Initialize
        init_request = {
            "jsonrpc": "2.0",
            "method": "initialize",
            "params": {
                "protocolVersion": "2024-11-05",
                "capabilities": {"sampling": {}},
                "clientInfo": {"name": "test-client", "version": "1.0.0"}
            },
            "id": 1
        }
        proc.stdin.write(json.dumps(init_request) + '\n')
        proc.stdin.flush()
        proc.stdout.readline()  # Read response
        
        # Load workspace
        load_request = {
            "jsonrpc": "2.0",
            "method": "tools/call",
            "params": {
                "name": "dotnet-load-workspace",
                "arguments": {"path": "./src/McpRoslyn/McpRoslyn.sln"}
            },
            "id": 2
        }
        proc.stdin.write(json.dumps(load_request) + '\n')
        proc.stdin.flush()
        proc.stdout.readline()  # Read response
        time.sleep(2)  # Give time to load
        
        # Test different patterns
        patterns = [
            "*Controller",    # Find all controllers
            "I*",            # Find all interfaces
            "*Manager",      # Find all managers
            "Base*",         # Find all base classes
            "*Repository",   # Find all repositories
            "User*",         # Find all user-related types
            "*Status",       # Find all status enums
            "Point*",        # Find structs starting with Point
            "*Service",      # Find all services
            "?ser*"          # Find types with second char 's', third 'e', fourth 'r'
        ]
        
        id_counter = 3
        for pattern in patterns:
            find_request = {
                "jsonrpc": "2.0",
                "method": "tools/call",
                "params": {
                    "name": "dotnet-find-class",
                    "arguments": {"pattern": pattern}
                },
                "id": id_counter
            }
            send_request(proc, find_request)
            id_counter += 1
            time.sleep(0.5)
        
    finally:
        proc.terminate()
        proc.wait()

if __name__ == "__main__":
    print("🔍 Testing MCP Find-Class Tool with Various Patterns")
    print("=" * 60)
    test_find_patterns()