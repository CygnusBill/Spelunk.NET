#!/usr/bin/env python3
"""
Test semantic enrichment features for query-syntax, navigate, and get-ast tools
"""

import sys
import os
import json

# Add parent directory to path
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..'))

from utils.test_client import TestClient

def test_query_syntax_semantic(client):
    """Test query-syntax with semantic info"""
    
    # Load workspace first
    workspace_result = client.call_tool("dotnet-load-workspace", {
        "path": os.path.abspath("test-workspace/TestProject.csproj")
    })
    if not workspace_result.get("success"):
        raise AssertionError(f"Failed to load workspace: {workspace_result.get('message')}")
    
    # Test 1: Query methods with semantic info
    result = client.call_tool("dotnet-query-syntax", {
        "roslynPath": "//method",
        "file": "test-workspace/Program.cs",
        "includeSemanticInfo": True
    })
    
    if not result.get("success"):
        raise AssertionError(f"Failed to query syntax: {result.get('message')}")
    
    nodes = result["result"].get("nodes", [])
    assert len(nodes) > 0, "Should find at least one method"
    
    # Check semantic info is present
    first_method = nodes[0]
    assert "semanticInfo" in first_method, "Should have semantic info"
    
    semantic_info = first_method["semanticInfo"]
    
    # Verify semantic fields
    assert "symbolKind" in semantic_info
    assert "returnType" in semantic_info
    assert "accessibility" in semantic_info
    
    return True

def test_navigate_semantic(client):
    """Test navigate with semantic info"""
    
    result = client.call_tool("dotnet-navigate", {
        "from": {
            "file": "test-workspace/Program.cs",
            "line": 10,
            "column": 5
        },
        "path": "ancestor::class[1]",
        "includeSemanticInfo": True
    })
    
    if not result.get("success"):
        raise AssertionError(f"Failed to navigate: {result.get('message')}")
    
    target = result["result"].get("target")
    assert target is not None, "Should find target"
    
    # Check semantic info
    assert "semanticInfo" in target, "Should have semantic info"
    
    semantic_info = target["semanticInfo"]
    assert "symbolKind" in semantic_info
    assert semantic_info["symbolKind"] == "NamedType", "Should be a class"
    
    return True

def test_get_ast_semantic(client):
    """Test get-ast with semantic info"""
    
    result = client.call_tool("dotnet-get-ast", {
        "file": "test-workspace/Program.cs",
        "root": "//method[Main]",
        "depth": 2,
        "includeSemanticInfo": True
    })
    
    if not result.get("success"):
        raise AssertionError(f"Failed to get AST: {result.get('message')}")
    
    ast = result["result"].get("ast", [])
    assert len(ast) > 0, "Should have AST nodes"
    
    # Check root node has semantic info
    root = ast[0]
    assert "semanticInfo" in root, "Should have semantic info"
    
    semantic_info = root["semanticInfo"]
    assert "symbolKind" in semantic_info
    assert "returnType" in semantic_info
    
    # Check children also have semantic info if applicable
    if "children" in root and len(root["children"]) > 0:
        for child in root["children"]:
            if child.get("nodeType") in ["Parameter", "LocalDeclarationStatement"]:
                assert "semanticInfo" in child, f"Child {child.get('nodeType')} should have semantic info"
    
    return True

def test_semantic_info_without_flag(client):
    """Test that semantic info is not included when flag is false"""
    
    result = client.call_tool("dotnet-query-syntax", {
        "roslynPath": "//method",
        "file": "test-workspace/Program.cs"
        # includeSemanticInfo not specified, defaults to false
    })
    
    if not result.get("success"):
        raise AssertionError(f"Failed to query syntax without flag: {result.get('message')}")
    
    nodes = result["result"].get("nodes", [])
    assert len(nodes) > 0, "Should find at least one method"
    
    # Verify no semantic info
    first_method = nodes[0]
    assert "semanticInfo" not in first_method, "Should not have semantic info when flag is false"
    
    return True

def test_semantic_info_types(client):
    """Test various semantic info for different node types"""
    
    # Test variable declarations
    result = client.call_tool("dotnet-query-syntax", {
        "roslynPath": "//local-declaration-statement",
        "file": "test-workspace/Program.cs",
        "includeSemanticInfo": True
    })
    
    if not result.get("success"):
        raise AssertionError(f"Failed to query variable declarations: {result.get('message')}")
    
    nodes = result["result"].get("nodes", [])
    
    if len(nodes) > 0:
        var_decl = nodes[0]
        if "semanticInfo" in var_decl:
            semantic_info = var_decl["semanticInfo"]
            assert "variables" in semantic_info or "type" in semantic_info, \
                "Variable declaration should have variable or type info"
    
    return True

def main():
    """Run all semantic enrichment tests"""
    client = TestClient(allowed_paths=["test-workspace"])
    
    tests = [
        ("Query Syntax with Semantic Info", test_query_syntax_semantic),
        ("Navigate with Semantic Info", test_navigate_semantic),
        ("Get AST with Semantic Info", test_get_ast_semantic),
        ("No Semantic Info Without Flag", test_semantic_info_without_flag),
        ("Semantic Info for Various Types", test_semantic_info_types)
    ]
    
    passed = 0
    failed = 0
    
    for test_name, test_func in tests:
        try:
            print(f"\n{'='*60}")
            print(f"Running: {test_name}")
            print(f"{'='*60}")
            
            if test_func(client):
                print(f"✓ {test_name} - PASSED")
                passed += 1
            else:
                print(f"✗ {test_name} - FAILED")
                failed += 1
        except Exception as e:
            print(f"✗ {test_name} - FAILED: {e}")
            failed += 1
    
    print(f"\n{'='*60}")
    print(f"Semantic Enrichment Test Results: {passed} passed, {failed} failed")
    print(f"{'='*60}")
    
    return failed == 0

if __name__ == "__main__":
    sys.exit(0 if main() else 1)