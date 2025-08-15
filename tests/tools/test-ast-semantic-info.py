#!/usr/bin/env python3
"""
Test BuildAstNode semantic info functionality.
Verifies that the async version properly includes semantic information.
"""

import json
import sys
import os
from pathlib import Path

# Add parent directory to path for imports
sys.path.insert(0, str(Path(__file__).parent.parent))

from utils.test_client import TestClient

def test_ast_with_semantic_info():
    """Test that BuildAstNode async version includes semantic info."""
    print("\n=== Testing AST with Semantic Info ===")
    
    with TestClient() as client:
        # Load the test workspace
        print("Loading workspace...")
        result = client.call_tool("dotnet-load-workspace", {
            "path": str(Path(__file__).parent.parent.parent / "test-workspace" / "TestProject.csproj")
        })
        
        if not result or "error" in result:
            print(f"Failed to load workspace: {result}")
            return False
            
        # Create a test file with some code
        test_file = Path(__file__).parent.parent.parent / "test-workspace" / "SemanticTest.cs"
        test_file.write_text("""
using System;

namespace TestProject
{
    public class SemanticTest
    {
        private int _count = 0;
        
        public void IncrementCount()
        {
            _count++;
            Console.WriteLine($"Count is now: {_count}");
        }
        
        public int GetCount()
        {
            return _count;
        }
    }
}
""")
        
        # Test getting AST without semantic info
        print("\nGetting AST without semantic info...")
        result = client.call_tool("dotnet-get-ast", {
            "file": str(test_file),
            "depth": 3,
            "includeSemanticInfo": False
        })
        
        if result and "ast" in result:
            ast = result["ast"]
            # Check that semantic info is not present
            has_semantic = check_for_semantic_info(ast)
            print(f"AST without semantic info has semanticInfo: {has_semantic}")
            assert not has_semantic, "AST should not have semantic info when includeSemanticInfo=false"
        else:
            print(f"Failed to get AST: {result}")
            return False
            
        # Test getting AST with semantic info
        print("\nGetting AST with semantic info...")
        result = client.call_tool("dotnet-get-ast", {
            "file": str(test_file),
            "depth": 3,
            "includeSemanticInfo": True
        })
        
        if result and "ast" in result:
            ast = result["ast"]
            # Check that semantic info is present
            has_semantic = check_for_semantic_info(ast)
            print(f"AST with semantic info has semanticInfo: {has_semantic}")
            assert has_semantic, "AST should have semantic info when includeSemanticInfo=true"
            
            # Verify semantic info content
            semantic_nodes = find_nodes_with_semantic_info(ast)
            print(f"Found {len(semantic_nodes)} nodes with semantic info")
            
            for node in semantic_nodes[:3]:  # Print first 3 for brevity
                print(f"  Node type: {node.get('type', 'unknown')}")
                if 'semanticInfo' in node:
                    info = node['semanticInfo']
                    if isinstance(info, dict):
                        print(f"    Semantic info keys: {list(info.keys())}")
        else:
            print(f"Failed to get AST with semantic info: {result}")
            return False
            
        print("\n✅ AST semantic info test passed!")
        return True

def check_for_semantic_info(node):
    """Recursively check if any node has semantic info."""
    if isinstance(node, dict):
        if 'semanticInfo' in node:
            return True
        for value in node.values():
            if check_for_semantic_info(value):
                return True
    elif isinstance(node, list):
        for item in node:
            if check_for_semantic_info(item):
                return True
    return False

def find_nodes_with_semantic_info(node, nodes=None):
    """Find all nodes that have semantic info."""
    if nodes is None:
        nodes = []
    
    if isinstance(node, dict):
        if 'semanticInfo' in node:
            nodes.append(node)
        for value in node.values():
            find_nodes_with_semantic_info(value, nodes)
    elif isinstance(node, list):
        for item in node:
            find_nodes_with_semantic_info(item, nodes)
    
    return nodes

if __name__ == "__main__":
    success = test_ast_with_semantic_info()
    sys.exit(0 if success else 1)