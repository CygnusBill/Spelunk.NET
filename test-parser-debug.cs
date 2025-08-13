using System;
using System.Linq;
using McpRoslyn.Server.RoslynPath;

class TestParser
{
    static void Main()
    {
        var path = "//*[@name='foo']";
        Console.WriteLine($"Testing path: {path}");
        
        var lexer = new PathLexer(path);
        var tokens = lexer.Tokenize();
        
        Console.WriteLine("\nTokens:");
        foreach (var token in tokens)
        {
            Console.WriteLine($"  {token.Type}: '{token.Value}' at position {token.Position}");
        }
        
        Console.WriteLine("\nParsing...");
        var parser = new RoslynPathParser();
        try
        {
            var expression = parser.Parse(path);
            Console.WriteLine("Parse successful!");
            
            if (expression is PathSequence sequence)
            {
                Console.WriteLine($"Steps count: {sequence.Steps.Count}");
                foreach (var step in sequence.Steps)
                {
                    Console.WriteLine($"  Step: Type={step.Type}, NodeTest={step.NodeTest}, Predicates={step.Predicates.Count}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Parse failed: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
}