#!/usr/bin/env python3
"""Simple test for refactored fix-pattern tool."""

import json
import os
import subprocess
import sys
from pathlib import Path

def run_mcp_command(method, params):
    """Run an MCP command and return the result."""
    request = {
        "jsonrpc": "2.0",
        "method": f"tools/{method}",
        "params": params,
        "id": 1
    }
    
    # Run the server with the request
    cmd = ["dotnet", "run", "--project", "../../src/McpRoslyn/McpDotnet.Server", "--no-build"]
    
    # Set environment variable for allowed paths
    env = os.environ.copy()
    env["MCP_DOTNET_ALLOWED_PATHS"] = os.path.abspath(".")
    
    proc = subprocess.Popen(cmd, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, env=env)
    
    # Send request
    proc.stdin.write(json.dumps(request) + "\n")
    proc.stdin.flush()
    
    # Read response
    response_line = proc.stdout.readline()
    proc.terminate()
    
    if response_line:
        response = json.loads(response_line)
        if "result" in response:
            return json.loads(response["result"])
        elif "error" in response:
            raise Exception(f"Error: {response['error']}")
    
    return None

def test_convert_to_interpolation():
    """Test converting string.Format to string interpolation."""
    print("\n=== Testing Convert to Interpolation ===")
    
    # Create test file
    test_file = Path("../../test-workspace/InterpolationTest.cs")
    test_file.write_text("""
namespace TestProject
{
    public class InterpolationTest
    {
        public string FormatMessage(string name, int age)
        {
            var msg1 = string.Format("Hello {0}, you are {1} years old", name, age);
            var msg2 = String.Format("Welcome {0}!", name);
            return msg1;
        }
    }
}
""")
    
    try:
        # Load workspace
        print("Loading workspace...")
        result = run_mcp_command("spelunk-load-workspace", {
            "path": "./test-workspace/TestProject.csproj"
        })
        print(f"Workspace loaded: {result.get('Success', False)}")
        
        # Test fix-pattern
        print("\nRunning fix-pattern...")
        result = run_mcp_command("spelunk-fix-pattern", {
            "findPattern": "//statement[@contains='string.Format' or @contains='String.Format']",
            "replacePattern": "",
            "patternType": "convert-to-interpolation",
            "preview": True
        })
        
        if result and "Fixes" in result:
            print(f"Found {len(result['Fixes'])} fixes:")
            for fix in result["Fixes"]:
                print(f"\n  File: {fix['FilePath']}")
                print(f"  Line: {fix['Line']}")
                print(f"  Original: {fix['OriginalCode']}")
                print(f"  Replacement: {fix['ReplacementCode']}")
                print(f"  Description: {fix.get('Description', 'N/A')}")
        else:
            print("No fixes found or error occurred")
            print(json.dumps(result, indent=2))
            
    finally:
        # Clean up
        if test_file.exists():
            test_file.unlink()

def test_add_null_check():
    """Test adding null checks."""
    print("\n=== Testing Add Null Check ===")
    
    # Create test file
    test_file = Path("../../test-workspace/NullCheckTest.cs")
    test_file.write_text("""
namespace TestProject
{
    public class NullCheckTest
    {
        public void ProcessUser(User user)
        {
            user.UpdateProfile();
            var name = user.Name;
        }
    }
    
    public class User
    {
        public string Name { get; set; }
        public void UpdateProfile() { }
    }
}
""")
    
    try:
        # Test fix-pattern
        print("Running fix-pattern for null checks...")
        result = run_mcp_command("spelunk-fix-pattern", {
            "findPattern": "//statement[@type=ExpressionStatement and @contains='.']",
            "replacePattern": "",
            "patternType": "add-null-check", 
            "preview": True
        })
        
        if result and "Fixes" in result:
            print(f"Found {len(result['Fixes'])} fixes:")
            for fix in result["Fixes"]:
                print(f"\n  Line: {fix['Line']}")
                print(f"  Original: {fix['OriginalCode']}")
                print(f"  Replacement: {fix['ReplacementCode']}")
        else:
            print("Result:", json.dumps(result, indent=2))
            
    finally:
        # Clean up
        if test_file.exists():
            test_file.unlink()

if __name__ == "__main__":
    # Change to script directory
    script_dir = Path(__file__).parent
    import os
    os.chdir(script_dir)
    
    test_convert_to_interpolation()
    test_add_null_check()
    
    print("\n=== Tests Complete ===")