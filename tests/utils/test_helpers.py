#!/usr/bin/env python3
"""Test helper functions and classes"""

import json
import os
import sys
from typing import Dict, Any, Optional
from test_client import TestClient

class TestRunner:
    """Helper class for running tests"""
    
    def __init__(self, allowed_paths: Optional[list] = None):
        server_path = os.path.join(os.path.dirname(__file__), "..", "..", "src", "McpRoslyn.Server")
        if allowed_paths is None:
            allowed_paths = ["."]
        self.client = TestClient(server_path=server_path, allowed_paths=allowed_paths)
        
    def call_tool(self, tool_name: str, arguments: Dict[str, Any]) -> Dict[str, Any]:
        """Call a tool and return the result"""
        return self.client.call_tool(tool_name, arguments)
    
    def send_request(self, method: str, params: Dict[str, Any] = None) -> Dict[str, Any]:
        """Send a raw request to the server"""
        return self.client.send_request(method, params or {})
    
    def close(self):
        """Close the client"""
        self.client.close()
    
    def __enter__(self):
        return self
    
    def __exit__(self, exc_type, exc_val, exc_tb):
        self.close()

def assert_equals(actual: Any, expected: Any, message: str = "Values should be equal"):
    """Assert that two values are equal"""
    if actual != expected:
        raise AssertionError(f"{message}. Expected: {expected}, Actual: {actual}")

def assert_exists(obj: Dict[str, Any], key: str, message: str = None):
    """Assert that a key exists in a dictionary"""
    if message is None:
        message = f"Key '{key}' should exist"
    if key not in obj:
        raise AssertionError(f"{message}. Available keys: {list(obj.keys())}")

def assert_success(result: Dict[str, Any], message: str = "Expected successful result"):
    """Assert that result indicates success"""
    if not result.get("success"):
        error_msg = result.get("message", "Unknown error")
        raise AssertionError(f"{message}. Got error: {error_msg}")

def assert_error(result: Dict[str, Any], message: str = "Expected error result"):
    """Assert that result indicates an error"""
    if result.get("success"):
        raise AssertionError(f"{message}. Got successful result instead.")

def assert_contains(container: Any, item: Any, message: str = "Container should contain item"):
    """Assert that container contains item"""
    if item not in container:
        raise AssertionError(f"{message}. Item '{item}' not found in {container}")

def assert_not_empty(obj: Any, message: str = "Object should not be empty"):
    """Assert that object is not empty"""
    if not obj:
        raise AssertionError(f"{message}. Got: {obj}")

def print_result(result: Dict[str, Any], title: str = "Result"):
    """Pretty print a test result"""
    print(f"\n=== {title} ===")
    print(json.dumps(result, indent=2))
    print("=" * (len(title) + 8))