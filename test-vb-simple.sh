#!/bin/bash

# Simple test script for VB.NET support
PROJECT_ROOT="$(cd "$(dirname "$0")" && pwd)"
SERVER_PATH="$PROJECT_ROOT/src/McpRoslyn/McpRoslyn.Server/bin/Debug/net10.0/McpRoslyn.Server"
VB_PROJECT="$PROJECT_ROOT/test-workspace/VBTestProject/VBTestProject.vbproj"

echo "Testing VB.NET support..."
echo "Server: $SERVER_PATH"
echo "VB Project: $VB_PROJECT"
echo

# Create test request
cat > test-request.jsonl << 'EOF'
{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05"}}
{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"dotnet-load-workspace","arguments":{"path":"VBPROJECT"}}}
{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"dotnet-find-statements","arguments":{"pattern":"Return","patternType":"text"}}}
{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"dotnet-find-statements","arguments":{"pattern":"Throw","patternType":"text"}}}
EOF

# Replace placeholder with actual path
sed -i.bak "s|VBPROJECT|$VB_PROJECT|g" test-request.jsonl

# Run the test
echo "Running test..."
MCP_ROSLYN_ALLOWED_PATHS="$PROJECT_ROOT" cat test-request.jsonl | "$SERVER_PATH" 2>/dev/null | grep -E '"result"|"error"' | jq .

# Cleanup
rm test-request.jsonl test-request.jsonl.bak