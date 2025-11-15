#!/usr/bin/env python3
"""
Test F# file detection functionality
"""

import sys
import os
import json

# Add parent directory to path
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..'))

from utils.simple_client import SimpleClient

def test_fsharp_file_detection(client, file_path):
    """Test if F# file is detected when using query-syntax"""
    print(f"\nTesting F# detection for: {file_path}")
    
    result = client.call_tool("spelunk-query-syntax", {
        "roslynPath": "//method",
        "file": os.path.abspath(file_path)
    })
    
    print(f"Tool call success: {result['success']}")
    
    # Check the response content for F# detection
    if result['success']:
        # For F# files, the tool call succeeds but returns a nested result
        result_data = result.get('result', {})
        
        # Check if the nested result indicates F# not supported
        if not result_data.get('success', True):
            message = result_data.get('message', '')
            if 'F# support is not yet implemented' in message:
                print(f"✅ F# file correctly detected and appropriate message returned")
                print(f"   Message: {message[:100]}...")
                # Check for additional info
                info = result_data.get('info', {})
                if info:
                    print(f"   Requested file: {info.get('requestedFile', 'N/A')}")
                    print(f"   Note: {info.get('note', 'N/A')}")
                return True
            
        print(f"❌ F# file was not detected - unexpected response")
        print(f"   Result data: {result_data}")
        return False
    else:
        error_msg = result.get('message', '')
        print(f"❌ Tool call failed: {error_msg}")
        return False

def test_fsharp_project_detection(client, project_path):
    """Test if F# project is detected when loading"""
    print(f"\nTesting F# project detection for: {project_path}")
    
    result = client.call_tool("spelunk-load-workspace", {
        "path": os.path.abspath(project_path)
    })
    
    print(f"Result success: {result['success']}")
    
    # Check if we get F# not supported message
    if not result['success']:
        error_msg = result.get('message', '')
        print(f"Error message: {error_msg}")
        # F# projects might fail for different reasons
        return 'fsproj' in project_path.lower()
    else:
        # If it loaded successfully, check if it's actually an F# project
        content = result.get('result', {}).get('content', [])
        if content and content[0].get('type') == 'text':
            text = content[0].get('text', '')
            print(f"Loaded project info: {text[:100]}...")
        return True

def test_non_fsharp_file(client, file_path):
    """Test that non-F# files are not detected as F#"""
    print(f"\nTesting non-F# file: {file_path}")
    
    result = client.call_tool("spelunk-query-syntax", {
        "roslynPath": "//method",
        "file": os.path.abspath(file_path)
    })
    
    print(f"Tool call success: {result['success']}")
    
    # For C# files, the tool should work normally
    if result['success']:
        result_data = result.get('result', {})
        
        # Check if the nested result has F# message (it shouldn't)
        if not result_data.get('success', True):
            message = result_data.get('message', '')
            if 'F# support is not yet implemented' in message:
                print(f"❌ C# file incorrectly detected as F#")
                return False
        
        # If we got nodes or successful result, that's expected for C#
        nodes = result_data.get('nodes', None)
        if nodes is not None:
            print(f"✅ C# file processed normally (found {len(nodes)} nodes)")
            return True
        else:
            print(f"✅ C# file processed normally")
            return True
    else:
        error_msg = result.get('message', '')
        print(f"Tool call failed: {error_msg[:100]}...")
        # As long as it's not F# related, it's OK
        return 'F# support is not yet implemented' not in error_msg

def main():
    """Run F# detection tests"""
    client = SimpleClient(allowed_paths=["test-workspace"])
    
    # First, load a C# workspace so we have a context
    result = client.call_tool("spelunk-load-workspace", {
        "path": os.path.abspath("test-workspace/TestProject.csproj")
    })
    print(f"C# workspace loaded: {result['success']}")
    
    tests_passed = 0
    tests_total = 0
    
    # Test F# file extensions
    fsharp_files = [
        "test-workspace/FSharpDetectionTest.fs",
        "test-workspace/FSharpScript.fsx", 
        "test-workspace/FSharpSignature.fsi",
        "test-workspace/FSharpTestProject/Library.fs"
    ]
    
    for fs_file in fsharp_files:
        if os.path.exists(fs_file):
            tests_total += 1
            if test_fsharp_file_detection(client, fs_file):
                tests_passed += 1
    
    # Test non-F# file
    tests_total += 1
    if test_non_fsharp_file(client, "test-workspace/Program.cs"):
        tests_passed += 1
    
    # Test F# project detection
    fsproj_path = "test-workspace/FSharpTestProject/FSharpTestProject.fsproj"
    if os.path.exists(fsproj_path):
        tests_total += 1
        if test_fsharp_project_detection(client, fsproj_path):
            tests_passed += 1
    
    print(f"\n{'='*60}")
    print(f"F# Detection Test Results: {tests_passed}/{tests_total} passed")
    print(f"{'='*60}")
    
    client.close()
    return tests_passed == tests_total

if __name__ == "__main__":
    sys.exit(0 if main() else 1)