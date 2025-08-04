#!/bin/bash

# Get the script's directory and project root
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

echo "Starting MCP Roslyn Server (STDIO mode)..."
echo "Project root: $PROJECT_ROOT"

cd "$PROJECT_ROOT"
dotnet run --project "src/McpRoslyn.Server" -- --allowed-path "$PROJECT_ROOT"