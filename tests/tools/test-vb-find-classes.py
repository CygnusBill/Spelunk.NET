#!/usr/bin/env python3
"""
Test script for VB.NET class finding functionality
"""

import sys
import os
sys.path.append(os.path.join(os.path.dirname(__file__), '..', 'utils'))

from test_client import TestClient
import json

def test_vb_find_classes():
    client = TestClient()
    
    # Load the VB test workspace
    vb_project_path = os.path.join(os.getcwd(), "test-workspace", "VBTestProject", "VBTestProject.vbproj")
    
    print("Testing VB.NET Class Finding...")
    print("=" * 50)
    
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
    
    # Test 2: Find all classes
    print("\n2. Finding all classes...")
    find_result = client.call_tool("spelunk-find-class", {
        "pattern": "*",
        "workspacePath": workspace_id
    })
    
    if find_result["success"]:
        print("✓ Found classes successfully")
        classes = find_result["result"]
        print(f"Found {len(classes)} classes:")
        for cls in classes:
            print(f"  - {cls['name']} ({cls.get('accessLevel', 'unknown')})")
            if cls.get('isAbstract'):
                print("    [ABSTRACT]")
            if cls.get('isStatic'):
                print("    [STATIC/SHARED]")
    else:
        print(f"✗ Failed to find classes: {find_result['message']}")
        return False
    
    # Test 3: Find specific classes by pattern
    print("\n3. Finding classes with 'Logger' pattern...")
    logger_result = client.call_tool("spelunk-find-class", {
        "pattern": "*Logger",
        "workspacePath": workspace_id
    })
    
    if logger_result["success"]:
        classes = logger_result["result"]
        print(f"✓ Found {len(classes)} classes matching '*Logger':")
        for cls in classes:
            print(f"  - {cls['name']}")
            if cls.get('baseTypes'):
                print(f"    Implements/Inherits: {', '.join(cls['baseTypes'])}")
    else:
        print(f"✗ Failed to find Logger classes: {logger_result['message']}")
        return False
    
    # Test 4: Find abstract classes using RoslynPath
    print("\n4. Finding abstract (MustInherit) classes using RoslynPath...")
    abstract_result = client.call_tool("spelunk-find-statements", {
        "pattern": "//class[@abstract]",
        "patternType": "roslynpath",
        "workspacePath": workspace_id
    })
    
    if abstract_result["success"]:
        statements = abstract_result["result"]
        print(f"✓ Found {len(statements)} abstract classes using RoslynPath:")
        for stmt in statements:
            print(f"  - {stmt.get('text', 'N/A')[:60]}...")
    else:
        print(f"✗ Failed to find abstract classes: {abstract_result['message']}")
        return False
    
    # Test 5: Find classes with specific modifiers
    print("\n5. Finding public classes using RoslynPath...")
    public_result = client.call_tool("spelunk-find-statements", {
        "pattern": "//class[@public]",
        "patternType": "roslynpath",
        "workspacePath": workspace_id
    })
    
    if public_result["success"]:
        statements = public_result["result"]
        print(f"✓ Found {len(statements)} public classes using RoslynPath:")
        for stmt in statements[:5]:  # Show first 5
            file_name = os.path.basename(stmt.get('location', {}).get('filePath', 'unknown'))
            print(f"  - In {file_name}: {stmt.get('text', 'N/A')[:40]}...")
    else:
        print(f"✗ Failed to find public classes: {public_result['message']}")
        return False
    
    # Test 6: Find interface implementations
    print("\n6. Finding interface implementations...")
    interface_result = client.call_tool("spelunk-find-class", {
        "pattern": "I*",  # Interface pattern
        "workspacePath": workspace_id
    })
    
    if interface_result["success"]:
        classes = interface_result["result"]
        interfaces = [cls for cls in classes if 'Interface' in cls.get('name', '')]
        print(f"✓ Found {len(interfaces)} interfaces:")
        for iface in interfaces:
            print(f"  - {iface['name']}")
    else:
        print(f"Interface search returned: {interface_result}")
    
    print("\n" + "=" * 50)
    print("VB.NET Class Finding Tests PASSED!")
    return True

if __name__ == "__main__":
    success = test_vb_find_classes()
    sys.exit(0 if success else 1)