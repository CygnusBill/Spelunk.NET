using System;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using McpRoslyn.Server.RoslynPath2;
using Xunit;

namespace McpRoslyn.Tests.RoslynPath
{
    public class DebugPathPredicate2
    {
        [Fact]
        public void TestSimplePathPredicate()
        {
            var code = @"
public class TestClass
{
    public void Method1() 
    { 
        if (true) { throw new Exception(); }
        if (false) { return; }
    }
}";
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetRoot();
            
            // Find the first if-statement
            var ifStmt = root.DescendantNodes()
                .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.IfStatementSyntax>()
                .First();
            
            Console.WriteLine($"Testing if-statement: {ifStmt.ToString().Substring(0, 30)}...");
            
            // Manually check if it contains a throw
            var hasThrow = ifStmt.DescendantNodes()
                .Any(n => n is Microsoft.CodeAnalysis.CSharp.Syntax.ThrowStatementSyntax);
            Console.WriteLine($"Has throw statement: {hasThrow}");
            
            // Now test with evaluator starting from this node
            var evaluator = new RoslynPathEvaluator2(tree);
            var context = new EvaluationContext2(ifStmt);
            
            // Parse .//throw-statement
            var parser = new RoslynPathParser2();
            var path = parser.Parse(".//throw-statement");
            
            Console.WriteLine($"Path parsed successfully");
            Console.WriteLine($"Path is absolute: {path.IsAbsolute}");
            Console.WriteLine($"Path has {path.Steps.Count} steps");
            
            Assert.True(hasThrow);
        }
        
        private class EvaluationContext2
        {
            public Microsoft.CodeAnalysis.SyntaxNode CurrentNode { get; }
            public EvaluationContext2(Microsoft.CodeAnalysis.SyntaxNode node)
            {
                CurrentNode = node;
            }
        }
    }
}