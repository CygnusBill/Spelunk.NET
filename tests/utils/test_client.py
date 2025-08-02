#!/usr/bin/env python3
"""
Simple test client for MCP tools testing
"""

import json
import subprocess
import sys
import os
import time
import threading
import queue

class TestClient:
    def __init__(self, server_path=None):
        if server_path is None:
            # Default to the server in the project
            server_path = os.path.join(os.path.dirname(__file__), "..", "..", "src", "McpRoslyn", "McpRoslyn.Server")
        
        self.server_path = server_path
        self.process = None
        self.response_queue = queue.Queue()
        self.request_id = 0
        self._start_server()
    
    def _start_server(self):
        """Start the MCP server"""
        cmd = ["dotnet", "run", "--project", self.server_path]
        print(f"Starting server: {' '.join(cmd)}")
        
        self.process = subprocess.Popen(
            cmd,
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE, 
            stderr=subprocess.PIPE,
            text=True,
            bufsize=0
        )
        
        # Start background threads to handle server output
        threading.Thread(target=self._read_stdout, daemon=True).start()
        threading.Thread(target=self._read_stderr, daemon=True).start()
        
        # Give server time to start
        time.sleep(2)
        
        # Send initialize request
        self._initialize()
    
    def _read_stdout(self):
        """Read responses from server stdout"""
        for line in self.process.stdout:
            if line.strip():
                # Skip build output
                if any(skip in line for skip in ['/bin/', '.csproj', 'warning CS', 'Determining projects']):
                    continue
                print(f"Server response: {line.strip()}")
                try:
                    response = json.loads(line)
                    self.response_queue.put(response)
                except json.JSONDecodeError:
                    pass  # Skip non-JSON lines
    
    def _read_stderr(self):
        """Read errors from server stderr"""
        for line in self.process.stderr:
            if line.strip() and not line.startswith("warn:"):
                print(f"Server stderr: {line.strip()}")
    
    def _initialize(self):
        """Send MCP initialize request"""
        init_request = {
            "jsonrpc": "2.0",
            "id": self._next_id(),
            "method": "initialize",
            "params": {
                "protocolVersion": "2024-11-05",
                "capabilities": {},
                "clientInfo": {
                    "name": "test-client",
                    "version": "1.0.0"
                }
            }
        }
        
        self._send_request(init_request)
        self._wait_for_response()  # Wait for initialize response
    
    def _next_id(self):
        self.request_id += 1
        return self.request_id
    
    def _send_request(self, request):
        """Send JSON-RPC request to server"""
        request_json = json.dumps(request)
        print(f"Sending: {request_json}")
        self.process.stdin.write(request_json + '\n')
        self.process.stdin.flush()
    
    def _wait_for_response(self, timeout=10):
        """Wait for and return next response"""
        try:
            return self.response_queue.get(timeout=timeout)
        except queue.Empty:
            raise TimeoutError(f"No response received within {timeout} seconds")
    
    def call_tool(self, tool_name, arguments=None):
        """Call an MCP tool and return the result"""
        if arguments is None:
            arguments = {}
            
        request = {
            "jsonrpc": "2.0",
            "id": self._next_id(),
            "method": "tools/call",
            "params": {
                "name": tool_name,
                "arguments": arguments
            }
        }
        
        self._send_request(request)
        response = self._wait_for_response()
        
        if "error" in response:
            return {
                "success": False,
                "message": response["error"].get("message", "Unknown error"),
                "error": response["error"]
            }
        
        return {
            "success": True,
            "result": response.get("result", {}),
            "message": "Success"
        }
    
    def close(self):
        """Clean up and close server connection"""
        if self.process:
            self.process.terminate()
            self.process.wait()
    
    def __enter__(self):
        return self
    
    def __exit__(self, exc_type, exc_val, exc_tb):
        self.close()