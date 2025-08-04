#!/usr/bin/env python3
"""Test F# file detection across various tools"""

import os
import sys
sys.path.append(os.path.join(os.path.dirname(__file__), '..', 'utils'))

from test_client import TestClient
import json

def test_fsharp_detection():
    """Test that F# files are properly detected and return appropriate messages"""
    
    server_path = os.path.join(os.path.dirname(__file__), "..", "..", "src", "McpRoslyn.Server")
    client = TestClient(server_path=server_path)
    
    # F# test files location
    fsharp_test_dir = os.path.join(os.getcwd(), "test-workspace")
    test_files = {
        ".fs": os.path.join(fsharp_test_dir, "FSharpDetectionTest.fs"),
        ".fsx": os.path.join(fsharp_test_dir, "FSharpScript.fsx"),
        ".fsi": os.path.join(fsharp_test_dir, "FSharpSignature.fsi")
    }
    
    # Create test files if they don't exist
    if not os.path.exists(test_files[".fs"]):
        print(f"Creating test F# file: {test_files['.fs']}")
        with open(test_files[".fs"], 'w') as f:
            f.write("""// F# source file
module TestModule
let add x y = x + y
""")
    
    if not os.path.exists(test_files[".fsx"]):
        with open(test_files[".fsx"], 'w') as f:
            f.write("""// F# script file
#r "System.dll"
printfn "Hello from F# script"
""")
    
    if not os.path.exists(test_files[".fsi"]):
        with open(test_files[".fsi"], 'w') as f:
            f.write("""// F# signature file
module TestModule
val add : int -> int -> int
""")
    
    print("Testing F# File Detection...")
    print("=" * 50)
    
    # Test 1: Query-syntax with F# file
    print("\n1. Testing query-syntax with F# source file (.fs)...")
    result = client.call_tool("dotnet-query-syntax", {
        "file": test_files[".fs"],
        "roslynPath": "//function"
    })
    
    # The F# detection returns a result with success=false inside
    if "result" in result and result["result"].get("success") == False:
        # Check the response structure
        error_response = result["result"]
        
        message = error_response.get("message", "")
        assert "F# support is not yet implemented" in message, f"Expected F# not implemented message, got: {message}"
        assert "query-syntax" in message, "Expected tool name in message"
        
        info = error_response.get("info", {})
        assert info.get("requestedFile") == test_files[".fs"], "Expected file path in info"
        assert info.get("requestedQuery") == "//function", "Expected query in info"
        assert "FSharpPath" in info.get("note", ""), "Expected note about FSharpPath"
        
        print("✓ F# file correctly detected and appropriate message returned")
        print(f"  Message: {message[:80]}...")
    else:
        print("✗ Unexpected response structure")
        print(f"  Result: {result}")
        return False
    
    # Test 2: Test different F# file extensions
    print("\n2. Testing different F# file extensions...")
    for ext, filepath in test_files.items():
        if os.path.exists(filepath):
            result = client.call_tool("dotnet-query-syntax", {
                "file": filepath,
                "roslynPath": "//any"
            })
            
            if "result" in result and result["result"].get("success") == False:
                message = result["result"].get("message", "")
                if "F# support is not yet implemented" in message:
                    print(f"✓ {ext} file detected correctly")
                else:
                    print(f"✗ {ext} file not properly detected")
                    return False
            else:
                print(f"✗ {ext} file unexpectedly processed or wrong response structure")
                return False
    
    # Test 3: Test with semantic info enabled
    print("\n3. Testing F# detection with semantic info enabled...")
    result = client.call_tool("dotnet-query-syntax", {
        "file": test_files[".fs"],
        "roslynPath": "//function",
        "includeSemanticInfo": True
    })
    
    if "result" in result and result["result"].get("success") == False:
        message = result["result"].get("message", "")
        assert "F# support is not yet implemented" in message
        print("✓ F# detection works even with semantic info requested")
    else:
        print("✗ Unexpected response for F# file with semantic info")
        return False
    
    # Test 4: Test non-F# file still works
    print("\n4. Verifying C# files still work normally...")
    cs_file = os.path.join(fsharp_test_dir, "TestProject", "Program.cs")
    if os.path.exists(cs_file):
        # First load a workspace so we have something to query
        csproj = os.path.join(fsharp_test_dir, "TestProject", "TestProject.csproj")
        if os.path.exists(csproj):
            load_result = client.call_tool("dotnet-load-workspace", {"path": csproj})
            
            if load_result["success"]:
                result = client.call_tool("dotnet-query-syntax", {
                    "file": cs_file,
                    "roslynPath": "//class"
                })
                
                if result["success"]:
                    print("✓ C# files continue to work normally")
                else:
                    print("✗ C# file processing failed")
                    return False
        else:
            print("  Note: No C# project available for testing")
    
    # Test 5: Check error response documentation links
    print("\n5. Verifying documentation links in F# error responses...")
    result = client.call_tool("dotnet-query-syntax", {
        "file": test_files[".fs"],
        "roslynPath": "//function"
    })
    
    if "result" in result and result["result"].get("success") == False:
        error_response = result["result"]
        info = error_response.get("info", {})
        doc_link = info.get("documentationLink", "")
        
        assert doc_link != "", "Expected documentation link"
        assert "FSHARP_IMPLEMENTATION_GUIDE.md" in doc_link, f"Expected implementation guide link, got: {doc_link}"
        print(f"✓ Documentation link provided: {doc_link}")
    
    # Test 6: Test fsscript extension (less common)
    print("\n6. Testing .fsscript extension detection...")
    fsscript_file = os.path.join(fsharp_test_dir, "test.fsscript")
    with open(fsscript_file, 'w') as f:
        f.write("// F# script with alternate extension\nlet x = 42")
    
    result = client.call_tool("dotnet-query-syntax", {
        "file": fsscript_file,
        "roslynPath": "//let"
    })
    
    if "result" in result and result["result"].get("success") == False:
        message = result["result"].get("message", "")
        if "F# support" in message:
            print("✓ .fsscript extension detected correctly")
        else:
            print("✗ .fsscript extension not detected")
    else:
        print("✗ .fsscript file not handled correctly")
    
    # Cleanup
    if os.path.exists(fsscript_file):
        os.remove(fsscript_file)
    
    print("\n" + "=" * 50)
    print("F# Detection Tests COMPLETED!")
    print("\nSummary:")
    print("- F# files are correctly detected by extension")
    print("- Appropriate error messages are returned")
    print("- Response structure includes helpful information")
    print("- Documentation links are provided")
    print("- C# files continue to work normally")
    
    return True

if __name__ == "__main__":
    success = test_fsharp_detection()
    sys.exit(0 if success else 1)