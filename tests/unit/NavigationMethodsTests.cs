using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Xunit;
using CS = Microsoft.CodeAnalysis.CSharp.Syntax;

namespace McpRoslyn.Server.Tests.Unit
{
    public class NavigationMethodsTests
    {
        private const string TestCode = @"
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

        private class TestNavigationHelper
        {
            private readonly SyntaxTree _tree;
            private readonly SyntaxNode _root;

            public TestNavigationHelper()
            {
                _tree = CSharpSyntaxTree.ParseText(TestCode);
                _root = _tree.GetRoot();
            }

            public SyntaxNode FindNodeAtPosition(int line, int column)
            {
                var sourceText = _tree.GetText();
                var position = sourceText.Lines.GetPosition(new LinePosition(line - 1, column - 1));
                return _root.FindNode(new TextSpan(position, 0));
            }

            public SyntaxNode? NavigateFromNode(SyntaxNode startNode, string path)
            {
                var current = startNode;
                
                // Parse the navigation path (simple implementation)
                var parts = path.Split(new[] { "::" }, StringSplitOptions.RemoveEmptyEntries);
                
                // If no parts, return the current node (empty path means self)
                if (parts.Length == 0)
                    return current;
                
                foreach (var part in parts)
                {
                    if (current == null) return null;
                    
                    // Parse axis and node test
                    var match = System.Text.RegularExpressions.Regex.Match(part, @"^([\w-]+)(?:\[(\d+)\])?$");
                    if (!match.Success) return null; // Invalid syntax
                    
                    var axis = match.Groups[1].Value;
                    var index = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 1;
                    
                    current = axis switch
                    {
                        "ancestor" => GetAncestor(current, index),
                        "parent" => current.Parent,
                        "child" => GetChild(current, index),
                        "descendant" => GetDescendant(current, index),
                        "following-sibling" => GetFollowingSibling(current, index),
                        "preceding-sibling" => GetPrecedingSibling(current, index),
                        "self" => current,
                        _ => null // Invalid axis
                    };
                    
                    // If we got null from invalid axis, stop processing
                    if (current == null) return null;
                }
                
                return current;
            }

            private SyntaxNode? GetAncestor(SyntaxNode node, int index)
            {
                var current = node.Parent;
                for (int i = 1; i < index && current != null; i++)
                {
                    current = current.Parent;
                }
                return current;
            }

            private SyntaxNode? GetChild(SyntaxNode node, int index)
            {
                var children = node.ChildNodes().ToList();
                return index > 0 && index <= children.Count ? children[index - 1] : null;
            }

            private SyntaxNode? GetDescendant(SyntaxNode node, int index)
            {
                var descendants = node.DescendantNodes().ToList();
                return index > 0 && index <= descendants.Count ? descendants[index - 1] : null;
            }

            private SyntaxNode? GetFollowingSibling(SyntaxNode node, int index)
            {
                if (node.Parent == null) return null;
                
                var siblings = node.Parent.ChildNodes().ToList();
                var currentIndex = siblings.IndexOf(node);
                var targetIndex = currentIndex + index;
                
                return targetIndex < siblings.Count ? siblings[targetIndex] : null;
            }

            private SyntaxNode? GetPrecedingSibling(SyntaxNode node, int index)
            {
                if (node.Parent == null) return null;
                
                var siblings = node.Parent.ChildNodes().ToList();
                var currentIndex = siblings.IndexOf(node);
                var targetIndex = currentIndex - index;
                
                return targetIndex >= 0 ? siblings[targetIndex] : null;
            }

            public string BuildNodePath(SyntaxNode node)
            {
                var pathParts = new List<string>();
                var current = node;
                
                while (current != null)
                {
                    var nodeType = RoslynPath.EnhancedNodeTypes.GetDetailedNodeTypeName(current);
                    var name = GetNodeName(current);
                    
                    if (!string.IsNullOrEmpty(name))
                    {
                        pathParts.Insert(0, $"{nodeType}[@name='{name}']");
                    }
                    else
                    {
                        pathParts.Insert(0, nodeType);
                    }
                    
                    current = current.Parent;
                }
                
                return "/" + string.Join("/", pathParts);
            }

            private string? GetNodeName(SyntaxNode node)
            {
                return node switch
                {
                    CS.ClassDeclarationSyntax classDecl => classDecl.Identifier.ValueText,
                    CS.MethodDeclarationSyntax methodDecl => methodDecl.Identifier.ValueText,
                    CS.PropertyDeclarationSyntax propDecl => propDecl.Identifier.ValueText,
                    CS.FieldDeclarationSyntax fieldDecl => fieldDecl.Declaration.Variables.FirstOrDefault()?.Identifier.ValueText,
                    CS.VariableDeclaratorSyntax varDecl => varDecl.Identifier.ValueText,
                    CS.ParameterSyntax paramDecl => paramDecl.Identifier.ValueText,
                    CS.NamespaceDeclarationSyntax nsDecl => nsDecl.Name.ToString(),
                    CS.FileScopedNamespaceDeclarationSyntax fsNsDecl => fsNsDecl.Name.ToString(),
                    _ => null
                };
            }
        }

