#!/usr/bin/env python3
"""
Run unit tests for AST navigation functionality
"""

import subprocess
import sys
import os

def run_command(command, description):
    """Run a command and return success status"""
    print(f"\n{'='*60}")
    print(f"Running: {description}")
    print(f"Command: {command}")
    print('='*60)
    
    try:
        result = subprocess.run(command, shell=True, check=True, capture_output=True, text=True)
        print("STDOUT:", result.stdout)
        if result.stderr:
            print("STDERR:", result.stderr)
        print(f"✅ {description} completed successfully")
        return True
    except subprocess.CalledProcessError as e:
        print(f"❌ {description} failed with exit code {e.returncode}")
        print("STDOUT:", e.stdout)
        print("STDERR:", e.stderr)
        return False

def main():
    """Run all unit tests"""
    print("🧪 Running AST Navigation Unit Tests")
    
    # Change to project directory
    os.chdir(os.path.dirname(os.path.abspath(__file__)))
    
    # Build the test project
    if not run_command("dotnet build tests/McpRoslyn.Server.Tests.csproj", "Building test project"):
        return 1
    
    # Run the tests
    if not run_command("dotnet test tests/McpRoslyn.Server.Tests.csproj --verbosity normal", "Running unit tests"):
        return 1
    
    # Run tests with detailed output
    if not run_command("dotnet test tests/McpRoslyn.Server.Tests.csproj --logger 'console;verbosity=detailed'", "Running tests with detailed output"):
        return 1
    
    print("\n🎉 All tests completed successfully!")
    return 0

if __name__ == "__main__":
    sys.exit(main())