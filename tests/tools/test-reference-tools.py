#!/usr/bin/env python3
import json
import subprocess
import time

def send_request(proc, request):
    """Send a JSON-RPC request and get response"""
    json_str = json.dumps(request)
    tool_name = request['params']['name']
    args = request['params']['arguments']
    
    # Print what we're doing
    if 'find-references' in tool_name:
        symbol_type = args.get('symbolType', '')
        symbol_name = args.get('symbolName', '')
        container = args.get('containerName', '')
        if container:
            print(f"\n📤 Finding references to {symbol_type} '{container}.{symbol_name}'")
        else:
            print(f"\n📤 Finding references to {symbol_type} '{symbol_name}'")
    elif 'find-implementations' in tool_name:
        print(f"\n📤 Finding implementations of '{args.get('interfaceName', '')}'")
    elif 'find-overrides' in tool_name:
        print(f"\n📤 Finding overrides of '{args.get('className', '')}.{args.get('methodName', '')}'")
    elif 'find-derived-types' in tool_name:
        print(f"\n📤 Finding types derived from '{args.get('baseClassName', '')}'")
    
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

def test_reference_tools():
    # Start the MCP server
    cmd = [
        "dotnet", "run", 
        "--project", "./src/McpRoslyn/McpRoslyn.Server/McpRoslyn.Server.csproj",
        "--", "--allowed-path", "."
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
                "name": "dotnet-load-workspace",
                "arguments": {"path": "./src/McpRoslyn/McpRoslyn.sln"}
            },
            "id": 2
        }
        proc.stdin.write(json.dumps(load_request) + '\n')
        proc.stdin.flush()
        proc.stdout.readline()  # Read response
        time.sleep(2)  # Give time to load
        
        print("🔍 Testing Reference Analysis Tools")
        print("=" * 60)
        
        id_counter = 3
        
        # Test 1: Find references to a type
        print("\n=== 1. FIND REFERENCES TO TYPES ===")
        request = {
            "jsonrpc": "2.0",
            "method": "tools/call",
            "params": {
                "name": "dotnet-find-references",
                "arguments": {
                    "symbolName": "UserController",
                    "symbolType": "type"
                }
            },
            "id": id_counter
        }
        send_request(proc, request)
        id_counter += 1
        time.sleep(0.5)
        
        # Test 2: Find references to a method
        print("\n=== 2. FIND REFERENCES TO METHODS ===")
        request = {
            "jsonrpc": "2.0",
            "method": "tools/call",
            "params": {
                "name": "dotnet-find-references",
                "arguments": {
                    "symbolName": "GetDefaultUserName",
                    "symbolType": "method",
                    "containerName": "UserController"
                }
            },
            "id": id_counter
        }
        send_request(proc, request)
        id_counter += 1
        time.sleep(0.5)
        
        # Test 3: Find references to a property
        print("\n=== 3. FIND REFERENCES TO PROPERTIES ===")
        request = {
            "jsonrpc": "2.0",
            "method": "tools/call",
            "params": {
                "name": "dotnet-find-references",
                "arguments": {
                    "symbolName": "Name",
                    "symbolType": "property",
                    "containerName": "UserController"
                }
            },
            "id": id_counter
        }
        send_request(proc, request)
        id_counter += 1
        time.sleep(0.5)
        
        # Test 4: Find implementations of IWorkspaceService
        print("\n=== 4. FIND INTERFACE IMPLEMENTATIONS ===")
        request = {
            "jsonrpc": "2.0",
            "method": "tools/call",
            "params": {
                "name": "dotnet-find-implementations",
                "arguments": {
                    "interfaceName": "IWorkspaceService"
                }
            },
            "id": id_counter
        }
        send_request(proc, request)
        id_counter += 1
        time.sleep(0.5)
        
        # Test 5: Find implementations of IMessageLogger
        request = {
            "jsonrpc": "2.0",
            "method": "tools/call",
            "params": {
                "name": "dotnet-find-implementations",
                "arguments": {
                    "interfaceName": "IMessageLogger"
                }
            },
            "id": id_counter
        }
        send_request(proc, request)
        id_counter += 1
        time.sleep(0.5)
        
        # Test 6: Find overrides of GetByIdAsync
        print("\n=== 5. FIND METHOD OVERRIDES ===")
        request = {
            "jsonrpc": "2.0",
            "method": "tools/call",
            "params": {
                "name": "dotnet-find-overrides",
                "arguments": {
                    "methodName": "GetByIdAsync",
                    "className": "BaseRepository"
                }
            },
            "id": id_counter
        }
        send_request(proc, request)
        id_counter += 1
        time.sleep(0.5)
        
        # Test 7: Find types derived from BaseRepository
        print("\n=== 6. FIND DERIVED TYPES ===")
        request = {
            "jsonrpc": "2.0",
            "method": "tools/call",
            "params": {
                "name": "dotnet-find-derived-types",
                "arguments": {
                    "baseClassName": "BaseRepository"
                }
            },
            "id": id_counter
        }
        send_request(proc, request)
        id_counter += 1
        time.sleep(0.5)
        
        # Test 8: Find types derived from WorkspaceInfo
        request = {
            "jsonrpc": "2.0",
            "method": "tools/call",
            "params": {
                "name": "dotnet-find-derived-types",
                "arguments": {
                    "baseClassName": "WorkspaceInfo"
                }
            },
            "id": id_counter
        }
        send_request(proc, request)
        
    finally:
        proc.terminate()
        proc.wait()

if __name__ == "__main__":
    print("🔍 Testing MCP Reference Analysis Tools")
    print("=" * 70)
    test_reference_tools()