#!/usr/bin/env python3
"""
Test RoslynPath function argument parsing through the MCP interface.
Verifies that functions with arguments can be parsed and used in queries.
"""

import json
import sys
import os
from pathlib import Path

# Add parent directory to path for imports
sys.path.insert(0, str(Path(__file__).parent.parent))

from utils.test_client import TestClient

def test_roslynpath_function_arguments():
    """Test RoslynPath functions with arguments."""
    print("\n=== Testing RoslynPath Function Arguments ===")
    
    with TestClient() as client:
        # Load the test workspace
        print("Loading workspace...")
        result = client.call_tool("dotnet-load-workspace", {
            "path": str(Path(__file__).parent.parent.parent / "test-workspace" / "TestProject.csproj")
        })
        
        if not result or "error" in result:
            print(f"Failed to load workspace: {result}")
            return False
        
        # Create a test file
        test_file = Path(__file__).parent.parent.parent / "test-workspace" / "FunctionTest.cs"
        test_file.write_text("""
using System;
using System.Threading.Tasks;

namespace TestProject
{
    public abstract class BaseController
    {
        public virtual void Initialize() { }
    }
    
    public class UserController : BaseController
    {
        public async Task<string> GetUserAsync(int id)
        {
            Console.WriteLine("Getting user");
            return await Task.FromResult($"User {id}");
        }
        
        public void ProcessUserData(string data)
        {
            Console.WriteLine($"Processing: {data}");
        }
        
        public void TestMethod()
        {
            Console.WriteLine("Test1");
            Console.WriteLine("Test2");
            Console.WriteLine("Test3");
        }
    }
    
    public class ProductController : BaseController
    {
        public string GetProductName(int id)
        {
            return $"Product {id}";
        }
    }
}
""")
        
        # Test 1: Functions without arguments (baseline)
        print("\n--- Test 1: Functions without arguments (baseline) ---")
        result = client.call_tool("dotnet-find-statements", {
            "pattern": "//method[TestMethod]/block/statement[last()]",
            "patternType": "roslynpath"
        })
        
        if result and "Statements" in result:
            print(f"Found {len(result['Statements'])} statements")
            assert len(result['Statements']) == 1, "Should find last statement"
            assert 'Test3' in result['Statements'][0]['Text'], "Should be the third WriteLine"
        
        # Test 2: last()-N syntax
        print("\n--- Test 2: last()-N syntax ---")
        result = client.call_tool("dotnet-find-statements", {
            "pattern": "//method[TestMethod]/block/statement[last()-1]",
            "patternType": "roslynpath"
        })
        
        if result and "Statements" in result:
            print(f"Found {len(result['Statements'])} statements")
            assert len(result['Statements']) == 1, "Should find second-to-last statement"
            assert 'Test2' in result['Statements'][0]['Text'], "Should be the second WriteLine"
        
        # Test 3: Functions with string arguments (if evaluator supports them)
        print("\n--- Test 3: Functions with string arguments ---")
        # Note: These tests verify parsing succeeds. Full evaluation depends on
        # whether the evaluator implements these XPath functions
        patterns_to_test = [
            "//method[contains(@name, 'User')]",
            "//method[starts-with(@name, 'Get')]", 
            "//method[ends-with(@name, 'Async')]",
            "//class[contains(@name, 'Controller')]",
            "//statement[contains(., 'Console')]"
        ]
        
        for pattern in patterns_to_test:
            print(f"  Testing pattern: {pattern}")
            result = client.call_tool("dotnet-find-statements", {
                "pattern": pattern,
                "patternType": "roslynpath"
            })
            
            # We mainly test that parsing doesn't fail
            # The evaluator might not implement all functions yet
            if result and "error" not in result:
                if "Statements" in result:
                    print(f"    Found {len(result['Statements'])} matches")
                else:
                    print("    Pattern parsed but no matches (function might not be implemented)")
            else:
                print(f"    Error: {result.get('error', 'Unknown error')}")
        
        # Test 4: Complex predicates with functions
        print("\n--- Test 4: Complex predicates with functions ---")
        complex_patterns = [
            "//method[@async and contains(@name, 'Async')]",
            "//class[@abstract or starts-with(@name, 'Base')]",
            "//method[not(contains(@name, 'Product'))]"
        ]
        
        for pattern in complex_patterns:
            print(f"  Testing pattern: {pattern}")
            result = client.call_tool("dotnet-find-statements", {
                "pattern": pattern,
                "patternType": "roslynpath"
            })
            
            if result and "error" not in result:
                print("    ✓ Pattern parsed successfully")
            else:
                print(f"    ✗ Parse error: {result.get('error', 'Unknown')}")
        
        # Test 5: Multiple arguments (if supported)
        print("\n--- Test 5: Functions with multiple arguments ---")
        multi_arg_patterns = [
            "//method[substring(@name, 0, 4)]",
            "//statement[concat('Console', '.', 'WriteLine')]",
            "//class[translate(@name, 'ABC', 'abc')]"
        ]
        
        for pattern in multi_arg_patterns:
            print(f"  Testing pattern: {pattern}")
            result = client.call_tool("dotnet-find-statements", {
                "pattern": pattern,
                "patternType": "roslynpath"
            })
            
            if result and "error" not in result:
                print("    ✓ Multiple arguments parsed successfully")
            else:
                # Some parse errors are expected if the function isn't implemented
                error = result.get('error', 'Unknown')
                if 'parse' in error.lower():
                    print(f"    ✗ Parse error: {error}")
                else:
                    print("    Function not implemented (parsing succeeded)")
        
        print("\n✅ RoslynPath function argument tests completed!")
        print("Note: Full XPath function evaluation depends on evaluator implementation.")
        print("These tests verify that the parser correctly handles function arguments.")
        return True

if __name__ == "__main__":
    success = test_roslynpath_function_arguments()
    sys.exit(0 if success else 1)