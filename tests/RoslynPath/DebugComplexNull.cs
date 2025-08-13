using System;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using McpRoslyn.Server.RoslynPath2;
using Xunit;

namespace McpRoslyn.Tests.RoslynPath
{
    public class DebugComplexNull
    {
        [Fact]
        public void TestComplexNullTest()
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
            
            // Debug the complex path
            var path = "//if-statement[.//binary-expression[@operator='==' and @right-text='null']][.//throw-statement]";
            Console.WriteLine($"Testing path: {path}");
            
            var results = evaluator.Evaluate(path).ToList();
            Console.WriteLine($"Found {results.Count} matches:");
            
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