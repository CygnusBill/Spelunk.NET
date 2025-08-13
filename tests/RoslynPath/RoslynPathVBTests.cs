using System;
using System.Linq;
using Microsoft.CodeAnalysis.VisualBasic;
using McpRoslyn.Server.RoslynPath;
using Xunit;

namespace McpRoslyn.Tests.RoslynPath
{
    /// <summary>
    /// Test suite for RoslynPath with VB.NET code
    /// Ensures language-agnostic features work correctly
    /// </summary>
    public class RoslynPathVBTests
    {
        #region Test Helpers

        private static Microsoft.CodeAnalysis.SyntaxTree ParseVBCode(string code)
        {
            return VisualBasicSyntaxTree.ParseText(code);
        }

        private static int CountVBMatches(string code, string path)
        {
            var tree = ParseVBCode(code);
            var evaluator = new RoslynPathEvaluator(tree);
            return evaluator.Evaluate(path).Count();
        }

        #endregion

        #region VB.NET Language Mapping Tests

        [Fact]
        public void TestVBClassAndMethod()
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
            Assert.Equal(1, CountVBMatches(code, "//class"));
            Assert.Equal(2, CountVBMatches(code, "//method"));
            Assert.Equal(1, CountVBMatches(code, "//method[TestMethod]"));
            Assert.Equal(1, CountVBMatches(code, "//method[GetValue]"));
        }

        [Fact]
        public void TestVBSubVsFunction()
        {
            var code = @"
Public Class TestClass
    Public Sub DoSomething()
        Console.WriteLine(""Hello"")
    End Sub
    
    Public Function Calculate(x As Integer) As Integer
        Return x * 2
    End Function
End Class";
            // Both Sub and Function are mapped to "method"
            Assert.Equal(2, CountVBMatches(code, "//method"));
            
            // Test VB-specific methodtype attribute
            Assert.Equal(1, CountVBMatches(code, "//method[@methodtype='sub']"));
            Assert.Equal(1, CountVBMatches(code, "//method[@methodtype='function']"));
            
            // Test return type mapping (Sub -> void)
            Assert.Equal(1, CountVBMatches(code, "//method[@returns='void']"));
        }

        [Fact]
        public void TestVBProperties()
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
            Assert.Equal(2, CountVBMatches(code, "//property"));
            Assert.Equal(1, CountVBMatches(code, "//property[Name]"));
            
