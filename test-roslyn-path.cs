using System;
using System.Linq;
using McpRoslyn.Server.RoslynPath;

class RoslynPathDemo
{
    static void Main()
    {
        // Sample C# code to analyze
        var sourceCode = @"
using System;
using System.Threading.Tasks;

namespace MyApp.Services
{
    public class UserService
    {
        private readonly ILogger _logger;
        
        public UserService(ILogger logger)
        {
            _logger = logger;
        }

        public async Task<User> GetUserAsync(int userId)
        {
            // TODO: Add caching
            Console.WriteLine($""Getting user {userId}"");
            
            if (userId <= 0)
            {
                throw new ArgumentException(""Invalid user ID"");
            }
            
            var user = await FetchUserFromDatabase(userId);
            
            if (user == null)
            {
                Console.WriteLine(""User not found"");
                return null;
            }
            
            Console.WriteLine($""Found user: {user.Name}"");
            return user;
        }
        
        public void DeleteUser(int userId)
        {
            if (userId <= 0) return;
            
            Console.WriteLine($""Deleting user {userId}"");
            // Delete logic here
        }
        
        private async Task<User> FetchUserFromDatabase(int id)
        {
            // Simulate database call
            await Task.Delay(100);
            return new User { Id = id, Name = ""John Doe"" };
        }
    }
    
    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}";

        Console.WriteLine("=== RoslynPath Demo ===\n");

        // Test various RoslynPath expressions
        TestPath(sourceCode, "Find all methods", 
            "//method");

        TestPath(sourceCode, "Find async methods", 
            "//method[@async]");

        TestPath(sourceCode, "Find all Console.WriteLine statements", 
            "//statement[@contains='Console.WriteLine']");

        TestPath(sourceCode, "Find return statements in GetUserAsync", 
            "//method[GetUserAsync]//statement[@type=ReturnStatement]");

        TestPath(sourceCode, "Find if statements checking for null", 
            "//statement[@type=IfStatement and @contains='== null']");

        TestPath(sourceCode, "Find methods with 'User' in the name", 
            "//method[*User*]");

        TestPath(sourceCode, "Find public methods", 
            "//method[@public]");

        TestPath(sourceCode, "Find the first statement in each method", 
            "//method/block/statement[1]");

        TestPath(sourceCode, "Find TODO comments", 
            "//comment[@contains='TODO']");

        TestPath(sourceCode, "Find throw statements", 
            "//statement[@type=ThrowStatement]");

        TestPath(sourceCode, "Find methods that throw exceptions", 
            "//method[.//statement[@type=ThrowStatement]]");

        TestPath(sourceCode, "Find async methods without await (anti-pattern)", 
            "//method[@async and not(.//expression[@contains='await'])]");

        TestPath(sourceCode, "Find all properties", 
            "//property");

        TestPath(sourceCode, "Find classes in MyApp.Services namespace", 
            "//namespace[MyApp.Services]/class");

        TestPath(sourceCode, "Find the last return statement in GetUserAsync", 
            "//method[GetUserAsync]//statement[@type=ReturnStatement][last()]");
    }

    static void TestPath(string sourceCode, string description, string path)
    {
        Console.WriteLine($"\n{description}:");
        Console.WriteLine($"Path: {path}");
        Console.WriteLine("Results:");

        try
        {
            var results = RoslynPath.Find(sourceCode, path).ToList();
            
            if (results.Count == 0)
            {
                Console.WriteLine("  (No matches found)");
            }
            else
            {
                foreach (var result in results)
                {
                    Console.WriteLine($"  - {result.NodeType} at line {result.Location.StartLine}");
                    
                    // Show first line of text for context
                    var firstLine = result.Text.Split('\n')[0].Trim();
                    if (firstLine.Length > 60)
                        firstLine = firstLine.Substring(0, 57) + "...";
                    Console.WriteLine($"    {firstLine}");
                    
                    // Show the generated stable path
                    Console.WriteLine($"    Path: {result.Path}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ERROR: {ex.Message}");
        }
    }
}