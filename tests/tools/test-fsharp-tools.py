#!/usr/bin/env python3
"""
Test F# tools functionality - load project, find symbols, query, and get AST
"""

import sys
import os
import json

# Add parent directory to path
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..'))

from utils.simple_client import SimpleClient

def test_fsharp_load_project(client, project_path):
    """Test loading an F# project"""
    print(f"\n=== Testing F# Project Load ===")
    print(f"Project: {project_path}")
    
    result = client.call_tool("dotnet-fsharp-load-project", {
        "projectPath": os.path.abspath(project_path)
    })
    
    if result['success']:
        content = result['result']['content'][0]['text']
        print(f"✅ Successfully loaded F# project")
        print(f"   Response: {content[:200]}...")
        
        # Check if project info is returned
        project_info = result['result'].get('projectInfo')
        if project_info:
            print(f"   Project Name: {project_info.get('projectName', 'N/A')}")
            print(f"   Source Files: {len(project_info.get('sourceFiles', []))}")
            print(f"   Target Framework: {project_info.get('targetFramework', 'N/A')}")
        return True
    else:
        print(f"❌ Failed to load F# project: {result.get('message', 'Unknown error')}")
        return False

def test_fsharp_find_symbols(client, file_path, pattern):
    """Test finding F# symbols"""
    print(f"\n=== Testing F# Find Symbols ===")
    print(f"File: {file_path}")
    print(f"Pattern: {pattern}")
    
    result = client.call_tool("dotnet-fsharp-find-symbols", {
        "pattern": pattern,
        "filePath": os.path.abspath(file_path)
    })
    
    if result['success']:
        content = result['result']['content'][0]['text']
        symbols = result['result'].get('symbols', [])
        print(f"✅ Successfully found {len(symbols)} symbols")
        
        for i, symbol in enumerate(symbols[:5]):  # Show first 5
            print(f"   [{i+1}] {symbol['kind']}: {symbol['name']} @ line {symbol['startLine']}")
        
        if len(symbols) > 5:
            print(f"   ... and {len(symbols) - 5} more")
        return True
    else:
        print(f"❌ Failed to find symbols: {result.get('message', 'Unknown error')}")
        return False

def test_fsharp_query(client, file_path, query):
    """Test FSharpPath query"""
    print(f"\n=== Testing FSharpPath Query ===")
    print(f"File: {file_path}")
    print(f"Query: {query}")
    
    result = client.call_tool("dotnet-fsharp-query", {
        "fsharpPath": query,
        "file": os.path.abspath(file_path),
        "includeContext": True,
        "contextLines": 2
    })
    
    if result['success']:
        content = result['result']['content'][0]['text']
        nodes = result['result'].get('nodes', [])
        print(f"✅ Query found {len(nodes)} matches")
        
        # Show first few matches
        for i, node in enumerate(nodes[:3]):
            print(f"   [{i+1}] {node['type']}: {node.get('name', 'unnamed')}")
            if node.get('location'):
                loc = node['location']
                print(f"       @ {loc['file']}:{loc['startLine']}:{loc['startColumn']}")
        
        if len(nodes) > 3:
            print(f"   ... and {len(nodes) - 3} more")
        return True
    else:
        print(f"❌ Query failed: {result.get('message', 'Unknown error')}")
        return False

def test_fsharp_get_ast(client, file_path, root_query=None):
    """Test getting F# AST"""
    print(f"\n=== Testing F# Get AST ===")
    print(f"File: {file_path}")
    if root_query:
        print(f"Root: {root_query}")
    
    params = {
        "filePath": os.path.abspath(file_path),
        "depth": 3,
        "includeRange": True
    }
    
    if root_query:
        params["root"] = root_query
    
    result = client.call_tool("dotnet-fsharp-get-ast", params)
    
    if result['success']:
        content = result['result']['content'][0]['text']
        ast = result['result'].get('ast')
        print(f"✅ Successfully retrieved F# AST")
        
        # Show AST structure
        if ast:
            # Handle different possible AST structures
            node_type = ast.get('Type') or ast.get('type') or 'Unknown'
            node_kind = ast.get('Kind') or ast.get('kind') or 'N/A'
            node_name = ast.get('Name') or ast.get('name')
            children = ast.get('Children') or ast.get('children') or []
            
            print(f"   Root: {node_type} ({node_kind})")
            if node_name:
                print(f"   Name: {node_name}")
            print(f"   Children: {len(children)}")
            
            # Show first few children
            for i, child in enumerate(children[:3]):
                child_type = child.get('Type') or child.get('type') or 'Unknown'
                child_kind = child.get('Kind') or child.get('kind') or 'N/A'
                child_name = child.get('Name') or child.get('name')
                print(f"     [{i+1}] {child_type} ({child_kind})")
                if child_name:
                    print(f"         Name: {child_name}")
        
        return True
    else:
        print(f"❌ Failed to get AST: {result.get('message', 'Unknown error')}")
        return False

