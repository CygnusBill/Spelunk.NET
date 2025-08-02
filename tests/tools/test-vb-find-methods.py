#!/usr/bin/env python3
"""
Test script for VB.NET method finding functionality
"""

import sys
import os
sys.path.append(os.path.join(os.path.dirname(__file__), '..', 'utils'))

from test_client import TestClient
import json

def test_vb_find_methods():
    client = TestClient()
    
    # Load the VB test workspace
    vb_project_path = os.path.join(os.getcwd(), "test-workspace", "VBTestProject", "VBTestProject.vbproj")
    
    print("Testing VB.NET Method Finding...")
    print("=" * 50)
    
    # Test 1: Load VB workspace
    print("\n1. Loading VB.NET workspace...")
    load_result = client.call_tool("dotnet-load-workspace", {
        "path": vb_project_path
    })
    
    if load_result["success"]:
        print("✓ VB.NET workspace loaded successfully")
        workspace_id = load_result["workspaceId"]
    else:
        print(f"✗ Failed to load workspace: {load_result['message']}")
        return False
    
    # Test 2: Find all methods in Calculator class
    print("\n2. Finding all methods in Calculator class...")
    find_result = client.call_tool("dotnet-find-method", {
        "pattern": "*",
        "workspacePath": workspace_id
    })
    
    if find_result["success"]:
        print("✓ Found methods successfully")
        methods = find_result["result"]
        print(f"Found {len(methods)} methods:")
        for method in methods:
            print(f"  - {method['name']} ({method.get('returnType', 'unknown')})")
            if method.get('isAsync'):
                print("    [ASYNC]")
    else:
        print(f"✗ Failed to find methods: {find_result['message']}")
        return False
    
    # Test 3: Find specific methods by pattern
    print("\n3. Finding methods with 'Add' pattern...")
    add_result = client.call_tool("dotnet-find-method", {
        "pattern": "Add*",
        "workspacePath": workspace_id
    })
    
    if add_result["success"]:
        methods = add_result["result"]
        print(f"✓ Found {len(methods)} methods matching 'Add*':")
        for method in methods:
            print(f"  - {method['name']}")
    else:
        print(f"✗ Failed to find Add methods: {add_result['message']}")
        return False
    
    # Test 4: Find async methods
    print("\n4. Finding async methods in TestClasses...")
    # First, let's get files with async methods
    async_result = client.call_tool("dotnet-find-method", {
        "pattern": "*Async",
        "workspacePath": workspace_id
    })
    
    if async_result["success"]:
        methods = async_result["result"]
        print(f"✓ Found {len(methods)} async methods:")
        for method in methods:
            print(f"  - {method['name']} in {os.path.basename(method.get('location', {}).get('filePath', 'unknown'))}")
    else:
        print(f"✗ Failed to find async methods: {async_result['message']}")
        return False
    
    # Test 5: Find methods with RoslynPath
    print("\n5. Finding VB.NET Sub methods using RoslynPath...")
    roslynpath_result = client.call_tool("dotnet-find-statements", {
        "pattern": "//method[@methodtype='sub']",
        "patternType": "roslynpath",
        "workspacePath": workspace_id
    })
    
    if roslynpath_result["success"]:
        statements = roslynpath_result["result"]
        print(f"✓ Found {len(statements)} Sub methods using RoslynPath:")
        for stmt in statements[:5]:  # Show first 5
            print(f"  - {stmt.get('text', 'N/A')[:60]}...")
    else:
        print(f"✗ Failed to find Sub methods: {roslynpath_result['message']}")
        return False
    
    print("\n" + "=" * 50)
    print("VB.NET Method Finding Tests PASSED!")
    return True

if __name__ == "__main__":
    success = test_vb_find_methods()
    sys.exit(0 if success else 1)