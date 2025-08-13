using System;
using System.Linq;
using Microsoft.CodeAnalysis.VisualBasic;
using McpRoslyn.Server.RoslynPath2;
using Xunit;

namespace McpRoslyn.Tests.RoslynPath
{
    public class DebugVBBasic
    {
        [Fact]
        public void TestVBBasicDebug()
        {
            var code = @"
Namespace TestNamespace
    Public Class TestClass
        Public Sub TestMethod()
        End Sub
        
        Public Function GetValue() As Integer
            Return 42
        End Function
    End Class
End Namespace";
            var tree = VisualBasicSyntaxTree.ParseText(code);
            var evaluator = new RoslynPathEvaluator2(tree);
            
            var classes = evaluator.Evaluate("//class").ToList();
            Console.WriteLine($"Classes found: {classes.Count}");
            foreach(var c in classes)
            {
                Console.WriteLine($"  {c.GetType().Name}");
            }
            
            var methods = evaluator.Evaluate("//method").ToList();
            Console.WriteLine($"Methods found: {methods.Count}");
            foreach(var m in methods)
            {
                Console.WriteLine($"  {m.GetType().Name}: {m.ToString().Split('\n')[0]}");
            }
            
            Assert.Equal(1, classes.Count);
            Assert.Equal(2, methods.Count);
        }
    }
}