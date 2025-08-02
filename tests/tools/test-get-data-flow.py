#!/usr/bin/env python3
"""Test get-data-flow tool functionality."""

import asyncio
import json
import sys
from pathlib import Path

# Add parent directory to path for imports
sys.path.insert(0, str(Path(__file__).parent.parent))

from utils.test_base import ToolTestBase
from utils.test_utils import validate_response_structure, print_json

class TestGetDataFlow(ToolTestBase):
    """Test the dotnet-get-data-flow tool."""
    
    async def test_simple_data_flow(self):
        """Test data flow analysis for a simple code region."""
        # First load the workspace
        load_result = await self.call_tool("dotnet-load-workspace", {
            "path": "./test-workspace/TestProject.csproj"
        })
        self.assertIn("success", json.dumps(load_result).lower())
        
        # Create a test file with simple data flow
        test_file = "./test-workspace/DataFlowTest.cs"
        with open(test_file, "w") as f:
            f.write("""
namespace TestProject
{
    public class DataFlowTest
    {
        public int Calculate(int x, int y)
        {
            int sum = x + y;
            int product = x * y;
            int result = sum + product;
            return result;
        }
    }
}
""")
        
        try:
            # Analyze data flow for the method body
            result = await self.call_tool("dotnet-get-data-flow", {
                "file": test_file,
                "startLine": 8,
                "startColumn": 13,
                "endLine": 11,
                "endColumn": 26,
                "includeControlFlow": True
            })
            
            # Validate structure
            self.assertIn("region", result)
            self.assertIn("dataFlow", result)
            self.assertIn("variableFlows", result)
            self.assertIn("dependencies", result)
            self.assertIn("warnings", result)
            
            # Check data flow
            data_flow = result["dataFlow"]
            self.assertIn("dataFlowsIn", data_flow)
            self.assertIn("readInside", data_flow)
            self.assertIn("writtenInside", data_flow)
            
            # Should detect x and y flow in
            self.assertIn("x", data_flow["dataFlowsIn"])
            self.assertIn("y", data_flow["dataFlowsIn"])
            
            # Should detect variables written
            self.assertIn("sum", data_flow["writtenInside"])
            self.assertIn("product", data_flow["writtenInside"])
            self.assertIn("result", data_flow["writtenInside"])
            
            print_json(result, "Simple data flow analysis")
            
        finally:
            # Clean up
            if Path(test_file).exists():
                Path(test_file).unlink()
                
    async def test_complex_data_flow(self):
        """Test data flow with control structures."""
        test_file = "./test-workspace/ComplexFlowTest.cs"
        with open(test_file, "w") as f:
            f.write("""
namespace TestProject
{
    public class ComplexFlowTest
    {
        public string Process(string input, bool flag)
        {
            string result = null;
            
            if (flag)
            {
                result = input.ToUpper();
            }
            else
            {
                result = input.ToLower();
            }
            
            string output = result + "!";
            return output;
        }
    }
}
""")
        
        try:
            # Load workspace
            await self.call_tool("dotnet-load-workspace", {
                "path": "./test-workspace/TestProject.csproj"
            })
            
            # Analyze the entire method body
            result = await self.call_tool("dotnet-get-data-flow", {
                "file": test_file,
                "startLine": 8,
                "startColumn": 13,
                "endLine": 19,
                "endColumn": 27,
                "includeControlFlow": True
            })
            
            # Check control flow
            self.assertIn("controlFlow", result)
            control_flow = result["controlFlow"]
            self.assertIn("alwaysReturns", control_flow)
            self.assertIn("returnStatements", control_flow)
            self.assertEqual(control_flow["returnStatements"], 1)
            
            # Check dependencies
            dependencies = result["dependencies"]
            self.assertIsInstance(dependencies, list)
            
            # Find dependency for output variable
            output_dep = next((d for d in dependencies if d["variable"] == "output"), None)
            if output_dep:
                self.assertIn("result", output_dep["dependsOn"])
                
            print_json(result, "Complex data flow with control structures")
            
        finally:
            # Clean up
            if Path(test_file).exists():
                Path(test_file).unlink()
                
    async def test_data_flow_warnings(self):
        """Test data flow warnings for unused and uninitialized variables."""
        test_file = "./test-workspace/WarningsTest.cs"
        with open(test_file, "w") as f:
            f.write("""
namespace TestProject
{
    public class WarningsTest
    {
        public void TestWarnings()
        {
            int unused = 42;  // Unused variable
            int x;
            int y = x + 1;    // Using uninitialized variable
        }
    }
}
""")
        
        try:
            # Load workspace
            await self.call_tool("dotnet-load-workspace", {
                "path": "./test-workspace/TestProject.csproj"
            })
            
            # Analyze method body
            result = await self.call_tool("dotnet-get-data-flow", {
                "file": test_file,
                "startLine": 8,
                "startColumn": 13,
                "endLine": 10,
                "endColumn": 39
            })
            
            # Check warnings
            warnings = result.get("warnings", [])
            self.assertGreater(len(warnings), 0)
            
            # Should have unused variable warning
            unused_warning = next((w for w in warnings if w["type"] == "UnusedVariable"), None)
            if unused_warning:
                self.assertIn("unused", unused_warning["message"].lower())
                
            print_json(warnings, "Data flow warnings")
            
        finally:
            # Clean up
            if Path(test_file).exists():
                Path(test_file).unlink()
                
    async def test_variable_flow_tracking(self):
        """Test detailed variable flow tracking."""
        test_file = "./test-workspace/VariableFlowTest.cs"
        with open(test_file, "w") as f:
            f.write("""
namespace TestProject
{
    public class VariableFlowTest
    {
        public int Transform(int input)
        {
            int temp = input * 2;
            temp = temp + 10;
            int result = temp / 2;
            return result;
        }
    }
}
""")
        
        try:
            # Load workspace
            await self.call_tool("dotnet-load-workspace", {
                "path": "./test-workspace/TestProject.csproj"
            })
            
            # Analyze method body
            result = await self.call_tool("dotnet-get-data-flow", {
                "file": test_file,
                "startLine": 8,
                "startColumn": 13,
                "endLine": 11,
                "endColumn": 26
            })
            
            # Check variable flows
            var_flows = result.get("variableFlows", [])
            self.assertGreater(len(var_flows), 0)
            
            # Find flow for temp variable
            temp_flow = next((v for v in var_flows if v["name"] == "temp"), None)
            if temp_flow:
                self.assertIn("readLocations", temp_flow)
                self.assertIn("writeLocations", temp_flow)
                self.assertGreater(len(temp_flow["writeLocations"]), 1)  # Written twice
                
                print_json(temp_flow, "Variable flow for 'temp'")
                
        finally:
            # Clean up
            if Path(test_file).exists():
                Path(test_file).unlink()
                
    async def test_captured_variables(self):
        """Test detection of captured variables in closures."""
        test_file = "./test-workspace/CaptureTest.cs"
        with open(test_file, "w") as f:
            f.write("""
using System;
using System.Linq;

namespace TestProject
{
    public class CaptureTest
    {
        public void TestCapture()
        {
            int localVar = 10;
            var numbers = new[] { 1, 2, 3, 4, 5 };
            
            var filtered = numbers.Where(n => n > localVar / 2);
            
            Console.WriteLine(string.Join(",", filtered));
        }
    }
}
""")
        
        try:
            # Load workspace
            await self.call_tool("dotnet-load-workspace", {
                "path": "./test-workspace/TestProject.csproj"
            })
            
            # Analyze the lambda region
            result = await self.call_tool("dotnet-get-data-flow", {
                "file": test_file,
                "startLine": 11,
                "startColumn": 13,
                "endLine": 16,
                "endColumn": 59
            })
            
            # Check for captured variables
            data_flow = result.get("dataFlow", {})
            captured = data_flow.get("captured", [])
            
            # localVar should be captured
            if captured:
                self.assertIn("localVar", captured)
                
            print_json(data_flow, "Data flow with captured variables")
            
        finally:
            # Clean up
            if Path(test_file).exists():
                Path(test_file).unlink()

async def main():
    """Run all tests."""
    tester = TestGetDataFlow()
    await tester.run_all_tests()

if __name__ == "__main__":
    asyncio.run(main())