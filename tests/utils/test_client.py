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
import signal

class TestClient:
    def __init__(self, server_path=None, allowed_paths=None):
        if server_path is None:
            # Default to the server in the project
            server_path = os.path.join(os.path.dirname(__file__), "..", "..", "src", "McpRoslyn.Server")
        
        self.server_path = server_path
        self.allowed_paths = allowed_paths or ["."]
        self.process = None
        self.response_queue = queue.Queue()
        self.request_id = 0
        self._start_server()
    
    def _start_server(self):
        """Start the MCP server with robust error handling"""
        # Build the command
        cmd = ["dotnet", "run", "--project", self.server_path]
        
        print(f"Starting server: {' '.join(cmd)}")
        
        # Set up environment with allowed paths
        env = os.environ.copy()
        if self.allowed_paths:
            # Use the legacy environment variable format which works more reliably
            env["MCP_ROSLYN_ALLOWED_PATHS"] = os.pathsep.join(os.path.abspath(path) for path in self.allowed_paths)
            print(f"Allowed paths: {env['MCP_ROSLYN_ALLOWED_PATHS']}")
        
        try:
            self.process = subprocess.Popen(
                cmd,
                stdin=subprocess.PIPE,
                stdout=subprocess.PIPE, 
                stderr=subprocess.PIPE,
                text=True,
                bufsize=0,
                env=env,
                preexec_fn=os.setsid  # Create new process group for better cleanup
            )
        except Exception as e:
            raise RuntimeError(f"Failed to start server: {e}")
        
        # Start background threads to handle server output
        self._stdout_thread = threading.Thread(target=self._read_stdout, daemon=True)
        self._stderr_thread = threading.Thread(target=self._read_stderr, daemon=True)
        self._stdout_thread.start()
        self._stderr_thread.start()
        
        # Wait for server to start with more robust checking
        max_wait_time = 10
        wait_interval = 0.5
        elapsed = 0
        
        while elapsed < max_wait_time:
            if self.process.poll() is not None:
                # Process has already terminated
                return_code = self.process.returncode
                stderr_output = self.process.stderr.read() if self.process.stderr else "No stderr"
                raise RuntimeError(f"Server process terminated during startup with code {return_code}. Stderr: {stderr_output}")
            
            time.sleep(wait_interval)
            elapsed += wait_interval
            
            # Check if we can communicate with the server by trying initialization
            if elapsed >= 2:  # Give it at least 2 seconds before trying to initialize
                try:
                    self._initialize()
                    return  # Success!
                except Exception as e:
                    # If initialization fails, continue waiting unless we've exceeded max time
                    if elapsed >= max_wait_time:
                        raise RuntimeError(f"Server failed to initialize within {max_wait_time} seconds: {e}")
                    continue
    
    def _read_stdout(self):
        """Read responses from server stdout"""
        try:
            for line in self.process.stdout:
                if line.strip():
                    # Skip build output
                    if any(skip in line for skip in ['/bin/', '.csproj', 'warning CS', 'Determining projects', 'NETSDK']):
                        continue
                    print(f"Server response: {line.strip()}")
                    try:
                        response = json.loads(line)
                        self.response_queue.put(response)
                    except json.JSONDecodeError:
                        pass  # Skip non-JSON lines
        except Exception as e:
            # Handle broken pipe or other stdout reading errors gracefully
            print(f"stdout reader thread ended: {e}")
    
    def _read_stderr(self):
        """Read errors from server stderr"""
        try:
            for line in self.process.stderr:
                if line.strip() and not line.startswith("warn:"):
                    print(f"Server stderr: {line.strip()}")
        except Exception as e:
            # Handle broken pipe or other stderr reading errors gracefully
            print(f"stderr reader thread ended: {e}")
    
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
        if self.process.poll() is not None:
            raise RuntimeError(f"Server process has terminated with code {self.process.returncode}")
            
        request_json = json.dumps(request)
        print(f"Sending: {request_json}")
        
        try:
            self.process.stdin.write(request_json + '\n')
            self.process.stdin.flush()
        except BrokenPipeError as e:
            raise RuntimeError(f"Failed to send request to server (broken pipe): {e}. Server may have crashed.")
    
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
        """Clean up and close server connection"""
        if self.process:
            try:
                # Try graceful termination first
                self.process.terminate()
                self.process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                # Force kill if graceful termination fails
                try:
                    os.killpg(os.getpgid(self.process.pid), signal.SIGKILL)
                except (ProcessLookupError, AttributeError):
                    pass  # Process already dead or no process group
            except Exception as e:
                print(f"Error during cleanup: {e}")
            finally:
                self.process = None
    
    def __enter__(self):
        return self
    
    def __exit__(self, exc_type, exc_val, exc_tb):
        self.close()