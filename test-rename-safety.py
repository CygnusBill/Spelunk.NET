#!/usr/bin/env python3
import json
import subprocess
import time

def send_request(proc, request):
    """Send a JSON-RPC request and get response"""
    json_str = json.dumps(request)
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

def test_rename_safety():
    # Start the MCP server
    cmd = [
        "dotnet", "run", 
        "--project", "/Users/bill/ClaudeDir/McpDotnet/src/McpRoslyn/McpRoslyn.Server/McpRoslyn.Server.csproj",
        "--", "--allowed-path", "/Users/bill/ClaudeDir/McpDotnet"
    ]
    
    print("Starting MCP server...")
    proc = subprocess.Popen(
        cmd,
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        bufsize=0
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
                "name": "dotnet/load-workspace",
                "arguments": {"path": "/Users/bill/ClaudeDir/McpDotnet/src/McpRoslyn/McpRoslyn.sln"}
            },
            "id": 2
        }
        proc.stdin.write(json.dumps(load_request) + '\n')
        proc.stdin.flush()
        proc.stdout.readline()  # Read response
        time.sleep(2)  # Give time to load
        
        print("🔐 Testing Rename Safety Features")
        print("=" * 60)
        
        id_counter = 3
        
        # Test 1: Try to rename a C# keyword
        print("\n=== 1. TRY TO RENAME TO A C# KEYWORD ===")
        print("📤 Attempting to rename 'GetUserAsync' to 'class' (reserved keyword)")
        request = {
            "jsonrpc": "2.0",
            "method": "tools/call",
            "params": {
                "name": "dotnet/rename-symbol",
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
        
        # Test 2: Try to rename a system type
        print("\n=== 2. TRY TO RENAME A SYSTEM TYPE ===")
        print("📤 Attempting to rename 'string' to 'MyString' (dangerous!)")
        request = {
            "jsonrpc": "2.0",
            "method": "tools/call",
            "params": {
                "name": "dotnet/rename-symbol",
                "arguments": {
                    "oldName": "string",
                    "newName": "MyString",
                    "symbolType": "type",
                    "preview": True
                }
            },
            "id": id_counter
        }
        send_request(proc, request)
        id_counter += 1
        time.sleep(0.5)
        
        # Test 3: Safe rename with preview - showing impact
        print("\n=== 3. SAFE RENAME WITH IMPACT ANALYSIS ===")
        print("📤 Preview renaming 'UserController' to 'UserManager'")
        request = {
            "jsonrpc": "2.0",
            "method": "tools/call",
            "params": {
                "name": "dotnet/rename-symbol",
                "arguments": {
                    "oldName": "UserController",
                    "newName": "UserManager",
                    "symbolType": "type",
                    "preview": True
                }
            },
            "id": id_counter
        }
        send_request(proc, request)
        id_counter += 1
        time.sleep(0.5)
        
        # Test 4: Try to create a naming conflict
        print("\n=== 4. TRY TO CREATE A NAMING CONFLICT ===")
        print("📤 Attempting to rename 'GetDefaultUserName' to 'Name' (property already exists)")
        request = {
            "jsonrpc": "2.0",
            "method": "tools/call",
            "params": {
                "name": "dotnet/rename-symbol",
                "arguments": {
                    "oldName": "GetDefaultUserName",
                    "newName": "Name",
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
        
        # Test 5: Public API warning
        print("\n=== 5. PUBLIC API WARNING ===")
        print("📤 Renaming public method 'GetByIdAsync' (shows public API warning)")
        request = {
            "jsonrpc": "2.0",
            "method": "tools/call",
            "params": {
                "name": "dotnet/rename-symbol",
                "arguments": {
                    "oldName": "GetByIdAsync",
                    "newName": "FetchByIdAsync",
                    "symbolType": "method",
                    "containerName": "UserRepository",
                    "preview": True
                }
            },
            "id": id_counter
        }
        send_request(proc, request)
        id_counter += 1
        
        # Test 6: Empty name validation
        print("\n=== 6. EMPTY NAME VALIDATION ===")
        print("📤 Attempting to rename to empty string")
        request = {
            "jsonrpc": "2.0",
            "method": "tools/call",
            "params": {
                "name": "dotnet/rename-symbol",
                "arguments": {
                    "oldName": "ProcessUser",
                    "newName": "",
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
    print("🔐 Testing MCP Rename Tool Safety Features")
    print("=" * 70)
    test_rename_safety()