using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System.Text.Json;
using Xunit;
using CS = Microsoft.CodeAnalysis.CSharp.Syntax;

namespace McpRoslyn.Server.Tests.Integration
{
    [Collection("AST Navigation Tests")]
    public class AstNavigationIntegrationTests
    {
        private const string TestCode = @"
using System;

namespace TestNamespace
{
    public class Calculator
    {
        private int _value;
        
        public int Value { get; set; }
        
        public Calculator()
        {
            _value = 0;
        }
        
        public Calculator(int initialValue)
        {
            _value = initialValue;
        }
        
        public int Add(int a, int b)
        {
            var result = a + b;
            if (result > 100)
            {
                Console.WriteLine(""Large result: "" + result);
            }
            return result;
        }
        
        public int Multiply(int a, int b)
        {
            return a * b;
        }
        
        public bool IsEven(int number)
        {
            return number % 2 == 0;
        }
    }
    
    public class MathUtils
    {
        public static double Pi = 3.14159;
        
        public static int Square(int x)
        {
            return x * x;
        }
        
        public static bool IsPrime(int n)
        {
            if (n <= 1) return false;
            for (int i = 2; i * i <= n; i++)
            {
                if (n % i == 0) return false;
            }
            return true;
        }
    }
}";

        private class TestWorkspaceManager
        {
            private readonly Dictionary<string, Workspace> _workspaces = new();
            
            public string LoadTestWorkspace()
            {
                var workspaceId = Guid.NewGuid().ToString();
                var tree = CSharpSyntaxTree.ParseText(TestCode);
                var compilation = CSharpCompilation.Create("TestAssembly", new[] { tree });
                
                // For this test, we'll create a mock workspace
                // In real scenarios, this would be a proper MSBuild workspace
                _workspaces[workspaceId] = null!; // Mock workspace
                
                return workspaceId;
            }
            
            public SyntaxTree GetSyntaxTree()
            {
                return CSharpSyntaxTree.ParseText(TestCode);
            }
        }

        private class MockMcpJsonRpcServer
        {
            private readonly TestWorkspaceManager _workspaceManager = new();
            private string? _workspaceId;

            public async Task<JsonElement> LoadWorkspaceAsync(string path)
            {
                _workspaceId = _workspaceManager.LoadTestWorkspace();
                
                var result = new
                {
                    Id = _workspaceId,
                    Path = path,
                    Type = "Project",
                    Status = "Loaded"
                };
                
                return JsonSerializer.SerializeToElement(result);
            }

            public async Task<JsonElement> QuerySyntaxAsync(string roslynPath, string? file = null)
            {
                if (_workspaceId == null)
                    throw new InvalidOperationException("Workspace not loaded");

                var tree = _workspaceManager.GetSyntaxTree();
                var evaluator = new RoslynPath.RoslynPathEvaluator(tree);
                var results = evaluator.Evaluate(roslynPath);
                
                var matches = results.Select(node => new
                {
                    node = new
                    {
                        type = RoslynPath.EnhancedNodeTypes.GetDetailedNodeTypeName(node),
                        text = node.ToString().Split('\n')[0] + "...",
                        location = new
                        {
                            file = file ?? "test.cs",
                            line = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                            column = node.GetLocation().GetLineSpan().StartLinePosition.Character + 1
                        }
                    }
                }).ToArray();

                return JsonSerializer.SerializeToElement(new { matches });
            }

            public async Task<JsonElement> NavigateAsync(string filePath, int line, int column, string path, bool returnPath = false)
            {
                if (_workspaceId == null)
                    throw new InvalidOperationException("Workspace not loaded");

                var tree = _workspaceManager.GetSyntaxTree();
                var root = tree.GetRoot();
                var sourceText = tree.GetText();
                var position = sourceText.Lines.GetPosition(new LinePosition(line - 1, column - 1));
                var startNode = root.FindNode(new TextSpan(position, 0));
                
                var targetNode = NavigateFromNode(startNode, path);
                
                if (targetNode == null)
                {
                    return JsonSerializer.SerializeToElement(new { navigatedTo = (object?)null });
                }
                
                var lineSpan = targetNode.GetLocation().GetLineSpan();
                var result = new
                {
                    type = RoslynPath.EnhancedNodeTypes.GetDetailedNodeTypeName(targetNode),
                    name = GetNodeName(targetNode),
                    location = new
                    {
                        file = filePath,
                        line = lineSpan.StartLinePosition.Line + 1,
                        column = lineSpan.StartLinePosition.Character + 1
                    },
                    path = returnPath ? BuildNodePath(targetNode) : null
                };

                return JsonSerializer.SerializeToElement(new { navigatedTo = result });
            }

