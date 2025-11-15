#!/usr/bin/env python3
"""
Test thread issue with subprocess reading
"""

import subprocess
import threading
import time
import os

def read_stdout(process):
    """Read stdout in thread"""
    print("Reader thread started")
    count = 0
    try:
        while True:
            line = process.stdout.readline()
            if not line:
                print("EOF reached")
                break
            count += 1
            print(f"Read line {count}: {len(line.strip())} chars")
    except Exception as e:
        print(f"Error: {e}")
    finally:
        print(f"Thread exiting after {count} lines")

# Start a process that outputs multiple lines
cmd = ["dotnet", "run", "--project", "src/McpDotnet.Server", "--no-build", "--no-restore"]
env = os.environ.copy()
env["MCP_DOTNET_ALLOWED_PATHS"] = os.path.abspath("test-workspace")

print("Starting process...")
process = subprocess.Popen(
    cmd,
    stdin=subprocess.PIPE,
    stdout=subprocess.PIPE,
    stderr=subprocess.DEVNULL,  # Ignore stderr for this test
    text=True,
    bufsize=1
)

# Start reader thread
thread = threading.Thread(target=read_stdout, args=(process,))
thread.daemon = True
thread.start()

# Give it time to start
time.sleep(2)

# Send two requests
print("\nSending request 1...")
process.stdin.write('{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}}}\n')
process.stdin.flush()

time.sleep(1)

print("\nSending request 2...")
process.stdin.write('{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"dotnet-load-workspace","arguments":{"path":"' + os.path.abspath("test-workspace/TestProject.csproj") + '"}}}\n')
process.stdin.flush()

# Wait and check thread
time.sleep(2)
print(f"\nThread alive: {thread.is_alive()}")

# Clean up
process.terminate()
thread.join(timeout=1)