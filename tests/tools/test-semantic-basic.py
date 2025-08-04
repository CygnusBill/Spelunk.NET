#!/usr/bin/env python3
"""Basic test for semantic enrichment functionality"""

import os
import sys
sys.path.append(os.path.join(os.path.dirname(__file__), '..', 'utils'))

from test_client import TestClient
import json

def test_semantic_basic():
    """Test basic semantic enrichment in query-syntax"""
    
    server_path = os.path.join(os.path.dirname(__file__), "..", "..", "src", "McpRoslyn.Server")
    client = TestClient(server_path=server_path)
    
    # First load the test workspace
    workspace_path = os.path.join(os.getcwd(), "test-workspace", "TestProject.csproj")
    print(f"Loading workspace: {workspace_path}")
    
    result = client.call_tool("dotnet-load-workspace", {
        "path": workspace_path
    })
    
    if not result.get("success"):
        print(f"Failed to load workspace: {result}")
        return False
    
    print("✅ Workspace loaded successfully")
    
    # Test query-syntax with semantic info on Program.cs
    program_cs = os.path.join(os.getcwd(), "test-workspace", "Program.cs")
    print(f"\nTesting semantic enrichment on: {program_cs}")
    
    result = client.call_tool("dotnet-query-syntax", {
        "file": program_cs,
        "roslynPath": "//class",
        "includeSemanticInfo": True
    })
    
    print(f"Query result success: {result.get('success')}")
    
    if result.get("success") and "result" in result:
        matches = result["result"].get("matches", [])
        print(f"Found {len(matches)} class(es)")
        
        for i, match in enumerate(matches):
            print(f"\nClass {i+1}:")
            node = match.get("node", {})
            print(f"  Type: {node.get('type')}")
            print(f"  Kind: {node.get('kind')}")
            
            if "semanticInfo" in match:
                sem_info = match["semanticInfo"]
                print("  ✅ Semantic info present!")
                
                if "declaredSymbol" in sem_info:
                    sym = sem_info["declaredSymbol"]
                    print(f"    Symbol name: {sym.get('name')}")
                    print(f"    Symbol kind: {sym.get('kind')}")
                    print(f"    Fully qualified: {sym.get('fullyQualifiedName')}")
                
                return True
            else:
                print("  ❌ No semantic info found")
                return False
    
    print("❌ Query failed or no results")
    return False

if __name__ == "__main__":
    try:
        success = test_semantic_basic()
        sys.exit(0 if success else 1)
    except Exception as e:
        print(f"Test failed with error: {e}")
        import traceback
        traceback.print_exc()
        sys.exit(1)