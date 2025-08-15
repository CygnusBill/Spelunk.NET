using System;

class Test {
    void Method() {
        // Top level in method
        Console.WriteLine("outer");
        
        if (true) {
            // Nested in if statement
            Console.WriteLine("nested");
        }
        
        // Back to top level
        Console.WriteLine("outer2");
    }
}