            public async Task<JsonElement> GetAstAsync(string filePath, int depth = 3)
            {
                if (_workspaceId == null)
                    throw new InvalidOperationException("Workspace not loaded");

                var tree = _workspaceManager.GetSyntaxTree();
                var root = tree.GetRoot();
                
                var ast = BuildAstNode(root, depth, 0);
                return JsonSerializer.SerializeToElement(new { ast });
            }

            private SyntaxNode? NavigateFromNode(SyntaxNode startNode, string path)
            {
                var current = startNode;
                var parts = path.Split(new[] { "::" }, StringSplitOptions.RemoveEmptyEntries);
                
                foreach (var part in parts)
                {
                    if (current == null) return null;
                    
                    var match = System.Text.RegularExpressions.Regex.Match(part, @"^(\w+)(?:\[(\d+)\])?$");
                    if (!match.Success) continue;
                    
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
                        _ => null
                    };
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

            private string BuildNodePath(SyntaxNode node)
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

            private object BuildAstNode(SyntaxNode node, int maxDepth, int currentDepth)
            {
                var result = new Dictionary<string, object>
                {
                    ["type"] = RoslynPath.EnhancedNodeTypes.GetDetailedNodeTypeName(node),
                    ["kind"] = node.Kind().ToString()
                };
                
                var name = GetNodeName(node);
                if (!string.IsNullOrEmpty(name))
                    result["name"] = name;
                
                if (currentDepth < maxDepth)
                {
                    var children = new List<object>();
                    foreach (var child in node.ChildNodes())
                    {
                        children.Add(BuildAstNode(child, maxDepth, currentDepth + 1));
                    }
                    
                    if (children.Count > 0)
                        result["children"] = children;
                }
                
                return result;
            }
        }

        [Fact]
        public async Task FullWorkflow_QuerySyntax_FindsAllClasses()
        {
            // Arrange
            var server = new MockMcpJsonRpcServer();
            await server.LoadWorkspaceAsync("test.csproj");

            // Act
            var result = await server.QuerySyntaxAsync("//class");

            // Assert
            Assert.True(result.TryGetProperty("matches", out var matchesElement));
            var matches = matchesElement.EnumerateArray().ToList();
            Assert.Equal(2, matches.Count);
            
            var classNames = matches.Select(m => 
                m.GetProperty("node").GetProperty("type").GetString()).ToList();
            Assert.All(classNames, name => Assert.Equal("class", name));
        }

        [Fact]
        public async Task FullWorkflow_QuerySyntax_FindsSpecificMethod()
        {
            // Arrange
            var server = new MockMcpJsonRpcServer();
            await server.LoadWorkspaceAsync("test.csproj");

            // Act
            var result = await server.QuerySyntaxAsync("//method[@name='Add']");

            // Assert
            Assert.True(result.TryGetProperty("matches", out var matchesElement));
            var matches = matchesElement.EnumerateArray().ToList();
            Assert.Single(matches);
            
            var match = matches[0];
            Assert.Equal("method", match.GetProperty("node").GetProperty("type").GetString());
        }

        [Fact]
        public async Task FullWorkflow_QuerySyntax_FindsBinaryExpressions()
        {
            // Arrange
            var server = new MockMcpJsonRpcServer();
            await server.LoadWorkspaceAsync("test.csproj");

            // Act
            var result = await server.QuerySyntaxAsync("//binary-expression");

            // Assert
            Assert.True(result.TryGetProperty("matches", out var matchesElement));
            var matches = matchesElement.EnumerateArray().ToList();
            Assert.True(matches.Count >= 5); // Multiple binary expressions in the code
        }

        [Fact]
        public async Task FullWorkflow_Navigate_FromLiteralToParent()
        {
            // Arrange
            var server = new MockMcpJsonRpcServer();
            await server.LoadWorkspaceAsync("test.csproj");

            // Act - Navigate from a position inside a literal to its parent
            var result = await server.NavigateAsync("test.cs", 24, 25, "parent", true);

            // Assert
            Assert.True(result.TryGetProperty("navigatedTo", out var navigatedElement));
            Assert.NotEqual(JsonValueKind.Null, navigatedElement.ValueKind);
            
            var navigated = navigatedElement;
            Assert.True(navigated.TryGetProperty("type", out var typeElement));
            Assert.True(navigated.TryGetProperty("path", out var pathElement));
            
            // Should navigate to some parent node
            Assert.NotNull(typeElement.GetString());
            Assert.NotNull(pathElement.GetString());
        }

