#!/usr/bin/env python3
"""Test just the parser part without the server"""

import sys
import os

# Add the project to Python path
sys.path.insert(0, 'ParserTest/bin/Debug/net10.0')

# Create a small C# program to test the parser
test_code = """
using System;
using McpRoslyn.Server.RoslynPath;

class TestParser
{
    static void Main()
    {
        var path = "//*[@name='foo']";
        Console.WriteLine($"Testing path: {path}");
        
        try 
        {
            var parser = new RoslynPathParser();
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
        }
    }
}
"""

# Write and compile
with open("TestParser2.cs", "w") as f:
    f.write(test_code)

import subprocess

# Compile
result = subprocess.run([
    "csc", 
    "-reference:src/McpRoslyn.Server/bin/Debug/net10.0/McpRoslyn.Server.dll",
    "-reference:src/McpRoslyn.Server/bin/Debug/net10.0/System.Runtime.dll",
    "-reference:src/McpRoslyn.Server/bin/Debug/net10.0/System.Collections.dll",
    "TestParser2.cs"
], capture_output=True, text=True)

if result.returncode != 0:
    print("Compilation failed:")
    print(result.stderr)
    sys.exit(1)

# Run
result = subprocess.run(["mono", "TestParser2.exe"], capture_output=True, text=True)
print(result.stdout)
if result.stderr:
    print("Errors:", result.stderr)