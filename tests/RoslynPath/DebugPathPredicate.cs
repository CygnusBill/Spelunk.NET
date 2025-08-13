using System;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using McpRoslyn.Server.RoslynPath2;
using Xunit;

namespace McpRoslyn.Tests.RoslynPath
{
    public class DebugPathPredicate
    {
        [Fact]
        public void TestPathPredicateTest()
        {
            var code = @"
public class TestClass
{
    public void Method1(string param) 
    { 
        if (param == null) throw new ArgumentNullException();
        if (null == param) return;
        if (param != null) { DoSomething(); }
    }
}";
            var tree = CSharpSyntaxTree.ParseText(code);
            var evaluator = new RoslynPathEvaluator2(tree);
            
            // Test nested path predicate
            var results = evaluator.Evaluate("//if-statement[.//throw-statement]").ToList();
            Console.WriteLine($"If statements with throw: {results.Count}");
            
            foreach (var node in results)
            {
                var text = node.ToString();
                if (text.Length > 80) text = text.Substring(0, 80) + "...";
                Console.WriteLine($"  {text}");
            }
            
            Assert.Equal(1, results.Count);
        }
    }
}