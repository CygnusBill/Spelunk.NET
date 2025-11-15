#!/usr/bin/env python3
"""
Simple test for semantic enrichment
"""

import sys
import os

# Add parent directory to path
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..'))

from utils.test_client import TestClient

def main():
    # Create client
    client = TestClient(allowed_paths=["test-workspace"])
    
    print("Loading workspace...")
    result = client.call_tool("spelunk-load-workspace", {
        "path": os.path.abspath("test-workspace/TestProject.csproj")
    })
    
    print(f"Load workspace result: {result}")
    
    if result.get("success"):
        # Extract actual result content
        content = result.get("result", {}).get("content", [])
        if content and content[0].get("type") == "text":
            text = content[0].get("text", "")
            print(f"Workspace response: {text}")
    
    # Now test query-syntax with semantic info
    print("\nTesting query-syntax with semantic info...")
    result = client.call_tool("spelunk-query-syntax", {
        "roslynPath": "//method",
        "file": "test-workspace/Program.cs",
        "includeSemanticInfo": True
    })
    
    print(f"Query result success: {result.get('success')}")
    if result.get("success"):
        nodes = result.get("result", {}).get("nodes", [])
        print(f"Found {len(nodes)} methods")
        
        if nodes:
            first = nodes[0]
            print(f"First method: {first.get('text', '')[:50]}...")
            if "semanticInfo" in first:
                print("✓ Semantic info present!")
                print(f"Symbol kind: {first['semanticInfo'].get('symbolKind')}")
                print(f"Return type: {first['semanticInfo'].get('returnType')}")
            else:
                print("✗ No semantic info found")
    else:
        print(f"Query failed: {result.get('message')}")
    
    client.close()

if __name__ == "__main__":
    main()