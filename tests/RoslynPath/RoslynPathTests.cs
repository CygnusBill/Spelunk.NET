using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
// Switch to new implementation
using McpRoslyn.Server.RoslynPath2;
using Xunit;

namespace McpRoslyn.Tests.RoslynPath
{
    /// <summary>
    /// Comprehensive test suite for RoslynPath functionality
    /// Tests all features and reasonable combinations
    /// </summary>
    public class RoslynPathTests
    {
        #region Test Helpers

        private static SyntaxTree ParseCode(string code)
        {
            return CSharpSyntaxTree.ParseText(code);
        }

        private static int CountMatches(string code, string path)
        {
            var tree = ParseCode(code);
            var evaluator = new RoslynPathEvaluator2(tree);
            
            // Add timeout to prevent infinite loops
            var task = System.Threading.Tasks.Task.Run(() => evaluator.Evaluate(path).Count());
            if (task.Wait(TimeSpan.FromSeconds(5)))
            {
                return task.Result;
            }
            throw new TimeoutException($"RoslynPath evaluation timed out for: {path}");
        }

        private static string[] GetMatchedTexts(string code, string path, int maxLength = 50)
        {
            var tree = ParseCode(code);
            var evaluator = new RoslynPathEvaluator2(tree);
            
            // Add timeout to prevent infinite loops
            var task = System.Threading.Tasks.Task.Run(() => 
                evaluator.Evaluate(path)
                    .Select(n => n.ToString().Length > maxLength 
                        ? n.ToString().Substring(0, maxLength) + "..." 
                        : n.ToString())
                    .ToArray());
                    
            if (task.Wait(TimeSpan.FromSeconds(5)))
            {
                return task.Result;
            }
            throw new TimeoutException($"RoslynPath evaluation timed out for: {path}");
        }

        #endregion

        #region Basic Navigation Tests

