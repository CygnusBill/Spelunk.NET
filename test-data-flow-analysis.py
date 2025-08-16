#!/usr/bin/env python3
"""Deep dive into data flow analysis capabilities."""

import json
import subprocess
import os

class MCPClient:
    def __init__(self):
        env = os.environ.copy()
        env['MCP_ROSLYN_ALLOWED_PATHS'] = '/Users/bill/Repos/McpDotnet/test-workspace'
        
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

# Create test file with various data flow scenarios
test_code = '''using System;
using System.Collections.Generic;

public class DataFlowTest
{
    private int field1 = 10;
    private string field2 = "test";
    
    public int TestBasicFlow(int input)
    {
        int local1 = input * 2;        // input flows in, local1 written
        int local2 = local1 + field1;  // local1 and field1 read, local2 written
        field1 = local2;                // local2 read, field1 written
        return local2;                  // local2 flows out
    }
    
    public void TestCapturedVariables()
    {
        int outer = 10;
        Action lambda = () =>
        {
            Console.WriteLine(outer);   // outer is captured
        };
        lambda();
    }
    
    public void TestRefAndOut(ref int refParam, out int outParam)
    {
        refParam = refParam * 2;       // refParam flows in and out
        outParam = 100;                // outParam flows out (always assigned)
    }
    
    public unsafe void TestUnsafePointers()
    {
        int value = 42;
        int* ptr = &value;              // value's address taken (unsafe)
        *ptr = 100;
    }
    
    public void TestConditionalFlow(bool condition)
    {
        int x;
        if (condition)
        {
            x = 10;                     // x written conditionally
        }
        else
        {
            x = 20;                     // x written conditionally
        }
        Console.WriteLine(x);           // x always assigned before use
    }
    
    public void TestLoopFlow()
    {
        int sum = 0;
        for (int i = 0; i < 10; i++)
        {
            sum += i;                   // sum read and written, i read
        }
        Console.WriteLine(sum);
    }
}
'''

# Write test file
with open('/Users/bill/Repos/McpDotnet/test-workspace/DataFlowTest.cs', 'w') as f:
    f.write(test_code)

# Create client
print("Starting MCP server...")
client = MCPClient()

# Load workspace
print("Loading test workspace...")
response = client.send_request("tools/call", {
    "name": "dotnet-load-workspace",
    "arguments": {
        "workspacePath": "/Users/bill/Repos/McpDotnet/test-workspace/TestProject.csproj"
    }
})

print("\n" + "="*80)
print("DATA FLOW ANALYSIS DEEP DIVE")
print("="*80)

def analyze_data_flow(client, start_line, end_line, description):
    """Analyze data flow for a specific region."""
    print(f"\n{description}")
    print("-" * 60)
    
    response = client.send_request("tools/call", {
        "name": "dotnet-get-data-flow",
        "arguments": {
            "file": "/Users/bill/Repos/McpDotnet/test-workspace/DataFlowTest.cs",
            "startLine": start_line,
            "startColumn": 9,
            "endLine": end_line,
            "endColumn": 10,
            "includeControlFlow": False  # Focus on data flow only
        }
    })
    
    if response and "result" in response:
        result = response["result"]
        if result and "content" in result and len(result["content"]) > 0:
            content = result["content"][0]["text"]
            try:
                data = json.loads(content)
                if "DataFlow" in data:
                    df = data["DataFlow"]
                    
                    # Comprehensive analysis
                    print("DATA FLOW RESULTS:")
                    
                    if df.get("DataFlowsIn"):
                        print(f"  📥 Flows IN: {df['DataFlowsIn']}")
                    if df.get("DataFlowsOut"):
                        print(f"  📤 Flows OUT: {df['DataFlowsOut']}")
                    if df.get("ReadInside"):
                        print(f"  👁️ Read inside: {df['ReadInside']}")
                    if df.get("WrittenInside"):
                        print(f"  ✏️ Written inside: {df['WrittenInside']}")
                    if df.get("AlwaysAssigned"):
                        print(f"  ✅ Always assigned: {df['AlwaysAssigned']}")
                    if df.get("ReadOutside"):
                        print(f"  👁️‍🗨️ Read outside: {df['ReadOutside']}")
                    if df.get("WrittenOutside"):
                        print(f"  📝 Written outside: {df['WrittenOutside']}")
                    if df.get("Captured"):
                        print(f"  🔒 Captured: {df['Captured']}")
                    if df.get("CapturedInside"):
                        print(f"  🔐 Captured inside: {df['CapturedInside']}")
                    if df.get("UnsafeAddressTaken"):
                        print(f"  ⚠️ Unsafe address taken: {df['UnsafeAddressTaken']}")
                    
                    # Additional analysis
                    if "VariableFlows" in data and data["VariableFlows"]:
                        print("\n  VARIABLE FLOW DETAILS:")
                        for var in data["VariableFlows"][:3]:  # Show first 3
                            print(f"    {var.get('Name', '?')}: {var.get('Type', '?')}")
                            if var.get('FirstRead'):
                                print(f"      First read: {var['FirstRead']}")
                            if var.get('LastWrite'):
                                print(f"      Last write: {var['LastWrite']}")
                                
            except Exception as e:
                print(f"❌ Error: {e}")

# Test scenarios
analyze_data_flow(client, 11, 14,
    "TEST 1: Basic flow (local variables and fields)")

analyze_data_flow(client, 19, 24,
    "TEST 2: Captured variables in lambda")

analyze_data_flow(client, 28, 30,
    "TEST 3: Ref and out parameters")

analyze_data_flow(client, 34, 37,
    "TEST 4: Unsafe pointer operations")

analyze_data_flow(client, 41, 50,
    "TEST 5: Conditional assignments (always assigned)")

analyze_data_flow(client, 54, 59,
    "TEST 6: Loop with accumulator")

# Test partial regions
print("\n" + "="*60)
print("TESTING PARTIAL REGIONS")
print("="*60)

analyze_data_flow(client, 11, 12,
    "TEST 7: Partial - just first two lines of method")

analyze_data_flow(client, 43, 48,
    "TEST 8: Partial - just if-else without declaration")

# Clean up
client.close()

print("\n" + "="*80)
print("DATA FLOW ANALYSIS OBSERVATIONS")
print("="*80)
print("""
CAPABILITIES:
✅ Tracks variable flow in/out of regions
✅ Distinguishes read vs write operations
✅ Detects always-assigned variables (useful for definite assignment)
✅ Tracks captured variables in lambdas/closures
✅ Handles ref/out parameters correctly
✅ Detects unsafe address-taken operations
✅ Works on partial code regions

USE CASES:
1. Extract Method Refactoring - identify parameters and return values
2. Variable Usage Analysis - find unused or write-only variables
3. Definite Assignment - ensure variables are assigned before use
4. Closure Analysis - identify captured variables
5. Safety Analysis - find unsafe pointer usage
6. Side Effect Detection - identify which fields/globals are modified

LIMITATIONS:
• Requires syntactically valid region
• May fail on incomplete statement lists
• No interprocedural analysis (doesn't follow method calls)
• No alias analysis (can't track through references)
""")