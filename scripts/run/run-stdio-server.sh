#!/bin/bash

# Get the script's directory and project root
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
CURRENT_DIR="$(pwd)"

echo "Starting MCP Roslyn Server (STDIO mode)..."
echo "Current directory: $CURRENT_DIR"
echo "Allowed paths: $CURRENT_DIR"

# Run the server with current directory as allowed path
MCP_DOTNET_ALLOWED_PATHS="$CURRENT_DIR" dotnet run --project "$PROJECT_ROOT/src/McpDotnet.Server"