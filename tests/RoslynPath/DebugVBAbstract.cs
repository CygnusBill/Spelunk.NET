using System;
using System.Linq;
using Microsoft.CodeAnalysis.VisualBasic;
using McpRoslyn.Server.RoslynPath2;
using Xunit;

namespace McpRoslyn.Tests.RoslynPath
{
    public class DebugVBAbstract
    {
        [Fact]
        public void TestVBAbstractMethod()
        {
            var code = @"
Public MustInherit Class TestClass
    Public MustOverride Sub AbstractMethod()
End Class";
            var tree = VisualBasicSyntaxTree.ParseText(code);
            var root = tree.GetRoot();
            
            // Find all nodes
            var allNodes = root.DescendantNodes().ToList();
            Console.WriteLine($"Total nodes: {allNodes.Count}");
            
            // Find method-like nodes
            foreach (var node in allNodes)
            {
                var typeName = node.GetType().Name;
                if (typeName.Contains("Method") || typeName.Contains("Sub") || typeName.Contains("Function"))
                {
                    Console.WriteLine($"Found: {typeName}");
                    Console.WriteLine($"  Text: {node.ToString()}");
                }
            }
            
            Assert.NotNull(tree);
        }
    }
}