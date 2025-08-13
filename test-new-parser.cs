using System;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using McpRoslyn.Server.RoslynPath2;

class TestNewParser
{
    static void Main()
    {
        var testCode = @"
namespace TestNS
{
    public class TestClass
    {
        public void GetUser() { }
        public void GetUserById() { }
        public void UpdateUser() { }
        private string foo = ""field"";
        
        public async Task ProcessAsync()
        {
            if (param == null) throw new ArgumentNullException();
            await Task.Delay(100);
        }
    }
}";

        var tree = CSharpSyntaxTree.ParseText(testCode);
        var evaluator = new RoslynPathEvaluator2(tree);

        // Test cases that were failing
        TestPattern(evaluator, "//*[@name='foo']", "Wildcard with name attribute");
        TestPattern(evaluator, "//method[Get*]", "Method with wildcard prefix");
        TestPattern(evaluator, "//method[*User]", "Method with wildcard suffix"); 
        TestPattern(evaluator, "//method[@async and @public]", "AND predicate");
        TestPattern(evaluator, "//method[@public or @private]", "OR predicate");
        TestPattern(evaluator, "//method[not(@private)]", "NOT predicate");
        TestPattern(evaluator, "//if-statement[.//throw-statement]", "Nested path predicate");
        TestPattern(evaluator, "//binary-expression[@operator='==']", "Binary expression with operator");
        TestPattern(evaluator, "//statement[@contains='Task.Delay']", "Contains predicate");
        TestPattern(evaluator, "//method[@modifiers~='public']", "Modifiers contains");
    }

    static void TestPattern(RoslynPathEvaluator2 evaluator, string pattern, string description)
    {
        try
        {
            var results = evaluator.Evaluate(pattern).ToList();
            Console.WriteLine($"✓ {description}: {results.Count} matches");
            foreach (var result in results.Take(2))
            {
                var preview = result.ToString().Replace("\n", " ").Replace("\r", "");
                if (preview.Length > 60) preview = preview.Substring(0, 60) + "...";
                Console.WriteLine($"  - {preview}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ {description}: {ex.Message}");
        }
    }
}