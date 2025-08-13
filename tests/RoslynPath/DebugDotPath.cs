using System;
using McpRoslyn.Server.RoslynPath2;
using Xunit;

namespace McpRoslyn.Tests.RoslynPath
{
    public class DebugDotPath
    {
        [Fact]
        public void TestDotPathParsing()
        {
            var parser = new RoslynPathParser2();
            
            // Parse .//throw-statement
            var path = parser.Parse(".//throw-statement");
            
            Console.WriteLine($"Path is absolute: {path.IsAbsolute}");
            Console.WriteLine($"Path has {path.Steps.Count} steps");
            
            for (int i = 0; i < path.Steps.Count; i++)
            {
                var step = path.Steps[i];
                Console.WriteLine($"Step {i}: axis={step.Axis}, nodeTest='{step.NodeTest}'");
            }
            
            Assert.Equal(1, path.Steps.Count);
            Assert.Equal(StepAxis.DescendantOrSelf, path.Steps[0].Axis);
            Assert.Equal("throw-statement", path.Steps[0].NodeTest);
        }
    }
}