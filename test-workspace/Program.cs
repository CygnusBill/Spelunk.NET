namespace TestProject;

public class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("Hello, World!");  // Missing using System; - should cause CS0103
        
        var calculator = new Calculator();
        var result = calculator.Add(5, 3);
        Console.WriteLine($"5 + 3 = {result}");
        
        // Introduce various errors for testing diagnostics:
        undeclaredVariable = 10;              // CS0103: undeclared variable
        int x = "string";                     // CS0029: type mismatch  
        calculator.NonExistentMethod();       // CS1061: missing method
        MissingType missing = null;           // CS0246: missing type
    }
    
    private void InvalidPlacement() { }       // CS0621: invalid placement in Main method
}

public class Calculator
{
    public int Add(int a, int b)
    {
        return a + b;
    }
    
    public int Subtract(int a, int b)
    {
        return a - b;
    }
}