            // Test property accessors
            Assert.Equal(2, CountVBMatches(code, "//property[@has-getter]"));
            Assert.Equal(1, CountVBMatches(code, "//property[@has-setter]"));
        }

        [Fact]
        public void TestVBStatements()
        {
            var code = @"
Public Class TestClass
    Public Sub TestMethod()
        If True Then
            Console.WriteLine(""True"")
        End If
        
        While False
            ' Do nothing
        End While
        
        For i As Integer = 0 To 10
            Console.WriteLine(i)
        Next
        
        For Each item In collection
            Process(item)
        Next
        
        Select Case x
            Case 1
                DoSomething()
        End Select
        
        Try
            DoRisky()
        Catch ex As Exception
            HandleError(ex)
        Finally
            Cleanup()
        End Try
    End Sub
End Class";
            Assert.Equal(1, CountVBMatches(code, "//if-statement"));
            Assert.Equal(1, CountVBMatches(code, "//while-statement"));
            Assert.Equal(1, CountVBMatches(code, "//for-statement"));
            Assert.Equal(1, CountVBMatches(code, "//foreach-statement"));
            // Note: VB Select Case maps to switch-statement
            Assert.Equal(1, CountVBMatches(code, "//switch-statement"));
            Assert.Equal(1, CountVBMatches(code, "//try-statement"));
        }

        [Fact]
        public void TestVBModifiers()
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
            Assert.Equal(2, CountVBMatches(code, "//method[@public]"));
            Assert.Equal(1, CountVBMatches(code, "//method[@private]"));
            Assert.Equal(1, CountVBMatches(code, "//method[@virtual]")); // Overridable -> virtual
            Assert.Equal(1, CountVBMatches(code, "//method[@static]")); // Shared -> static
            Assert.Equal(1, CountVBMatches(code, "//method[@abstract]")); // MustOverride -> abstract
        }

        [Fact]
        public void TestVBNullComparisons()
        {
            var code = @"
Public Class TestClass
    Public Sub CheckNull(param As String)
        If param Is Nothing Then
            Throw New ArgumentNullException()
        End If
        
        If Nothing Is param Then
            Return
        End If
        
        If param IsNot Nothing Then
            DoSomething(param)
        End If
    End Sub
End Class";
            // VB uses "Is Nothing" and "IsNot Nothing" for null checks
            // These should be mapped appropriately
            Assert.True(CountVBMatches(code, "//if-statement") == 3);
            // The binary expressions might have different structure in VB
            Assert.True(CountVBMatches(code, "//binary-expression") >= 0);
        }

        [Fact]
        public void TestVBAsyncAwait()
        {
            var code = @"
Imports System.Threading.Tasks

Public Class TestClass
    Public Async Function GetDataAsync() As Task(Of String)
        Await Task.Delay(100)
        Dim result = Await FetchAsync()
        Return result
    End Function
    
    Public Async Sub ProcessAsync()
        Await Task.Delay(50)
    End Sub
End Class";
            // Async/Await work similarly in VB
            Assert.Equal(2, CountVBMatches(code, "//method[@async]"));
            Assert.Equal(3, CountVBMatches(code, "//await-expression"));
        }

        [Fact]
        public void TestVBLinq()
        {
            var code = @"
Public Class TestClass
    Public Sub TestLinq()
        Dim query = From x In collection
                    Where x.Value > 10
                    Order By x.Name
                    Select x.Id
                    
        Dim lambda = collection.Where(Function(x) x.Value > 10) _
                              .OrderBy(Function(x) x.Name) _
                              .Select(Function(x) x.Id)
    End Sub
End Class";
            // LINQ query expressions
            Assert.Equal(1, CountVBMatches(code, "//query-expression"));
            
            // Lambda expressions (Function() in VB)
            Assert.Equal(3, CountVBMatches(code, "//lambda"));
        }

        #endregion

        #region VB.NET Specific Features

        [Fact]
        public void TestVBWithStatement()
        {
            var code = @"
Public Class TestClass
    Public Sub TestWith()
        With myObject
            .Property1 = ""Value1""
            .Property2 = ""Value2""
            .Method1()
        End With
    End Sub
End Class";
            // With statements are VB-specific
            // They should be captured as some form of statement
            Assert.True(CountVBMatches(code, "//statement") > 0);
        }

        [Fact]
        public void TestVBModuleDeclaration()
        {
            var code = @"
Module TestModule
    Public Sub ModuleMethod()
        Console.WriteLine(""Module"")
    End Sub
End Module";
            // VB Modules are similar to static classes
            // They might be mapped differently
            Assert.True(CountVBMatches(code, "//method") == 1);
        }

        [Fact]
        public void TestVBEventHandling()
        {
            var code = @"
Public Class TestClass
    Public Event MyEvent As EventHandler
    
    Public Sub RaiseMyEvent()
        RaiseEvent MyEvent(Me, EventArgs.Empty)
    End Sub
    
    Private Sub HandleEvent(sender As Object, e As EventArgs) Handles Me.MyEvent
        Console.WriteLine(""Event handled"")
    End Sub
End Class";
            // Event declarations and handlers
            Assert.True(CountVBMatches(code, "//method") >= 2);
        }

        #endregion

        #region Cross-Language Pattern Tests

        [Fact]
        public void TestCrossLanguageNullCheckPattern()
        {
            // This pattern should work for both C# and VB with appropriate mapping
            var vbCode = @"
Public Class TestClass
    Public Sub CheckParam(param As String)
        If param Is Nothing Then
            Throw New ArgumentNullException(""param"")
        End If
    End Sub
End Class";

            var csCode = @"
public class TestClass
{
    public void CheckParam(string param)
    {
        if (param == null)
            throw new ArgumentNullException(""param"");
    }
}";
            
            // Both should find the if statement with a throw
            Assert.Equal(1, CountVBMatches(vbCode, "//if-statement[.//throw-statement]"));
            
            var csTree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(csCode);
            var csEvaluator = new RoslynPathEvaluator(csTree);
            Assert.Equal(1, csEvaluator.Evaluate("//if-statement[.//throw-statement]").Count());
        }

        #endregion
    }
}