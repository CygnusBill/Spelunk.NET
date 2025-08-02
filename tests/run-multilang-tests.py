#!/usr/bin/env python3
"""
Comprehensive test runner for multi-language support (VB.NET and F#)
"""

import sys
import os
import subprocess
import time
from pathlib import Path

def run_test(test_script):
    """Run a single test script and return success status"""
    print(f"\n{'='*60}")
    print(f"Running: {test_script}")
    print('='*60)
    
    try:
        result = subprocess.run([sys.executable, test_script], 
                              capture_output=False, 
                              text=True,
                              cwd=os.path.dirname(os.path.abspath(__file__)))
        
        success = result.returncode == 0
        print(f"\n{'✓' if success else '✗'} {test_script}: {'PASSED' if success else 'FAILED'}")
        return success
    except Exception as e:
        print(f"✗ {test_script}: ERROR - {e}")
        return False

def main():
    print("Multi-Language Support Test Suite")
    print("=" * 60)
    print("Testing VB.NET and F# integration with MCP Roslyn Server")
    
    # List of test scripts to run
    test_scripts = [
        # VB.NET tests
        "tools/test-vb-find-methods.py",
        "tools/test-vb-find-classes.py", 
        "tools/test-roslynpath-vb.py",
        
        # F# tests
        "tools/test-fsharp-projects.py",
    ]
    
    # Check if all test files exist
    missing_tests = []
    for test in test_scripts:
        test_path = os.path.join(os.path.dirname(__file__), test)
        if not os.path.exists(test_path):
            missing_tests.append(test)
    
    if missing_tests:
        print(f"\n✗ Missing test files:")
        for test in missing_tests:
            print(f"  - {test}")
        return False
    
    # Run all tests
    results = {}
    start_time = time.time()
    
    for test_script in test_scripts:
        test_path = os.path.join(os.path.dirname(__file__), test_script)
        results[test_script] = run_test(test_path)
        
        # Small delay between tests
        time.sleep(1)
    
    end_time = time.time()
    duration = end_time - start_time
    
    # Summary
    print(f"\n{'='*60}")
    print("MULTI-LANGUAGE TEST SUMMARY")
    print('='*60)
    
    passed = sum(1 for success in results.values() if success)
    total = len(results)
    
    print(f"Tests run: {total}")
    print(f"Passed: {passed}")
    print(f"Failed: {total - passed}")
    print(f"Duration: {duration:.2f} seconds")
    print()
    
    # Detailed results
    print("Detailed Results:")
    for test, success in results.items():
        status = "✓ PASSED" if success else "✗ FAILED"
        test_name = os.path.basename(test).replace('.py', '').replace('-', ' ').title()
        print(f"  {status:<10} {test_name}")
    
    if passed == total:
        print(f"\n🎉 All multi-language tests PASSED!")
        print("VB.NET and F# support is working correctly.")
    else:
        print(f"\n⚠️  {total - passed} test(s) FAILED")
        print("Some multi-language features may not be working correctly.")
        
        # Provide troubleshooting info
        print("\nTroubleshooting:")
        print("- Ensure the MCP Roslyn Server is running")
        print("- Check that test-workspace contains VB.NET and F# projects")
        print("- Verify that FSharp.Compiler.Service is available for F# tests")
        print("- F# tests may fail if the F# compiler service integration is incomplete")
    
    return passed == total

if __name__ == "__main__":
    success = main()
    sys.exit(0 if success else 1)