#!/usr/bin/env python3
"""Test get-statement-context tool functionality."""

import asyncio
import json
import sys
from pathlib import Path

# Add parent directory to path for imports
sys.path.insert(0, str(Path(__file__).parent.parent))

from utils.test_base import ToolTestBase
from utils.test_utils import validate_response_structure, print_json

class TestGetStatementContext(ToolTestBase):
    """Test the dotnet-get-statement-context tool."""
    
    async def test_get_context_by_location(self):
        """Test getting statement context by file location."""
        # First load the workspace
        load_result = await self.call_tool("dotnet-load-workspace", {
            "path": "./test-workspace/TestProject.csproj"
        })
        self.assertIn("success", json.dumps(load_result).lower())
        
        # Get context for a specific statement
        result = await self.call_tool("dotnet-get-statement-context", {
            "file": "./test-workspace/Program.cs",
            "line": 15,  # Adjust based on actual test file
            "column": 9
        })
        
        # Validate structure
        self.assertIn("statement", result)
        self.assertIn("semanticInfo", result)
        self.assertIn("context", result)
        self.assertIn("diagnostics", result)
        self.assertIn("suggestions", result)
        
        # Check statement info
        statement = result["statement"]
        self.assertIn("id", statement)
        self.assertIn("code", statement)
        self.assertIn("kind", statement)
        self.assertIn("location", statement)
        
        # Check semantic info
        semantic = result["semanticInfo"]
        self.assertIn("symbols", semantic)
        self.assertIn("dataFlow", semantic)
        
        # Check context info
        context = result["context"]
        self.assertIn("availableSymbols", context)
        self.assertIn("usings", context)
        
        print_json(result, "Statement context result")
        
    async def test_get_context_by_statement_id(self):
        """Test getting statement context using statement ID."""
        # First load the workspace
        load_result = await self.call_tool("dotnet-load-workspace", {
            "path": "./test-workspace/TestProject.csproj"
        })
        self.assertIn("success", json.dumps(load_result).lower())
        
        # Find some statements first
        find_result = await self.call_tool("dotnet-find-statements", {
            "pattern": "Console.WriteLine",
            "patternType": "text"
        })
        
        self.assertIn("statements", find_result)
        self.assertGreater(len(find_result["statements"]), 0)
        
        # Get the first statement ID
        statement_id = find_result["statements"][0]["statementId"]
        
        # Get context using statement ID
        result = await self.call_tool("dotnet-get-statement-context", {
            "statementId": statement_id
        })
        
        # Validate we got context for the right statement
        self.assertIn("statement", result)
        self.assertEqual(result["statement"]["id"], statement_id)
        
        print_json(result, "Statement context by ID")
        
    async def test_semantic_info_details(self):
        """Test detailed semantic information in context."""
        # Load workspace
        await self.call_tool("dotnet-load-workspace", {
            "path": "./test-workspace/TestProject.csproj"
        })
        
        # Find a method call statement
        find_result = await self.call_tool("dotnet-find-statements", {
            "pattern": "GetUser",
            "patternType": "text"
        })
        
        if find_result.get("statements"):
            stmt = find_result["statements"][0]
            
            # Get context
            result = await self.call_tool("dotnet-get-statement-context", {
                "file": stmt["location"]["file"],
                "line": stmt["location"]["line"],
                "column": stmt["location"]["column"]
            })
            
            # Check for method symbol info
            symbols = result["semanticInfo"]["symbols"]
            method_symbols = [s for s in symbols if s["kind"] == "Method"]
            
            if method_symbols:
                method = method_symbols[0]
                self.assertIn("returnType", method)
                self.assertIn("parameters", method)
                
                print_json(method, "Method symbol info")
                
    async def test_data_flow_info(self):
        """Test data flow information in context."""
        # Load workspace
        await self.call_tool("dotnet-load-workspace", {
            "path": "./test-workspace/TestProject.csproj"
        })
        
        # Find a variable declaration
        find_result = await self.call_tool("dotnet-find-statements", {
            "pattern": "var ",
            "patternType": "text"
        })
        
        if find_result.get("statements"):
            stmt = find_result["statements"][0]
            
            # Get context
            result = await self.call_tool("dotnet-get-statement-context", {
                "file": stmt["location"]["file"],
                "line": stmt["location"]["line"],
                "column": stmt["location"]["column"]
            })
            
            # Check data flow
            data_flow = result["semanticInfo"]["dataFlow"]
            self.assertIn("variablesRead", data_flow)
            self.assertIn("variablesDeclared", data_flow)
            self.assertIn("variablesWritten", data_flow)
            
            print_json(data_flow, "Data flow info")
            
    async def test_diagnostics_in_context(self):
        """Test diagnostics information in context."""
        # Create a file with an error
        test_file = "./test-workspace/TestError.cs"
        with open(test_file, "w") as f:
            f.write("""
namespace TestProject
{
    public class TestError
    {
        public void Method()
        {
            int x = "not a number";  // Type error
            Console.WriteLine(y);    // Undefined variable
        }
    }
}
""")
        
        try:
            # Load workspace
            await self.call_tool("dotnet-load-workspace", {
                "path": "./test-workspace/TestProject.csproj"
            })
            
            # Get context for error statement
            result = await self.call_tool("dotnet-get-statement-context", {
                "file": test_file,
                "line": 8,
                "column": 13
            })
            
            # Check diagnostics
            diagnostics = result.get("diagnostics", [])
            self.assertGreater(len(diagnostics), 0)
            
            for diag in diagnostics:
                self.assertIn("id", diag)
                self.assertIn("severity", diag)
                self.assertIn("message", diag)
                
            print_json(diagnostics, "Diagnostics")
            
        finally:
            # Clean up
            if Path(test_file).exists():
                Path(test_file).unlink()

async def main():
    """Run all tests."""
    tester = TestGetStatementContext()
    await tester.run_all_tests()

if __name__ == "__main__":
    asyncio.run(main())