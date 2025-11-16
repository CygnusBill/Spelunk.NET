#!/usr/bin/env python3
import json
import subprocess
import time
import os

def send_request(proc, request):
    """Send a JSON-RPC request and get response"""
    json_str = json.dumps(request)
    pattern = request['params']['arguments'].get('methodPattern') or request['params']['arguments'].get('propertyPattern')
    class_pattern = request['params']['arguments'].get('classPattern', '')
    
    if class_pattern:
        print(f"\n📤 Searching: '{pattern}' in classes matching '{class_pattern}'")
    else:
        print(f"\n📤 Searching: '{pattern}'")
    
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

def test_member_search():
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
                "arguments": {"path": "./src/Spelunk.Server/Spelunk.Server.sln"}
            },
            "id": 2
        }
        proc.stdin.write(json.dumps(load_request) + '\n')
        proc.stdin.flush()
        proc.stdout.readline()  # Read response
        time.sleep(2)  # Give time to load
        
        print("🔍 Testing Find-Method Tool")
        print("=" * 60)
        
        # Test method patterns
        method_tests = [
            # Method pattern only
            {"methodPattern": "Get*"},              # All Get methods
            {"methodPattern": "*Async"},            # All async methods
            {"methodPattern": "Load*"},             # All Load methods
            {"methodPattern": "GetDefault*"},       # Static methods
            
            # With class pattern
            {"methodPattern": "Get*", "classPattern": "*Controller"},  # Get methods in controllers
            {"methodPattern": "*Async", "classPattern": "User*"},      # Async methods in User classes
            {"methodPattern": "*", "classPattern": "Base*"},           # All methods in base classes
        ]
        
        id_counter = 3
        for test in method_tests:
            request = {
                "jsonrpc": "2.0",
                "method": "tools/call",
                "params": {
                    "name": "spelunk-find-method",
                    "arguments": test
                },
                "id": id_counter
            }
            send_request(proc, request)
            id_counter += 1
            time.sleep(0.5)
        
        print("\n🔍 Testing Find-Property Tool")
        print("=" * 60)
        
        # Test property patterns
        property_tests = [
            # Property pattern only
            {"propertyPattern": "Is*"},             # All Is* properties (booleans)
            {"propertyPattern": "*Count"},          # All Count properties
            {"propertyPattern": "_*"},              # All private fields
            {"propertyPattern": "*Name"},           # All Name properties
            
            # With class pattern
            {"propertyPattern": "*", "classPattern": "*Controller"},   # All properties in controllers
            {"propertyPattern": "Is*", "classPattern": "*"},          # All boolean properties
            {"propertyPattern": "_*", "classPattern": "*Controller"}, # Private fields in controllers
        ]
        
        for test in property_tests:
            request = {
                "jsonrpc": "2.0",
                "method": "tools/call",
                "params": {
                    "name": "spelunk-find-property",
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
    print("🔍 Testing MCP Find-Method and Find-Property Tools")
    print("=" * 70)
    test_member_search()