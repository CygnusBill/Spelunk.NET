#!/usr/bin/env python3
"""
Wrapper script to run tests with proper cleanup.
Ensures all processes are terminated when the test completes.
"""

import sys
import subprocess
import signal
import os
import psutil
import atexit

# Track all child processes
child_processes = []

def cleanup():
    """Kill all child processes on exit"""
    for proc in child_processes:
        try:
            if proc.poll() is None:  # Process is still running
                print(f"Terminating process {proc.pid}")
                proc.terminate()
                proc.wait(timeout=5)
        except:
            try:
                proc.kill()  # Force kill if terminate didn't work
            except:
                pass
    
    # Also kill any orphaned python processes from tests
    try:
        current_pid = os.getpid()
        for proc in psutil.process_iter(['pid', 'name', 'cmdline']):
            if proc.info['pid'] == current_pid:
                continue
            if proc.info['name'] == 'python3' or proc.info['name'] == 'python':
                cmdline = proc.info.get('cmdline', [])
                if any('test-' in arg and '.py' in arg for arg in cmdline):
                    print(f"Killing orphaned test process {proc.info['pid']}")
                    proc.kill()
    except:
        pass

# Register cleanup on exit
atexit.register(cleanup)

# Handle signals
def signal_handler(signum, frame):
    print("\nReceived signal, cleaning up...")
    cleanup()
    sys.exit(1)

signal.signal(signal.SIGINT, signal_handler)
signal.signal(signal.SIGTERM, signal_handler)

def run_test(test_script, *args):
    """Run a test script with proper process tracking"""
    cmd = [sys.executable, test_script] + list(args)
    print(f"Running: {' '.join(cmd)}")
    
    proc = subprocess.Popen(cmd)
    child_processes.append(proc)
    
    try:
        return_code = proc.wait()
        return return_code
    except KeyboardInterrupt:
        print("\nInterrupted, cleaning up...")
        cleanup()
        sys.exit(1)
    finally:
        # Remove from tracking if it completed
        if proc in child_processes:
            child_processes.remove(proc)

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: python run-test-with-cleanup.py <test_script.py> [args...]")
        sys.exit(1)
    
    # Install psutil if not available
    try:
        import psutil
    except ImportError:
        print("Installing psutil for process management...")
        subprocess.check_call([sys.executable, "-m", "pip", "install", "psutil"])
        import psutil
    
    exit_code = run_test(sys.argv[1], *sys.argv[2:])
    cleanup()  # Ensure cleanup runs
    sys.exit(exit_code)