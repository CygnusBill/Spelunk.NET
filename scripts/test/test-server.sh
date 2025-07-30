#!/bin/bash

# Get the script's directory and project root
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

echo "Testing MCP Roslyn Server with predefined requests..."
echo "Project root: $PROJECT_ROOT"

# Check if test-requests.jsonl exists
if [ ! -f "$PROJECT_ROOT/test-requests.jsonl" ]; then
    echo "Error: test-requests.jsonl not found in project root"
    echo "Please create a file with JSON-RPC requests, one per line"
    exit 1
fi

# Run server with test requests
dotnet run --project "$PROJECT_ROOT/src/McpRoslyn/McpRoslyn.Server/McpRoslyn.Server.csproj" -- \
    --allowed-path "$PROJECT_ROOT" \
    < "$PROJECT_ROOT/test-requests.jsonl"