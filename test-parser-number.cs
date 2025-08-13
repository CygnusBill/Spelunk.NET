using System;
using McpRoslyn.Server.RoslynPath2;

class TestParser {
    static void Main() {
        var parser = new RoslynPathParser2();
        try {
            var result = parser.Parse("//block/statement[1]");
            Console.WriteLine("Success: parsed position predicate");
        } catch (Exception ex) {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}
