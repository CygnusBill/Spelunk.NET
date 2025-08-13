using System;
using Xunit;
using McpRoslyn.Server.RoslynPath2;

namespace McpRoslyn.Tests.RoslynPath
{
    public class DebugTests
    {
        [Fact]
        public void TestPositionPredicateParsing()
        {
            var parser = new RoslynPathParser2();
            
            try
            {
                // Test simple position predicate
                var path1 = parser.Parse("//statement[1]");
                Assert.NotNull(path1);
            }
            catch (Exception ex)
            {
                // Help debug the issue
                throw new Exception($"Failed to parse '//statement[1]': {ex.Message}\nStack: {ex.StackTrace}", ex);
            }
            
            // Test last()
            var path2 = parser.Parse("//statement[last()]");
            Assert.NotNull(path2);
            
            // Test last()-1
            var path3 = parser.Parse("//statement[last()-1]");
            Assert.NotNull(path3);
        }
        
        [Fact]
        public void TestWildcardParsing()
        {
            var parser = new RoslynPathParser2();
            
            // These should all parse without error
            var path1 = parser.Parse("//method[Get*]");
            Assert.NotNull(path1);
            
            var path2 = parser.Parse("//method[*User]");
            Assert.NotNull(path2);
            
            var path3 = parser.Parse("//method[*User*]");
            Assert.NotNull(path3);
        }
    }
}