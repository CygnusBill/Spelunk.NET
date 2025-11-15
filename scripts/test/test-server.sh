#!/bin/bash

# Get the script's directory and project root
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

echo "Testing MCP Roslyn Server with predefined requests..."
echo "Project root: $PROJECT_ROOT"

# Allow passing custom request file as first argument
REQUEST_FILE="$1"
if [ -z "$REQUEST_FILE" ]; then
    REQUEST_FILE="test-requests.jsonl"
fi

# Check if request file exists
if [ ! -f "$PROJECT_ROOT/$REQUEST_FILE" ]; then
    echo "Error: $REQUEST_FILE not found in project root"
    echo "Please create a file with JSON-RPC requests, one per line"
    exit 1
fi

# Run server with test requests (fix the project path)
cd "$PROJECT_ROOT"
dotnet run --project "src/McpDotnet.Server" -- \
    --allowed-path "$PROJECT_ROOT" \
    < "$PROJECT_ROOT/$REQUEST_FILE"