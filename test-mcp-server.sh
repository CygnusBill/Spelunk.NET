#!/bin/bash

# Test the MCP Roslyn server with some JSON-RPC commands

echo "Testing MCP Roslyn Server..."

# Create a named pipe for bidirectional communication
PIPE_IN=/tmp/mcp_test_in
PIPE_OUT=/tmp/mcp_test_out

# Clean up any existing pipes
rm -f $PIPE_IN $PIPE_OUT

# Create named pipes
mkfifo $PIPE_IN
mkfifo $PIPE_OUT

# Start the server in the background, redirecting stderr to a log file
dotnet run --project /Users/bill/ClaudeDir/McpDotnet/src/McpRoslyn/McpRoslyn.Server/McpRoslyn.Server.csproj < $PIPE_IN > $PIPE_OUT 2> mcp-server.log &
SERVER_PID=$!

# Give the server time to start
sleep 2

# Function to send a request and read response
send_request() {
    local request=$1
    echo "Sending: $request"
    echo "$request" > $PIPE_IN
    
    # Read response with timeout
    if timeout 2 head -n 1 < $PIPE_OUT; then
        echo ""
    else
        echo "No response received"
    fi
}

# Test 1: Initialize
send_request '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}'

# Test 2: List tools
send_request '{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}'

# Test 3: Call a tool
send_request '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"dotnet/workspace-status","arguments":{}}}'

# Clean up
echo "Stopping server..."
kill $SERVER_PID 2>/dev/null
rm -f $PIPE_IN $PIPE_OUT

echo "Test complete. Check mcp-server.log for server output."