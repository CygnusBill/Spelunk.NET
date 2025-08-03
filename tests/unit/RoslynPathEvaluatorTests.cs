using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;
using CS = Microsoft.CodeAnalysis.CSharp.Syntax;

namespace McpRoslyn.Server.Tests.Unit
{
    [Collection("RoslynPath Tests")]
    public class RoslynPathEvaluatorTests
    {
        private const string TestCode = @"
namespace TestNamespace
{
    public class TestClass
    {
        private int testField;
        
        public int TestProperty { get; set; }
        
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
        }
        
        public int Add(int a, int b)
        {
            return a + b;
        }
    }
    
    public class SecondClass
    {
        public void AnotherMethod()
        {
            var result = 5 * 3;
        }
    }
}";

        private SyntaxTree GetTestSyntaxTree()
        {
            return CSharpSyntaxTree.ParseText(TestCode);
        }

        [Fact]
        public void Evaluate_FindAllClasses_ReturnsAllClasses()
        {
            // Arrange
            var tree = GetTestSyntaxTree();
            var evaluator = new RoslynPath.RoslynPathEvaluator(tree);

            // Act
            var result = evaluator.Evaluate("//class").ToList();

            // Assert
            Assert.Equal(2, result.Count);
            Assert.All(result, node => Assert.IsType<CS.ClassDeclarationSyntax>(node));
            
            var classNames = result.Cast<CS.ClassDeclarationSyntax>()
                                   .Select(c => c.Identifier.ValueText)
                                   .ToList();
            Assert.Contains("TestClass", classNames);
            Assert.Contains("SecondClass", classNames);
        }

        [Fact]
        public void Evaluate_FindSpecificClass_ReturnsCorrectClass()
        {
            // Arrange
            var tree = GetTestSyntaxTree();
            var evaluator = new RoslynPath.RoslynPathEvaluator(tree);

            // Act
            var result = evaluator.Evaluate("//class[@name='TestClass']").ToList();

            // Assert
            Assert.Single(result);
            var classNode = Assert.IsType<CS.ClassDeclarationSyntax>(result[0]);
            Assert.Equal("TestClass", classNode.Identifier.ValueText);
        }

        [Fact]
        public void Evaluate_FindAllMethods_ReturnsAllMethods()
        {
            // Arrange
            var tree = GetTestSyntaxTree();
            var evaluator = new RoslynPath.RoslynPathEvaluator(tree);

            // Act
            var result = evaluator.Evaluate("//method").ToList();

            // Assert
            Assert.Equal(3, result.Count); // TestMethod, Add, AnotherMethod
            Assert.All(result, node => Assert.IsType<CS.MethodDeclarationSyntax>(node));
        }

        [Fact]
        public void Evaluate_FindMethodsInSpecificClass_ReturnsCorrectMethods()
        {
            // Arrange
            var tree = GetTestSyntaxTree();
            var evaluator = new RoslynPath.RoslynPathEvaluator(tree);

            // Act
            var result = evaluator.Evaluate("//class[@name='TestClass']//method").ToList();

            // Assert
            Assert.Equal(2, result.Count); // TestMethod and Add
            Assert.All(result, node => Assert.IsType<CS.MethodDeclarationSyntax>(node));
            
            var methodNames = result.Cast<CS.MethodDeclarationSyntax>()
                                    .Select(m => m.Identifier.ValueText)
                                    .ToList();
            Assert.Contains("TestMethod", methodNames);
            Assert.Contains("Add", methodNames);
        }

        [Fact]
        public void Evaluate_FindBinaryExpressions_ReturnsAllBinaryExpressions()
        {
            // Arrange
            var tree = GetTestSyntaxTree();
            var evaluator = new RoslynPath.RoslynPathEvaluator(tree);

            // Act
            var result = evaluator.Evaluate("//binary-expression").ToList();

            // Assert
            Assert.True(result.Count >= 3); // 1 + 2, x > 0, a + b, 5 * 3
            Assert.All(result, node => Assert.IsType<CS.BinaryExpressionSyntax>(node));
        }

        [Fact]
        public void Evaluate_FindBinaryExpressionsWithPlusOperator_ReturnsCorrectExpressions()
        {
            // Arrange
            var tree = GetTestSyntaxTree();
            var evaluator = new RoslynPath.RoslynPathEvaluator(tree);

            // Act
            var result = evaluator.Evaluate("//binary-expression[@operator='+']").ToList();

            // Assert
            Assert.Equal(2, result.Count); // 1 + 2 and a + b
            Assert.All(result, node => 
            {
                var binaryExpr = Assert.IsType<CS.BinaryExpressionSyntax>(node);
                Assert.True(binaryExpr.OperatorToken.IsKind(SyntaxKind.PlusToken));
            });
        }

