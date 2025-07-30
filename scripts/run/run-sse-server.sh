#!/bin/bash

# Get the script's directory and project root
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

echo "Starting MCP Roslyn SSE Server..."
echo "Project root: $PROJECT_ROOT"
echo "Note: SSE server is experimental and may not be fully functional"

cd "$PROJECT_ROOT/src/McpRoslyn/McpRoslyn.Server.Sse"

dotnet run -- --allowed-path "$PROJECT_ROOT"
