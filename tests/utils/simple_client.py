#!/usr/bin/env python3
"""
Simplified test client for MCP tools testing
"""

import json
import subprocess
import sys
import os
import time

class SimpleClient:
    def __init__(self, server_path=None, allowed_paths=None):
        if server_path is None:
            server_path = os.path.join(os.path.dirname(__file__), "..", "..", "src", "McpDotnet.Server")
        
        self.server_path = server_path
        self.allowed_paths = allowed_paths or ["."]
        self.request_id = 0
        
        # Build first
        print("Building server...")
        dotnet_path = "/usr/local/share/dotnet/dotnet"
        build_cmd = [dotnet_path, "build", self.server_path, "--configuration", "Debug"]
        subprocess.run(build_cmd, check=True, capture_output=True)
        print("✅ Build completed")
        
        # Start server
        cmd = [dotnet_path, "run", "--project", self.server_path, "--no-build", "--no-restore"]
        env = os.environ.copy()
        env["MCP_DOTNET_ALLOWED_PATHS"] = os.pathsep.join(os.path.abspath(p) for p in self.allowed_paths)
        
        print("Starting server...")
        self.process = subprocess.Popen(
            cmd,
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            bufsize=1
        )
        
        # Wait for startup
        time.sleep(2)
        
        # Initialize
        self._initialize()
    
    def _next_id(self):
        self.request_id += 1
        return self.request_id
    
    def _send_and_receive(self, request):
        """Send request and wait for response"""
        request_json = json.dumps(request)
        print(f"\nSending: {request_json[:100]}...")
        
        self.process.stdin.write(request_json + '\n')
        self.process.stdin.flush()
        
        # Read response
        response_line = self.process.stdout.readline()
        if not response_line:
            raise RuntimeError("No response received (EOF)")
            
        print(f"Received: {len(response_line.strip())} chars")
        if len(response_line.strip()) < 100:
            print(f"Short response: {response_line.strip()}")
            # Check stderr for errors
            stderr_lines = []
            while True:
                import select
                if select.select([self.process.stderr], [], [], 0.1)[0]:
                    line = self.process.stderr.readline()
                    if line:
                        stderr_lines.append(line)
                    else:
                        break
                else:
                    break
            if stderr_lines:
                print("Server stderr:")
                for line in stderr_lines:
                    print(f"  {line.strip()}")
        return json.loads(response_line)
    
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
                    "name": "simple-test-client",
                    "version": "1.0.0"
                }
            }
        }
        
        response = self._send_and_receive(init_request)
        if "error" in response and response["error"]:
            raise RuntimeError(f"Initialize failed: {response['error']}")
        print("✅ Server initialized")
    
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
        
        response = self._send_and_receive(request)
        
        if "error" in response and response["error"] is not None:
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
        """Clean up"""
        if self.process:
            try:
                self.process.terminate()
                self.process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                # Force kill if terminate doesn't work
                self.process.kill()
                self.process.wait()

if __name__ == "__main__":
    # Test the simple client
    client = SimpleClient(allowed_paths=["test-workspace"])
    
    # Load workspace
    result = client.call_tool("spelunk-load-workspace", {
        "path": os.path.abspath("test-workspace/TestProject.csproj")
    })
    print(f"\nWorkspace load result: {result['success']}")
    
    # Test semantic query
    if result["success"]:
        result = client.call_tool("spelunk-query-syntax", {
            "roslynPath": "//method",
            "file": os.path.abspath("test-workspace/Program.cs"),
            "includeSemanticInfo": True
        })
        print(f"Query result: {result['success']}")
        if result["success"]:
            # Direct nodes access
            nodes = result["result"].get("nodes", [])
            print(f"Found {len(nodes)} methods")
            if nodes:
                first = nodes[0]
                print(f"First method: {first.get('text', '')[:50]}...")
                if "semanticInfo" in first:
                    print("✅ Semantic info present!")
                    print(f"Semantic info: {json.dumps(first['semanticInfo'], indent=2)}")
                else:
                    print("❌ No semantic info found")
    
    client.close()