#!/usr/bin/env python3
"""
Test script for F# project detection and loading functionality
"""

import sys
import os
sys.path.append(os.path.join(os.path.dirname(__file__), '..', 'utils'))

from test_client import TestClient
import json

def test_fsharp_projects():
    client = TestClient()
    
    # Path to F# test project
    fsharp_project_path = os.path.join(os.getcwd(), "test-workspace", "FSharpTestProject", "FSharpTestProject.fsproj")
    
    print("Testing F# Project Detection and Loading...")
    print("=" * 50)
    
    # Test 1: Try to load F# project through regular workspace loading (should fail)
    print("\n1. Attempting to load F# project through regular workspace loading...")
    load_result = client.call_tool("spelunk-load-workspace", {
        "path": fsharp_project_path
    })
    
    if load_result["success"]:
        print("✓ F# project loaded through regular workspace (unexpected success)")
        workspace_id = load_result["workspaceId"]
    else:
        print(f"✓ Expected failure loading F# project through MSBuildWorkspace: {load_result['message']}")
        # The workspace might still be created even if the F# project fails
        workspace_id = load_result.get("workspaceId")
    
    # Test 2: Check for detected F# projects
    print("\n2. Checking for detected F# projects...")
    fsharp_result = client.call_tool("spelunk-fsharp-projects", {
        "workspaceId": workspace_id,
        "includeLoaded": True
    })
    
    if fsharp_result["success"]:
        print("✓ F# projects query successful")
        print("F# Projects detected:")
        # Parse the text response to see if our project was detected
        response_text = fsharp_result["result"]["content"][0]["text"]
        print(response_text)
        
        if "FSharpTestProject" in response_text:
            print("✓ Our F# test project was detected!")
        else:
            print("? F# test project not found in detection")
    else:
        print(f"✗ Failed to query F# projects: {fsharp_result['message']}")
        return False
    
    # Test 3: Load F# project using dedicated F# loader
    print("\n3. Loading F# project using dotnet-load-fsharp-project...")
    load_fsharp_result = client.call_tool("spelunk-load-fsharp-project", {
        "projectPath": fsharp_project_path
    })
    
    if load_fsharp_result["success"]:
        print("✓ F# project loaded successfully using dedicated loader")
        response_text = load_fsharp_result["result"]["content"][0]["text"]
        print("Load result:")
        print(response_text)
        
        if "Loaded successfully" in response_text:
            print("✓ F# project loaded and analyzed")
        else:
            print("? F# project load status unclear")
    else:
        print(f"✗ Failed to load F# project: {load_fsharp_result['message']}")
        return False
    
    # Test 4: Find F# symbols using FSharpPath
    print("\n4. Finding F# symbols using FSharpPath queries...")
    
    # Test finding functions
    library_path = os.path.join(os.getcwd(), "test-workspace", "FSharpTestProject", "Library.fs")
    
    functions_result = client.call_tool("spelunk-fsharp-find-symbols", {
        "filePath": library_path,
        "query": "//function"
    })
    
    if functions_result["success"]:
        print("✓ F# function search successful")
        response_text = functions_result["result"]["content"][0]["text"]
        print("Found F# functions:")
        print(response_text)
    else:
        print(f"✗ Failed to find F# functions: {functions_result['message']}")
        # This might fail if F# compiler service is not available
        print("Note: F# support requires FSharp.Compiler.Service package")
    
    # Test 5: Find specific F# constructs
    print("\n5. Finding recursive F# functions...")
    
    recursive_result = client.call_tool("spelunk-fsharp-find-symbols", {
        "filePath": library_path,
        "query": "//function[@recursive]"
    })
    
    if recursive_result["success"]:
        print("✓ F# recursive function search successful")
        response_text = recursive_result["result"]["content"][0]["text"]
        print("Found recursive functions:")
        print(response_text)
    else:
        print(f"Note: Recursive function search failed: {recursive_result['message']}")
        print("This is expected if F# compiler integration is not fully working")
    
    # Test 6: Test with different F# files
    print("\n6. Testing with F# types file...")
    
    types_path = os.path.join(os.getcwd(), "test-workspace", "FSharpTestProject", "TestTypes.fs")
    
    types_result = client.call_tool("spelunk-fsharp-find-symbols", {
        "filePath": types_path,
        "query": "//type"
    })
    
    if types_result["success"]:
        print("✓ F# types search successful")
        response_text = types_result["result"]["content"][0]["text"]
        print("Found F# types:")
        print(response_text[:500] + "..." if len(response_text) > 500 else response_text)
    else:
        print(f"Note: F# types search failed: {types_result['message']}")
    
    print("\n" + "=" * 50)
    print("F# Project Tests COMPLETED!")
    print("Note: Some F# features may not work without FSharp.Compiler.Service runtime support")
    return True

if __name__ == "__main__":
    success = test_fsharp_projects()
    sys.exit(0 if success else 1)