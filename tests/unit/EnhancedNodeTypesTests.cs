using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using Xunit;
using CS = Microsoft.CodeAnalysis.CSharp.Syntax;
using VB = Microsoft.CodeAnalysis.VisualBasic.Syntax;

namespace McpRoslyn.Server.Tests.Unit
{
    public class EnhancedNodeTypesTests
    {
        [Fact]
        public void GetDetailedNodeTypeName_CSharpClass_ReturnsClass()
        {
            // Arrange
            var code = "public class TestClass { }";
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetRoot();
            var classNode = root.DescendantNodes().OfType<CS.ClassDeclarationSyntax>().First();

            // Act
            var result = RoslynPath.EnhancedNodeTypes.GetDetailedNodeTypeName(classNode);

            // Assert
            Assert.Equal("class", result);
        }

        [Fact]
        public void GetDetailedNodeTypeName_CSharpMethod_ReturnsMethod()
        {
            // Arrange
            var code = "public class TestClass { public void TestMethod() { } }";
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetRoot();
            var methodNode = root.DescendantNodes().OfType<CS.MethodDeclarationSyntax>().First();

            // Act
            var result = RoslynPath.EnhancedNodeTypes.GetDetailedNodeTypeName(methodNode);

            // Assert
            Assert.Equal("method", result);
        }

        [Fact]
        public void GetDetailedNodeTypeName_CSharpBinaryExpression_ReturnsBinaryExpression()
        {
            // Arrange
            var code = "public class TestClass { public void Test() { var x = 1 + 2; } }";
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetRoot();
            var binaryNode = root.DescendantNodes().OfType<CS.BinaryExpressionSyntax>().First();

            // Act
            var result = RoslynPath.EnhancedNodeTypes.GetDetailedNodeTypeName(binaryNode);

            // Assert
            Assert.Equal("binary-expression", result);
        }

        [Fact]
        public void GetDetailedNodeTypeName_CSharpIfStatement_ReturnsIfStatement()
        {
            // Arrange
            var code = "public class TestClass { public void Test() { if (true) { } } }";
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetRoot();
            var ifNode = root.DescendantNodes().OfType<CS.IfStatementSyntax>().First();

            // Act
            var result = RoslynPath.EnhancedNodeTypes.GetDetailedNodeTypeName(ifNode);

            // Assert
            Assert.Equal("if-statement", result);
        }

        [Fact]
        public void GetDetailedNodeTypeName_CSharpLiteral_ReturnsLiteral()
        {
            // Arrange
            var code = "public class TestClass { public void Test() { var x = \"hello\"; } }";
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetRoot();
            var literalNode = root.DescendantNodes().OfType<CS.LiteralExpressionSyntax>().First();

            // Act
            var result = RoslynPath.EnhancedNodeTypes.GetDetailedNodeTypeName(literalNode);

            // Assert
            Assert.Equal("literal", result);
        }

        [Fact]
        public void GetBinaryOperator_CSharpAddition_ReturnsPlus()
        {
            // Arrange
            var code = "public class TestClass { public void Test() { var x = 1 + 2; } }";
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetRoot();
            var binaryNode = root.DescendantNodes().OfType<CS.BinaryExpressionSyntax>().First();

            // Act
            var result = RoslynPath.EnhancedNodeTypes.GetBinaryOperator(binaryNode);

            // Assert
            Assert.Equal("+", result);
        }

        [Fact]
        public void GetBinaryOperator_CSharpEquality_ReturnsEquals()
        {
            // Arrange
            var code = "public class TestClass { public void Test() { var x = 1 == 2; } }";
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetRoot();
            var binaryNode = root.DescendantNodes().OfType<CS.BinaryExpressionSyntax>().First();

            // Act
            var result = RoslynPath.EnhancedNodeTypes.GetBinaryOperator(binaryNode);

            // Assert
            Assert.Equal("==", result);
        }

        [Fact]
        public void GetLiteralValue_CSharpStringLiteral_ReturnsValue()
        {
            // Arrange
            var code = "public class TestClass { public void Test() { var x = \"hello\"; } }";
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetRoot();
            var literalNode = root.DescendantNodes().OfType<CS.LiteralExpressionSyntax>().First();

            // Act
            var result = RoslynPath.EnhancedNodeTypes.GetLiteralValue(literalNode);

            // Assert
            Assert.Equal("hello", result);
        }

