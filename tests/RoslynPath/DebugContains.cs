using System;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using McpRoslyn.Server.RoslynPath2;
using Xunit;

namespace McpRoslyn.Tests.RoslynPath
{
    public class DebugContains
    {
        [Fact]
        public void DebugContainsTest()
        {
            var code = @"
public class TestClass
{
    public void Method1() 
    { 
        Console.WriteLine(""Hello"");
        System.Console.WriteLine(""World"");
        Debug.WriteLine(""Debug"");
    }
}";
            var tree = CSharpSyntaxTree.ParseText(code);
            var evaluator = new RoslynPathEvaluator2(tree);
            
            var results = evaluator.Evaluate("//statement[@contains='Console.WriteLine']").ToList();
            
            Console.WriteLine($"Found {results.Count} matches:");
            foreach (var node in results)
            {
                var nodeType = node.GetType().Name;
                var text = node.ToString();
                if (text.Length > 100) text = text.Substring(0, 100) + "...";
                Console.WriteLine($"  {nodeType}: {text}");
            }
            
            Assert.Equal(2, results.Count);
        }
    }
}