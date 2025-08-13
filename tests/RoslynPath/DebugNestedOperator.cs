using System;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using McpRoslyn.Server.RoslynPath2;
using Xunit;

namespace McpRoslyn.Tests.RoslynPath
{
    public class DebugNestedOperator
    {
        [Fact]
        public void TestNestedOperatorPath()
        {
            var code = @"
public class TestClass
{
    public void Method1(string param) 
    { 
        if (param == null) throw new ArgumentNullException();
    }
}";
            var tree = CSharpSyntaxTree.ParseText(code);
            var evaluator = new RoslynPathEvaluator2(tree);
            
            // Test finding binary expressions at top level
            var binaries = evaluator.Evaluate("//binary-expression[@operator='==']").ToList();
            Console.WriteLine($"Direct: Found {binaries.Count} binary expressions with ==");
            
            // Test path predicate with simple nested path
            var ifWithBinary = evaluator.Evaluate("//if-statement[.//binary-expression]").ToList();
            Console.WriteLine($"If with any binary: {ifWithBinary.Count}");
            
            // Test path predicate with attribute check
            var ifWithEquals = evaluator.Evaluate("//if-statement[.//binary-expression[@operator='==']]").ToList();
            Console.WriteLine($"If with == binary: {ifWithEquals.Count}");
            
            Assert.Equal(1, binaries.Count);
            Assert.Equal(1, ifWithBinary.Count);
            Assert.Equal(1, ifWithEquals.Count);
        }
    }
}