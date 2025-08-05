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
        "--project", "./src/McpRoslyn/McpRoslyn.Server/McpRoslyn.Server.csproj"]
    
    print("Starting MCP server...")
    env = os.environ.copy()
    env["MCP_ROSLYN_ALLOWED_PATHS"] = os.path.abspath(".")
    
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
                "protocolVersion": "0.1.0",
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
        
        print("🔍 Testing Find-Method-Calls Tool")
        print("=" * 60)
        
        # Test find-method-calls (what methods does this method call?)
        calls_tests = [
            {"methodName": "GetUserAsync", "className": "UserController"},
            {"methodName": "GetByIdAsync", "className": "UserRepository"},
            {"methodName": "LoadWorkspaceAsync", "className": "RoslynWorkspaceManager"},
            {"methodName": "FindMethodsAsync", "className": "RoslynWorkspaceManager"},
        ]
        
        id_counter = 3
        for test in calls_tests:
            request = {
                "jsonrpc": "2.0",
                "method": "tools/call",
                "params": {
                    "name": "dotnet-find-method-calls",
                    "arguments": test
                },
                "id": id_counter
            }
            send_request(proc, request)
            id_counter += 1
            time.sleep(0.5)
        
        print("\n🔍 Testing Find-Method-Callers Tool")
        print("=" * 60)
        
        # Test find-method-callers (what methods call this method?)
        callers_tests = [
            {"methodName": "GetDefaultUserName", "className": "UserController"},
            {"methodName": "GetByIdAsync", "className": "BaseRepository"},
            {"methodName": "GetWorkspace", "className": "RoslynWorkspaceManager"},
            {"methodName": "GetAccessModifier", "className": "RoslynWorkspaceManager"},
        ]
        
        for test in callers_tests:
            request = {
                "jsonrpc": "2.0",
                "method": "tools/call",
                "params": {
                    "name": "dotnet-find-method-callers",
                    "arguments": test
                },
                "id": id_counter
            }
            send_request(proc, request)
            id_counter += 1
            time.sleep(0.5)
        
    finally:
        proc.terminate()
        proc.wait()

if __name__ == "__main__":
    print("🔍 Testing MCP Find-Method-Calls and Find-Method-Callers Tools")
    print("=" * 70)
    test_method_calls()