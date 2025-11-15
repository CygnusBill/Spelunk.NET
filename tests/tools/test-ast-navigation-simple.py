#!/usr/bin/env python3
"""
Test the new AST navigation tools
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
    
    print(f">>> Sending: {json.dumps(request)}")
    process.stdin.write(json.dumps(request) + '\n')
    process.stdin.flush()
    
    # Read response
    response_line = process.stdout.readline()
    print(f"<<< Received: {response_line.strip()}")
    
    try:
        response = json.loads(response_line)
        if "error" in response and response["error"] is not None:
            print(f"ERROR: {response['error']}")
            return None
        result = response.get("result", {})
        # Handle content array format
        if isinstance(result, dict) and "content" in result:
            content = result["content"]
            if content and len(content) > 0 and content[0].get("type") == "text":
                try:
                    return json.loads(content[0]["text"])
                except:
                    return content[0]["text"]
        return result
    except json.JSONDecodeError as e:
        print(f"Failed to parse response: {e}")
        return None

def test_query_syntax(process):
    """Test the dotnet-query-syntax tool"""
    print("\n" + "="*60)
    print("Testing dotnet-query-syntax")
    print("="*60)
    
    # Test 1: Find classes
    response = send_request(process, "tools/call", {
        "name": "spelunk-query-syntax",
        "arguments": {
            "roslynPath": "//class",
            "file": os.path.abspath("test-workspace/Program.cs")
        }
    })
    
    if response and "nodes" in response and len(response['nodes']) > 0:
        print(f"✓ Found {len(response['nodes'])} classes")
        for node in response['nodes'][:3]:  # Show first 3
            print(f"  - Type: {node['nodeType']}, Text: {node['text'][:50]}...")
    else:
        print("✗ Failed to find classes")
        return False
        
    # Test 2: Find binary expressions with ==
    print("\nQuerying for binary expressions with == operator...")
    response = send_request(process, "tools/call", {
        "name": "spelunk-query-syntax",
        "arguments": {
            "roslynPath": "//binary-expression[@operator='==']",
            "file": os.path.abspath("test-workspace/Program.cs")
        }
    })
    
    if response and "nodes" in response:
        print(f"✓ Found {len(response['nodes'])} == comparisons")
        return True
    else:
        print("✗ Failed to query binary expressions")
        return False

def test_navigate(process):
    """Test the dotnet-navigate tool"""
    print("\n" + "="*60)
    print("Testing dotnet-navigate")
    print("="*60)
    
    response = send_request(process, "tools/call", {
        "name": "spelunk-navigate",
        "arguments": {
            "from": {
                "file": os.path.abspath("test-workspace/Program.cs"),
                "line": 10,
                "column": 10
            },
            "path": "ancestor::class[1]",
            "returnPath": True
        }
    })
    
    if response and "navigatedTo" in response:
        nav = response["navigatedTo"]
        if nav:
            print(f"✓ Navigated to: {nav.get('type', 'unknown')} '{nav.get('name', 'unnamed')}'")
            print(f"  Location: line {nav.get('location', {}).get('line', '?')}")
            if nav.get('path'):
                print(f"  Path: {nav['path']}")
        else:
            print("✓ No navigation target found (expected for some positions)")
        return True
    else:
        print("✗ Failed to navigate")
        return False

def test_get_ast(process):
    """Test the dotnet-get-ast tool"""
    print("\n" + "="*60)
    print("Testing dotnet-get-ast")
    print("="*60)
    
    response = send_request(process, "tools/call", {
        "name": "spelunk-get-ast",
        "arguments": {
            "file": os.path.abspath("test-workspace/Program.cs"),
            "depth": 2
        }
    })
    
    if response and "ast" in response:
        print(f"✓ Retrieved AST:")
        print(f"  Type: {response['ast'].get('type', 'unknown')}")
        print(f"  Children: {len(response['ast'].get('children', []))}")
        return True
    else:
        print("✗ Failed to get AST")
        return False

def main():
    """Run tests for AST navigation tools"""
    # Start the server
    print("Starting MCP Roslyn Server...")
    
    # Set environment variable for allowed paths
    env = os.environ.copy()
    env['MCP_DOTNET_ALLOWED_PATHS'] = os.path.abspath(".")
    
    process = subprocess.Popen(
        ["dotnet", "run", "--project", "src/McpDotnet.Server/McpDotnet.Server.csproj", "--no-build"],
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        bufsize=1,
        env=env
    )
    
    try:
        # Give server time to start
        time.sleep(2)
        
        # Initialize
        print("Initializing server...")
        response = send_request(process, "initialize", {
            "protocolVersion": "2024-11-05",
            "capabilities": {"tools": {}},
            "clientInfo": {"name": "test-ast-navigation", "version": "1.0.0"}
        })
        
        if not response:
            print("Failed to initialize server")
            return 1
            
        # Load workspace
        print("\nLoading workspace...")
        response = send_request(process, "tools/call", {
            "name": "spelunk-load-workspace",
            "arguments": {
                "path": os.path.abspath("test-workspace/TestProject.csproj")
            }
        })
        
        if not response:
            print("Failed to load workspace - no response")
            return 1
        
        # Extract workspace ID from the response
        workspace_id = None
        if isinstance(response, dict) and "id" in response:
            workspace_id = response["id"]
        elif isinstance(response, dict) and "Id" in response:
            workspace_id = response["Id"]
        else:
            print(f"Warning: Could not extract workspace ID from response: {response}")
            
        print(f"✓ Workspace loaded{': ' + str(workspace_id) if workspace_id else ''}")
        
        # Run tests
        all_passed = True
        all_passed &= test_query_syntax(process)
        all_passed &= test_get_ast(process)
        all_passed &= test_navigate(process)
        
        if all_passed:
            print("\n✅ All tests passed!")
            return 0
        else:
            print("\n❌ Some tests failed!")
            return 1
            
    finally:
        # Clean up
        print("\nShutting down server...")
        process.terminate()
        try:
            process.wait(timeout=2)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait()

if __name__ == "__main__":
    sys.exit(main())