using System;
using System.Linq;
using Microsoft.CodeAnalysis.VisualBasic;
using McpRoslyn.Server.RoslynPath2;
using Xunit;

namespace McpRoslyn.Tests.RoslynPath
{
    public class DebugVBModifiers
    {
        [Fact]
        public void TestVBModifiersDebug()
        {
            var code = @"
Public Class TestClass
    Public Sub PublicMethod()
    End Sub
    
    Private Sub PrivateMethod()
    End Sub
    
    Protected Overridable Sub VirtualMethod()
    End Sub
    
    Public Shared Sub StaticMethod()
    End Sub
    
    Public MustOverride Sub AbstractMethod()
End Class";
            var tree = VisualBasicSyntaxTree.ParseText(code);
            var evaluator = new RoslynPathEvaluator2(tree);
            
            var allMethods = evaluator.Evaluate("//method").ToList();
            Console.WriteLine($"Total methods: {allMethods.Count}");
            
            var publicMethods = evaluator.Evaluate("//method[@public]").ToList();
            Console.WriteLine($"Public methods: {publicMethods.Count}");
            foreach(var m in publicMethods)
            {
                Console.WriteLine($"  {m.ToString().Split('\n')[0]}");
            }
            
            var virtualMethods = evaluator.Evaluate("//method[@virtual]").ToList();
            Console.WriteLine($"Virtual methods: {virtualMethods.Count}");
            
            var staticMethods = evaluator.Evaluate("//method[@static]").ToList();
            Console.WriteLine($"Static methods: {staticMethods.Count}");
            
            var abstractMethods = evaluator.Evaluate("//method[@abstract]").ToList();
            Console.WriteLine($"Abstract methods: {abstractMethods.Count}");
            
            Assert.Equal(5, allMethods.Count);
        }
    }
}