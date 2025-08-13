using System;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using McpRoslyn.Server.RoslynPath2;
using Xunit;

namespace McpRoslyn.Tests.RoslynPath
{
    public class DebugOperator
    {
        [Fact]
        public void TestOperatorAttribute()
        {
            var code = @"
public class TestClass
{
    public void Method1(string param) 
    { 
        var x = param == null;
    }
}";
            var tree = CSharpSyntaxTree.ParseText(code);
            var evaluator = new RoslynPathEvaluator2(tree);
            
            // Find binary expressions
            var binaries = evaluator.Evaluate("//binary-expression").ToList();
            Console.WriteLine($"Found {binaries.Count} binary expressions:");
            
            foreach (var bin in binaries)
            {
                var text = bin.ToString();
                Console.WriteLine($"  {text}");
                
                // Check what the operator attribute returns
                if (bin is Microsoft.CodeAnalysis.CSharp.Syntax.BinaryExpressionSyntax binaryExpr)
                {
                    var op = binaryExpr.Kind() switch
                    {
                        Microsoft.CodeAnalysis.CSharp.SyntaxKind.EqualsExpression => "==",
                        _ => "?"
                    };
                    Console.WriteLine($"    Operator: {op}");
                    Console.WriteLine($"    Right: {binaryExpr.Right}");
                }
            }
            
            // Test with operator predicate
            var withOp = evaluator.Evaluate("//binary-expression[@operator='==']").ToList();
            Console.WriteLine($"With @operator='==': {withOp.Count} matches");
            
            Assert.Equal(1, binaries.Count);
        }
    }
}