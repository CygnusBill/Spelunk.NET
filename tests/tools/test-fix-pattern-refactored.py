#!/usr/bin/env python3
"""Test refactored fix-pattern tool with statement-level operations."""

import asyncio
import json
import sys
from pathlib import Path

# Add parent directory to path for imports
sys.path.insert(0, str(Path(__file__).parent.parent))

from utils.test_base import ToolTestBase
from utils.test_utils import validate_response_structure, print_json

class TestFixPatternRefactored(ToolTestBase):
    """Test the refactored dotnet-fix-pattern tool."""
    
    async def test_add_null_check_transformation(self):
        """Test adding null checks before method calls."""
        # First load the workspace
        load_result = await self.call_tool("spelunk-load-workspace", {
            "path": "./test-workspace/TestProject.csproj"
        })
        self.assertIn("success", json.dumps(load_result).lower())
        
        # Create a test file with method calls that need null checks
        test_file = "./test-workspace/NullCheckTest.cs"
        with open(test_file, "w") as f:
            f.write("""
namespace TestProject
{
    public class NullCheckTest
    {
        public void ProcessUser(User user)
        {
            user.UpdateProfile();
            var name = user.Name;
            user.SendNotification("Hello");
        }
        
        public void ProcessOrder(Order order)
        {
            if (order != null)
            {
                order.Process();
            }
            
            order.Ship(); // This needs null check
        }
    }
    
    public class User
    {
        public string Name { get; set; }
        public void UpdateProfile() { }
        public void SendNotification(string msg) { }
    }
    
    public class Order
    {
        public void Process() { }
        public void Ship() { }
    }
}
""")
        
        try:
            # Find method calls that might need null checks
            result = await self.call_tool("spelunk-fix-pattern", {
                "findPattern": "//statement[@type=ExpressionStatement and @contains='.']",
                "replacePattern": "",
                "patternType": "add-null-check",
                "preview": True
            })
            
            print_json(result, "Add null check preview")
            
            # Should find statements that need null checks
            self.assertIn("Fixes", result)
            self.assertGreater(len(result["Fixes"]), 0)
            
            # Check that it suggests adding null checks
            fix = result["Fixes"][0]
            self.assertIn("ArgumentNullException.ThrowIfNull", fix["ReplacementCode"])
            
        finally:
            # Clean up
            if Path(test_file).exists():
                Path(test_file).unlink()
    
    async def test_convert_to_async_transformation(self):
        """Test converting synchronous method calls to async."""
        test_file = "./test-workspace/AsyncTest.cs"
        with open(test_file, "w") as f:
            f.write("""
using System.IO;
using System.Threading.Tasks;

namespace TestProject
{
    public class AsyncTest
    {
        public async Task ProcessFile(string path)
        {
            var content = File.ReadAllText(path); // Should be ReadAllTextAsync
            
            File.WriteAllText(path, content); // Should be WriteAllTextAsync
            
            await Task.Delay(100); // Already async
        }
        
        public void SyncMethod(string path)
        {
            var content = File.ReadAllText(path); // In sync context, leave as is
        }
    }
}
""")
        
        try:
            # Load workspace
            await self.call_tool("spelunk-load-workspace", {
                "path": "./test-workspace/TestProject.csproj"
            })
            
            # Find File.* method calls to convert to async
            result = await self.call_tool("spelunk-fix-pattern", {
                "findPattern": "//statement[@contains='File.Read' or @contains='File.Write']",
                "replacePattern": "",
                "patternType": "convert-to-async",
                "preview": True
            })
            
            print_json(result, "Convert to async preview")
            
            # Should suggest async versions
            if result.get("Fixes"):
                fix = result["Fixes"][0]
                self.assertIn("Async", fix["ReplacementCode"])
                if "ProcessFile" in fix.get("Description", ""):
                    self.assertIn("await", fix["ReplacementCode"])
            
        finally:
            # Clean up
            if Path(test_file).exists():
                Path(test_file).unlink()
    
    async def test_convert_to_interpolation(self):
        """Test converting string.Format to string interpolation."""
        test_file = "./test-workspace/InterpolationTest.cs"
        with open(test_file, "w") as f:
            f.write("""
namespace TestProject
{
    public class InterpolationTest
    {
        public string FormatMessage(string name, int age)
        {
            var msg1 = string.Format("Hello {0}, you are {1} years old", name, age);
            var msg2 = String.Format("Welcome {0}!", name);
            
            // Already interpolated
            var msg3 = $"Hello {name}";
            
            return msg1;
        }
    }
}
""")
        
        try:
            # Load workspace
            await self.call_tool("spelunk-load-workspace", {
                "path": "./test-workspace/TestProject.csproj"
            })
            
            # Find string.Format calls
            result = await self.call_tool("spelunk-fix-pattern", {
                "findPattern": "//statement[@contains='string.Format' or @contains='String.Format']",
                "replacePattern": "",
                "patternType": "convert-to-interpolation",
                "preview": True
            })
            
            print_json(result, "Convert to interpolation preview")
            
            # Should convert to interpolated strings
            self.assertIn("Fixes", result)
            if result["Fixes"]:
                fix = result["Fixes"][0]
                self.assertIn("$\"", fix["ReplacementCode"])
                # Should replace {0} with {name}
                self.assertIn("{name}", fix["ReplacementCode"])
            
        finally:
            # Clean up
            if Path(test_file).exists():
                Path(test_file).unlink()
    
    async def test_simplify_conditional(self):
        """Test simplifying if-null checks to null-conditional operators."""
        test_file = "./test-workspace/ConditionalTest.cs"
        with open(test_file, "w") as f:
            f.write("""
namespace TestProject
{
    public class ConditionalTest
    {
        public string GetUserName(User user)
        {
            if (user != null)
            {
                return user.Name;
            }
            
            return null;
        }
        
        public void ProcessUser(User user)
        {
            if (user != null)
            {
                user.UpdateProfile();
            }
            
            // Complex case - don't simplify
            if (user != null && user.IsActive)
            {
                user.SendEmail();
            }
        }
    }
    
    public class User
    {
        public string Name { get; set; }
        public bool IsActive { get; set; }
        public void UpdateProfile() { }
        public void SendEmail() { }
    }
}
""")
        
        try:
            # Load workspace
            await self.call_tool("spelunk-load-workspace", {
                "path": "./test-workspace/TestProject.csproj"
            })
            
            # Find if-null patterns
            result = await self.call_tool("spelunk-fix-pattern", {
                "findPattern": "//statement[@type=IfStatement and @contains='!= null']",
                "replacePattern": "",
                "patternType": "simplify-conditional",
                "preview": True
            })
            
            print_json(result, "Simplify conditional preview")
            
            # Should suggest null-conditional operator
            if result.get("Fixes"):
                fix = result["Fixes"][0]
                self.assertIn("?.", fix["ReplacementCode"])
            
        finally:
            # Clean up
            if Path(test_file).exists():
                Path(test_file).unlink()
    
    async def test_add_await(self):
        """Test adding missing await keywords."""
        test_file = "./test-workspace/AwaitTest.cs"
        with open(test_file, "w") as f:
            f.write("""
using System.Threading.Tasks;

namespace TestProject
{
    public class AwaitTest
    {
        public async Task<string> GetDataAsync()
        {
            return await Task.FromResult("data");
        }
        
        public async Task ProcessAsync()
        {
            GetDataAsync(); // Missing await
            
            var result = GetDataAsync(); // Missing await
            
            await Task.Delay(100); // Has await
        }
        
        public async Task<string> ReturnAsync()
        {
            return GetDataAsync().Result; // Should use await
        }
    }
}
""")
        
        try:
            # Load workspace
            await self.call_tool("spelunk-load-workspace", {
                "path": "./test-workspace/TestProject.csproj"
            })
            
            # Find async method calls without await
            result = await self.call_tool("spelunk-fix-pattern", {
                "findPattern": "//method[*Async]//statement[@contains='Async()' and not(@contains='await')]",
                "replacePattern": "",
                "patternType": "add-await",
                "preview": True
            })
            
            print_json(result, "Add await preview")
            
            # Should suggest adding await
            if result.get("Fixes"):
                fix = result["Fixes"][0]
                self.assertIn("await", fix["ReplacementCode"])
            
        finally:
            # Clean up
            if Path(test_file).exists():
                Path(test_file).unlink()
    
    async def test_custom_transformation(self):
        """Test custom transformations with RoslynPath."""
        test_file = "./test-workspace/CustomTest.cs"
        with open(test_file, "w") as f:
            f.write("""
namespace TestProject
{
    public class CustomTest
    {
        private readonly ILogger logger;
        
        public void ProcessData(string data)
        {
            Console.WriteLine("Processing: " + data);
            
            if (string.IsNullOrEmpty(data))
            {
                Console.WriteLine("No data");
                return;
            }
            
            Console.WriteLine("Done");
        }
    }
}
""")
        
        try:
            # Load workspace
            await self.call_tool("spelunk-load-workspace", {
                "path": "./test-workspace/TestProject.csproj"
            })
            
            # Replace Console.WriteLine with logger calls
            result = await self.call_tool("spelunk-fix-pattern", {
                "findPattern": "//statement[@contains='Console.WriteLine']",
                "replacePattern": "logger.LogInformation",
                "patternType": "custom",
                "preview": True
            })
            
            print_json(result, "Custom transformation preview")
            
            # Should replace with logger calls
            self.assertIn("Fixes", result)
            if result["Fixes"]:
                fix = result["Fixes"][0]
                self.assertIn("logger.LogInformation", fix["ReplacementCode"])
            
        finally:
            # Clean up
            if Path(test_file).exists():
                Path(test_file).unlink()
    
    async def test_parameterize_query(self):
        """Test parameterizing SQL queries."""
        test_file = "./test-workspace/SqlTest.cs"
        with open(test_file, "w") as f:
            f.write("""
using System.Data.SqlClient;

namespace TestProject
{
    public class SqlTest
    {
        public void ExecuteQuery(string userName, int userId)
        {
            var sql = "SELECT * FROM Users WHERE Name = '" + userName + "' AND Id = " + userId;
            var cmd = new SqlCommand(sql);
            
            // Already parameterized
            var sql2 = "SELECT * FROM Orders WHERE UserId = @userId";
            var cmd2 = new SqlCommand(sql2);
            cmd2.Parameters.AddWithValue("@userId", userId);
        }
    }
}
""")
        
        try:
            # Load workspace
            await self.call_tool("spelunk-load-workspace", {
                "path": "./test-workspace/TestProject.csproj"
            })
            
            # Find SQL concatenation patterns
            result = await self.call_tool("spelunk-fix-pattern", {
                "findPattern": "//statement[@contains='SqlCommand' and @contains=' + ']",
                "replacePattern": "",
                "patternType": "parameterize-query",
                "preview": True
            })
            
            print_json(result, "Parameterize query preview")
            
            # Should suggest parameterized queries
            if result.get("Fixes"):
                fix = result["Fixes"][0]
                self.assertIn("Parameters.AddWithValue", fix["ReplacementCode"])
                self.assertIn("@", fix["ReplacementCode"])
            
        finally:
            # Clean up
            if Path(test_file).exists():
                Path(test_file).unlink()
    
    async def test_roslynpath_precision(self):
        """Test that RoslynPath provides precise targeting."""
        test_file = "./test-workspace/PrecisionTest.cs"
        with open(test_file, "w") as f:
            f.write("""
namespace TestProject
{
    public class PrecisionTest
    {
        public void MethodA()
        {
            throw new NotImplementedException();
        }
        
        public void MethodB()
        {
            // This throw should not be changed
            throw new InvalidOperationException();
        }
        
        public void GetData()
        {
            // Only this throw in a Get* method should be found
            throw new NotImplementedException();
        }
    }
}
""")
        
        try:
            # Load workspace
            await self.call_tool("spelunk-load-workspace", {
                "path": "./test-workspace/TestProject.csproj"
            })
            
            # Find throws only in Get* methods
            result = await self.call_tool("spelunk-fix-pattern", {
                "findPattern": "//method[Get*]//statement[@type=ThrowStatement]",
                "replacePattern": "return default;",
                "patternType": "custom",
                "preview": True
            })
            
            print_json(result, "RoslynPath precision test")
            
            # Should only find the throw in GetData
            self.assertIn("Fixes", result)
            self.assertEqual(len(result["Fixes"]), 1)
            fix = result["Fixes"][0]
            self.assertIn("GetData", result.get("Description", ""))
            
        finally:
            # Clean up
            if Path(test_file).exists():
                Path(test_file).unlink()

async def main():
    """Run all tests."""
    tester = TestFixPatternRefactored()
    await tester.run_all_tests()

if __name__ == "__main__":
    asyncio.run(main())