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
            return None
    return None

def test_edit_code():
    # Start the MCP server
    cmd = [
        "dotnet", "run", 
        "--project", "./src/Spelunk.Server/Spelunk.Server.csproj"]
    
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
    time.sleep(3)
    
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
                "arguments": {"path": "./src/Spelunk.Server/Spelunk.Server.sln"}
            },
            "id": 2
        }
        proc.stdin.write(json.dumps(load_request) + '\n')
        proc.stdin.flush()
        proc.stdout.readline()  # Read response
        time.sleep(2)  # Give time to load
        
        print("🔧 Testing Surgical Code Edit Tool")
        print("=" * 60)
        
        id_counter = 3
        
        # Test 1: Add a method to UserController
        print("\n=== 1. ADD METHOD TO CLASS ===")
        print("📤 Adding 'ValidateUserAsync' method to UserController")
        request = {
            "jsonrpc": "2.0",
            "method": "tools/call",
            "params": {
                "name": "spelunk-edit-code",
                "arguments": {
                    "file": "./src/Spelunk.Server/TestClasses.cs",
                    "operation": "add-method",
                    "className": "UserController",
                    "code": """public async Task<bool> ValidateUserAsync(int userId)
{
    var user = await GetUserAsync(userId);
    return !string.IsNullOrEmpty(user);
}""",
                    "preview": True
                }
            },
            "id": id_counter
        }
        send_request(proc, request)
        id_counter += 1
        time.sleep(0.5)
        
        # Test 2: Add a property to UserController
        print("\n=== 2. ADD PROPERTY TO CLASS ===")
        print("📤 Adding 'LastAccessTime' property to UserController")
        request = {
            "jsonrpc": "2.0",
            "method": "tools/call",
            "params": {
                "name": "spelunk-edit-code",
                "arguments": {
                    "file": "./src/Spelunk.Server/TestClasses.cs",
                    "operation": "add-property",
                    "className": "UserController",
                    "code": "public DateTime LastAccessTime { get; set; } = DateTime.Now;",
                    "preview": True
                }
            },
            "id": id_counter
        }
        send_request(proc, request)
        id_counter += 1
        time.sleep(0.5)
        
        # Test 3: Make a method async
        print("\n=== 3. MAKE METHOD ASYNC ===")
        print("📤 Making 'GetUser' method async in UserController")
        request = {
            "jsonrpc": "2.0",
            "method": "tools/call",
            "params": {
                "name": "spelunk-edit-code",
                "arguments": {
                    "file": "./src/Spelunk.Server/TestClasses.cs",
                    "operation": "make-async",
                    "className": "UserController",
                    "methodName": "GetUser",
                    "preview": True
                }
            },
            "id": id_counter
        }
        send_request(proc, request)
        id_counter += 1
        time.sleep(0.5)
        
        # Test 4: Try to make an already async method async (should fail)
        print("\n=== 4. MAKE ALREADY ASYNC METHOD ASYNC (should fail) ===")
        print("📤 Trying to make 'GetUserAsync' async (already async)")
        request = {
            "jsonrpc": "2.0",
            "method": "tools/call",
            "params": {
                "name": "spelunk-edit-code",
                "arguments": {
                    "file": "./src/Spelunk.Server/TestClasses.cs",
                    "operation": "make-async",
                    "className": "UserController",
                    "methodName": "GetUserAsync",
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
    print("🔧 Testing MCP Surgical Code Edit Tool")
    print("=" * 70)
    test_edit_code()