        [Fact]
        public void GetLiteralValue_CSharpIntegerLiteral_ReturnsValue()
        {
            // Arrange
            var code = "public class TestClass { public void Test() { var x = 42; } }";
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetRoot();
            var literalNode = root.DescendantNodes().OfType<CS.LiteralExpressionSyntax>().First();

            // Act
            var result = RoslynPath.EnhancedNodeTypes.GetLiteralValue(literalNode);

            // Assert
            Assert.Equal("42", result);
        }

        [Fact]
        public void GetDetailedNodeTypeName_VisualBasicClass_ReturnsClass()
        {
            // Arrange
            var code = "Public Class TestClass\nEnd Class";
            var tree = VisualBasicSyntaxTree.ParseText(code);
            var root = tree.GetRoot();
            var classNode = root.DescendantNodes().OfType<VB.ClassBlockSyntax>().First();

            // Act
            var result = RoslynPath.EnhancedNodeTypes.GetDetailedNodeTypeName(classNode);

            // Assert
            Assert.Equal("class", result);
        }

        [Fact]
        public void GetDetailedNodeTypeName_VisualBasicMethod_ReturnsMethod()
        {
            // Arrange
            var code = @"
Public Class TestClass
    Public Sub TestMethod()
    End Sub
End Class";
            var tree = VisualBasicSyntaxTree.ParseText(code);
            var root = tree.GetRoot();
            var methodNode = root.DescendantNodes().OfType<VB.MethodBlockSyntax>().First();

            // Act
            var result = RoslynPath.EnhancedNodeTypes.GetDetailedNodeTypeName(methodNode);

            // Assert
            Assert.Equal("method", result);
        }

        [Fact]
        public void GetDetailedNodeTypeName_VisualBasicBinaryExpression_ReturnsBinaryExpression()
        {
            // Arrange
            var code = @"
Public Class TestClass
    Public Sub Test()
        Dim x = 1 + 2
    End Sub
End Class";
            var tree = VisualBasicSyntaxTree.ParseText(code);
            var root = tree.GetRoot();
            var binaryNode = root.DescendantNodes().OfType<VB.BinaryExpressionSyntax>().First();

            // Act
            var result = RoslynPath.EnhancedNodeTypes.GetDetailedNodeTypeName(binaryNode);

            // Assert
            Assert.Equal("binary-expression", result);
        }

        [Fact]
        public void GetDetailedNodeTypeName_VisualBasicIfStatement_ReturnsIfStatement()
        {
            // Arrange
            var code = @"
Public Class TestClass
    Public Sub Test()
        If True Then
        End If
    End Sub
End Class";
            var tree = VisualBasicSyntaxTree.ParseText(code);
            var root = tree.GetRoot();
            var ifNode = root.DescendantNodes().OfType<VB.MultiLineIfBlockSyntax>().First();

            // Act
            var result = RoslynPath.EnhancedNodeTypes.GetDetailedNodeTypeName(ifNode);

            // Assert
            Assert.Equal("if-statement", result);
        }

        [Fact]
        public void GetBinaryOperator_VisualBasicAddition_ReturnsPlus()
        {
            // Arrange
            var code = @"
Public Class TestClass
    Public Sub Test()
        Dim x = 1 + 2
    End Sub
End Class";
            var tree = VisualBasicSyntaxTree.ParseText(code);
            var root = tree.GetRoot();
            var binaryNode = root.DescendantNodes().OfType<VB.BinaryExpressionSyntax>().First();

            // Act
            var result = RoslynPath.EnhancedNodeTypes.GetBinaryOperator(binaryNode);

            // Assert
            Assert.Equal("+", result);
        }

