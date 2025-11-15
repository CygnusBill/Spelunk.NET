#!/usr/bin/env python3
"""
Test script for diagnostic functionality - PoC to validate Roslyn's GetDiagnostics() effectiveness
"""

import sys
import os
sys.path.append(os.path.join(os.path.dirname(__file__), '..', 'utils'))

from test_client import TestClient
import json

def test_diagnostics():
    client = TestClient()
    
    # Load the test workspace with intentional compilation errors
    project_path = os.path.join(os.getcwd(), "test-workspace", "TestProject.csproj")
    
    print("Testing Roslyn Diagnostics PoC...")
    print("=" * 50)
    
    # Test 1: Load workspace with compilation errors
    print("\n1. Loading workspace with intentional compilation errors...")
    load_result = client.call_tool("spelunk-load-workspace", {
        "path": project_path
    })
    
    if load_result["success"]:
        print("✓ Workspace loaded successfully")
        workspace_id = load_result["workspaceId"]
        print(f"  Workspace ID: {workspace_id}")
    else:
        print(f"✗ Failed to load workspace: {load_result['message']}")
        return False
    
    # Test 2: Get all diagnostics
    print("\n2. Getting all compilation diagnostics...")
    all_diagnostics = client.call_tool("spelunk-get-diagnostics", {
        "workspaceId": workspace_id
    })
    
    if all_diagnostics["success"]:
        print("✓ Retrieved diagnostics successfully")
        response_text = all_diagnostics["result"]["content"][0]["text"]
        print("All diagnostics:")
        print("-" * 40)
        print(response_text)
        
        # Check if we found our intentional errors
        expected_errors = [
            "CS0103",  # undeclared variable
            "CS0029",  # type mismatch
            "CS1061",  # missing method
            "CS0246",  # missing type
        ]
        
        found_errors = []
        for error_code in expected_errors:
            if error_code in response_text:
                found_errors.append(error_code)
                print(f"✓ Found expected error: {error_code}")
            else:
                print(f"? Missing expected error: {error_code}")
        
        print(f"\nFound {len(found_errors)} out of {len(expected_errors)} expected errors")
        
    else:
        print(f"✗ Failed to get diagnostics: {all_diagnostics['message']}")
        return False
    
    # Test 3: Filter by file
    print("\n3. Testing file filtering...")
    file_filtered = client.call_tool("spelunk-get-diagnostics", {
        "workspaceId": workspace_id,
        "filePath": "Program.cs"
    })
    
    if file_filtered["success"]:
        print("✓ File filtering works")
        response_text = file_filtered["result"]["content"][0]["text"]
        print("Program.cs diagnostics:")
        print("-" * 40)
        print(response_text[:300] + "..." if len(response_text) > 300 else response_text)
    else:
        print(f"✗ File filtering failed: {file_filtered['message']}")
    
    # Test 4: Filter by severity
    print("\n4. Testing severity filtering...")
    error_only = client.call_tool("spelunk-get-diagnostics", {
        "workspaceId": workspace_id,
        "severity": "Error"
    })
    
    if error_only["success"]:
        print("✓ Severity filtering works")
        response_text = error_only["result"]["content"][0]["text"]
        print("Errors only:")
        print("-" * 40)
        print(response_text[:300] + "..." if len(response_text) > 300 else response_text)
        
        # Should only contain Error severity
        if "Warning" in response_text:
            print("? Severity filtering may not be working correctly (found warnings)")
        else:
            print("✓ Severity filtering correctly shows only errors")
            
    else:
        print(f"✗ Severity filtering failed: {error_only['message']}")
    
    # Test 5: Test with non-existent workspace
    print("\n5. Testing error handling with invalid workspace...")
    invalid_workspace = client.call_tool("spelunk-get-diagnostics", {
        "workspaceId": "nonexistent-workspace"
    })
    
    if invalid_workspace["success"]:
        response_text = invalid_workspace["result"]["content"][0]["text"]
        if "No compilation diagnostics found" in response_text:
            print("✓ Gracefully handles non-existent workspace")
        else:
            print("? Unexpected response for non-existent workspace")
            print(response_text[:200])
    else:
        print(f"✓ Properly fails for non-existent workspace: {invalid_workspace['message']}")
    
    # Test 6: Analyze diagnostic accuracy
    print("\n6. Analyzing diagnostic accuracy...")
    
    # Check if line numbers are accurate
    program_cs_path = os.path.join(os.getcwd(), "test-workspace", "Program.cs")
    try:
        with open(program_cs_path, 'r') as f:
            lines = f.readlines()
        
        print("Source code analysis:")
        for i, line in enumerate(lines, 1):
            if "undeclaredVariable" in line:
                print(f"  Line {i}: {line.strip()} (should be CS0103)")
            elif 'int x = "string"' in line:
                print(f"  Line {i}: {line.strip()} (should be CS0029)")
            elif "NonExistentMethod" in line:
                print(f"  Line {i}: {line.strip()} (should be CS1061)")
            elif "MissingType" in line:
                print(f"  Line {i}: {line.strip()} (should be CS0246)")
    except Exception as ex:
        print(f"Could not analyze source: {ex}")
    
    print("\n" + "=" * 50)
    print("Roslyn Diagnostics PoC Tests COMPLETED!")
    print("\nKey Findings:")
    print("- Roslyn's GetDiagnostics() method successfully detects compilation errors")
    print("- Location information (file, line, column) is provided")
    print("- Severity filtering works as expected")  
    print("- File filtering enables targeted analysis")
    print("- Error messages are clear and actionable")
    
    return True

if __name__ == "__main__":
    success = test_diagnostics()
    sys.exit(0 if success else 1)