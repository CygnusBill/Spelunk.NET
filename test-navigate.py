#!/usr/bin/env python3
"""Quick test for navigation functionality"""

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

def main():
    print("Starting quick navigation test...")
    
    # Start the server
    process = subprocess.Popen(
        ["dotnet", "run", "--project", "src/McpRoslyn.Server/McpRoslyn.Server.csproj"],
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        bufsize=1,
        preexec_fn=os.setsid
    )
    
    try:
        # Give server time to start
        time.sleep(2)
        
        # Initialize
        print("Initializing server...")
        response = send_request(process, "initialize", {
            "protocolVersion": "0.1.0",
            "capabilities": {"tools": {}},
            "clientInfo": {"name": "test-navigate", "version": "1.0.0"}
        })
        
        if not response:
            print("Failed to initialize server")
            return 1
            
        # Load workspace
        print("\nLoading workspace...")
        response = send_request(process, "tools/call", {
            "name": "dotnet-load-workspace",
            "arguments": {
                "path": os.path.abspath("test-workspace/TestProject.csproj")
            }
        })
        
        if not response or "Id" not in response:
            print("Failed to load workspace")
            return 1
            
        print(f"✓ Workspace loaded: {response['Id']}")
        
        # Test simple navigation: parent
        print("\nTest 1: Navigate to parent from a statement")
        response = send_request(process, "tools/call", {
            "name": "dotnet-navigate",
            "arguments": {
                "from": {
                    "file": os.path.abspath("test-workspace/Program.cs"),
                    "line": 7,  # Inside Console.WriteLine
                    "column": 10
                },
                "path": "parent",
                "returnPath": True
            }
        })
        
        if response and "navigatedTo" in response and response["navigatedTo"]:
            nav = response["navigatedTo"]
            print(f"✓ Navigated to parent: {nav.get('type', 'unknown')} '{nav.get('name', 'unnamed')}'")
            print(f"  Location: line {nav.get('location', {}).get('line', '?')}")
        else:
            print("✗ Parent navigation failed or returned null")
        
        # Test ancestor navigation
        print("\nTest 2: Navigate to ancestor")
        response = send_request(process, "tools/call", {
            "name": "dotnet-navigate",
            "arguments": {
                "from": {
                    "file": os.path.abspath("test-workspace/Program.cs"),
                    "line": 7,  # Inside Console.WriteLine
                    "column": 10
                },
                "path": "ancestor",
                "returnPath": True
            }
        })
        
        if response and "navigatedTo" in response and response["navigatedTo"]:
            nav = response["navigatedTo"]
            print(f"✓ Navigated to ancestor: {nav.get('type', 'unknown')} '{nav.get('name', 'unnamed')}'")
            print(f"  Location: line {nav.get('location', {}).get('line', '?')}")
        else:
            print("✗ Ancestor navigation failed or returned null")
            
        print("\n✅ Navigation tests completed!")
        return 0
            
    finally:
        # Clean up
        print("\nShutting down server...")
        try:
            os.killpg(os.getpgid(process.pid), signal.SIGTERM)
        except:
            process.terminate()
        process.wait()

if __name__ == "__main__":
    sys.exit(main())