        [Fact]
        public void GetLiteralValue_VisualBasicStringLiteral_ReturnsValue()
        {
            // Arrange
            var code = @"
Public Class TestClass
    Public Sub Test()
        Dim x = ""hello""
    End Sub
End Class";
            var tree = VisualBasicSyntaxTree.ParseText(code);
            var root = tree.GetRoot();
            var literalNode = root.DescendantNodes().OfType<VB.LiteralExpressionSyntax>().First();

            // Act
            var result = RoslynPath.EnhancedNodeTypes.GetLiteralValue(literalNode);

            // Assert
            Assert.Equal("hello", result);
        }

        [Fact]
        public void GetDetailedNodeTypeName_UnknownNode_ReturnsGenericName()
        {
            // Arrange
            var code = "public class TestClass { }";
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetRoot();
            var compilationUnit = root as CS.CompilationUnitSyntax;

            // Act
            var result = RoslynPath.EnhancedNodeTypes.GetDetailedNodeTypeName(compilationUnit!);

            // Assert
            Assert.Equal("compilationunit", result);
        }

        [Theory]
        [InlineData("1 + 2", "+")]
        [InlineData("1 - 2", "-")]
        [InlineData("1 * 2", "*")]
        [InlineData("1 / 2", "/")]
        [InlineData("1 == 2", "==")]
        [InlineData("1 != 2", "!=")]
        [InlineData("1 < 2", "<")]
        [InlineData("1 > 2", ">")]
        [InlineData("1 <= 2", "<=")]
        [InlineData("1 >= 2", ">=")]
        [InlineData("true && false", "&&")]
        [InlineData("true || false", "||")]
        public void GetBinaryOperator_VariousOperators_ReturnsCorrectOperator(string expression, string expectedOperator)
        {
            // Arrange
            var code = $"public class TestClass {{ public void Test() {{ var x = {expression}; }} }}";
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetRoot();
            var binaryNode = root.DescendantNodes().OfType<CS.BinaryExpressionSyntax>().First();

            // Act
            var result = RoslynPath.EnhancedNodeTypes.GetBinaryOperator(binaryNode);

            // Assert
            Assert.Equal(expectedOperator, result);
        }

        [Theory]
        [InlineData("42", "42")]
        [InlineData("\"hello\"", "hello")]
        [InlineData("'c'", "c")]
        [InlineData("true", "true")]
        [InlineData("false", "false")]
        [InlineData("3.14", "3.14")]
        public void GetLiteralValue_VariousLiterals_ReturnsCorrectValue(string literal, string expectedValue)
        {
            // Arrange
            var code = $"public class TestClass {{ public void Test() {{ var x = {literal}; }} }}";
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetRoot();
            var literalNode = root.DescendantNodes().OfType<CS.LiteralExpressionSyntax>().First();

            // Act
            var result = RoslynPath.EnhancedNodeTypes.GetLiteralValue(literalNode);

            // Assert
            Assert.Equal(expectedValue, result);
        }

        [Fact]
        public void GetDetailedNodeTypeName_CSharpProperty_ReturnsProperty()
        {
            // Arrange
            var code = "public class TestClass { public int TestProperty { get; set; } }";
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetRoot();
            var propertyNode = root.DescendantNodes().OfType<CS.PropertyDeclarationSyntax>().First();

            // Act
            var result = RoslynPath.EnhancedNodeTypes.GetDetailedNodeTypeName(propertyNode);

            // Assert
            Assert.Equal("property", result);
        }

        [Fact]
        public void GetDetailedNodeTypeName_CSharpField_ReturnsField()
        {
            // Arrange
            var code = "public class TestClass { private int testField; }";
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetRoot();
            var fieldNode = root.DescendantNodes().OfType<CS.FieldDeclarationSyntax>().First();

            // Act
            var result = RoslynPath.EnhancedNodeTypes.GetDetailedNodeTypeName(fieldNode);

            // Assert
            Assert.Equal("field", result);
        }

        [Fact]
        public void GetDetailedNodeTypeName_CSharpConstructor_ReturnsConstructor()
        {
            // Arrange
            var code = "public class TestClass { public TestClass() { } }";
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetRoot();
            var constructorNode = root.DescendantNodes().OfType<CS.ConstructorDeclarationSyntax>().First();

            // Act
            var result = RoslynPath.EnhancedNodeTypes.GetDetailedNodeTypeName(constructorNode);

            // Assert
            Assert.Equal("constructor", result);
        }
    }
}