#!/usr/bin/env python3
"""
Test script for RoslynPath language-agnostic functionality with VB.NET
"""

import sys
import os
sys.path.append(os.path.join(os.path.dirname(__file__), '..', 'utils'))

from test_client import TestClient
import json

def test_roslynpath_vb():
    client = TestClient()
    
    # Load the VB test workspace
    vb_project_path = os.path.join(os.getcwd(), "test-workspace", "VBTestProject", "VBTestProject.vbproj")
    
    print("Testing RoslynPath Language-Agnostic Support with VB.NET...")
    print("=" * 60)
    
    # Test 1: Load VB workspace
    print("\n1. Loading VB.NET workspace...")
    load_result = client.call_tool("spelunk-load-workspace", {
        "path": vb_project_path
    })
    
    if load_result["success"]:
        print("✓ VB.NET workspace loaded successfully")
        workspace_id = load_result["workspaceId"]
    else:
        print(f"✗ Failed to load workspace: {load_result['message']}")
        return False
    
    # Test 2: Find VB.NET methods using language-agnostic RoslynPath
    print("\n2. Finding VB.NET methods using RoslynPath...")
    methods_result = client.call_tool("spelunk-find-statements", {
        "pattern": "//method",
        "patternType": "roslynpath",
        "workspacePath": workspace_id
    })
    
    if methods_result["success"]:
        statements = methods_result["result"]
        print(f"✓ Found {len(statements)} methods using RoslynPath:")
        for stmt in statements[:5]:  # Show first 5
            file_name = os.path.basename(stmt.get('location', {}).get('filePath', 'unknown'))
            print(f"  - In {file_name}: {stmt.get('text', 'N/A')[:50]}...")
    else:
        print(f"✗ Failed to find methods: {methods_result['message']}")
        return False
    
    # Test 3: Find VB.NET Sub methods (void equivalents)
    print("\n3. Finding VB.NET Sub methods (void return type)...")
    sub_result = client.call_tool("spelunk-find-statements", {
        "pattern": "//method[@returns='void']",
        "patternType": "roslynpath",
        "workspacePath": workspace_id
    })
    
    if sub_result["success"]:
        statements = sub_result["result"]
        print(f"✓ Found {len(statements)} Sub methods using language-agnostic mapping:")
        for stmt in statements[:3]:
            print(f"  - {stmt.get('text', 'N/A')[:60]}...")
    else:
        print(f"✗ Failed to find Sub methods: {sub_result['message']}")
        return False
    
    # Test 4: Find async methods across languages
    print("\n4. Finding async methods using RoslynPath...")
    async_result = client.call_tool("spelunk-find-statements", {
        "pattern": "//method[@async]",
        "patternType": "roslynpath",
        "workspacePath": workspace_id
    })
    
    if async_result["success"]:
        statements = async_result["result"]
        print(f"✓ Found {len(statements)} async methods:")
        for stmt in statements[:3]:
            file_name = os.path.basename(stmt.get('location', {}).get('filePath', 'unknown'))
            print(f"  - In {file_name}: {stmt.get('text', 'N/A')[:50]}...")
    else:
        print(f"✗ Failed to find async methods: {async_result['message']}")
        return False
    
    # Test 5: Find public methods
    print("\n5. Finding public methods using RoslynPath...")
    public_result = client.call_tool("spelunk-find-statements", {
        "pattern": "//method[@public]",
        "patternType": "roslynpath",
        "workspacePath": workspace_id
    })
    
    if public_result["success"]:
        statements = public_result["result"]
        print(f"✓ Found {len(statements)} public methods:")
        for stmt in statements[:5]:
            print(f"  - {stmt.get('text', 'N/A')[:50]}...")
    else:
        print(f"✗ Failed to find public methods: {public_result['message']}")
        return False
    
    # Test 6: Find properties with getters and setters
    print("\n6. Finding properties with getters using RoslynPath...")
    property_result = client.call_tool("spelunk-find-statements", {
        "pattern": "//property[@has-getter]",
        "patternType": "roslynpath",
        "workspacePath": workspace_id
    })
    
    if property_result["success"]:
        statements = property_result["result"]
        print(f"✓ Found {len(statements)} properties with getters:")
        for stmt in statements[:3]:
            print(f"  - {stmt.get('text', 'N/A')[:50]}...")
    else:
        print(f"✗ Failed to find properties with getters: {property_result['message']}")
        return False
    
    # Test 7: Find Shared (static) methods in VB.NET
    print("\n7. Finding Shared (static) methods using RoslynPath...")
    static_result = client.call_tool("spelunk-find-statements", {
        "pattern": "//method[@static]",
        "patternType": "roslynpath",
        "workspacePath": workspace_id
    })
    
    if static_result["success"]:
        statements = static_result["result"]
        print(f"✓ Found {len(statements)} Shared/static methods:")
        for stmt in statements[:3]:
            print(f"  - {stmt.get('text', 'N/A')[:50]}...")
    else:
        print(f"✗ Failed to find static methods: {static_result['message']}")
        return False
    
    # Test 8: Find abstract (MustInherit) classes
    print("\n8. Finding abstract classes using RoslynPath...")
    abstract_result = client.call_tool("spelunk-find-statements", {
        "pattern": "//class[@abstract]",
        "patternType": "roslynpath",
        "workspacePath": workspace_id
    })
    
    if abstract_result["success"]:
        statements = abstract_result["result"]
        print(f"✓ Found {len(statements)} abstract classes:")
        for stmt in statements:
            print(f"  - {stmt.get('text', 'N/A')[:60]}...")
    else:
        print(f"✗ Failed to find abstract classes: {abstract_result['message']}")
        return False
    
    # Test 9: Find virtual (Overridable) methods
    print("\n9. Finding virtual methods using language-agnostic RoslynPath...")
    virtual_result = client.call_tool("spelunk-find-statements", {
        "pattern": "//method[@virtual]",
        "patternType": "roslynpath",
        "workspacePath": workspace_id
    })
    
    if virtual_result["success"]:
        statements = virtual_result["result"]
        print(f"✓ Found {len(statements)} virtual/overridable methods:")
        for stmt in statements:
            print(f"  - {stmt.get('text', 'N/A')[:50]}...")
    else:
        print(f"Note: Virtual method search: {virtual_result['message']}")
    
    print("\n" + "=" * 60)
    print("RoslynPath VB.NET Language-Agnostic Tests PASSED!")
    return True

if __name__ == "__main__":
    success = test_roslynpath_vb()
    sys.exit(0 if success else 1)