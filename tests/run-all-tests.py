#!/usr/bin/env python3
"""
Run all MCP Roslyn Server tests and report results
"""
import os
import subprocess
import sys
from pathlib import Path
import time

class Colors:
    GREEN = '\033[92m'
    RED = '\033[91m'
    YELLOW = '\033[93m'
    BLUE = '\033[94m'
    RESET = '\033[0m'

def run_test(test_path):
    """Run a single test and return (success, duration, output)"""
    start_time = time.time()
    try:
        result = subprocess.run(
            [sys.executable, test_path],
            capture_output=True,
            text=True,
            timeout=30
        )
        duration = time.time() - start_time
        success = result.returncode == 0
        output = result.stdout if success else result.stderr
        return success, duration, output
    except subprocess.TimeoutExpired:
        return False, 30.0, "Test timed out after 30 seconds"
    except Exception as e:
        return False, 0.0, f"Error running test: {e}"

def main():
    # Find all test files
    test_dir = Path(__file__).parent
    test_files = []
    
    for category in ['tools', 'protocol', 'integration']:
        category_path = test_dir / category
        if category_path.exists():
            test_files.extend(sorted(category_path.glob('test-*.py')))
    
    if not test_files:
        print(f"{Colors.RED}No test files found!{Colors.RESET}")
        return 1
    
    print(f"{Colors.BLUE}Running {len(test_files)} tests...{Colors.RESET}\n")
    
    # Run tests
    passed = 0
    failed = 0
    results = []
    
    for test_file in test_files:
        relative_path = test_file.relative_to(test_dir)
        print(f"Running {relative_path}... ", end='', flush=True)
        
        success, duration, output = run_test(test_file)
        results.append((relative_path, success, duration, output))
        
        if success:
            print(f"{Colors.GREEN}✓ ({duration:.2f}s){Colors.RESET}")
            passed += 1
        else:
            print(f"{Colors.RED}✗ ({duration:.2f}s){Colors.RESET}")
            failed += 1
    
    # Summary
    print(f"\n{Colors.BLUE}{'='*60}{Colors.RESET}")
    print(f"Test Summary: {Colors.GREEN}{passed} passed{Colors.RESET}, ", end='')
    
    if failed > 0:
        print(f"{Colors.RED}{failed} failed{Colors.RESET}")
    else:
        print(f"{failed} failed")
    
    # Show failed test details
    if failed > 0:
        print(f"\n{Colors.RED}Failed Tests:{Colors.RESET}")
        for path, success, duration, output in results:
            if not success:
                print(f"\n{Colors.YELLOW}{path}:{Colors.RESET}")
                print(output.strip())
                print(f"{Colors.BLUE}{'-'*60}{Colors.RESET}")
    
    # Performance summary
    print(f"\n{Colors.BLUE}Performance Summary:{Colors.RESET}")
    total_time = sum(r[2] for r in results)
    print(f"Total execution time: {total_time:.2f}s")
    
    # Find slowest tests
    slowest = sorted(results, key=lambda x: x[2], reverse=True)[:3]
    print(f"\nSlowest tests:")
    for path, _, duration, _ in slowest:
        print(f"  {path}: {duration:.2f}s")
    
    return 0 if failed == 0 else 1

if __name__ == "__main__":
    sys.exit(main())