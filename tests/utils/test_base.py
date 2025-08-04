#!/usr/bin/env python3
"""Base class for tool tests"""

import os
import sys
import json
from typing import Dict, Any, Optional
from test_client import TestClient

class ToolTestBase:
    """Base class for testing MCP tools"""
    
    def __init__(self, tool_name: str):
        self.tool_name = tool_name
        self.server_path = os.path.join(os.path.dirname(__file__), "..", "..", "src", "McpRoslyn.Server")
        self.client: Optional[TestClient] = None
        
    def setup(self, allowed_paths: Optional[list] = None):
        """Set up the test client"""
        if allowed_paths is None:
            allowed_paths = ["."]
        self.client = TestClient(server_path=self.server_path, allowed_paths=allowed_paths)
        
    def teardown(self):
        """Clean up the test client"""
        if self.client:
            self.client.close()
            self.client = None
    
    def call_tool(self, arguments: Dict[str, Any]) -> Dict[str, Any]:
        """Call the tool with given arguments"""
        if not self.client:
            raise RuntimeError("Test not set up. Call setup() first.")
        return self.client.call_tool(self.tool_name, arguments)
    
    def load_workspace(self, workspace_path: str) -> Dict[str, Any]:
        """Load a workspace"""
        if not self.client:
            raise RuntimeError("Test not set up. Call setup() first.")
        return self.client.call_tool("dotnet-load-workspace", {"path": workspace_path})
    
    def assert_success(self, result: Dict[str, Any], message: str = "Expected successful result"):
        """Assert that result indicates success"""
        if not result.get("success"):
            error_msg = result.get("message", "Unknown error")
            raise AssertionError(f"{message}. Got error: {error_msg}")
    
    def assert_error(self, result: Dict[str, Any], message: str = "Expected error result"):
        """Assert that result indicates an error"""
        if result.get("success"):
            raise AssertionError(f"{message}. Got successful result instead.")
    
    def run_test(self):
        """Override this method in subclasses"""
        raise NotImplementedError("Subclasses must implement run_test()")
    
    def __enter__(self):
        self.setup()
        return self
    
    def __exit__(self, exc_type, exc_val, exc_tb):
        self.teardown()