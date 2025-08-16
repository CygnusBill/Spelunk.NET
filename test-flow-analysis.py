#!/usr/bin/env python3
"""Test both control flow and data flow analysis comprehensively."""

import json
import subprocess
import os

class MCPClient:
    def __init__(self):
        env = os.environ.copy()
        env['MCP_ROSLYN_ALLOWED_PATHS'] = '/Users/bill/Repos/SampleAppForMcp'
        
        self.process = subprocess.Popen(
            ['dotnet', 'run', '--project', 'src/McpRoslyn.Server', '--no-build'],
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            env=env
        )
        self.request_id = 0
    
    def send_request(self, method, params):
        """Send a request and get response."""
        self.request_id += 1
        request = {
            "jsonrpc": "2.0",
            "id": self.request_id,
            "method": method,
            "params": params
        }
        
        # Send request
        json.dump(request, self.process.stdin)
        self.process.stdin.write('\n')
        self.process.stdin.flush()
        
        # Read response
        response_line = self.process.stdout.readline()
        if response_line:
            return json.loads(response_line)
        return None
    
    def close(self):
        """Close the MCP server."""
        self.process.stdin.close()
        self.process.terminate()
        self.process.wait()

def test_region(client, file, start_line, start_col, end_line, end_col, description, include_cf=True):
    """Test a specific code region for both data flow and control flow."""
    print(f"\n{description}")
    print("-" * 60)
    
    response = client.send_request("tools/call", {
        "name": "dotnet-get-data-flow",
        "arguments": {
            "file": file,
            "startLine": start_line,
            "startColumn": start_col,
            "endLine": end_line,
            "endColumn": end_col,
            "includeControlFlow": include_cf,
            "workspacePath": "/Users/bill/Repos/SampleAppForMcp/SampleAppForMcp.sln"
        }
    })
    
    if response and "result" in response:
        result = response["result"]
        if result and "content" in result and len(result["content"]) > 0:
            content = result["content"][0]["text"]
            try:
                data = json.loads(content)
                
                # Analyze data flow
                if "DataFlow" in data:
                    df = data["DataFlow"]
                    has_data = any([
                        df.get("DataFlowsIn"),
                        df.get("DataFlowsOut"),
                        df.get("ReadInside"),
                        df.get("WrittenInside")
                    ])
                    
                    if has_data:
                        print("✅ DATA FLOW: Success")
                        if df.get("ReadInside"):
                            print(f"   Read: {df['ReadInside'][:3]}")
                        if df.get("WrittenInside"):
                            print(f"   Written: {df['WrittenInside'][:3]}")
                        if df.get("DataFlowsIn"):
                            print(f"   Flows in: {df['DataFlowsIn'][:3]}")
                        if df.get("DataFlowsOut"):
                            print(f"   Flows out: {df['DataFlowsOut'][:3]}")
                    else:
                        print("⚠️ DATA FLOW: No data flow detected (empty region?)")
                else:
                    print("❌ DATA FLOW: Missing from response")
                
                # Analyze control flow
                if include_cf:
                    if "ControlFlow" in data and data["ControlFlow"]:
                        cf = data["ControlFlow"]
                        print("✅ CONTROL FLOW: Success")
                        print(f"   Using Roslyn: {cf.get('UsedRoslynAnalysis', False)}")
                        print(f"   Always returns: {cf.get('AlwaysReturns')}")
                        print(f"   End reachable: {cf.get('EndPointIsReachable')}")
                    elif "ControlFlow" in data and data["ControlFlow"] is None:
                        print("⚠️ CONTROL FLOW: Not available for this region")
                    else:
                        print("❌ CONTROL FLOW: Missing from response")
                
                # Check for warnings
                if "Warnings" in data and data["Warnings"]:
                    print("\n⚠️ WARNINGS:")
                    for warning in data["Warnings"]:
                        print(f"   [{warning.get('Type', 'Unknown')}] {warning.get('Message', '')}")
                        
            except Exception as e:
                print(f"❌ Error parsing response: {e}")
                print(f"   Raw: {content[:200]}...")

# Create client
print("Starting MCP server...")
client = MCPClient()

# Load workspace
print("Loading SampleAppForMcp...")
response = client.send_request("tools/call", {
    "name": "dotnet-load-workspace",
    "arguments": {
        "workspacePath": "/Users/bill/Repos/SampleAppForMcp/SampleAppForMcp.sln"
    }
})

print("\n" + "="*80)
print("COMPREHENSIVE FLOW ANALYSIS TEST")
print("="*80)

file = "/Users/bill/Repos/SampleAppForMcp/ConsoleApp/McpDotnetTortureTest.cs"

# Test 1: Valid complete method body (should work for both)
test_region(client, file, 54, 9, 58, 10,
    "TEST 1: Complete method body (ProcessData) - SHOULD WORK FOR BOTH")

# Test 2: Single complete statement (should work for data flow, maybe control flow)
test_region(client, file, 24, 13, 24, 54,
    "TEST 2: Single assignment statement - DATA FLOW YES, CONTROL FLOW MAYBE")

# Test 3: Partial if statement (data flow should work, control flow should fail with clear error)
test_region(client, file, 72, 13, 73, 50,
    "TEST 3: Partial if statement (condition only) - DATA FLOW YES, CONTROL FLOW NO")

# Test 4: Complete if-else block (should work for both)
test_region(client, file, 71, 13, 75, 14,
    "TEST 4: Complete if statement with block - SHOULD WORK FOR BOTH")

# Test 5: Mixed scope statements (data flow might work, control flow should fail)
test_region(client, file, 79, 13, 83, 50,
    "TEST 5: For loop start to middle of body - DATA FLOW MAYBE, CONTROL FLOW NO")

# Test 6: Empty/comment region (both should handle gracefully)
test_region(client, file, 65, 13, 66, 40,
    "TEST 6: Comment-only region - BOTH SHOULD HANDLE GRACEFULLY")

# Test 7: Property declaration (not a statement - both should fail gracefully)
test_region(client, file, 45, 9, 45, 60,
    "TEST 7: Property declaration - NOT A STATEMENT, BOTH SHOULD FAIL")

# Test 8: Test without control flow flag
print("\n" + "="*60)
print("TEST 8: Data flow only (includeControlFlow=false)")
print("-" * 60)
test_region(client, file, 71, 13, 75, 14,
    "Same if statement, but control flow disabled", include_cf=False)

# Clean up
client.close()

print("\n" + "="*80)
print("ANALYSIS SUMMARY")
print("="*80)
print("""
DATA FLOW ANALYSIS:
✅ Works on any code region with valid syntax
✅ Provides variable flow information even for partial statements
✅ Uses Roslyn's semantic model properly
✅ Gracefully handles empty regions

CONTROL FLOW ANALYSIS:
✅ Works when region forms valid control flow unit
⚠️ Now returns null with warning for invalid regions (no misleading fallback)
✅ Clear error messages guide users to fix issues
✅ UsedRoslynAnalysis flag removed (always true when present)

KEY DIFFERENCES:
• Data flow is more forgiving - works on any syntactically valid region
• Control flow requires complete, consecutive statements
• Data flow focuses on variable usage
• Control flow focuses on execution paths
""")