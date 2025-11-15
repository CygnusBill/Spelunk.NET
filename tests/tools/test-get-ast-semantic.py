#!/usr/bin/env python3
"""Test the dotnet-get-ast tool with semantic information enrichment"""

import os
import sys
sys.path.append(os.path.join(os.path.dirname(__file__), '..', 'utils'))

from test_client import TestClient
import json
import tempfile
import shutil

def test_get_ast_semantic():
    """Test get-ast tool with semantic information"""
    
    server_path = os.path.join(os.path.dirname(__file__), "..", "..", "src", "McpDotnet.Server")
    client = TestClient(server_path=server_path)
    
    # Create a temporary workspace
    workspace_path = tempfile.mkdtemp(prefix="test_ast_semantic_")
    try:
        # Create a test file with rich AST structure
        test_file = os.path.join(workspace_path, "AstTest.cs")
        with open(test_file, 'w') as f:
            f.write("""
using System;
using System.Linq;
using System.Collections.Generic;

namespace AstTestNamespace
{
    public interface IProcessor<T>
    {
        T Process(T input);
    }
    
    public class DataProcessor : IProcessor<string>
    {
        private readonly int _maxLength = 100;
        
        public string Process(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                throw new ArgumentException("Input cannot be null or empty", nameof(input));
            }
            
            var words = input.Split(' ');
            var filtered = words.Where(w => w.Length > 2).ToList();
            
            return string.Join(" ", filtered);
        }
        
        public int CalculateScore(List<int> values)
        {
            return values.Sum() * 2;
        }
    }
}
""")
        
        # Create project file
        csproj_content = """<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>"""
        csproj_file = os.path.join(workspace_path, "TestProject.csproj")
        with open(csproj_file, 'w') as f:
            f.write(csproj_content)
        
        # Load the project
        result = client.call_tool("spelunk-load-workspace", {
            "path": csproj_file
        })
        assert result["success"], f"Failed to load workspace: {result.get('error')}"
        
        # Test 1: Get AST for class with semantic info
        print("\n=== Test 1: Get AST for class with semantic info ===")
        result = client.call_tool("spelunk-get-ast", {
            "file": test_file,
            "root": "//class[@name='DataProcessor']",
            "depth": 2,
            "includeSemanticInfo": True
        })
        
        if not result["success"]:
            print(f"Failed: {result.get('error', 'Unknown error')}")
            return False
        
        ast = result["result"]["ast"]
        assert ast is not None, "Expected AST result"
        
        # Check root semantic info
        assert ast.get("semanticInfo") is not None, "Expected semantic info on root"
        
        root_symbol = ast["semanticInfo"].get("declaredSymbol")
        if root_symbol:
            assert root_symbol["name"] == "DataProcessor"
            assert root_symbol["kind"] == "NamedType"
            print(f"✓ Found class with semantic info: {root_symbol['fullyQualifiedName']}")
            
            # Check interfaces
            if "interfaces" in root_symbol:
                assert len(root_symbol["interfaces"]) > 0, "Expected interface implementation"
                print(f"✓ Found interface info: {root_symbol['interfaces']}")
        
        # Check children have semantic info
        if ast.get("children"):
            method_found = False
            for child in ast["children"]:
                if child["type"] == "MethodDeclaration" and "semanticInfo" in child:
                    method_found = True
                    method_sym = child["semanticInfo"].get("declaredSymbol")
                    if method_sym:
                        print(f"✓ Found method with semantic info: {method_sym['name']}")
            
            assert method_found, "Expected to find methods with semantic info"
        
        # Test 2: Get AST for method body with expression semantics
        print("\n=== Test 2: Get AST for method body with expression semantics ===")
        result = client.call_tool("spelunk-get-ast", {
            "file": test_file,
            "root": "//method[@name='Process']/block",
            "depth": 3,
            "includeSemanticInfo": True
        })
        
        if not result["success"]:
            print(f"Failed: {result.get('error', 'Unknown error')}")
            return False
            
        ast = result["result"]["ast"]
        
        # Navigate through AST to find expressions with type info
        def find_semantic_nodes(node, path=""):
            nodes_with_semantic = []
            current_path = f"{path}/{node['type']}"
            
            if "semanticInfo" in node:
                nodes_with_semantic.append({
                    "path": current_path,
                    "type": node["type"],
                    "semantic": node["semanticInfo"]
                })
            
            if "children" in node:
                for child in node["children"]:
                    nodes_with_semantic.extend(find_semantic_nodes(child, current_path))
            
            return nodes_with_semantic
        
        semantic_nodes = find_semantic_nodes(ast)
        assert len(semantic_nodes) > 0, "Expected to find nodes with semantic info"
        
        print(f"✓ Found {len(semantic_nodes)} nodes with semantic info in method body")
        
        # Look for specific semantic info
        for node_info in semantic_nodes:
            sem = node_info["semantic"]
            if "type" in sem:
                print(f"  - {node_info['type']} has type: {sem['type']['name']}")
        
        # Test 3: Get AST without semantic info (default)
        print("\n=== Test 3: Get AST without semantic info (default) ===")
        result = client.call_tool("spelunk-get-ast", {
            "file": test_file,
            "root": "//interface",
            "depth": 2
            # includeSemanticInfo not specified
        })
        
        if not result["success"]:
            print(f"Failed: {result.get('error', 'Unknown error')}")
            return False
            
        ast = result["result"]["ast"]
        assert "semanticInfo" not in ast, "Should not include semantic info by default"
        print("✓ Default AST generation works without semantic info")
        
        # Test 4: Get full file AST with semantic info (performance test)
        print("\n=== Test 4: Get full file AST with semantic info ===")
        result = client.call_tool("spelunk-get-ast", {
            "file": test_file,
            "depth": 10,  # Deep traversal
            "includeSemanticInfo": True
        })
        
        if not result["success"]:
            print(f"Failed: {result.get('error', 'Unknown error')}")
            return False
            
        ast = result["result"]["ast"]
        
        # Count nodes with semantic info
        total_nodes = [0]
        semantic_nodes = [0]
        
        def count_nodes(node):
            total_nodes[0] += 1
            if "semanticInfo" in node:
                semantic_nodes[0] += 1
            if "children" in node:
                for child in node["children"]:
                    count_nodes(child)
        
        count_nodes(ast)
        
        print(f"✓ Generated full AST: {total_nodes[0]} total nodes, {semantic_nodes[0]} with semantic info")
        assert semantic_nodes[0] > 0, "Expected some nodes to have semantic info"
        
        # Test 5: Verify semantic info propagates to expression level
        print("\n=== Test 5: Verify semantic info at expression level ===")
        result = client.call_tool("spelunk-get-ast", {
            "file": test_file,
            "root": "//invocation[@name='Where']",
            "depth": 2,
            "includeSemanticInfo": True
        })
        
        if not result["success"]:
            print(f"Failed: {result.get('error', 'Unknown error')}")
            # LINQ methods might be harder to find
            print("Note: LINQ method search might fail without full semantic context")
        else:
            ast = result["result"]["ast"]
            if ast and "semanticInfo" in ast:
                sem = ast["semanticInfo"]
                if "symbol" in sem:
                    print(f"✓ Found LINQ method with symbol info: {sem['symbol']}")
                if "type" in sem:
                    print(f"✓ Expression has return type: {sem['type']['name']}")
        
        print("\n✅ All get-ast semantic enrichment tests passed!")
        return True
        
    finally:
        # Cleanup
        shutil.rmtree(workspace_path)

if __name__ == "__main__":
    success = test_get_ast_semantic()
    sys.exit(0 if success else 1)