#!/usr/bin/env python3
"""
Test the find-statements tool with RoslynPath integration in MCP Roslyn Server
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
    
    request_str = json.dumps(request)
    print(f"\n>>> Sending: {request_str}")
    
    process.stdin.write(request_str + "\n")
    process.stdin.flush()
    
    # Read response
    response_line = process.stdout.readline()
    if response_line:
        print(f"<<< Response: {response_line.strip()}")
        return json.loads(response_line)
    return None

def run_test(title, pattern, pattern_type="roslynpath", expected_count=None):
    """Run a single test case"""
    print(f"\n{'='*60}")
    print(f"Test: {title}")
    print(f"Pattern: {pattern}")
    print(f"Type: {pattern_type}")
    print('='*60)
    
    # Find the server project
    server_project = os.path.join(os.path.dirname(__file__), 
                                  "../../src/McpRoslyn/McpRoslyn.Server/McpRoslyn.Server.csproj")
    test_workspace = os.path.join(os.path.dirname(__file__), "../../test-workspace")
    
    # Start the server
    server_cmd = [
        "dotnet", "run",
        "--project", os.path.abspath(server_project)]
    
    process = subprocess.Popen(
        server_cmd,
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        preexec_fn=os.setsid if sys.platform != "win32" else None
    )
    
    try:
        time.sleep(3)  # Give server time to start
        
        # Initialize
        response = send_request(process, "initialize", {
            "protocolVersion": "0.1.0",
            "capabilities": {
                "tools": {}
            },
            "clientInfo": {
                "name": "test-client",
                "version": "1.0.0"
            }
        })
        
        if not response:
            print("ERROR: No response from initialize")
            return False
            
        # Load workspace
        print("\n--- Loading workspace ---")
        response = send_request(process, "tools/call", {
            "name": "dotnet-load-workspace",
            "arguments": {
                "path": os.path.join(test_workspace, "TestProject.csproj")
            }
        })
        
        time.sleep(2)  # Give time to load
        
        # Find statements with RoslynPath
        print(f"\n--- Finding statements with pattern: {pattern} ---")
        response = send_request(process, "tools/call", {
            "name": "dotnet-find-statements",
            "arguments": {
                "pattern": pattern,
                "patternType": pattern_type,
                "includeNestedStatements": True
            }
        })
        
        if response and "result" in response:
            result = response["result"]
            if "content" in result and len(result["content"]) > 0:
                text = result["content"][0].get("text", "")
                print(f"\nResult:\n{text}")
                
                # Check expected count if provided
                if expected_count is not None:
                    if f"Found {expected_count} statement" in text:
                        print(f"✓ Expected count {expected_count} matches!")
                        return True
                    else:
                        print(f"✗ Expected count {expected_count} does not match!")
                        return False
                return True
            else:
                print("ERROR: No content in response")
                return False
        else:
            print("ERROR: Invalid response format")
            return False
            
    except Exception as e:
        print(f"ERROR: {e}")
        return False
    finally:
        # Clean up
        if sys.platform == "win32":
            process.terminate()
        else:
            os.killpg(os.getpgid(process.pid), signal.SIGTERM)
        process.wait()

def main():
    print("Testing find-statements with RoslynPath integration...")
    
    tests = [
        # Test 1: Find all if statements
        ("Find all if statements", "//statement[@type=IfStatement]", "roslynpath"),
        
        # Test 2: Find Console.WriteLine statements
        ("Find Console.WriteLine calls", "//statement[@contains='Console.WriteLine']", "roslynpath"),
        
        # Test 3: Find return statements in async methods
        ("Find returns in async methods", "//method[@async]//statement[@type=ReturnStatement]", "roslynpath"),
        
        # Test 4: Traditional text search for comparison
        ("Traditional text search for Console", "Console", "text"),
        
        # Test 5: Find null checks in if statements
        ("Find null checks", "//statement[@type=IfStatement and @contains='== null']", "roslynpath"),
        
        # Test 6: Find statements in specific method
        ("Find statements in Main method", "//method[Main]//statement", "roslynpath")
    ]
    
    passed = 0
    failed = 0
    
    for test in tests:
        if len(test) == 3:
            title, pattern, pattern_type = test
            expected = None
        else:
            title, pattern, pattern_type, expected = test
            
        if run_test(title, pattern, pattern_type, expected):
            passed += 1
        else:
            failed += 1
    
    print(f"\n{'='*60}")
    print(f"Test Summary: {passed} passed, {failed} failed")
    print('='*60)
    
    return 0 if failed == 0 else 1

if __name__ == "__main__":
    sys.exit(main())