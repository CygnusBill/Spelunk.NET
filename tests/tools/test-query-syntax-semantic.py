#!/usr/bin/env python3
"""Test the dotnet-query-syntax tool with semantic information enrichment"""

import os
import sys
sys.path.append(os.path.join(os.path.dirname(__file__), '..', 'utils'))

from test_client import TestClient
import json
import tempfile
import shutil

def test_query_syntax_semantic():
    """Test query-syntax with semantic information"""
    
    # Correct the server path
    server_path = os.path.join(os.path.dirname(__file__), "..", "..", "src", "McpRoslyn.Server")
    client = TestClient(server_path=server_path)
    
    # Create a temporary workspace
    workspace_path = tempfile.mkdtemp(prefix="test_semantic_")
    try:
        # Create a test file with various syntax elements
        test_file = os.path.join(workspace_path, "SemanticTest.cs")
        with open(test_file, 'w') as f:
            f.write("""
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TestProject
{
    public interface IService
    {
        Task<string> ProcessAsync(int id);
    }

    public class OrderService : IService
    {
        private readonly string _connectionString;
        
        public OrderService(string connectionString)
        {
            _connectionString = connectionString;
        }
        
        public async Task<string> ProcessAsync(int id)
        {
            if (id == 0)
            {
                throw new ArgumentException("Invalid ID", nameof(id));
            }
            
            var result = await GetOrderAsync(id);
            return result?.ToString() ?? "Not found";
        }
        
        private async Task<Order?> GetOrderAsync(int orderId)
        {
            // Simulate async operation
            await Task.Delay(100);
            return orderId > 0 ? new Order { Id = orderId } : null;
        }
    }
    
    public class Order
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        
        public override string ToString()
        {
            return $"Order {Id}: {Name ?? "Unnamed"}";
        }
    }
}
""")
        
        # Load the workspace first
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
        result = client.call_tool("dotnet-load-workspace", {
            "path": csproj_file
        })
        assert result["success"], f"Failed to load workspace: {result.get('error')}"
        
        # Test 1: Query method declarations with semantic info
        print("\n=== Test 1: Query methods with semantic information ===")
        result = client.call_tool("dotnet-query-syntax", {
            "roslynPath": "//method[@name='ProcessAsync']",
            "workspacePath": workspace_path,
            "includeSemanticInfo": True
        })
        
        if not result["success"]:
            print(f"Failed: {result.get('error', 'Unknown error')}")
            return False
        
        matches = result["result"]["matches"]
        assert len(matches) == 1, f"Expected 1 match, got {len(matches)}"
        
        match = matches[0]
        assert match.get("semanticInfo") is not None, "Expected semantic info"
        
        # Check declared symbol info
        declared = match["semanticInfo"].get("declaredSymbol")
        assert declared is not None, "Expected declared symbol info"
        assert declared["name"] == "ProcessAsync"
        assert declared["kind"] == "Method"
        assert declared["isAsync"] == True
        
        print(f"✓ Found method with semantic info: {declared.get('fullyQualifiedName', declared['name'])}")
        
        # Test 2: Query property access with type information
        print("\n=== Test 2: Query property access with type information ===")
        result = client.call_tool("dotnet-query-syntax", {
            "roslynPath": "//property[@name='Id']",
            "workspacePath": workspace_path,
            "includeSemanticInfo": True
        })
        
        if not result["success"]:
            print(f"Failed: {result.get('error', 'Unknown error')}")
            return False
            
        matches = result["result"]["matches"]
        assert len(matches) == 1, f"Expected 1 property match, got {len(matches)}"
        
        prop_info = matches[0]["semanticInfo"].get("declaredSymbol")
        assert prop_info is not None
        assert prop_info["name"] == "Id"
        
        print(f"✓ Found property with type info: {prop_info.get('fullyQualifiedName', prop_info['name'])}")
        
        # Test 3: Query binary expressions with semantic analysis
        print("\n=== Test 3: Query null comparisons with semantic info ===")
        result = client.call_tool("dotnet-query-syntax", {
            "roslynPath": "//binary-expression[@operator='==' and @right-text='0']",
            "workspacePath": workspace_path,
            "includeSemanticInfo": True
        })
        
        if not result["success"]:
            print(f"Failed: {result.get('error', 'Unknown error')}")
            return False
            
        matches = result["result"]["matches"]
        assert len(matches) > 0, "Expected at least one comparison"
        
        # Check type information for the comparison
        comparison = matches[0]
        sem_info = comparison.get("semanticInfo")
        if sem_info and "type" in sem_info:
            type_info = sem_info["type"]
            assert type_info["name"] == "bool", f"Expected bool type, got {type_info['name']}"
            print(f"✓ Found comparison with type: {type_info['name']}")
        
        # Test 4: Query with enclosing context
        print("\n=== Test 4: Query with enclosing context ===")
        result = client.call_tool("dotnet-query-syntax", {
            "roslynPath": "//throw-statement",
            "workspacePath": workspace_path,
            "includeSemanticInfo": True
        })
        
        if not result["success"]:
            print(f"Failed: {result.get('error', 'Unknown error')}")
            return False
            
        matches = result["result"]["matches"]
        assert len(matches) > 0, "Expected at least one throw statement"
        
        throw_stmt = matches[0]
        sem_info = throw_stmt.get("semanticInfo")
        if sem_info and "enclosingContext" in sem_info:
            context = sem_info["enclosingContext"]
            assert "ProcessAsync" in context["symbol"], "Expected ProcessAsync as enclosing method"
            print(f"✓ Found throw statement in context: {context['symbol']}")
        
        # Test 5: Query without semantic info (default behavior)
        print("\n=== Test 5: Query without semantic info (default) ===")
        result = client.call_tool("dotnet-query-syntax", {
            "roslynPath": "//class",
            "workspacePath": workspace_path
            # includeSemanticInfo not specified, defaults to false
        })
        
        if not result["success"]:
            print(f"Failed: {result.get('error', 'Unknown error')}")
            return False
            
        matches = result["result"]["matches"]
        assert len(matches) == 2, f"Expected 2 classes, got {len(matches)}"
        
        # Check that semantic info is not included
        for match in matches:
            assert "semanticInfo" not in match, "Should not include semantic info by default"
        
        print("✓ Default query works without semantic info")
        
        print("\n✅ All semantic enrichment tests passed!")
        return True
        
    finally:
        # Cleanup
        shutil.rmtree(workspace_path)

if __name__ == "__main__":
    success = test_query_syntax_semantic()
    sys.exit(0 if success else 1)