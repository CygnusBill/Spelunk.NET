#!/usr/bin/env python3
import json
import subprocess
import time
import os

def send_request(proc, request):
    """Send a JSON-RPC request and get response"""
    json_str = json.dumps(request)
    method_name = request['params']['arguments'].get('methodName', '')
    class_name = request['params']['arguments'].get('className', '')
    tool_name = request['params']['name']
    
    if 'find-method-calls' in tool_name:
        print(f"\n📤 Finding methods called by {class_name}.{method_name}")
    else:
        print(f"\n📤 Finding methods that call {class_name}.{method_name}")
    
    proc.stdin.write(json_str + '\n')
    proc.stdin.flush()
    
    # Read response
    response = proc.stdout.readline()
    if response:
        data = json.loads(response)
        if 'result' in data and 'content' in data['result']:
            text = data['result']['content'][0]['text']
            print(f"📥 Response:\n{text}")
        elif 'error' in data:
            print(f"❌ Error: {data['error']}")
        return data
    return None

def test_method_calls():
    # Start the MCP server
    cmd = [
        "dotnet", "run", 
        "--project", "./src/McpRoslyn/McpDotnet.Server/McpDotnet.Server.csproj"]
    
    print("Starting MCP server...")
    proc = subprocess.Popen(
        cmd,
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        bufsize=0
    ,
        env={"MCP_DOTNET_ALLOWED_PATHS": os.path.abspath(.)})
    
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
                "name": "spelunk-load-workspace",
                "arguments": {"path": "./src/McpRoslyn/McpRoslyn.sln"}
            },
            "id": 2
        }
        proc.stdin.write(json.dumps(load_request) + '\n')
        proc.stdin.flush()
        proc.stdout.readline()  # Read response
        time.sleep(2)  # Give time to load
        
        print("🔍 Enhanced Testing of Method Call Analysis")
        print("=" * 60)
        
        # Test interesting method call chains
        id_counter = 3
        
        # Test UserController.GetUserAsync - should show the call tree
        request = {
            "jsonrpc": "2.0",
            "method": "tools/call",
            "params": {
                "name": "spelunk-find-method-calls",
                "arguments": {
                    "methodName": "GetUserAsync",
                    "className": "UserController"
                }
            },
            "id": id_counter
        }
        send_request(proc, request)
        id_counter += 1
        time.sleep(0.5)
        
        # Test UserController.ProcessUser - who calls it?
        request = {
            "jsonrpc": "2.0",
            "method": "tools/call",
            "params": {
                "name": "spelunk-find-method-callers",
                "arguments": {
                    "methodName": "ProcessUser",
                    "className": "UserController"
                }
            },
            "id": id_counter
        }
        send_request(proc, request)
        id_counter += 1
        time.sleep(0.5)
        
        # Test UserController.GetDefaultUserName - who calls this static method?
        request = {
            "jsonrpc": "2.0",
            "method": "tools/call",
            "params": {
                "name": "spelunk-find-method-callers",
                "arguments": {
                    "methodName": "GetDefaultUserName",
                    "className": "UserController"
                }
            },
            "id": id_counter
        }
        send_request(proc, request)
        id_counter += 1
        time.sleep(0.5)
        
        # Test UserRepository.GetByIdAsync - what does it call?
        request = {
            "jsonrpc": "2.0",
            "method": "tools/call",
            "params": {
                "name": "spelunk-find-method-calls",
                "arguments": {
                    "methodName": "GetByIdAsync",
                    "className": "UserRepository"
                }
            },
            "id": id_counter
        }
        send_request(proc, request)
        id_counter += 1
        time.sleep(0.5)
        
        # Test IMessageLogger.Log - who calls it?
        request = {
            "jsonrpc": "2.0",
            "method": "tools/call",
            "params": {
                "name": "spelunk-find-method-callers",
                "arguments": {
                    "methodName": "Log",
                    "className": "IMessageLogger"
                }
            },
            "id": id_counter
        }
        send_request(proc, request)
        id_counter += 1
        
    finally:
        proc.terminate()
        proc.wait()

if __name__ == "__main__":
    print("🔍 Enhanced Testing of MCP Method Call Analysis Tools")
    print("=" * 70)
    test_method_calls()