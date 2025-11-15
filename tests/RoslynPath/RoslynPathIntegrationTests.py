#!/usr/bin/env python3
"""
Integration tests for RoslynPath functionality through MCP tools
Tests the complete pipeline from query to results
"""

import json
import sys
import os
import tempfile
import subprocess
import time
from typing import Dict, List, Any

class RoslynPathIntegrationTester:
    def __init__(self):
        self.server_proc = None
        self.test_dir = tempfile.mkdtemp(prefix="roslynpath_test_")
        self.passed = 0
        self.failed = 0
        self.tests = []
        
    def start_server(self):
        """Start the MCP server"""
        env = os.environ.copy()
        env["MCP_DOTNET_ALLOWED_PATHS"] = self.test_dir
        
        self.server_proc = subprocess.Popen(
            ["dotnet", "run", "--project", "src/McpDotnet.Server", "--no-build"],
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            env=env,
            text=True
        )
        
        # Initialize
        self.send_request({
            "jsonrpc": "2.0",
            "id": 0,
            "method": "initialize",
            "params": {"protocolVersion": "2024-11-05"}
        })
        
        response = self.read_response()
        if not response or "error" in response:
            raise Exception(f"Failed to initialize server: {response}")
            
    def stop_server(self):
        """Stop the MCP server"""
        if self.server_proc:
            self.server_proc.terminate()
            self.server_proc.wait()
            
    def send_request(self, request: Dict[str, Any]):
        """Send a request to the server"""
        json_str = json.dumps(request) + "\n"
        self.server_proc.stdin.write(json_str)
        self.server_proc.stdin.flush()
        
    def read_response(self) -> Dict[str, Any]:
        """Read a response from the server"""
        line = self.server_proc.stdout.readline()
        if line:
            return json.loads(line)
        return None
        
    def create_test_file(self, filename: str, content: str) -> str:
        """Create a test file and return its path"""
        filepath = os.path.join(self.test_dir, filename)
        with open(filepath, "w") as f:
            f.write(content)
        return filepath
        
    def query_syntax(self, filepath: str, roslyn_path: str) -> Dict[str, Any]:
        """Execute a RoslynPath query"""
        # Load workspace
        self.send_request({
            "jsonrpc": "2.0",
            "id": 1,
            "method": "tools/call",
            "params": {
                "name": "dotnet-load-workspace",
                "arguments": {"path": filepath}
            }
        })
        
        load_response = self.read_response()
        if not load_response or "error" in load_response:
            return {"error": f"Failed to load workspace: {load_response}"}
            
        # Query syntax
        self.send_request({
            "jsonrpc": "2.0",
            "id": 2,
            "method": "tools/call",
            "params": {
                "name": "dotnet-query-syntax",
                "arguments": {
                    "file": filepath,
                    "roslynPath": roslyn_path
                }
            }
        })
        
        query_response = self.read_response()
        if not query_response:
            return {"error": "No response from query"}
            
        if "error" in query_response:
            return {"error": query_response["error"]}
            
        # Parse the result
        try:
            content = query_response["result"]["content"][0]["text"]
            return json.loads(content)
        except:
            return {"error": f"Failed to parse response: {query_response}"}
            
    def run_test(self, name: str, code: str, path: str, expected_count: int = None, 
                  expected_types: List[str] = None, should_contain: str = None):
        """Run a single test"""
        print(f"\nTest: {name}")
        print(f"  Path: {path}")
        
        # Create test file
        filepath = self.create_test_file(f"test_{len(self.tests)}.cs", code)
        
        # Run query
        result = self.query_syntax(filepath, path)
        
        if "error" in result:
            print(f"  ❌ FAILED: {result['error']}")
            self.failed += 1
            return False
            
        matches = result.get("matches", [])
        
        # Check expected count
        if expected_count is not None:
            if len(matches) != expected_count:
                print(f"  ❌ FAILED: Expected {expected_count} matches, got {len(matches)}")
                self.failed += 1
                return False
                
        # Check expected types
        if expected_types:
            actual_types = [m.get("nodeType", "") for m in matches]
            for expected_type in expected_types:
                if expected_type not in actual_types:
                    print(f"  ❌ FAILED: Expected type '{expected_type}' not found in {actual_types}")
                    self.failed += 1
                    return False
                    
        # Check content
        if should_contain:
            found = False
            for match in matches:
                if should_contain in match.get("preview", ""):
                    found = True
                    break
            if not found:
                print(f"  ❌ FAILED: No match contains '{should_contain}'")
                self.failed += 1
                return False
                
        print(f"  ✅ PASSED: {len(matches)} matches")
        self.passed += 1
        return True
        
    def run_all_tests(self):
        """Run all integration tests"""
        print("=" * 60)
        print("RoslynPath Integration Tests")
        print("=" * 60)
        
        # Test 1: Basic navigation
        self.run_test(
            "Basic child navigation",
            """
namespace TestNS
{
    public class TestClass
    {
        public void TestMethod() { }
    }
}
""",
            "/namespace/class/method",
            expected_count=1
        )
        
        # Test 2: Wildcard with name predicate (the bug we fixed)
        self.run_test(
            "Wildcard with name predicate",
            """
public class TestClass
{
    public void foo() { }
    public void bar() { }
    private string foo = "field";
}
""",
            "//*[@name='foo']",
            expected_count=2
        )
        
        # Test 3: Descendant navigation
        self.run_test(
            "Descendant navigation",
            """
namespace NS
{
    public class Outer
    {
        public class Inner
        {
            public void Method1() { }
            public void Method2() { }
        }
        public void Method3() { }
    }
}
""",
            "//method",
            expected_count=3
        )
        
        # Test 4: Position predicates
        self.run_test(
            "Position predicates",
            """
public class Test
{
    public void Method()
    {
        var a = 1;
        var b = 2;
        var c = 3;
        var d = 4;
    }
}
""",
            "//block/statement[1]",
            expected_count=1,
            should_contain="var a = 1"
        )
        
        # Test 5: Last position
        self.run_test(
            "Last position predicate",
            """
public class Test
{
    public void Method()
    {
        var a = 1;
        var b = 2;
        var c = 3;
        var d = 4;
    }
}
""",
            "//block/statement[last()]",
            expected_count=1,
            should_contain="var d = 4"
        )
        
        # Test 6: Enhanced node types
        self.run_test(
            "Enhanced if-statement type",
            """
public class Test
{
    public void Method()
    {
        if (true) { }
        while (false) { }
        for (int i = 0; i < 10; i++) { }
    }
}
""",
            "//if-statement",
            expected_count=1
        )
        
        # Test 7: Binary expressions with operator
        self.run_test(
            "Binary expression with operator",
            """
public class Test
{
    public void Method()
    {
        var a = x == null;
        var b = y != null;
        var c = 1 + 2;
    }
}
""",
            "//binary-expression[@operator='==']",
            expected_count=1
        )
        
        # Test 8: Contains predicate
        self.run_test(
            "Contains predicate",
            """
public class Test
{
    public void Method()
    {
        Console.WriteLine("Hello");
        System.Console.WriteLine("World");
        Debug.WriteLine("Debug");
    }
}
""",
            "//statement[@contains='Console.WriteLine']",
            expected_count=2
        )
        
        # Test 9: Boolean predicates
        self.run_test(
            "Async method predicate",
            """
public class Test
{
    public async Task Method1() { await Task.Delay(1); }
    public void Method2() { }
    public static async Task Method3() { await Task.Delay(1); }
}
""",
            "//method[@async]",
            expected_count=2
        )
        
        # Test 10: Complex AND predicate
        self.run_test(
            "Complex AND predicate",
            """
public class Test
{
    public async Task Method1() { }
    private async Task Method2() { }
    public void Method3() { }
    public static async Task Method4() { }
}
""",
            "//method[@async and @public]",
            expected_count=2
        )
        
        # Test 11: Complex OR predicate
        self.run_test(
            "Complex OR predicate",
            """
public class Test
{
    public void Method1() { }
    private void Method2() { }
    protected void Method3() { }
}
""",
            "//method[@public or @private]",
            expected_count=2
        )
        
        # Test 12: NOT predicate
        self.run_test(
            "NOT predicate",
            """
public class Test
{
    public void Method1() { }
    private void Method2() { }
    public static void Method3() { }
}
""",
            "//method[not(@static)]",
            expected_count=2
        )
        
        # Test 13: Null check pattern
        self.run_test(
            "Null check pattern",
            """
public class Test
{
    public void Method(string param)
    {
        if (param == null) throw new ArgumentNullException();
        if (null == param) return;
        if (param != null) { DoSomething(); }
    }
}
""",
            "//binary-expression[@operator='==' and @right-text='null']",
            expected_count=1
        )
        
        # Test 14: Method with wildcard name
        self.run_test(
            "Method with wildcard name",
            """
public class Test
{
    public void GetUser() { }
    public void GetUserById() { }
    public void UpdateUser() { }
    public void DeleteUser() { }
}
""",
            "//method[Get*]",
            expected_count=2
        )
        
        # Test 15: Complex nested predicate
        self.run_test(
            "Complex nested predicate",
            """
public class Test
{
    public void Method1()
    {
        if (x == null) 
        {
            throw new ArgumentNullException();
        }
    }
    
    public void Method2()
    {
        if (y == null)
        {
            return;
        }
    }
}
""",
            "//if-statement[.//throw-statement]",
            expected_count=1
        )
        
        # Print summary
        print("\n" + "=" * 60)
        print(f"Tests Passed: {self.passed}")
        print(f"Tests Failed: {self.failed}")
        print("=" * 60)
        
        return self.failed == 0

def main():
    tester = RoslynPathIntegrationTester()
    
    try:
        print("Starting MCP server...")
        tester.start_server()
        time.sleep(1)  # Give server time to start
        
        success = tester.run_all_tests()
        
        sys.exit(0 if success else 1)
        
    except Exception as e:
        print(f"Error: {e}")
        sys.exit(1)
        
    finally:
        tester.stop_server()
        
if __name__ == "__main__":
    main()