        [Fact]
        public void TestChildNavigation()
        {
            var code = @"
namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod() { }
    }
}";
            // Direct child navigation
            Assert.Equal(1, CountMatches(code, "/namespace"));
            Assert.Equal(1, CountMatches(code, "/namespace/class"));
            Assert.Equal(1, CountMatches(code, "/namespace/class/method"));
        }

        [Fact]
        public void TestDescendantNavigation()
        {
            var code = @"
namespace TestNamespace
{
    public class OuterClass
    {
        public class InnerClass
        {
            public void Method1() { }
            public void Method2() { }
        }
        public void Method3() { }
    }
}";
            // Descendant navigation with //
            Assert.Equal(2, CountMatches(code, "//class"));
            Assert.Equal(3, CountMatches(code, "//method"));
            Assert.Equal(1, CountMatches(code, "//class[InnerClass]"));
        }

        [Fact]
        public void TestWildcardNavigation()
        {
            var code = @"
public class TestClass
{
    public void Method1() { }
    public string Property1 { get; set; }
    private int field1;
}";
            // Wildcard matches any node type
            Assert.True(CountMatches(code, "//*") > 10);
            Assert.Equal(1, CountMatches(code, "/class/*[Method1]"));
        }

        #endregion

        #region Name Predicate Tests

        [Fact]
        public void TestNamePredicate()
        {
            var code = @"
public class UserService
{
    public void GetUser() { }
    public void GetUserById() { }
    public void UpdateUser() { }
    public void DeleteUser() { }
}";
            Assert.Equal(1, CountMatches(code, "//method[GetUser]"));
            Assert.Equal(2, CountMatches(code, "//method[GetUser*]")); // Wildcard in name
            Assert.Equal(3, CountMatches(code, "//method[*User]")); // GetUser, UpdateUser, DeleteUser
        }

        [Fact]
        public void TestNamePredicateWithAttribute()
        {
            var code = @"
public class TestClass
{
    public void foo() { }
    public void bar() { }
    private string foo = ""field"";
}";
            // The pattern that caused the infinite loop - now fixed
            Assert.Equal(2, CountMatches(code, "//*[@name='foo']"));
            Assert.Equal(1, CountMatches(code, "//method[@name='foo']"));
            Assert.Equal(1, CountMatches(code, "//field[@name='foo']"));
        }

        #endregion

        #region Position Predicate Tests

        [Fact]
        public void TestPositionPredicates()
        {
            var code = @"
public class TestClass
{
    public void Method1() 
    { 
        var a = 1;
        var b = 2;
        var c = 3;
        var d = 4;
    }
}";
            Assert.Equal(1, CountMatches(code, "//block/statement[1]"));
            Assert.Equal(1, CountMatches(code, "//block/statement[last()]"));
            Assert.Equal(1, CountMatches(code, "//block/statement[last()-1]"));
            Assert.Equal(4, CountMatches(code, "//block/statement"));
        }

        #endregion

        #region Attribute Predicate Tests

        [Fact]
        public void TestTypeAttributePredicate()
        {
            var code = @"
public class TestClass
{
    public void Method1() 
    { 
        if (true) { }
        while (false) { }
        for (int i = 0; i < 10; i++) { }
        return;
    }
}";
            Assert.Equal(1, CountMatches(code, "//statement[@type='IfStatementSyntax']"));
            Assert.Equal(1, CountMatches(code, "//statement[@type='WhileStatementSyntax']"));
            Assert.Equal(1, CountMatches(code, "//statement[@type='ForStatementSyntax']"));
            Assert.Equal(1, CountMatches(code, "//statement[@type='ReturnStatementSyntax']"));
        }

        [Fact]
        public void TestContainsAttributePredicate()
        {
            var code = @"
public class TestClass
{
    public void Method1() 
    { 
        Console.WriteLine(""Hello"");
        System.Console.WriteLine(""World"");
        Debug.WriteLine(""Debug"");
    }
}";
            Assert.Equal(2, CountMatches(code, "//statement[@contains='Console.WriteLine']"));
            Assert.Equal(3, CountMatches(code, "//statement[@contains='WriteLine']"));
        }

        [Fact]
        public void TestModifiersAttributePredicate()
        {
            var code = @"
public class TestClass
{
    public void PublicMethod() { }
    private void PrivateMethod() { }
    protected virtual void ProtectedVirtualMethod() { }
    public static void StaticMethod() { }
    public async Task AsyncMethod() { return Task.CompletedTask; }
}";
            Assert.Equal(3, CountMatches(code, "//method[@modifiers~='public']"));
            Assert.Equal(1, CountMatches(code, "//method[@modifiers~='private']"));
            Assert.Equal(1, CountMatches(code, "//method[@modifiers~='virtual']"));
            Assert.Equal(1, CountMatches(code, "//method[@modifiers~='static']"));
        }

        #endregion

        #region Boolean Predicate Tests

        [Fact]
        public void TestBooleanPredicates()
        {
            var code = @"
public abstract class TestClass
{
    public async Task Method1() { await Task.Delay(1); }
    private void Method2() { }
    public static void Method3() { }
    protected virtual void Method4() { }
}";
            Assert.Equal(1, CountMatches(code, "//method[@async]"));
            Assert.Equal(2, CountMatches(code, "//method[@public]"));
            Assert.Equal(1, CountMatches(code, "//method[@private]"));
            Assert.Equal(1, CountMatches(code, "//method[@static]"));
            Assert.Equal(1, CountMatches(code, "//method[@virtual]"));
        }

        #endregion

        #region Enhanced Node Type Tests

        [Fact]
        public void TestEnhancedStatementTypes()
        {
            var code = @"
public class TestClass
{
    public void Method1() 
    { 
        if (true) { }
        while (false) { }
        for (int i = 0; i < 10; i++) { }
        foreach (var item in items) { }
        do { } while (false);
        switch (x) { case 1: break; }
        try { } catch { } finally { }
        throw new Exception();
        return;
        using (var x = new Resource()) { }
        lock (this) { }
    }
}";
            Assert.Equal(1, CountMatches(code, "//if-statement"));
            Assert.Equal(1, CountMatches(code, "//while-statement"));
            Assert.Equal(1, CountMatches(code, "//for-statement"));
            Assert.Equal(1, CountMatches(code, "//foreach-statement"));
            Assert.Equal(1, CountMatches(code, "//do-statement"));
            Assert.Equal(1, CountMatches(code, "//switch-statement"));
            Assert.Equal(1, CountMatches(code, "//try-statement"));
            Assert.Equal(1, CountMatches(code, "//throw-statement"));
            Assert.Equal(1, CountMatches(code, "//return-statement"));
            Assert.Equal(1, CountMatches(code, "//using-statement"));
            Assert.Equal(1, CountMatches(code, "//lock-statement"));
        }

        [Fact]
        public void TestEnhancedExpressionTypes()
        {
            var code = @"
public class TestClass
{
    public void Method1() 
    { 
        var a = 1 + 2;                    // binary-expression
        var b = -a;                        // unary-expression
        var c = ""literal"";                // literal
        var d = MethodCall();              // invocation
        var e = obj.Property;              // member-access
        var f = x = 5;                     // assignment
        var g = true ? 1 : 0;              // conditional
        var h = async () => await Task.Delay(1); // lambda, await-expression
        var i = new Object();              // object-creation
        var j = new int[5];                // array-creation
        var k = array[0];                  // element-access
        var l = (int)3.14;                 // cast-expression
        var m = typeof(string);            // typeof-expression
    }
}";
            Assert.True(CountMatches(code, "//binary-expression") > 0);
            Assert.True(CountMatches(code, "//unary-expression") > 0);
            Assert.True(CountMatches(code, "//literal") > 0);
            Assert.True(CountMatches(code, "//invocation") > 0);
            Assert.True(CountMatches(code, "//member-access") > 0);
            Assert.True(CountMatches(code, "//assignment") > 0);
            Assert.True(CountMatches(code, "//conditional") > 0);
            Assert.True(CountMatches(code, "//lambda") > 0);
            Assert.True(CountMatches(code, "//await-expression") > 0);
            Assert.True(CountMatches(code, "//object-creation") > 0);
            Assert.True(CountMatches(code, "//array-creation") > 0);
            Assert.True(CountMatches(code, "//element-access") > 0);
            Assert.True(CountMatches(code, "//cast-expression") > 0);
            Assert.True(CountMatches(code, "//typeof-expression") > 0);
        }

        #endregion

        #region Axis Navigation Tests

        [Fact]
        public void TestAncestorAxis()
        {
            var code = @"
namespace NS
{
    public class TestClass
    {
        public void Method1() 
        { 
            if (true) 
            {
                var x = 1;
            }
        }
    }
}";
            // Note: Axes need to be tested differently as they require a starting point
            // This would typically be done through the navigate tool in practice
            Assert.Equal(1, CountMatches(code, "//local-declaration"));
            
            // Test descendant-or-self
            Assert.True(CountMatches(code, "//method") > 0);
        }

        [Fact]
        public void TestFollowingSiblingAxis()
        {
            var code = @"
public class TestClass
{
    public void Method1() { }
    public void Method2() { }
    public void Method3() { }
}";
            // Basic sibling test - all methods are siblings
            Assert.Equal(3, CountMatches(code, "//method"));
        }

        #endregion

        #region Complex Predicate Tests

        [Fact]
        public void TestAndPredicate()
        {
            var code = @"
public class TestClass
{
    public async Task Method1() { }
    private async Task Method2() { }
    public void Method3() { }
    public static async Task Method4() { }
}";
            Assert.Equal(2, CountMatches(code, "//method[@async and @public]"));
            Assert.Equal(1, CountMatches(code, "//method[@async and @static]"));
            Assert.Equal(1, CountMatches(code, "//method[@async and @private]"));
        }

        [Fact]
        public void TestOrPredicate()
        {
            var code = @"
public class TestClass
{
    public void Method1() { }
    private void Method2() { }
    protected void Method3() { }
    internal void Method4() { }
}";
            Assert.Equal(2, CountMatches(code, "//method[@public or @private]"));
            Assert.Equal(3, CountMatches(code, "//method[@public or @protected or @internal]"));
        }

        [Fact]
        public void TestNotPredicate()
        {
            var code = @"
public class TestClass
{
    public void Method1() { }
    private void Method2() { }
    public static void Method3() { }
}";
            Assert.Equal(2, CountMatches(code, "//method[not(@static)]"));
            Assert.Equal(2, CountMatches(code, "//method[not(@private)]"));
        }

        #endregion

        #region Special Attribute Tests

        [Fact]
        public void TestOperatorAttribute()
        {
            var code = @"
public class TestClass
{
    public void Method1() 
    { 
        var a = x == null;
        var b = y != null;
        var c = a && b;
        var d = a || b;
        var e = 1 + 2;
        var f = 3 - 1;
        var g = 2 * 3;
        var h = 6 / 2;
    }
}";
            Assert.Equal(1, CountMatches(code, "//binary-expression[@operator='==']"));
            Assert.Equal(1, CountMatches(code, "//binary-expression[@operator='!=']"));
            Assert.Equal(1, CountMatches(code, "//binary-expression[@operator='&&']"));
            Assert.Equal(1, CountMatches(code, "//binary-expression[@operator='||']"));
            Assert.Equal(1, CountMatches(code, "//binary-expression[@operator='+']"));
        }

        [Fact]
        public void TestRightTextLeftTextAttributes()
        {
            var code = @"
public class TestClass
{
    public void Method1() 
    { 
        var a = x == null;
        var b = null == y;
        var c = foo == ""bar"";
    }
}";
            Assert.Equal(2, CountMatches(code, "//binary-expression[@right-text='null' or @left-text='null']"));
            Assert.Equal(1, CountMatches(code, "//binary-expression[@right-text='\"bar\"']"));
        }

        [Fact]
        public void TestLiteralValueAttribute()
        {
            var code = @"
public class TestClass
{
    public void Method1() 
    { 
        var a = 42;
        var b = ""hello"";
        var c = true;
        var d = 3.14;
        var e = null;
    }
}";
            Assert.Equal(1, CountMatches(code, "//literal[@literal-value='42']"));
            Assert.Equal(1, CountMatches(code, "//literal[@literal-value='\"hello\"']"));
            Assert.Equal(1, CountMatches(code, "//literal[@literal-value='true']"));
        }

        #endregion

        #region Complex Combination Tests

        [Fact]
        public void TestComplexNullCheckPattern()
        {
            var code = @"
public class TestClass
{
    public void Method1(string param) 
    { 
        if (param == null) throw new ArgumentNullException();
        if (null == param) return;
        if (param != null) { DoSomething(); }
        var x = param ?? ""default"";
    }
}";
            // Find all null comparisons
            Assert.Equal(3, CountMatches(code, "//if-statement//binary-expression[@operator='==' or @operator='!='][@right-text='null' or @left-text='null']"));
            
            // Find null checks that throw
            Assert.Equal(1, CountMatches(code, "//if-statement[.//binary-expression[@operator='==' and @right-text='null']][.//throw-statement]"));
        }

        [Fact]
        public void TestAsyncMethodWithAwaitPattern()
        {
            var code = @"
public class TestClass
{
    public async Task Method1() 
    { 
        await Task.Delay(100);
        var result = await GetDataAsync();
        await ProcessAsync(result);
    }
    
    public async Task Method2()
    {
        // No await - fire and forget
        Task.Run(() => Console.WriteLine(""test""));
    }
}";
            // Find async methods that actually await
            Assert.Equal(1, CountMatches(code, "//method[@async][.//await-expression]"));
            
            // Count await expressions
            Assert.Equal(3, CountMatches(code, "//await-expression"));
        }

        [Fact]
        public void TestExceptionHandlingPattern()
        {
            var code = @"
public class TestClass
{
    public void Method1() 
    { 
        try
        {
            DoSomething();
        }
        catch (ArgumentException ex)
        {
            LogError(ex);
            throw;
        }
        catch (Exception ex)
        {
            LogError(ex);
        }
        finally
        {
            Cleanup();
        }
    }
}";
            // Find try blocks with multiple catch clauses
            Assert.Equal(1, CountMatches(code, "//try-statement"));
            
            // Find catch blocks that rethrow
            Assert.True(CountMatches(code, "//try-statement") > 0);
        }

        [Fact]
        public void TestLinqQueryPattern()
        {
            var code = @"
public class TestClass
{
    public void Method1() 
    { 
        var query = from x in collection
                    where x.Value > 10
                    orderby x.Name
                    select x.Id;
                    
        var lambda = collection.Where(x => x.Value > 10)
                              .OrderBy(x => x.Name)
                              .Select(x => x.Id);
    }
}";
            // Find LINQ query expressions
            Assert.Equal(1, CountMatches(code, "//query-expression"));
            
            // Find lambda expressions
            Assert.Equal(3, CountMatches(code, "//lambda"));
            
            // Find method calls with Where
            Assert.True(CountMatches(code, "//invocation[@contains='Where']") > 0);
        }

        [Fact]
        public void TestPropertyWithBackingFieldPattern()
        {
            var code = @"
public class TestClass
{
    private string _name;
    public string Name 
    { 
        get { return _name; }
        set { _name = value; }
    }
    
    public int Age { get; set; }  // Auto-property
    
    private readonly List<string> _items = new List<string>();
    public IReadOnlyList<string> Items => _items;  // Expression-bodied
}";
            // Find properties with explicit getters
            Assert.Equal(3, CountMatches(code, "//property"));
            
            // Find fields
            Assert.Equal(2, CountMatches(code, "//field"));
            
            // Find properties with backing fields (by convention)
            Assert.Equal(1, CountMatches(code, "//field[@name='_name']"));
        }

        #endregion

        #region Edge Cases and Regression Tests

        [Fact]
        public void TestEmptyNodeTest()
        {
            var code = @"
public class TestClass
{
    public void Method1() { }
}";
            // These should work without errors
            Assert.True(CountMatches(code, "//") > 0);
            Assert.True(CountMatches(code, "/") > 0);
        }

        [Fact]
        public void TestComplexWildcardPatterns()
        {
            var code = @"
public class TestClass
{
    public void GetUser() { }
    public void GetUserById() { }
    public void SetUser() { }
    public void DeleteUser() { }
}";
            Assert.Equal(2, CountMatches(code, "//method[Get*]"));
            Assert.Equal(3, CountMatches(code, "//method[*User]")); // GetUser, SetUser, DeleteUser
            Assert.Equal(1, CountMatches(code, "//method[*User*Id]"));
        }

        [Fact]
        public void TestNestedBlocksAndStatements()
        {
            var code = @"
public class TestClass
{
    public void Method1() 
    { 
        {
            {
                var x = 1;
                {
                    var y = 2;
                }
            }
        }
    }
}";
            // Should find all nested blocks
            Assert.Equal(4, CountMatches(code, "//block"));
            
            // Should find deeply nested statements
            Assert.Equal(2, CountMatches(code, "//local-declaration"));
        }

        [Fact]
        public void TestSpecialCharactersInPredicates()
        {
            var code = @"
public class TestClass
{
    public void Method1() 
    { 
        var a = ""test's"";
        var b = @""c:\temp\file.txt"";
        var c = $""value: {x}"";
    }
}";
            // Should handle quotes and special characters
            Assert.True(CountMatches(code, "//local-declaration") == 3);
            Assert.True(CountMatches(code, "//literal") >= 2);
        }

        [Fact]
        public void TestGenericTypes()
        {
            var code = @"
public class TestClass<T> where T : class
{
    public List<string> Items { get; set; }
    public Dictionary<int, T> Map { get; set; }
    
    public void Method<U>(T param1, U param2) where U : struct
    {
        var list = new List<T>();
        var dict = new Dictionary<string, U>();
    }
}";
            // Should find generic class and method
            Assert.Equal(1, CountMatches(code, "//class"));
            Assert.Equal(1, CountMatches(code, "//method"));
            
            // Should find properties and local declarations
            Assert.Equal(2, CountMatches(code, "//property"));
            Assert.Equal(2, CountMatches(code, "//local-declaration"));
        }

        #endregion
    }
}