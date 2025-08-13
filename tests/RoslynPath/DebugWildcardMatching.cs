using System;
using System.Text.RegularExpressions;
using Xunit;

namespace McpRoslyn.Tests.RoslynPath
{
    public class DebugWildcardMatching
    {
        [Fact]
        public void TestWildcardRegex()
        {
            // Simulate MatchesWildcard logic
            var pattern = "*User";
            var regexPattern = "^" + Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";
            
            Console.WriteLine($"Pattern: {pattern}");
            Console.WriteLine($"Regex: {regexPattern}");
            
            // Test various method names
            var names = new[] { "GetUser", "GetUserById", "SetUser", "DeleteUser" };
            foreach (var name in names)
            {
                var matches = Regex.IsMatch(name, regexPattern, RegexOptions.IgnoreCase);
                Console.WriteLine($"  {name}: {matches}");
            }
            
            // Should match only GetUser and DeleteUser (ending with "User" exactly)
            Assert.True(Regex.IsMatch("GetUser", regexPattern, RegexOptions.IgnoreCase));
            Assert.True(Regex.IsMatch("DeleteUser", regexPattern, RegexOptions.IgnoreCase));
            Assert.True(Regex.IsMatch("SetUser", regexPattern, RegexOptions.IgnoreCase)); // This is matching!
            Assert.False(Regex.IsMatch("GetUserById", regexPattern, RegexOptions.IgnoreCase));
        }
    }
}