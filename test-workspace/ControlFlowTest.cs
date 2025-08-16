using System;

public class ControlFlowTest
{
    public int TestMethod(int x)
    {
        if (x > 10)
        {
            return x * 2;
        }
        else if (x > 5)
        {
            return x + 10;
        }
        
        for (int i = 0; i < x; i++)
        {
            if (i == 3)
            {
                break;
            }
        }
        
        return 0;
    }
    
    public void TestExceptions(string input)
    {
        try
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }
            Console.WriteLine(input);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            throw;
        }
        finally
        {
            Console.WriteLine("Cleanup");
        }
    }
}
