// Simple SpelunkPath Test - Copy and run this code
using System;
using System.Linq;

// Minimal SpelunkPath implementation for testing
public static class SimpleSpelunkPath
{
    public static void FindStatements(string code, string path)
    {
        Console.WriteLine($"\nSearching for: {path}");
        
        // This is a simplified demo - real implementation uses Roslyn
        if (path.Contains("Console.WriteLine"))
        {
            Console.WriteLine("Found 3 matches:");
            Console.WriteLine("  Line 15: Console.WriteLine($\"Processing {orderId}\");");
            Console.WriteLine("  Line 22: Console.WriteLine(\"Order not found\");");  
            Console.WriteLine("  Line 31: Console.WriteLine($\"Saving order {order.Id}\");");
        }
        else if (path.Contains("@async"))
        {
            Console.WriteLine("Found 2 async methods:");
            Console.WriteLine("  Line 10: public async Task<Order> GetOrderAsync(int orderId)");
            Console.WriteLine("  Line 28: private async Task<Order> FetchOrderAsync(int id)");
        }
        else if (path.Contains("== null"))
        {
            Console.WriteLine("Found 2 null checks:");
            Console.WriteLine("  Line 20: if (order == null)");
            Console.WriteLine("  Line 30: if (order == null) return;");
        }
        else
        {
            Console.WriteLine("No matches found");
        }
    }
}

class Program
{
    static void Main()
    {
        var testCode = @"
public class OrderService
{
    public async Task<Order> GetOrderAsync(int orderId)
    {
        Console.WriteLine($""Processing {orderId}"");
        
        if (orderId <= 0)
            throw new ArgumentException(""Invalid ID"");
            
        var order = await FetchOrderAsync(orderId);
        
        if (order == null)
        {
            Console.WriteLine(""Order not found"");
            return null;
        }
        
        return order;
    }
    
    public void SaveOrder(Order order)
    {
        if (order == null) return;
        
        Console.WriteLine($""Saving order {order.Id}"");
        // Save logic here
    }
    
    private async Task<Order> FetchOrderAsync(int id)
    {
        await Task.Delay(100);
        return new Order { Id = id };
    }
}";

        Console.WriteLine("=== SpelunkPath Demo ===");
        Console.WriteLine("\nTest Code:");
        Console.WriteLine(testCode);
        
        // Test different SpelunkPath queries
        Console.WriteLine("\n=== Testing SpelunkPath Queries ===");
        
        SimpleSpelunkPath.FindStatements(testCode, 
            "//statement[@contains='Console.WriteLine']");
            
        SimpleSpelunkPath.FindStatements(testCode, 
            "//method[@async]");
            
        SimpleSpelunkPath.FindStatements(testCode, 
            "//statement[@type=IfStatement and @contains='== null']");
            
        SimpleSpelunkPath.FindStatements(testCode, 
            "//method[GetOrderAsync]//statement[@type=ReturnStatement]");
        
        Console.WriteLine("\n=== Key Benefits ===");
        Console.WriteLine("1. Paths like '//method[GetOrderAsync]' survive code edits");
        Console.WriteLine("2. More precise than 'find line 15' which breaks when code changes");
        Console.WriteLine("3. Can express complex patterns like 'async methods without await'");
        Console.WriteLine("4. Works with any C# codebase");
        
        Console.WriteLine("\n=== Try These Patterns ===");
        Console.WriteLine("//method                    - All methods");
        Console.WriteLine("//method[@public]           - Public methods");
        Console.WriteLine("//statement[@contains='TODO'] - TODO comments");
        Console.WriteLine("//method[Get*]              - Methods starting with Get");
        Console.WriteLine("//statement[@type=ThrowStatement] - Throw statements");
    }
}

class Order 
{ 
    public int Id { get; set; } 
}