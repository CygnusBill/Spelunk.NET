using System;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using McpRoslyn.Server.RoslynPath;

class TestEvaluator
{
    static void Main()
    {
        var code = @"
namespace Test
{
    public class TestClass
    {
        public void foo() { }
        public void bar() { }
        private string foo = ""field"";
    }
}";

        var tree = CSharpSyntaxTree.ParseText(code);
        var evaluator = new RoslynPathEvaluator(tree);
        
        TestQuery(evaluator, "//*[@name='foo']");
        TestQuery(evaluator, "//method[@name='foo']");
        TestQuery(evaluator, "//*");
        TestQuery(evaluator, "//method");
    }
    
    static void TestQuery(RoslynPathEvaluator evaluator, string path)
    {
        Console.WriteLine($"\n=== Testing query: {path} ===");
        
        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var timeout = TimeSpan.FromSeconds(2);
            
            var task = System.Threading.Tasks.Task.Run(() => 
            {
                return evaluator.Evaluate(path).ToList();
            });
            
            if (task.Wait(timeout))
            {
                stopwatch.Stop();
                var results = task.Result;
                Console.WriteLine($"Found {results.Count} matches in {stopwatch.ElapsedMilliseconds}ms");
                foreach (var node in results.Take(5))
                {
                    Console.WriteLine($"  - {node.GetType().Name}: {node.ToString().Replace("\n", " ").Substring(0, Math.Min(50, node.ToString().Length))}...");
                }
            }
            else
            {
                Console.WriteLine($"ERROR: Query timed out after {timeout.TotalSeconds} seconds - likely infinite loop!");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
        }
    }
}