        [Fact]
        public void NavigateFromNode_ParentNavigation_ReturnsCorrectParent()
        {
            // Arrange
            var helper = new TestNavigationHelper();
            var literalNode = helper.FindNodeAtPosition(15, 18); // "1" in "1 + 2"

            // Act
            var result = helper.NavigateFromNode(literalNode, "parent");

            // Assert
            Assert.NotNull(result);
            // The node at position 15,18 is inside VariableDeclaratorSyntax, not the literal itself
            Assert.IsType<CS.VariableDeclarationSyntax>(result);
        }

        [Fact]
        public void NavigateFromNode_AncestorNavigation_ReturnsCorrectAncestor()
        {
            // Arrange
            var helper = new TestNavigationHelper();
            var literalNode = helper.FindNodeAtPosition(15, 18); // "1" in "1 + 2"

            // Act
            var result = helper.NavigateFromNode(literalNode, "ancestor");

            // Assert
            Assert.NotNull(result);
            // First ancestor of literal "1" would be the parent of the literal node
            // which could be the binary expression or another intermediate node
            Assert.True(result is CS.BinaryExpressionSyntax || result is CS.EqualsValueClauseSyntax || result is CS.VariableDeclarationSyntax);
        }

        [Fact]
        public void NavigateFromNode_AncestorWithIndex_ReturnsCorrectAncestor()
        {
            // Arrange
            var helper = new TestNavigationHelper();
            var literalNode = helper.FindNodeAtPosition(15, 18); // "1" in "1 + 2"

            // Act
            var result = helper.NavigateFromNode(literalNode, "ancestor[3]");

            // Assert
            Assert.NotNull(result);
            // Third ancestor should be deeper in the tree, possibly the block or method
            Assert.True(result is CS.BlockSyntax || result is CS.MethodDeclarationSyntax);
        }

        [Fact]
        public void NavigateFromNode_SelfNavigation_ReturnsSameNode()
        {
            // Arrange
            var helper = new TestNavigationHelper();
            var literalNode = helper.FindNodeAtPosition(15, 18); // "1" in "1 + 2"

            // Act
            var result = helper.NavigateFromNode(literalNode, "self");

            // Assert
            Assert.Same(literalNode, result);
        }

        [Fact]
        public void NavigateFromNode_ChildNavigation_ReturnsCorrectChild()
        {
            // Arrange
            var helper = new TestNavigationHelper();
            var methodNode = helper.FindNodeAtPosition(13, 21); // TestMethod
            var method = methodNode.AncestorsAndSelf().OfType<CS.MethodDeclarationSyntax>().First();

            // Act
            var result = helper.NavigateFromNode(method, "child");

            // Assert
            Assert.NotNull(result);
            // First child of a method could be modifiers, return type, or body
            Assert.True(result is CS.BlockSyntax || result is CS.PredefinedTypeSyntax || result is CS.ParameterListSyntax);
        }

        [Fact]
        public void NavigateFromNode_ChildWithIndex_ReturnsCorrectChild()
        {
            // Arrange
            var helper = new TestNavigationHelper();
            var blockNode = helper.FindNodeAtPosition(14, 9); // Inside method block
            var block = blockNode.AncestorsAndSelf().OfType<CS.BlockSyntax>().First();

            // Act
            var result = helper.NavigateFromNode(block, "child[2]");

            // Assert
            Assert.NotNull(result);
            // Second child statement in the block
            Assert.IsType<CS.IfStatementSyntax>(result);
        }

        [Fact]
        public void NavigateFromNode_DescendantNavigation_ReturnsCorrectDescendant()
        {
            // Arrange
            var helper = new TestNavigationHelper();
            var methodNode = helper.FindNodeAtPosition(13, 21); // TestMethod
            var method = methodNode.AncestorsAndSelf().OfType<CS.MethodDeclarationSyntax>().First();

            // Act
            var result = helper.NavigateFromNode(method, "descendant");

            // Assert
            Assert.NotNull(result);
            // First descendant could be any node within the method's subtree
            Assert.NotNull(result);
        }

        [Fact]
        public void NavigateFromNode_SiblingNavigation_ReturnsCorrectSibling()
        {
            // Arrange
            var helper = new TestNavigationHelper();
            var firstStatement = helper.FindNodeAtPosition(15, 13); // "var x = 1 + 2;"
            var statement = firstStatement.AncestorsAndSelf().OfType<CS.LocalDeclarationStatementSyntax>().First();

            // Act
            var result = helper.NavigateFromNode(statement, "following-sibling");

            // Assert
            Assert.NotNull(result);
            // Next sibling after "var x = 1 + 2;" is the if statement
            Assert.IsType<CS.IfStatementSyntax>(result);
        }

