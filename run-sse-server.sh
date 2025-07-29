#!/bin/bash

echo "Starting MCP Roslyn SSE Server..."

cd /Users/bill/ClaudeDir/McpDotnet/src/McpRoslyn/McpRoslyn.Server.Sse

dotnet run -- --allowed-path /Users/bill/ClaudeDir/McpDotnet
