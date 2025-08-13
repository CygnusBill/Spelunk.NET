using System;
using System.Linq;
using McpRoslyn.Server.RoslynPath2;
using Xunit;

namespace McpRoslyn.Tests.RoslynPath
{
    public class DebugWildcardParsing
    {
        [Fact]
        public void DebugParsingTest()
        {
            var parser = new RoslynPathParser2();
            
            // Parse //method[*User]
            var path = parser.Parse("//method[*User]");
            
            Console.WriteLine($"Path has {path.Steps.Count} steps");
            var step = path.Steps[0];
            Console.WriteLine($"Step axis: {step.Axis}");
            Console.WriteLine($"Step node test: {step.NodeTest}");
            Console.WriteLine($"Step has {step.Predicates.Count} predicates");
            
            if (step.Predicates.Count > 0)
            {
                var pred = step.Predicates[0];
                Console.WriteLine($"Predicate type: {pred.GetType().Name}");
                if (pred is NameExpr name)
                {
                    Console.WriteLine($"Name pattern: '{name.Pattern}'");
                    Console.WriteLine($"Pattern contains *: {name.Pattern.Contains('*')}");
                }
            }
            
            Assert.NotNull(path);
        }
    }
}