        [Fact]
        public void Evaluate_FindLiterals_ReturnsAllLiterals()
        {
            // Arrange
            var tree = GetTestSyntaxTree();
            var evaluator = new RoslynPath.RoslynPathEvaluator(tree);

            // Act
            var result = evaluator.Evaluate("//literal").ToList();

            // Assert
            Assert.True(result.Count >= 5); // 0, 1, 2, "Hello", 5, 3
            Assert.All(result, node => Assert.IsType<CS.LiteralExpressionSyntax>(node));
        }

        [Fact]
        public void Evaluate_FindStringLiterals_ReturnsOnlyStringLiterals()
        {
            // Arrange
            var tree = GetTestSyntaxTree();
            var evaluator = new RoslynPath.RoslynPathEvaluator(tree);

            // Act
            var result = evaluator.Evaluate("//literal[@literal-value='Hello']").ToList();

            // Assert
            Assert.Single(result);
            var literal = Assert.IsType<CS.LiteralExpressionSyntax>(result[0]);
            Assert.Equal("\"Hello\"", literal.Token.ValueText);
        }

        [Fact]
        public void Evaluate_FindIfStatements_ReturnsAllIfStatements()
        {
            // Arrange
            var tree = GetTestSyntaxTree();
            var evaluator = new RoslynPath.RoslynPathEvaluator(tree);

            // Act
            var result = evaluator.Evaluate("//if-statement").ToList();

            // Assert
            Assert.Single(result);
            Assert.IsType<CS.IfStatementSyntax>(result[0]);
        }

        [Fact]
        public void Evaluate_FindProperties_ReturnsAllProperties()
        {
            // Arrange
            var tree = GetTestSyntaxTree();
            var evaluator = new RoslynPath.RoslynPathEvaluator(tree);

            // Act
            var result = evaluator.Evaluate("//property").ToList();

            // Assert
            Assert.Single(result);
            var property = Assert.IsType<CS.PropertyDeclarationSyntax>(result[0]);
            Assert.Equal("TestProperty", property.Identifier.ValueText);
        }

        [Fact]
        public void Evaluate_FindFields_ReturnsAllFields()
        {
            // Arrange
            var tree = GetTestSyntaxTree();
            var evaluator = new RoslynPath.RoslynPathEvaluator(tree);

            // Act
            var result = evaluator.Evaluate("//field").ToList();

            // Assert
            Assert.Single(result);
            Assert.IsType<CS.FieldDeclarationSyntax>(result[0]);
        }

        [Fact]
        public void Evaluate_FindConstructors_ReturnsAllConstructors()
        {
            // Arrange
            var tree = GetTestSyntaxTree();
            var evaluator = new RoslynPath.RoslynPathEvaluator(tree);

            // Act
            var result = evaluator.Evaluate("//constructor").ToList();

            // Assert
            Assert.Single(result);
            Assert.IsType<CS.ConstructorDeclarationSyntax>(result[0]);
        }

        [Fact]
        public void Evaluate_FindMethodWithSpecificName_ReturnsCorrectMethod()
        {
            // Arrange
            var tree = GetTestSyntaxTree();
            var evaluator = new RoslynPath.RoslynPathEvaluator(tree);

            // Act
            var result = evaluator.Evaluate("//method[@name='Add']").ToList();

            // Assert
            Assert.Single(result);
            var method = Assert.IsType<CS.MethodDeclarationSyntax>(result[0]);
            Assert.Equal("Add", method.Identifier.ValueText);
        }

        [Fact]
        public void Evaluate_UseChildAxis_ReturnsDirectChildren()
        {
            // Arrange
            var tree = GetTestSyntaxTree();
            var evaluator = new RoslynPath.RoslynPathEvaluator(tree);

            // Act
            var result = evaluator.Evaluate("//class[@name='TestClass']/child::method").ToList();

            // Assert
            Assert.Equal(2, result.Count); // TestMethod and Add are direct children
            Assert.All(result, node => Assert.IsType<CS.MethodDeclarationSyntax>(node));
        }

