#!/usr/bin/env python3
import json
import subprocess
import time
import os

def send_request(proc, request):
    """Send a JSON-RPC request and get response"""
    json_str = json.dumps(request)
    proc.stdin.write(json_str + '\n')
    proc.stdin.flush()
    
    # Read response
    response = proc.stdout.readline()
    if response and response.strip():
        try:
            data = json.loads(response)
            if 'result' in data and 'content' in data['result']:
                text = data['result']['content'][0]['text']
                print(f"📥 Response:\n{text}")
            elif 'error' in data:
                print(f"❌ Error: {data['error']}")
            return data
        except json.JSONDecodeError as e:
            print(f"❌ Failed to parse response: {e}")
            print(f"Raw response: {response}")
            return None
    return None

def test_at_keywords():
    # Start the MCP server
    cmd = [
        "dotnet", "run", 
        "--project", "./src/McpRoslyn/McpDotnet.Server/McpDotnet.Server.csproj",
        "--no-build"]
    
    print("Starting MCP server...")
    proc = subprocess.Popen(
        cmd,
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        bufsize=0,
        env={"MCP_DOTNET_ALLOWED_PATHS": os.path.abspath(".")})
    
    # Give server time to start and check stderr
    time.sleep(3)
    
    # Check if server started properly
    stderr_output = proc.stderr.readline()
    if stderr_output:
        print(f"Server stderr: {stderr_output.strip()}")
    
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
                "name": "spelunk-load-workspace",
                "arguments": {"path": "./src/McpRoslyn/McpRoslyn.sln"}
            },
            "id": 2
        }
        proc.stdin.write(json.dumps(load_request) + '\n')
        proc.stdin.flush()
        proc.stdout.readline()  # Read response
        time.sleep(2)  # Give time to load
        
        print("🔐 Testing @ Keyword Identifiers")
        print("=" * 60)
        
        id_counter = 3
        
        # Test 1: Try to rename to 'class' (should fail)
        print("\n=== 1. RENAME TO 'class' (should fail) ===")
        request = {
            "jsonrpc": "2.0",
            "method": "tools/call",
            "params": {
                "name": "spelunk-rename-symbol",
                "arguments": {
                    "oldName": "GetUserAsync",
                    "newName": "class",
                    "symbolType": "method",
                    "containerName": "UserController",
                    "preview": True
                }
            },
            "id": id_counter
        }
        send_request(proc, request)
        id_counter += 1
        time.sleep(0.5)
        
        # Test 2: Rename to '@class' (should succeed)
        print("\n=== 2. RENAME TO '@class' (should succeed) ===")
        request = {
            "jsonrpc": "2.0",
            "method": "tools/call",
            "params": {
                "name": "spelunk-rename-symbol",
                "arguments": {
                    "oldName": "GetUserAsync",
                    "newName": "@class",
                    "symbolType": "method",
                    "containerName": "UserController",
                    "preview": True
                }
            },
            "id": id_counter
        }
        send_request(proc, request)
        id_counter += 1
        time.sleep(0.5)
        
        # Test 3: Invalid identifier
        print("\n=== 3. INVALID IDENTIFIER '123abc' ===")
        request = {
            "jsonrpc": "2.0",
            "method": "tools/call",
            "params": {
                "name": "spelunk-rename-symbol",
                "arguments": {
                    "oldName": "GetUserAsync",
                    "newName": "123abc",
                    "symbolType": "method",
                    "containerName": "UserController",
                    "preview": True
                }
            },
            "id": id_counter
        }
        send_request(proc, request)
        id_counter += 1
        time.sleep(0.5)
        
        # Test 4: Valid identifier with underscore
        print("\n=== 4. VALID IDENTIFIER '_myMethod' ===")
        request = {
            "jsonrpc": "2.0",
            "method": "tools/call",
            "params": {
                "name": "spelunk-rename-symbol",
                "arguments": {
                    "oldName": "GetUserAsync",
                    "newName": "_myMethod",
                    "symbolType": "method",
                    "containerName": "UserController",
                    "preview": True
                }
            },
            "id": id_counter
        }
        send_request(proc, request)
        id_counter += 1
        time.sleep(0.5)
        
        # Test 5: Empty @ identifier
        print("\n=== 5. EMPTY @ IDENTIFIER '@' ===")
        request = {
            "jsonrpc": "2.0",
            "method": "tools/call",
            "params": {
                "name": "spelunk-rename-symbol",
                "arguments": {
                    "oldName": "GetUserAsync",
                    "newName": "@",
                    "symbolType": "method",
                    "containerName": "UserController",
                    "preview": True
                }
            },
            "id": id_counter
        }
        send_request(proc, request)
        
    finally:
        proc.terminate()
        proc.wait()

if __name__ == "__main__":
    print("🔐 Testing C# @ Keyword Identifier Support")
    print("=" * 70)
    test_at_keywords()