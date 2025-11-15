#!/usr/bin/env python3
"""Test the new AST navigation tools"""

import sys
import os
import json

# Add parent directory to path for imports
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..'))

from utils.test_helpers import TestRunner, assert_equals, assert_exists, assert_success

def main():
    """Test AST navigation tools"""
    runner = TestRunner()
    
    try:
        print("Testing AST Navigation Tools...\n")
        
        # Initialize server
        runner.send_request("initialize", {
            "protocolVersion": "2024-11-05",
            "capabilities": {"tools": {}},
            "clientInfo": {"name": "test-ast-navigation", "version": "1.0.0"}
        })
        
        # Load workspace
        print("Loading test workspace...")
        response = runner.call_tool("spelunk-load-workspace", {
            "path": os.path.abspath("test-workspace/TestProject.csproj")
        })
        assert_exists(response, "Id")
        print(f"✓ Workspace loaded: {response['Id']}")
        
        # Test 1: Query syntax with enhanced RoslynPath
        print("\nTest 1: Query null comparisons with enhanced RoslynPath")
        response = runner.call_tool("spelunk-query-syntax", {
            "roslynPath": "//binary-expression[@operator='==']",
            "file": os.path.abspath("test-workspace/src/Program.cs")
        })
        assert_exists(response, "matches")
        print(f"✓ Found {len(response['matches'])} binary expressions with == operator")
        
        # Test 2: Navigate from position
        print("\nTest 2: Navigate to parent method")
        response = runner.call_tool("spelunk-navigate", {
            "from": {
                "file": os.path.abspath("test-workspace/src/Program.cs"),
                "line": 10,
                "column": 1
            },
            "path": "ancestor::method[1]",
            "returnPath": True
        })
        assert_exists(response, "navigatedTo")
        if response["navigatedTo"]:
            print(f"✓ Navigated to: {response['navigatedTo']['name']} at line {response['navigatedTo']['location']['line']}")
        else:
            print("✗ No parent method found")
        
        # Test 3: Get AST structure
        print("\nTest 3: Get AST structure")
        response = runner.call_tool("spelunk-get-ast", {
            "file": os.path.abspath("test-workspace/src/Program.cs"),
            "depth": 2
        })
        assert_exists(response, "ast")
        print(f"✓ Retrieved AST with type: {response['ast']['type']}")
        
        # Test 4: Query with low-level node types
        print("\nTest 4: Query if-statements")
        response = runner.call_tool("spelunk-query-syntax", {
            "roslynPath": "//if-statement",
            "file": os.path.abspath("test-workspace/src/Program.cs")
        })
        assert_exists(response, "matches")
        print(f"✓ Found {len(response['matches'])} if statements")
        
        print("\n✅ All AST navigation tests passed!")
        return 0
        
    except Exception as e:
        print(f"\n❌ Test failed: {e}")
        return 1
    finally:
        runner.cleanup()

if __name__ == "__main__":
    sys.exit(main())