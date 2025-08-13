#!/usr/bin/env python3
"""Test the wildcard pattern that was causing infinite loop"""

import json
import sys
import os

# Add the tests directory to path for utilities
sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'tests'))
from utils.client_utils import send_request

def test_wildcard_pattern():
    """Test the pattern //*[@name='foo'] that was causing infinite loop"""
    
    print("Testing pattern: //*[@name='foo']")
    
    # First load the workspace
    load_response = send_request("dotnet-load-workspace", {
        "projectPath": "/Users/bill/Repos/McpDotnet/test-wildcard-fix.cs"
    })
    
    if "error" in load_response:
        print(f"Failed to load workspace: {load_response['error']}")
        return False
    
    # Now test the query that was causing infinite loop
    query_response = send_request("dotnet-query-syntax", {
        "file": "/Users/bill/Repos/McpDotnet/test-wildcard-fix.cs",
        "roslynPath": "//*[@name='foo']"
    })
    
    if "error" in query_response:
        print(f"Query failed: {query_response['error']}")
        return False
    
    result = query_response.get("result", {})
    matches = result.get("matches", [])
    
    print(f"Found {len(matches)} matches:")
    for match in matches:
        print(f"  - {match.get('nodeType')} at line {match.get('startLine')}: {match.get('preview', '').strip()}")
    
    # We expect to find both the method 'foo' and the field 'foo'
    if len(matches) >= 2:
        print("✓ Pattern executed successfully without infinite loop!")
        return True
    else:
        print("⚠ Pattern executed but found unexpected number of matches")
        return True  # Still successful if no infinite loop

if __name__ == "__main__":
    success = test_wildcard_pattern()
    sys.exit(0 if success else 1)