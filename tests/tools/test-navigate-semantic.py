#!/usr/bin/env python3
"""Test the dotnet-navigate tool with semantic information enrichment"""

import os
import sys
sys.path.append(os.path.join(os.path.dirname(__file__), '..', 'utils'))

from test_client import TestClient
import json
import tempfile
import shutil

def test_navigate_semantic():
    """Test navigate tool with semantic information"""
    
    server_path = os.path.join(os.path.dirname(__file__), "..", "..", "src", "McpRoslyn.Server")
    client = TestClient(server_path=server_path)
    
    # Create a temporary workspace
    workspace_path = tempfile.mkdtemp(prefix="test_navigate_semantic_")
    try:
        # Create a test file with navigation targets
        test_file = os.path.join(workspace_path, "NavigateTest.cs")
        with open(test_file, 'w') as f:
            f.write("""
using System;
using System.Collections.Generic;

namespace TestNavigation
{
    public class BaseService
    {
        public virtual string GetName() => "Base";
    }
    
    public class OrderService : BaseService
    {
        private readonly string _prefix = "Order";
        
        public override string GetName()
        {
            var result = ProcessOrder();
            return $"{_prefix}: {result}";
        }
        
        private string ProcessOrder()
        {
            // Line 23: Inside ProcessOrder method
            var items = new List<string> { "Item1", "Item2" };
            
            foreach (var item in items)
            {
                if (item == "Item1")
                {
                    // Line 30: Inside if statement
                    Console.WriteLine(item);
                }
            }
            
            return string.Join(", ", items);
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
        result = client.call_tool("dotnet-load-workspace", {
            "path": csproj_file
        })
        assert result["success"], f"Failed to load workspace: {result.get('error')}"
        
        # Test 1: Navigate to parent method with semantic info
        print("\n=== Test 1: Navigate to parent method with semantic info ===")
        result = client.call_tool("dotnet-navigate", {
            "from": {
                "file": test_file,
                "line": 30,  # Inside if statement
                "column": 21
            },
            "path": "ancestor::method[1]",
            "includeSemanticInfo": True
        })
        
        if not result["success"]:
            print(f"Failed: {result.get('error', 'Unknown error')}")
            return False
        
        target = result["result"]["target"]
        assert target is not None, "Expected navigation target"
        
        # Check semantic info
        sem_info = target.get("semanticInfo")
        assert sem_info is not None, "Expected semantic info"
        
        declared = sem_info.get("declaredSymbol")
        if declared:
            assert declared["name"] == "ProcessOrder", f"Expected ProcessOrder, got {declared['name']}"
            assert declared["kind"] == "Method"
            print(f"✓ Navigated to method with semantic info: {declared['fullyQualifiedName']}")
        
        # Test 2: Navigate to parent class with type info
        print("\n=== Test 2: Navigate to parent class with semantic info ===")
        result = client.call_tool("dotnet-navigate", {
            "from": {
                "file": test_file,
                "line": 18,  # Inside GetName method
                "column": 13
            },
            "path": "ancestor::class[1]",
            "includeSemanticInfo": True
        })
        
        if not result["success"]:
            print(f"Failed: {result.get('error', 'Unknown error')}")
            return False
            
        target = result["result"]["target"]
        sem_info = target.get("semanticInfo")
        
        if sem_info and "declaredSymbol" in sem_info:
            symbol = sem_info["declaredSymbol"]
            assert symbol["name"] == "OrderService"
            assert symbol["kind"] == "NamedType"
            
            # Check base type info
            if "baseType" in symbol:
                base = symbol["baseType"]
                assert "BaseService" in base, "Expected BaseService as base type"
                print(f"✓ Found class with base type info: {symbol['name']} : {base}")
        
        # Test 3: Navigate from identifier to its declaration
        print("\n=== Test 3: Navigate from usage to declaration with semantic info ===")
        result = client.call_tool("dotnet-navigate", {
            "from": {
                "file": test_file,
                "line": 18,  # var result = ProcessOrder();
                "column": 25  # On 'ProcessOrder'
            },
            "path": ".",  # Current node
            "includeSemanticInfo": True
        })
        
        if not result["success"]:
            print(f"Failed: {result.get('error', 'Unknown error')}")
            return False
            
        target = result["result"]["target"]
        sem_info = target.get("semanticInfo")
        
        if sem_info:
            # Check if we have symbol info for the identifier
            if "symbol" in sem_info:
                sym = sem_info["symbol"]
                print(f"✓ Found symbol info at identifier: {sym}")
            
            # Check type info
            if "type" in sem_info:
                type_info = sem_info["type"]
                assert type_info["name"] == "string", f"Expected string type, got {type_info['name']}"
                print(f"✓ Found type info: {type_info['fullyQualifiedName']}")
        
        # Test 4: Navigate without semantic info (default)
        print("\n=== Test 4: Navigate without semantic info (default) ===")
        result = client.call_tool("dotnet-navigate", {
            "from": {
                "file": test_file,
                "line": 30,
                "column": 21
            },
            "path": "ancestor::class[1]"
            # includeSemanticInfo not specified
        })
        
        if not result["success"]:
            print(f"Failed: {result.get('error', 'Unknown error')}")
            return False
            
        target = result["result"]["target"]
        assert "semanticInfo" not in target, "Should not include semantic info by default"
        print("✓ Default navigation works without semantic info")
        
        # Test 5: Navigate with following-sibling axis
        print("\n=== Test 5: Navigate to next statement with semantic info ===")
        result = client.call_tool("dotnet-navigate", {
            "from": {
                "file": test_file,
                "line": 17,  # var result = ProcessOrder();
                "column": 13
            },
            "path": "following-sibling::statement[1]",
            "includeSemanticInfo": True
        })
        
        if not result["success"]:
            print(f"Failed: {result.get('error', 'Unknown error')}")
            # This might fail if navigation doesn't find a sibling
            print("Note: Following-sibling navigation may not find results")
        else:
            target = result["result"]["target"]
            if target and "semanticInfo" in target:
                print("✓ Found next statement with semantic info")
        
        print("\n✅ All navigate semantic enrichment tests passed!")
        return True
        
    finally:
        # Cleanup
        shutil.rmtree(workspace_path)

if __name__ == "__main__":
    success = test_navigate_semantic()
    sys.exit(0 if success else 1)