        [Fact]
        public void Evaluate_UseDescendantAxis_ReturnsAllDescendants()
        {
            // Arrange
            var tree = GetTestSyntaxTree();
            var evaluator = new RoslynPath.RoslynPathEvaluator(tree);

            // Act
            var result = evaluator.Evaluate("//class[@name='TestClass']/descendant::literal").ToList();

            // Assert
            Assert.True(result.Count >= 4); // Multiple literals in TestClass methods
            Assert.All(result, node => Assert.IsType<CS.LiteralExpressionSyntax>(node));
        }

        [Fact]
        public void Evaluate_UseParentAxis_ReturnsParentNodes()
        {
            // Arrange
            var tree = GetTestSyntaxTree();
            var evaluator = new RoslynPath.RoslynPathEvaluator(tree);

            // Act
            var result = evaluator.Evaluate("//method[@name='TestMethod']/parent::*").ToList();

            // Assert
            Assert.Single(result);
            var parent = Assert.IsType<CS.ClassDeclarationSyntax>(result[0]);
            Assert.Equal("TestClass", parent.Identifier.ValueText);
        }

        [Fact]
        public void Evaluate_UseSelfAxis_ReturnsSameNode()
        {
            // Arrange
            var tree = GetTestSyntaxTree();
            var evaluator = new RoslynPath.RoslynPathEvaluator(tree);

            // Act
            var result = evaluator.Evaluate("//method[@name='Add']/self::method").ToList();

            // Assert
            Assert.Single(result);
            var method = Assert.IsType<CS.MethodDeclarationSyntax>(result[0]);
            Assert.Equal("Add", method.Identifier.ValueText);
        }

        [Fact]
        public void Evaluate_ComplexPath_ReturnsCorrectResults()
        {
            // Arrange
            var tree = GetTestSyntaxTree();
            var evaluator = new RoslynPath.RoslynPathEvaluator(tree);

            // Act - Find all binary expressions with '+' operator inside TestMethod
            var result = evaluator.Evaluate("//class[@name='TestClass']//method[@name='TestMethod']//binary-expression[@operator='+']").ToList();

            // Assert
            Assert.Single(result); // Only "1 + 2" in TestMethod
            var binaryExpr = Assert.IsType<CS.BinaryExpressionSyntax>(result[0]);
            Assert.True(binaryExpr.OperatorToken.IsKind(SyntaxKind.PlusToken));
        }

        [Fact]
        public void Evaluate_InvalidPath_ReturnsEmpty()
        {
            // Arrange
            var tree = GetTestSyntaxTree();
            var evaluator = new RoslynPath.RoslynPathEvaluator(tree);

            // Act
            var result = evaluator.Evaluate("//nonexistent-node-type").ToList();

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void Evaluate_EmptyPath_ReturnsEmpty()
        {
            // Arrange
            var tree = GetTestSyntaxTree();
            var evaluator = new RoslynPath.RoslynPathEvaluator(tree);

            // Act
            var result = evaluator.Evaluate("").ToList();

            // Assert
            Assert.Empty(result);
        }

        [Theory]
        [InlineData("//class")]
        [InlineData("//method")]
        [InlineData("//property")]
        [InlineData("//field")]
        [InlineData("//constructor")]
        [InlineData("//binary-expression")]
        [InlineData("//literal")]
        [InlineData("//if-statement")]
        public void Evaluate_BasicNodeTypes_ReturnsResults(string path)
        {
            // Arrange
            var tree = GetTestSyntaxTree();
            var evaluator = new RoslynPath.RoslynPathEvaluator(tree);

            // Act
            var result = evaluator.Evaluate(path).ToList();

            // Assert
            Assert.NotEmpty(result);
        }

        [Fact]
        public void Evaluate_MultiplePredicates_ReturnsCorrectResults()
        {
            // Arrange
            var code = @"
public class TestClass
{
    public int Method1() { return 1; }
    public string Method2() { return ""test""; }
    public void Method3() { }
}";
            var tree = CSharpSyntaxTree.ParseText(code);
            var evaluator = new RoslynPath.RoslynPathEvaluator(tree);

            // Act - Find methods that return int and have name starting with "Method"
            var result = evaluator.Evaluate("//method[@returns='int']").ToList();

            // Assert
            Assert.Single(result);
            var method = Assert.IsType<CS.MethodDeclarationSyntax>(result[0]);
            Assert.Equal("Method1", method.Identifier.ValueText);
        }
    }
}