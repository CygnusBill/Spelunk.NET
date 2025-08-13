using System;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using McpRoslyn.Server.RoslynPath2;
using Xunit;

namespace McpRoslyn.Tests.RoslynPath
{
    public class DebugComplexNull2
    {
        [Fact]
        public void TestComplexNullStep()
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
            
            // Step 1: Find if-statements
            var ifStmts = evaluator.Evaluate("//if-statement").ToList();
            Console.WriteLine($"Found {ifStmts.Count} if-statements");
            
            // Step 2: Find if-statements with throw
            var withThrow = evaluator.Evaluate("//if-statement[.//throw-statement]").ToList();
            Console.WriteLine($"Found {withThrow.Count} if-statements with throw");
            
            // Step 3: Find if-statements with == null check
            var withNull = evaluator.Evaluate("//if-statement[.//binary-expression[@operator='==']]").ToList();
            Console.WriteLine($"Found {withNull.Count} if-statements with == operator");
            
            // Step 4: Find if-statements with == null check (right side)
            var withNullRight = evaluator.Evaluate("//if-statement[.//binary-expression[@right-text='null']]").ToList();
            Console.WriteLine($"Found {withNullRight.Count} if-statements with null on right");
            
            // Step 5: Combined == and null
            var combined = evaluator.Evaluate("//if-statement[.//binary-expression[@operator='==' and @right-text='null']]").ToList();
            Console.WriteLine($"Found {combined.Count} if-statements with == null");
            
            Assert.Equal(3, ifStmts.Count);
            Assert.Equal(1, withThrow.Count);
        }
    }
}