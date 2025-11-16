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
            server_path = os.path.join(os.path.dirname(__file__), "..", "..", "src", "Spelunk.Server")
        
        self.server_path = server_path
        self.allowed_paths = allowed_paths or ["."]
        self.process = None
        self.response_queue = queue.Queue()
        self.request_id = 0
        self._initialized = False
        self._start_server()
    
    def _start_server(self):
        """Start the MCP server with robust error handling"""
        print("Building server project...")
        
        # First, ensure the project is built
        build_cmd = ["dotnet", "build", self.server_path, "--configuration", "Debug"]
        try:
            build_result = subprocess.run(
                build_cmd,
                capture_output=True,
                text=True,
                timeout=60  # 1 minute timeout for build
            )
            if build_result.returncode != 0:
                raise RuntimeError(f"Build failed: {build_result.stderr}")
            print("✅ Build completed successfully")
        except subprocess.TimeoutExpired:
            raise RuntimeError("Build timed out after 60 seconds")
        except Exception as e:
            raise RuntimeError(f"Build error: {e}")
        
        # Now run with --no-build --no-restore for predictable startup
        cmd = ["dotnet", "run", "--project", self.server_path, "--no-build", "--no-restore"]
        
        print(f"Starting server: {' '.join(cmd)}")
        
        # Set up environment with allowed paths
        env = os.environ.copy()
        if self.allowed_paths:
            # Use the legacy environment variable format which works more reliably
            env["MCP_DOTNET_ALLOWED_PATHS"] = os.pathsep.join(os.path.abspath(path) for path in self.allowed_paths)
            print(f"Allowed paths: {env['MCP_DOTNET_ALLOWED_PATHS']}")
        
        try:
            self.process = subprocess.Popen(
                cmd,
                stdin=subprocess.PIPE,
                stdout=subprocess.PIPE, 
                stderr=subprocess.PIPE,
                text=True,
                bufsize=1,  # Line buffered
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
        
        # Give threads a moment to start
        time.sleep(0.5)
        
        # Wait for server to start - now should be much more predictable
        max_wait_time = 8  # Reduced timeout since startup should be fast
        wait_interval = 0.2  # More frequent checks
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
            if elapsed >= 1 and not self._initialized:  # Reduced to 1 second since startup should be fast now
                try:
                    self._initialize()
                    self._initialized = True
                    print(f"✅ Server initialized successfully in {elapsed:.1f} seconds")
                    return  # Success!
                except Exception as e:
                    print(f"Initialization attempt failed: {e}")
                    # If initialization fails, continue waiting unless we've exceeded max time
                    if elapsed >= max_wait_time:
                        raise RuntimeError(f"Server failed to initialize within {max_wait_time} seconds: {e}")
                    continue
    
    def _read_stdout(self):
        """Read responses from server stdout"""
        print("stdout reader: thread started")
        try:
            while True:
                line = self.process.stdout.readline()
                if not line:
                    print("stdout reader: EOF reached")
                    break
                    
                line = line.strip()
                if not line:
                    continue
                
                # Skip build output
                if any(skip in line for skip in ['/bin/', '.csproj', 'warning CS', 'Determining projects', 'NETSDK']):
                    continue
                    
                print(f"Server response received: {len(line)} chars")
                print(f"First 100 chars: {repr(line[:100])}")
                try:
                    response = json.loads(line)
                    self.response_queue.put(response)
                    print(f"✅ JSON parsed and queued successfully - ID: {response.get('id', 'no-id')}")
                    print(f"Queue size now: {self.response_queue.qsize()}")
                except json.JSONDecodeError as e:
                    print(f"JSON parse error: {e} for line: {line[:200]}...")  # Show first 200 chars
        except Exception as e:
            print(f"stdout reader thread error: {e}")
        finally:
            print("stdout reader: thread exiting")
    
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
        self._wait_for_response(request_id=init_request["id"])  # Wait for initialize response
    
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
    
    def _wait_for_response(self, timeout=10, request_id=None):
        """Wait for and return next response, optionally matching request ID"""
        start_time = time.time()
        deferred = []  # Store non-matching responses
        
        try:
            while time.time() - start_time < timeout:
                try:
                    response = self.response_queue.get(timeout=0.1)
                    resp_id = response.get("id")
                    print(f"Got response with ID {resp_id}, looking for {request_id}")
                    if request_id is None or resp_id == request_id:
                        print(f"✓ Found matching response for ID {request_id}")
                        return response
                    else:
                        # Defer non-matching responses
                        print(f"✗ Response ID {resp_id} doesn't match, deferring")
                        deferred.append(response)
                except queue.Empty:
                    continue
                    
            raise TimeoutError(f"No response received within {timeout} seconds for request ID {request_id}")
        finally:
            # Put deferred responses back
            for resp in deferred:
                self.response_queue.put(resp)
    
    def call_tool(self, tool_name, arguments=None, timeout=30):
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
        
        # Check if stdout thread is alive
        if hasattr(self, '_stdout_thread') and not self._stdout_thread.is_alive():
            print("WARNING: stdout thread is dead!")
        
        self._send_request(request)
        response = self._wait_for_response(timeout=timeout, request_id=request["id"])  # Use configurable timeout
        
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