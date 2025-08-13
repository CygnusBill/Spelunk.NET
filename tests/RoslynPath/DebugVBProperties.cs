using System;
using System.Linq;
using Microsoft.CodeAnalysis.VisualBasic;
using McpRoslyn.Server.RoslynPath2;
using Xunit;

namespace McpRoslyn.Tests.RoslynPath
{
    public class DebugVBProperties
    {
        [Fact]
        public void TestVBPropertiesDebug()
        {
            var code = @"
Public Class TestClass
    Private _name As String
    
    Public Property Name As String
        Get
            Return _name
        End Get
        Set(value As String)
            _name = value
        End Set
    End Property
    
    Public ReadOnly Property Count As Integer
        Get
            Return 10
        End Get
    End Property
End Class";
            var tree = VisualBasicSyntaxTree.ParseText(code);
            var root = tree.GetRoot();
            
            // Find all property-like nodes
            foreach (var node in root.DescendantNodes())
            {
                var typeName = node.GetType().Name;
                if (typeName.Contains("Property"))
                {
                    Console.WriteLine($"Found: {typeName}");
                    var parent = node.Parent?.GetType().Name ?? "null";
                    Console.WriteLine($"  Parent: {parent}");
                    Console.WriteLine($"  Text: {node.ToString().Split('\n')[0]}");
                }
            }
            
            var evaluator = new RoslynPathEvaluator2(tree);
            var properties = evaluator.Evaluate("//property").ToList();
            Console.WriteLine($"Properties found by evaluator: {properties.Count}");
            
            Assert.NotNull(tree);
        }
    }
}