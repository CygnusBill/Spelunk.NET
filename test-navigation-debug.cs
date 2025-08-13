
namespace TestNamespace
{
    public class TestClass
    {
        private int testField;
        
        public TestClass()
        {
            testField = 0;
        }
        
        public void TestMethod()
        {
            var x = 1 + 2;
            if (x > 0)
            {
                Console.WriteLine("Hello");
            }
            var y = x * 3;
        }
    }
}