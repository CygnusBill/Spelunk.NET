#!/usr/bin/env python3
"""
Interactive test client for MCP Roslyn Server
Allows you to send JSON-RPC requests and see responses
"""

import json
import subprocess
import sys
import threading
import queue
import time

class McpTestClient:
    def __init__(self, server_command):
        self.server_command = server_command
        self.process = None
        self.response_queue = queue.Queue()
        self.request_id = 0
        
    def start(self):
        """Start the MCP server process"""
        print(f"Starting server: {' '.join(self.server_command)}")
        self.process = subprocess.Popen(
            self.server_command,
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            bufsize=0
        )
        
        # Start threads to read stdout and stderr
        threading.Thread(target=self._read_stdout, daemon=True).start()
        threading.Thread(target=self._read_stderr, daemon=True).start()
        
        # Give server time to start
        time.sleep(1)
        
    def _read_stdout(self):
        """Read JSON-RPC responses from stdout"""
        for line in self.process.stdout:
            if line.strip():
                # Skip build output and warnings
                if line.strip().startswith('/') or 'warning CS' in line or '.csproj' in line:
                    continue
                print(f"<<< RESPONSE: {line.strip()}")
                try:
                    response = json.loads(line)
                    self.response_queue.put(response)
                except json.JSONDecodeError as e:
                    print(f"Error parsing response: {e}")
                    
    def _read_stderr(self):
        """Read log messages from stderr"""
        for line in self.process.stderr:
            print(f"[LOG] {line.strip()}")
            
    def send_request(self, method, params=None):
        """Send a JSON-RPC request"""
        self.request_id += 1
        request = {
            "jsonrpc": "2.0",
            "id": self.request_id,
            "method": method
        }
        if params is not None:
            request["params"] = params
            
        request_json = json.dumps(request)
        print(f">>> REQUEST: {request_json}")
        
        self.process.stdin.write(request_json + "\n")
        self.process.stdin.flush()
        
        # Wait for response
        try:
            response = self.response_queue.get(timeout=5)
            return response
        except queue.Empty:
            print("Timeout waiting for response")
            return None
            
    def interactive_mode(self):
        """Run in interactive mode"""
        print("\nMCP Test Client - Interactive Mode")
        print("Commands:")
        print("  init - Send initialize request")
        print("  list - List available tools")
        print("  load <path> - Load a workspace")
        print("  status - Get workspace status")
        print("  call <tool> <args> - Call a tool with JSON args")
        print("  raw <json> - Send raw JSON-RPC request")
        print("  quit - Exit\n")
        
        while True:
            try:
                cmd = input("> ").strip()
                if not cmd:
                    continue
                    
                parts = cmd.split(maxsplit=1)
                command = parts[0].lower()
                
                if command == "quit":
                    break
                elif command == "init":
                    self.send_request("initialize", {})
                elif command == "list":
                    self.send_request("tools/list", {})
                elif command == "status":
                    self.send_request("tools/call", {
                        "name": "dotnet-workspace-status",
                        "arguments": {}
                    })
                elif command == "load":
                    if len(parts) < 2:
                        print("Usage: load <path>")
                        continue
                    self.send_request("tools/call", {
                        "name": "dotnet-load-workspace",
                        "arguments": {"path": parts[1]}
                    })
                elif command == "call":
                    if len(parts) < 2:
                        print("Usage: call <tool> <args>")
                        continue
                    tool_parts = parts[1].split(maxsplit=1)
                    tool_name = tool_parts[0]
                    args = json.loads(tool_parts[1]) if len(tool_parts) > 1 else {}
                    self.send_request("tools/call", {
                        "name": tool_name,
                        "arguments": args
                    })
                elif command == "raw":
                    if len(parts) < 2:
                        print("Usage: raw <json>")
                        continue
                    request = json.loads(parts[1])
                    self.send_request(request.get("method", ""), request.get("params"))
                else:
                    print(f"Unknown command: {command}")
                    
            except KeyboardInterrupt:
                print("\nUse 'quit' to exit")
            except Exception as e:
                print(f"Error: {e}")
                
    def stop(self):
        """Stop the server"""
        if self.process:
            self.process.terminate()
            self.process.wait()

def main():
    # Server command - adjust path as needed
    server_cmd = [
        "dotnet", "run",
        "--project", "/Users/bill/Desktop/McpDotnet/src/McpRoslyn/McpRoslyn.Server/McpRoslyn.Server.csproj",
        "--",
        "--allowed-path", "/Users/bill/Desktop/McpDotnet"
    ]
    
    client = McpTestClient(server_cmd)
    
    try:
        client.start()
        client.interactive_mode()
    finally:
        client.stop()

if __name__ == "__main__":
    main()