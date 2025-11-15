#!/usr/bin/env python3
"""Simple test for F# file detection"""

import os
import sys
sys.path.append(os.path.join(os.path.dirname(__file__), '..', 'utils'))

from test_client import TestClient
import json

def test_fsharp_detection():
    """Test that F# files are properly detected"""
    
    server_path = os.path.join(os.path.dirname(__file__), "..", "..", "src", "McpDotnet.Server")
    client = TestClient(server_path=server_path)
    
    # Use the F# test file we created
    fsharp_file = os.path.join(os.getcwd(), "test-workspace", "FSharpDetectionTest.fs")
    
    print("Testing F# File Detection...")
    print("Testing file:", fsharp_file)
    
    # Test query-syntax with F# file
    result = client.call_tool("spelunk-query-syntax", {
        "file": fsharp_file,
        "roslynPath": "//function"
    })
    
    print(f"Result success: {result.get('success')}")
    print(f"Result: {json.dumps(result, indent=2)}")
    
    # Check if we got the F# not supported response
    if result.get("success") and "result" in result:
        inner_result = result["result"]
        if isinstance(inner_result, dict) and inner_result.get("success") == False:
            message = inner_result.get("message", "")
            if "F# support is not yet implemented" in message:
                print("✅ F# file correctly detected!")
                print(f"Message: {message[:100]}...")
                return True
    
    print("❌ F# file not properly detected")
    return False

if __name__ == "__main__":
    try:
        success = test_fsharp_detection()
        sys.exit(0 if success else 1)
    except Exception as e:
        print(f"Test failed with error: {e}")
        import traceback
        traceback.print_exc()
        sys.exit(1)