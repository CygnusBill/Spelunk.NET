#!/usr/bin/env python3
"""Utility functions for tests"""

import json
import subprocess
import sys
import os
from typing import Dict, Any, Optional

def send_request(process, method: str, params: Dict[str, Any], request_id: int = 1) -> str:
    """Send a JSON-RPC request to a process"""
    request = {
        "jsonrpc": "2.0",
        "id": request_id,
        "method": method,
        "params": params
    }
    
    request_str = json.dumps(request)
    process.stdin.write(request_str + "\n")
    process.stdin.flush()
    
    # Read response
    response_line = process.stdout.readline()
    return response_line.strip()

def read_response(process) -> str:
    """Read a response line from a process"""
    return process.stdout.readline().strip()

def validate_response_structure(response: Dict[str, Any], required_fields: Optional[list] = None) -> bool:
    """Validate that a response has the expected structure"""
    if required_fields is None:
        required_fields = ["jsonrpc", "id"]
    
    for field in required_fields:
        if field not in response:
            print(f"Missing required field: {field}")
            return False
    
    if "error" in response and "result" in response:
        print("Response cannot have both error and result")
        return False
    
    return True

def print_json(obj: Any, indent: int = 2):
    """Pretty print a JSON object"""
    print(json.dumps(obj, indent=indent))

def setup_test_workspace() -> str:
    """Set up and return path to a test workspace"""
    # Use the existing test workspace
    return os.path.join(os.getcwd(), "test-workspace", "TestProject.csproj")

def create_temp_file(content: str, filename: str = "temp.cs") -> str:
    """Create a temporary file with given content"""
    import tempfile
    
    temp_dir = tempfile.mkdtemp(prefix="mcp_test_")
    temp_file = os.path.join(temp_dir, filename)
    
    with open(temp_file, 'w') as f:
        f.write(content)
    
    return temp_file

def cleanup_temp_file(filepath: str):
    """Clean up a temporary file and its directory"""
    import shutil
    
    if os.path.exists(filepath):
        temp_dir = os.path.dirname(filepath)
        shutil.rmtree(temp_dir, ignore_errors=True)