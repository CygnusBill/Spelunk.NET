#!/bin/bash

echo "Testing MCP Roslyn Server with predefined requests..."

# Run server with test requests
dotnet run --project /Users/bill/ClaudeDir/McpDotnet/src/McpRoslyn/McpRoslyn.Server/McpRoslyn.Server.csproj -- \
    --allowed-path /Users/bill/ClaudeDir/McpDotnet \
    < test-requests.jsonl