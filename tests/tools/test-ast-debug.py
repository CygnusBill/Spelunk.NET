#!/usr/bin/env python3
"""Debug AST structure for navigation tests"""

import subprocess
import json
import os
import sys

# Add parent directory to path
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))

from utils.test_client import TestClient

# Test code from NavigationMethodsTests
test_code = '''
namespace TestNamespace
{
    public class TestClass
    {
        private int testField;
        
        public TestClass()
        {
            testField = 0;
        }
        
        public void TestMethod()
        {
            var x = 1 + 2;
            if (x > 0)
            {
                Console.WriteLine("Hello");
            }
            var y = x * 3;
        }
    }
}'''

def main():
    client = TestClient()
    
    # Create test file
    test_file = os.path.abspath("test-navigation-debug.cs")
    with open(test_file, 'w') as f:
        f.write(test_code)
    
    try:
        # Load workspace
        result = client.call_tool("spelunk-load-workspace", {
            "path": os.path.abspath("../test-workspace/TestProject.csproj")
        })
        print(f"Workspace loaded: {result}")
        
        # Get AST
        print("\n=== Full AST Structure ===")
        result = client.call_tool("spelunk-get-ast", {
            "file": test_file,
            "depth": 6
        })
        
        if "ast" in result:
            # Find method body and print statements
            def find_method_body(node, depth=0):
                indent = "  " * depth
                if node.get("name") == "TestMethod" and node.get("type") == "method":
                    print(f"\n{indent}Found TestMethod!")
                    if "children" in node:
                        for child in node["children"]:
                            if child.get("type") == "block":
                                print(f"\n{indent}Method body statements:")
                                if "children" in child:
                                    for i, stmt in enumerate(child["children"]):
                                        print(f"{indent}  Statement {i}: {stmt.get('type')} - {stmt.get('text', '')[:50]}...")
                
                if "children" in node:
                    for child in node["children"]:
                        find_method_body(child, depth + 1)
            
            find_method_body(result["ast"])
        
        # Test navigation from specific positions
        print("\n=== Testing Navigation ===")
        
        # Position (15,13) - should be in "var x = 1 + 2;"
        print("\nNavigating from position (15,13):")
        result = client.call_tool("spelunk-navigate", {
            "from": {
                "file": test_file,
                "line": 15,
                "column": 13
            },
            "path": "self",
            "returnPath": True
        })
        if "navigatedTo" in result:
            nav = result["navigatedTo"]
            print(f"  Current node: {nav.get('type')} - {nav.get('text', '')[:50]}")
            print(f"  Path: {nav.get('path')}")
        
        # Test following-sibling
        result = client.call_tool("spelunk-navigate", {
            "from": {
                "file": test_file,
                "line": 15,
                "column": 13
            },
            "path": "following-sibling",
            "returnPath": True
        })
        if "navigatedTo" in result:
            nav = result["navigatedTo"]
            print(f"  Following sibling: {nav.get('type')} - {nav.get('text', '')[:50] if nav else 'None'}")
        
    finally:
        os.remove(test_file)
        client.cleanup()

if __name__ == "__main__":
    main()