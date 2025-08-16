using System;
using System.Collections.Generic;

public class DataFlowTest
{
    private int field1 = 10;
    private string field2 = "test";
    
    public int TestBasicFlow(int input)
    {
        int local1 = input * 2;        // input flows in, local1 written
        int local2 = local1 + field1;  // local1 and field1 read, local2 written
        field1 = local2;                // local2 read, field1 written
        return local2;                  // local2 flows out
    }
    
    public void TestCapturedVariables()
    {
        int outer = 10;
        Action lambda = () =>
        {
            Console.WriteLine(outer);   // outer is captured
        };
        lambda();
    }
    
    public void TestRefAndOut(ref int refParam, out int outParam)
    {
        refParam = refParam * 2;       // refParam flows in and out
        outParam = 100;                // outParam flows out (always assigned)
    }
    
    public unsafe void TestUnsafePointers()
    {
        int value = 42;
        int* ptr = &value;              // value's address taken (unsafe)
        *ptr = 100;
    }
    
    public void TestConditionalFlow(bool condition)
    {
        int x;
        if (condition)
        {
            x = 10;                     // x written conditionally
        }
        else
        {
            x = 20;                     // x written conditionally
        }
        Console.WriteLine(x);           // x always assigned before use
    }
    
    public void TestLoopFlow()
    {
        int sum = 0;
        for (int i = 0; i < 10; i++)
        {
            sum += i;                   // sum read and written, i read
        }
        Console.WriteLine(sum);
    }
}
