#!/bin/bash

# Get the script's directory and project root
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
CURRENT_DIR="$(pwd)"

echo "Running MCP Roslyn Server with debug settings..."
echo "Current directory: $CURRENT_DIR"
echo "Allowed paths: $CURRENT_DIR"

# Run with debug logging enabled and current directory as allowed path
MCP_ROSLYN_ALLOWED_PATHS="$CURRENT_DIR" ASPNETCORE_ENVIRONMENT=Development dotnet run --project "$PROJECT_ROOT/src/McpRoslyn.Server"