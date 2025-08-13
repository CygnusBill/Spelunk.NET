#!/usr/bin/env python3
"""
Test for the infinite loop issue with //*[@name='foo']
"""

import json
import sys
import os
import time
import threading
import subprocess

def test_pattern_with_timeout(pattern, timeout=3):
    """Test a pattern with a timeout to detect infinite loops"""
    
    print(f"\n=== Testing pattern: {pattern} ===")
    
    # Create a simple test file
    test_file = "/tmp/test-wildcard.cs"
    with open(test_file, "w") as f:
        f.write("""
namespace Test
{
    public class TestClass
    {
        public void foo() { }
        public void bar() { }
        private string foo = "field";
    }
}
""")
    
    # Build the request
    request = {
        "jsonrpc": "2.0",
        "id": 1,
        "method": "tools/call",
        "params": {
            "name": "dotnet-query-syntax",
            "arguments": {
                "file": test_file,
                "roslynPath": pattern
            }
        }
    }
    
    # Run the server with timeout
    env = os.environ.copy()
    env["MCP_ROSLYN_ALLOWED_PATHS"] = "/tmp"
    
    proc = subprocess.Popen(
        ["dotnet", "run", "--project", "src/McpRoslyn.Server", "--no-build"],
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        env=env,
        text=True
    )
    
    # Initialize first
    init_request = json.dumps({
        "jsonrpc": "2.0",
        "id": 0,
        "method": "initialize",
        "params": {"protocolVersion": "2024-11-05"}
    }) + "\n"
    
    # Load workspace
    load_request = json.dumps({
        "jsonrpc": "2.0",
        "id": 1,
        "method": "tools/call",
        "params": {
            "name": "dotnet-load-workspace",
            "arguments": {"path": test_file}
        }
    }) + "\n"
    
    # Send the actual query
    query_request = json.dumps(request) + "\n"
    
    try:
        proc.stdin.write(init_request)
        proc.stdin.flush()
        time.sleep(0.5)
        
        proc.stdin.write(load_request)
        proc.stdin.flush()
        time.sleep(0.5)
        
        proc.stdin.write(query_request)
        proc.stdin.flush()
        
        # Wait for response with timeout
        start_time = time.time()
        response_lines = []
        
        def read_output():
            while True:
                line = proc.stdout.readline()
                if line:
                    response_lines.append(line)
                    if '"result"' in line or '"error"' in line:
                        return
        
        thread = threading.Thread(target=read_output)
        thread.daemon = True
        thread.start()
        thread.join(timeout)
        
        if thread.is_alive():
            print(f"  ❌ TIMEOUT after {timeout} seconds - likely infinite loop!")
            proc.terminate()
            return False
        else:
            elapsed = time.time() - start_time
            print(f"  ✓ Completed in {elapsed:.2f} seconds")
            
            # Parse the response
            for line in response_lines:
                if line.strip().startswith("{"):
                    try:
                        response = json.loads(line)
                        if "result" in response:
                            content = response["result"]["content"][0]["text"]
                            data = json.loads(content)
                            matches = data.get("matches", [])
                            print(f"  Found {len(matches)} matches")
                            return True
                    except:
                        pass
            
            return True
    
    finally:
        proc.terminate()
        proc.wait()

if __name__ == "__main__":
    patterns = [
        "//*[@name='foo']",      # The problematic pattern
        "//method[@name='foo']", # Control - should work
        "//*",                   # Wildcard without predicate
        "//method",              # Regular without predicate
    ]
    
    all_passed = True
    for pattern in patterns:
        if not test_pattern_with_timeout(pattern):
            all_passed = False
    
    if all_passed:
        print("\n✅ All patterns completed successfully!")
    else:
        print("\n❌ Some patterns failed or timed out")
        sys.exit(1)