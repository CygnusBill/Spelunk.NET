using System;
using McpRoslyn.Server.RoslynPath2;
using Xunit;

namespace McpRoslyn.Tests.RoslynPath
{
    public class DebugPathString  
    {
        [Fact]
        public void TestPathStringParsing()
        {
            var parser = new RoslynPathParser2();
            
            // Parse a path with nested predicate
            var path = parser.Parse("//if-statement[.//binary-expression[@operator='==']]");
            
            Console.WriteLine($"Path has {path.Steps.Count} steps");
            var step = path.Steps[0];
            Console.WriteLine($"Step has {step.Predicates.Count} predicates");
            
            if (step.Predicates[0] is PathPredicateExpr pathPred)
            {
                Console.WriteLine($"Path predicate string: '{pathPred.PathString}'");
                
                // Now try to parse this path string
                try
                {
                    var nestedPath = parser.Parse(pathPred.PathString);
                    Console.WriteLine($"Nested path parsed successfully with {nestedPath.Steps.Count} steps");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to parse nested path: {ex.Message}");
                }
            }
            
            Assert.NotNull(path);
        }
    }
}