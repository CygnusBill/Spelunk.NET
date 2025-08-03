using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

var testCode = @"
namespace TestNamespace
{
    public class TestClass
    {
        private int testField;
        
        public TestClass()
        {
            testField = 0;
        }
        
        public void TestMethod()
        {
            var x = 1 + 2;
            if (x > 0)
            {
                Console.WriteLine(""Hello"");
            }
            var y = x * 3;
        }
    }
}";

var tree = CSharpSyntaxTree.ParseText(testCode);
var root = tree.GetRoot();

Console.WriteLine("=== AST Structure for TestMethod ===");

// Find the TestMethod
var methodNode = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
    .First(m => m.Identifier.Text == "TestMethod");
var methodBody = methodNode.Body;

Console.WriteLine($"\nMethod body has {methodBody.Statements.Count} statements:");
int index = 0;
foreach (var statement in methodBody.Statements)
{
    Console.WriteLine($"\nStatement {index}: {statement.GetType().Name}");
    Console.WriteLine($"  Text: {statement.ToString().Replace("\n", " ").Substring(0, Math.Min(50, statement.ToString().Length))}...");
    var lineSpan = statement.GetLocation().GetLineSpan();
    Console.WriteLine($"  Line: {lineSpan.StartLinePosition.Line + 1}, Column: {lineSpan.StartLinePosition.Character + 1}");
    
    // Get parent to check siblings
    if (statement.Parent != null)
    {
        var siblings = statement.Parent.ChildNodes().ToList();
        var myIndex = siblings.IndexOf(statement);
        Console.WriteLine($"  Sibling index: {myIndex} of {siblings.Count}");
        if (myIndex < siblings.Count - 1)
        {
            var nextSibling = siblings[myIndex + 1];
            Console.WriteLine($"  Next sibling: {nextSibling.GetType().Name}");
        }
    }
    index++;
}

// Test positions from the failing tests
Console.WriteLine("\n=== Testing specific positions ===");

// Position 15,13 (should be "var x = 1 + 2;")
var pos1 = tree.GetText().Lines.GetPosition(new LinePosition(14, 12)); // 0-based
var node1 = root.FindNode(new TextSpan(pos1, 0));
Console.WriteLine($"\nAt position (15,13):");
Console.WriteLine($"  Node type: {node1.GetType().Name}");
Console.WriteLine($"  Node text: {node1.ToString().Replace("\n", " ")}");

// Walk up to find the statement
var currentNode = node1;
while (currentNode != null && !(currentNode is StatementSyntax))
{
    currentNode = currentNode.Parent;
}
if (currentNode != null)
{
    Console.WriteLine($"  Containing statement: {currentNode.GetType().Name}");
    Console.WriteLine($"  Statement text: {currentNode.ToString().Replace("\n", " ")}");
}

// Position 20,13 (should be "var y = x * 3;")  
var pos2 = tree.GetText().Lines.GetPosition(new LinePosition(19, 12)); // 0-based
var node2 = root.FindNode(new TextSpan(pos2, 0));
Console.WriteLine($"\nAt position (20,13):");
Console.WriteLine($"  Node type: {node2.GetType().Name}");
Console.WriteLine($"  Node text: {node2.ToString().Replace("\n", " ")}");

// Walk up to find the statement
currentNode = node2;
while (currentNode != null && !(currentNode is StatementSyntax))
{
    currentNode = currentNode.Parent;
}
if (currentNode != null)
{
    Console.WriteLine($"  Containing statement: {currentNode.GetType().Name}");
    Console.WriteLine($"  Statement text: {currentNode.ToString().Replace("\n", " ")}");
}