        [Fact]
        public async Task FullWorkflow_Navigate_ToAncestor()
        {
            // Arrange
            var server = new MockMcpJsonRpcServer();
            await server.LoadWorkspaceAsync("test.csproj");

            // Act - Navigate to ancestor
            var result = await server.NavigateAsync("test.cs", 24, 25, "ancestor[2]", false);

            // Assert
            Assert.True(result.TryGetProperty("navigatedTo", out var navigatedElement));
            if (navigatedElement.ValueKind != JsonValueKind.Null)
            {
                Assert.True(navigatedElement.TryGetProperty("type", out var typeElement));
                Assert.NotNull(typeElement.GetString());
            }
        }

        [Fact]
        public async Task FullWorkflow_GetAst_ReturnsCorrectStructure()
        {
            // Arrange
            var server = new MockMcpJsonRpcServer();
            await server.LoadWorkspaceAsync("test.csproj");

            // Act
            var result = await server.GetAstAsync("test.cs", 2);

            // Assert
            Assert.True(result.TryGetProperty("ast", out var astElement));
            Assert.True(astElement.TryGetProperty("type", out var typeElement));
            Assert.Equal("compilationunit", typeElement.GetString());
            
            Assert.True(astElement.TryGetProperty("children", out var childrenElement));
            var children = childrenElement.EnumerateArray().ToList();
            Assert.NotEmpty(children);
        }

        [Fact]
        public async Task FullWorkflow_ComplexQuery_FindsMethodsWithSpecificParameters()
        {
            // Arrange
            var server = new MockMcpJsonRpcServer();
            await server.LoadWorkspaceAsync("test.csproj");

            // Act - Find all methods in Calculator class
            var result = await server.QuerySyntaxAsync("//class[@name='Calculator']//method");

            // Assert
            Assert.True(result.TryGetProperty("matches", out var matchesElement));
            var matches = matchesElement.EnumerateArray().ToList();
            Assert.True(matches.Count >= 4); // Add, Multiply, IsEven, plus constructors
        }

        [Fact]
        public async Task FullWorkflow_ChainedOperations_QueryThenNavigate()
        {
            // Arrange
            var server = new MockMcpJsonRpcServer();
            await server.LoadWorkspaceAsync("test.csproj");

            // Act 1 - Find all binary expressions
            var queryResult = await server.QuerySyntaxAsync("//binary-expression");
            Assert.True(queryResult.TryGetProperty("matches", out var matchesElement));
            var matches = matchesElement.EnumerateArray().ToList();
            Assert.NotEmpty(matches);

            // Act 2 - Navigate from first binary expression to its parent method
            var firstMatch = matches[0];
            var location = firstMatch.GetProperty("node").GetProperty("location");
            var line = location.GetProperty("line").GetInt32();
            var column = location.GetProperty("column").GetInt32();
            
            var navResult = await server.NavigateAsync("test.cs", line, column, "ancestor[5]", true);
            
            // Assert
            Assert.True(navResult.TryGetProperty("navigatedTo", out var navigatedElement));
            // Should be able to navigate to some ancestor (might be method, class, or namespace)
        }

        [Fact]
        public async Task FullWorkflow_ErrorHandling_InvalidQuery()
        {
            // Arrange
            var server = new MockMcpJsonRpcServer();
            await server.LoadWorkspaceAsync("test.csproj");

            // Act
            var result = await server.QuerySyntaxAsync("//nonexistent-node-type");

            // Assert
            Assert.True(result.TryGetProperty("matches", out var matchesElement));
            var matches = matchesElement.EnumerateArray().ToList();
            Assert.Empty(matches);
        }

        [Fact]
        public async Task FullWorkflow_ErrorHandling_InvalidNavigation()
        {
            // Arrange
            var server = new MockMcpJsonRpcServer();
            await server.LoadWorkspaceAsync("test.csproj");

            // Act - Try to navigate with invalid path
            var result = await server.NavigateAsync("test.cs", 1, 1, "invalid-axis", false);

            // Assert
            Assert.True(result.TryGetProperty("navigatedTo", out var navigatedElement));
            Assert.Equal(JsonValueKind.Null, navigatedElement.ValueKind);
        }

        [Fact]
        public async Task FullWorkflow_Performance_LargeQuery()
        {
            // Arrange
            var server = new MockMcpJsonRpcServer();
            await server.LoadWorkspaceAsync("test.csproj");

            // Act - Query all nodes (potentially expensive)
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = await server.QuerySyntaxAsync("//*");
            stopwatch.Stop();

            // Assert
            Assert.True(result.TryGetProperty("matches", out var matchesElement));
            var matches = matchesElement.EnumerateArray().ToList();
            Assert.NotEmpty(matches);
            
            // Should complete in reasonable time (adjust threshold as needed)
            Assert.True(stopwatch.ElapsedMilliseconds < 1000, 
                $"Query took {stopwatch.ElapsedMilliseconds}ms, expected < 1000ms");
        }
    }
}