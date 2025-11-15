using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Spelunk.Server.SpelunkPath;

class ComplexSpelunkPathDemo
{
    static void Main()
    {
        // Real-world code with various async patterns
        var sourceCode = @"
using System;
using System.Threading.Tasks;
using System.Linq;

namespace MyApp.Services
{
    public class DataService
    {
        private readonly ILogger _logger;
        private readonly ICache _cache;
        
        // GOOD: Async method with await
        public async Task<User> GetUserAsync(int id)
        {
            var cached = await _cache.GetAsync<User>($""user:{id}"");
            if (cached != null) 
                return cached;
                
            var user = await FetchFromDatabaseAsync(id);
            await _cache.SetAsync($""user:{id}"", user);
            return user;
        }
        
        // BAD: Async method without await (just returns task)
        public async Task<Order> GetOrderAsync(int id)
        {
            _logger.Log($""Getting order {id}"");
            
            // This is suspicious - async method but no await!
            return FetchOrderFromDatabase(id);
        }
        
        // GOOD: Async void for event handler with proper await
        private async void OnDataChanged(object sender, EventArgs e)
        {
            try 
            {
                await RefreshCacheAsync();
                await NotifySubscribersAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ""Failed to handle data change"");
            }
        }
        
        // BAD: Async method that only awaits in one branch
        public async Task<bool> ValidateOrderAsync(Order order)
        {
            if (order == null)
            {
                _logger.LogWarning(""Null order provided"");
                return false;  // No await in this path!
            }
            
            if (order.Items.Count == 0)
            {
                return false;  // No await in this path either!
            }
            
            // Only this path has await
            var customer = await GetCustomerAsync(order.CustomerId);
            return customer != null && customer.IsActive;
        }
        
        // GOOD: Properly awaited with multiple awaits
        public async Task<Report> GenerateReportAsync(DateTime from, DateTime to)
        {
            var orders = await GetOrdersInRangeAsync(from, to);
            var customers = await GetCustomersAsync(orders.Select(o => o.CustomerId));
            
            var report = new Report();
            foreach (var order in orders)
            {
                var customer = customers.FirstOrDefault(c => c.Id == order.CustomerId);
                report.AddEntry(order, customer);
            }
            
            await report.SaveAsync();
            return report;
        }
        
        // BAD: Fire and forget without await
        public async Task ProcessBatchAsync(List<int> ids)
        {
            foreach (var id in ids)
            {
                // Fire and forget - dangerous!
                ProcessItemAsync(id);  // Missing await
            }
            
            _logger.Log(""Batch processing started"");
        }
        
        // Helper methods
        private Task<User> FetchFromDatabaseAsync(int id) => Task.FromResult(new User());
        private Task<Order> FetchOrderFromDatabase(int id) => Task.FromResult(new Order());
        private async Task RefreshCacheAsync() => await Task.Delay(100);
        private async Task NotifySubscribersAsync() => await Task.Delay(50);
        private async Task<Customer> GetCustomerAsync(int id) => new Customer();
        private async Task ProcessItemAsync(int id) => await Task.Delay(10);
    }
}";

        Console.WriteLine("=== Complex SpelunkPath Query Demo ===\n");

        // The complex query: Find async methods that might be missing await
        var complexQuery = @"//method[@async and (
            not(.//expression[@type=AwaitExpression]) or
            .//statement[@type=ReturnStatement and not(@contains='await')]
        )]";

        Console.WriteLine("QUERY: Find potentially problematic async methods");
        Console.WriteLine($"Path: {complexQuery}");
        Console.WriteLine("\nThis finds:");
        Console.WriteLine("- Async methods with NO await expressions at all");
        Console.WriteLine("- Async methods with return statements that don't use await");
        Console.WriteLine("\n" + new string('=', 80) + "\n");

        try
        {
            var results = SpelunkPath.Find(sourceCode, complexQuery).ToList();
            
            Console.WriteLine($"Found {results.Count} suspicious async methods:\n");

            foreach (var result in results)
            {
                Console.WriteLine($"❌ SUSPICIOUS: {result.NodeType}");
                Console.WriteLine($"   Location: Line {result.Location.StartLine}");
                Console.WriteLine($"   Stable Path: {result.Path}");
                
                // Get method signature (first line)
                var lines = result.Text.Split('\n');
                var signature = lines[0].Trim();
                Console.WriteLine($"   Signature: {signature}");
                
                // Analyze why it's suspicious
                var hasAwait = result.Text.Contains("await");
                var hasReturn = result.Text.Contains("return") && !result.Text.Contains("return await");
                
                Console.WriteLine("   Issues:");
                if (!hasAwait)
                {
                    Console.WriteLine("     - No await expressions found");
                }
                if (hasReturn)
                {
                    Console.WriteLine("     - Has return without await (Task not awaited)");
                }
                
                Console.WriteLine();
            }

            // Also show the GOOD async methods for comparison
            Console.WriteLine("\n" + new string('=', 80) + "\n");
            Console.WriteLine("For comparison, here are the GOOD async methods:");
            
            var goodAsyncQuery = @"//method[@async and .//expression[@type=AwaitExpression]]";
            var goodResults = SpelunkPath.Find(sourceCode, goodAsyncQuery).ToList();
            
            foreach (var result in goodResults)
            {
                var lines = result.Text.Split('\n');
                var signature = lines[0].Trim();
                var awaitCount = result.Text.Split(new[] { "await" }, StringSplitOptions.None).Length - 1;
                
                Console.WriteLine($"✅ GOOD: {signature}");
                Console.WriteLine($"   Has {awaitCount} await expression(s)");
            }

        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }

        // Show other useful complex queries
        Console.WriteLine("\n" + new string('=', 80) + "\n");
        Console.WriteLine("OTHER USEFUL COMPLEX QUERIES:\n");

        Console.WriteLine("1. Find methods with multiple exit points:");
        Console.WriteLine("   //method[count(.//statement[@type=ReturnStatement]) > 2]");
        
        Console.WriteLine("\n2. Find if-statements checking for null without using pattern matching:");
        Console.WriteLine("   //statement[@type=IfStatement and @contains='== null' and not(@contains='is null')]");
        
        Console.WriteLine("\n3. Find catch blocks that might be swallowing exceptions:");
        Console.WriteLine("   //catch[not(.//statement[@type=ThrowStatement]) and not(.//expression[@contains='Log'])]");
        
        Console.WriteLine("\n4. Find fire-and-forget async calls (dangerous pattern):");
        Console.WriteLine("   //expression[@type=InvocationExpression and @contains='Async(' and not(ancestor::expression[@type=AwaitExpression])]");
        
        Console.WriteLine("\n5. Find classes with too many dependencies (constructor with >5 parameters):");
        Console.WriteLine("   //constructor[count(parameter) > 5]");
    }
}

// Dummy classes for compilation
public interface ILogger 
{ 
    void Log(string message);
    void LogWarning(string message);
    void LogError(Exception ex, string message);
}
public interface ICache 
{ 
    Task<T> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value);
}
public class User { }
public class Order 
{ 
    public int CustomerId { get; set; }
    public List<object> Items { get; set; } = new List<object>();
}
public class Customer 
{ 
    public int Id { get; set; }
    public bool IsActive { get; set; }
}
public class Report 
{ 
    public void AddEntry(Order order, Customer customer) { }
    public Task SaveAsync() => Task.CompletedTask;
}