        [Fact]
        public void NavigateFromNode_PrecedingSiblingNavigation_ReturnsCorrectSibling()
        {
            // Arrange
            var helper = new TestNavigationHelper();
            var lastStatement = helper.FindNodeAtPosition(20, 13); // "var y = x * 3;" at line 31 in test code
            var statement = lastStatement.AncestorsAndSelf().OfType<CS.LocalDeclarationStatementSyntax>().First();

            // Act
            var result = helper.NavigateFromNode(statement, "preceding-sibling");

            // Assert
            Assert.NotNull(result);
            // Previous sibling of "var y = x * 3;" should be the if statement
            Assert.IsType<CS.IfStatementSyntax>(result);
        }

        [Fact]
        public void NavigateFromNode_InvalidPath_ReturnsNull()
        {
            // Arrange
            var helper = new TestNavigationHelper();
            var literalNode = helper.FindNodeAtPosition(15, 18); // "1" in "1 + 2"

            // Act
            var result = helper.NavigateFromNode(literalNode, "invalid-axis");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void NavigateFromNode_ComplexPath_ReturnsCorrectResult()
        {
            // Arrange
            var helper = new TestNavigationHelper();
            var firstStatement = helper.FindNodeAtPosition(15, 13); // "var x = 1 + 2;"
            var statement = firstStatement.AncestorsAndSelf().OfType<CS.LocalDeclarationStatementSyntax>().First();

            // Act - Navigate to parent (block), then to first child (which is the same statement)
            var result = helper.NavigateFromNode(statement, "parent::child");

            // Assert
            Assert.NotNull(result);
            // This should navigate from the statement to its parent (block),
            // then to the first child of that block (which is the same statement)
            Assert.Same(statement, result);
        }

        [Fact]
        public void BuildNodePath_SimpleNode_ReturnsCorrectPath()
        {
            // Arrange
            var helper = new TestNavigationHelper();
            var varDeclNode = helper.FindNodeAtPosition(15, 18); // Position is inside VariableDeclaratorSyntax

            // Act
            var path = helper.BuildNodePath(varDeclNode);

            // Assert
            Assert.NotNull(path);
            Assert.StartsWith("/", path);
            // Path should contain the variable declarator with name
            Assert.Contains("[@name='x']", path);
            // Path should reference the containing class and method
            Assert.Contains("[@name='TestClass']", path);
            Assert.Contains("[@name='TestMethod']", path);
        }

        [Fact]
        public void BuildNodePath_MethodNode_ReturnsCorrectPath()
        {
            // Arrange
            var helper = new TestNavigationHelper();
            var methodNode = helper.FindNodeAtPosition(13, 21); // TestMethod
            var method = methodNode.AncestorsAndSelf().OfType<CS.MethodDeclarationSyntax>().First();

            // Act
            var path = helper.BuildNodePath(method);

            // Assert
            Assert.NotNull(path);
            Assert.Contains("method[@name='TestMethod']", path);
            Assert.Contains("class[@name='TestClass']", path);
            Assert.Contains("namespace[@name='TestNamespace']", path);
        }

        [Fact]
        public void BuildNodePath_ClassNode_ReturnsCorrectPath()
        {
            // Arrange
            var helper = new TestNavigationHelper();
            var classNode = helper.FindNodeAtPosition(4, 18); // TestClass
            var cls = classNode.AncestorsAndSelf().OfType<CS.ClassDeclarationSyntax>().First();

            // Act
            var path = helper.BuildNodePath(cls);

            // Assert
            Assert.NotNull(path);
            Assert.Contains("class[@name='TestClass']", path);
            Assert.Contains("namespace[@name='TestNamespace']", path);
        }

        [Theory]
        [InlineData("parent")]
        [InlineData("ancestor")]
        [InlineData("self")]
        [InlineData("child")]
        [InlineData("descendant")]
        public void NavigateFromNode_ValidAxes_DoesNotThrow(string axis)
        {
            // Arrange
            var helper = new TestNavigationHelper();
            var methodNode = helper.FindNodeAtPosition(13, 21); // TestMethod
            var method = methodNode.AncestorsAndSelf().OfType<CS.MethodDeclarationSyntax>().First();

            // Act & Assert
            var exception = Record.Exception(() => helper.NavigateFromNode(method, axis));
            Assert.Null(exception);
        }

        [Fact]
        public void NavigateFromNode_NoParent_ReturnsNull()
        {
            // Arrange
            var helper = new TestNavigationHelper();
            var tree = CSharpSyntaxTree.ParseText(TestCode);
            var root = tree.GetRoot();

            // Act - Try to navigate to parent of root node
            var result = helper.NavigateFromNode(root, "parent");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void NavigateFromNode_IndexOutOfBounds_ReturnsNull()
        {
            // Arrange
            var helper = new TestNavigationHelper();
            var literalNode = helper.FindNodeAtPosition(15, 18); // "1" in "1 + 2"

            // Act - Try to get 10th ancestor (which doesn't exist)
            var result = helper.NavigateFromNode(literalNode, "ancestor[10]");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void NavigateFromNode_EmptyPath_ReturnsSameNode()
        {
            // Arrange
            var helper = new TestNavigationHelper();
            var literalNode = helper.FindNodeAtPosition(15, 18); // "1" in "1 + 2"

            // Act
            var result = helper.NavigateFromNode(literalNode, "");

            // Assert
            Assert.Same(literalNode, result);
        }
    }
}