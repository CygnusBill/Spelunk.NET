using System;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using McpRoslyn.Server.RoslynPath2;
using Xunit;

namespace McpRoslyn.Tests.RoslynPath
{
    public class DebugWildcards
    {
        [Fact]
        public void DebugWildcardTest()
        {
            var code = @"
public class TestClass
{
    public void GetUser() { }
    public void GetUserById() { }
    public void SetUser() { }
    public void DeleteUser() { }
}";
            var tree = CSharpSyntaxTree.ParseText(code);
            var evaluator = new RoslynPathEvaluator2(tree);
            
            // Test //method[Get*]
            var results1 = evaluator.Evaluate("//method[Get*]").ToList();
            Console.WriteLine($"//method[Get*] found {results1.Count} matches:");
            foreach (var node in results1)
            {
                var name = GetMethodName(node);
                Console.WriteLine($"  {name}");
            }
            
            // Test //method[*User]
            var results2 = evaluator.Evaluate("//method[*User]").ToList();
            Console.WriteLine($"//method[*User] found {results2.Count} matches:");
            foreach (var node in results2)
            {
                var name = GetMethodName(node);
                Console.WriteLine($"  {name}");
            }
            
            Assert.Equal(2, results1.Count); // GetUser, GetUserById
            Assert.Equal(2, results2.Count); // GetUser, DeleteUser
        }
        
        private string GetMethodName(Microsoft.CodeAnalysis.SyntaxNode node)
        {
            if (node is Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax method)
                return method.Identifier.Text;
            return "?";
        }
    }
}