def test_fsharp_path_queries(client, file_path):
    """Test various FSharpPath queries"""
    print(f"\n=== Testing FSharpPath Query Examples ===")
    
    queries = [
        ("//let", "All let bindings"),
        ("//function", "All functions"),
        ("//type", "All type definitions"),
        ("//let[@name='factorial']", "Specific function by name"),
        ("//module", "All modules"),
        ("//*[match]", "All nodes containing match"),
        ("//let[@name='factorial']", "Recursive factorial function"),
        ("descendant::type", "All type definitions (with axis)"),
        ("//*[@name]", "All named nodes")
    ]
    
    passed = 0
    for query, description in queries:
        print(f"\nTesting: {description}")
        if test_fsharp_query(client, file_path, query):
            passed += 1
    
    print(f"\nFSharpPath queries passed: {passed}/{len(queries)}")
    return passed == len(queries)

def main():
    """Run F# tools integration tests"""
    client = SimpleClient(allowed_paths=["test-workspace"])
    
    tests_passed = 0
    tests_total = 0
    
    # Test F# project paths
    fsproj_path = "test-workspace/FSharpTestProject/FSharpTestProject.fsproj"
    fs_file_path = "test-workspace/FSharpTestProject/Library.fs"
    
    # Don't create the file - use the existing Library.fs
    
    # Create test F# project if it doesn't exist
    if not os.path.exists(fsproj_path):
        print("Creating test F# project...")
        with open(fsproj_path, 'w') as f:
            f.write("""<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="Library.fs" />
  </ItemGroup>
</Project>
""")
    
    # Test 1: Load F# project
    if os.path.exists(fsproj_path):
        tests_total += 1
        if test_fsharp_load_project(client, fsproj_path):
            tests_passed += 1
    
    # Test 2: Find symbols
    if os.path.exists(fs_file_path):
        tests_total += 1
        if test_fsharp_find_symbols(client, fs_file_path, "*"):
            tests_passed += 1
        
        tests_total += 1
        if test_fsharp_find_symbols(client, fs_file_path, "calculate*"):
            tests_passed += 1
    
    # Test 3: FSharpPath queries
    if os.path.exists(fs_file_path):
        tests_total += 1
        if test_fsharp_query(client, fs_file_path, "//let"):
            tests_passed += 1
        
        tests_total += 1
        if test_fsharp_query(client, fs_file_path, "//type"):
            tests_passed += 1
        
        tests_total += 1
        if test_fsharp_query(client, fs_file_path, "//match"):
            tests_passed += 1
    
    # Test 4: Get AST
    if os.path.exists(fs_file_path):
        tests_total += 1
        if test_fsharp_get_ast(client, fs_file_path):
            tests_passed += 1
        
        tests_total += 1
        if test_fsharp_get_ast(client, fs_file_path, "//type[Person]"):
            tests_passed += 1
    
    # Test 5: Various FSharpPath queries
    if os.path.exists(fs_file_path):
        tests_total += 1
        if test_fsharp_path_queries(client, fs_file_path):
            tests_passed += 1
    
    print(f"\n{'='*60}")
    print(f"F# Tools Test Results: {tests_passed}/{tests_total} passed")
    print(f"{'='*60}")
    
    client.close()
    return tests_passed == tests_total

if __name__ == "__main__":
    sys.exit(0 if main() else 1)