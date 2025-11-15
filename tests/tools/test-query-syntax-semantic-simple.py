#!/usr/bin/env python3
"""Simple test for semantic enrichment in query-syntax tool"""

import os
import sys
import json
import subprocess

def test_semantic_simple():
    print("Testing semantic enrichment in query-syntax tool...")
    
    # Create init request
    init_request = {
        "jsonrpc": "2.0",
        "id": 0,
        "method": "initialize",
        "params": {
            "protocolVersion": "2024-11-05",
            "capabilities": {},
            "clientInfo": {"name": "test-client", "version": "1.0.0"}
        }
    }
    
    # Create a test request
    request = {
        "jsonrpc": "2.0",
        "id": 1,
        "method": "tools/call",
        "params": {
            "name": "spelunk-query-syntax",
            "arguments": {
                "roslynPath": "//method",
                "file": os.path.join(os.getcwd(), "test-workspace", "OrderService.cs"),
                "includeSemanticInfo": True
            }
        }
    }
    
    # Write request to file in project root
    request_file = os.path.join(os.path.dirname(__file__), "..", "..", "test-semantic-request.jsonl")
    with open(request_file, "w") as f:
        f.write(json.dumps(init_request) + '\n')
        f.write(json.dumps(request) + '\n')
    
    # Run using the test script approach from project root
    project_root = os.path.join(os.path.dirname(__file__), "..", "..")
    result = subprocess.run(
        ["bash", "scripts/test/test-server.sh", "test-semantic-request.jsonl"],
        capture_output=True,
        text=True,
        cwd=project_root
    )
    
    print("Output:")
    print(result.stdout)
    
    if result.stderr:
        print("\nErrors:")
        print(result.stderr)
    
    # Parse the response from stdout
    lines = result.stdout.strip().split('\n')
    response = None
    for line in lines:
        if line.startswith('{') and '"id":1' in line:
            response = json.loads(line)
            break
    
    if not response:
        print("No valid response found")
        return False
    
    if response.get("error"):
        print(f"Error: {response['error']}")
        return False
        
    result = response.get("result", {})
    matches = result.get("matches", [])
    
    print(f"\nFound {len(matches)} matches")
    
    for i, match in enumerate(matches):
        print(f"\nMatch {i+1}:")
        print(f"  Node type: {match['node']['type']}")
        print(f"  Location: {match['node']['location']}")
        
        if match.get("semanticInfo"):
            print("  Semantic info found:")
            sem_info = match["semanticInfo"]
            
            if "declaredSymbol" in sem_info:
                sym = sem_info["declaredSymbol"]
                print(f"    - Declared symbol: {sym.get('name')}")
                print(f"    - Kind: {sym.get('kind')}")
                print(f"    - Fully qualified: {sym.get('fullyQualifiedName')}")
                print(f"    - Is async: {sym.get('isAsync')}")
                
            if "type" in sem_info:
                type_info = sem_info["type"]
                print(f"    - Type: {type_info.get('name')}")
                print(f"    - Type kind: {type_info.get('kind')}")
                
            if "enclosingContext" in sem_info:
                ctx = sem_info["enclosingContext"]
                print(f"    - Enclosing context: {ctx.get('symbol')}")
        else:
            print("  No semantic info (this is a problem!)")
            
    return True

if __name__ == "__main__":
    success = test_semantic_simple()
    sys.exit(0 if success else 1)