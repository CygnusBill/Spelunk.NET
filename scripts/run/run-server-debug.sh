#!/bin/bash

# Get the script's directory and project root
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

echo "Running MCP Roslyn Server with debug settings..."
echo "Project root: $PROJECT_ROOT"

cd "$PROJECT_ROOT/src/McpRoslyn/McpRoslyn.Server"

# Run with debug logging enabled
ASPNETCORE_ENVIRONMENT=Development dotnet run -- --allowed-path "$PROJECT_ROOT"