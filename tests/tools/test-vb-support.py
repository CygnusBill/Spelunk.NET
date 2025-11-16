#!/usr/bin/env python3
"""Test VB.NET support in find-statements tool"""

import sys
import os
import json
import subprocess
import time

# Add parent directory to path to import test utilities
sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from utils.test_utils import send_request, read_response

def main():
    print("Starting VB.NET support test...")
    print()
    
    # Start the server
    project_root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    server_path = os.path.join(project_root, "src/Spelunk.Server/bin/Debug/net10.0/Spelunk.Server")
    
    if not os.path.exists(server_path):
        print(f"Server not found at {server_path}. Please build the project first.")
        return
    
    # Set environment variable for allowed paths

    
    env = os.environ.copy()

    
    env["MCP_DOTNET_ALLOWED_PATHS"] = os.path.abspath(".")

    
    

    
    process = subprocess.Popen(
        [server_path],
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        bufsize=0
    ,
        env=env)
    
    try:
        # Initialize
        print("=== Initializing ===")
        send_request(process, "initialize", {
            "protocolVersion": "2024-11-05"
        })
        response = read_response(process)
        print(f"<<< Response: {json.dumps(response, indent=2)}")
        print()
        
        # Load VB.NET test project
        print("=== Loading VB.NET project ===")
        vb_project_path = os.path.join(project_root, "test-workspace/VBTestProject/VBTestProject.vbproj")
        send_request(process, "tools/call", {
            "name": "spelunk-load-workspace",
            "arguments": {
                "path": vb_project_path
            }
        })
        response = read_response(process)
        print(f"<<< Response: {json.dumps(response, indent=2)}")
        print()
        
        # Test 1: Find all statements
        print("=== Test 1: Find all statements ===")
        send_request(process, "tools/call", {
            "name": "spelunk-find-statements",
            "arguments": {
                "pattern": "Return",
                "patternType": "text"
            }
        })
        response = read_response(process)
        print(f"<<< Response: {json.dumps(response, indent=2)}")
        print()
        
        # Test 2: Find statements with specific pattern
        print("=== Test 2: Find Throw statements ===")
        send_request(process, "tools/call", {
            "name": "spelunk-find-statements",
            "arguments": {
                "pattern": "Throw",
                "patternType": "text"
            }
        })
        response = read_response(process)
        print(f"<<< Response: {json.dumps(response, indent=2)}")
        print()
        
        # Test 3: Find statements in specific method
        print("=== Test 3: Find statements in Divide method ===")
        send_request(process, "tools/call", {
            "name": "spelunk-find-statements",
            "arguments": {
                "pattern": ".*",
                "patternType": "regex",
                "scope": {
                    "methodName": "Divide"
                }
            }
        })
        response = read_response(process)
        print(f"<<< Response: {json.dumps(response, indent=2)}")
        
    finally:
        print("\nShutting down server...")
        process.terminate()
        process.wait()

if __name__ == "__main__":
    main()