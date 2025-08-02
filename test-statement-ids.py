#!/usr/bin/env python3
"""
Test script to verify statement ID functionality works end-to-end:
1. Load workspace
2. Find statements (should return statement IDs)
3. Replace statement using statement ID
"""

import os
import sys

# Add test utilities to path
sys.path.append(os.path.join(os.path.dirname(__file__), 'tests', 'utils'))

from test_client import TestClient

def test_statement_id_workflow():
    """Test the complete statement ID workflow"""
    print("Testing Statement ID Workflow")
    print("=" * 50)
    
    # Set environment variable for allowed paths
    workspace_path = "/Users/bill/Repos/McpDotnet/test-workspace"
    os.environ["MCP_ROSLYN_ALLOWED_PATHS"] = "/Users/bill/Repos/McpDotnet"
    
    with TestClient() as client:
        print("1. Loading workspace...")
        load_result = client.call_tool("dotnet-load-workspace", {
            "path": f"{workspace_path}/TestProject.csproj"
        })
        if not load_result["success"]:
            print(f"Failed to load workspace: {load_result['message']}")
            return False
        print("✅ Workspace loaded successfully")
        
        print("\n2. Finding statements with 'Console.WriteLine'...")
        find_result = client.call_tool("dotnet-find-statements", {
            "pattern": "Console.WriteLine"
        })
        if not find_result["success"]:
            print(f"Failed to find statements: {find_result['message']}")
            return False
        
        # Parse the result to find statement IDs
        statements_text = find_result["result"]["content"][0]["text"]
        print("Find statements result:")
        print(statements_text)
        
        # Look for statement IDs in the output
        lines = statements_text.split('\n')
        statement_ids = []
        for line in lines:
            if "Statement ID:" in line:
                # Extract statement ID (format: "Statement ID: stmt-1")
                stmt_id = line.split("Statement ID:")[1].strip()
                statement_ids.append(stmt_id)
        
        if not statement_ids:
            print("❌ No statement IDs found in output")
            return False
        
        print(f"✅ Found {len(statement_ids)} statement IDs: {statement_ids}")
        
        print(f"\n3. Testing replace-statement with statement ID '{statement_ids[0]}'...")
        replace_result = client.call_tool("dotnet-replace-statement", {
            "statementId": statement_ids[0],
            "newStatement": 'Console.WriteLine("Statement ID test successful!");'
        })
        
        if not replace_result["success"]:
            print(f"❌ Replace statement failed: {replace_result['message']}")
            return False
        
        print("✅ Replace statement with statement ID succeeded!")
        
        # Show the result
        result_text = replace_result["result"]["content"][0]["text"]
        print("Replace result:")
        print(result_text)
        
        return True

if __name__ == "__main__":
    success = test_statement_id_workflow()
    if success:
        print("\n🎉 Statement ID workflow test PASSED!")
        sys.exit(0)
    else:
        print("\n💥 Statement ID workflow test FAILED!")
        sys.exit(1)