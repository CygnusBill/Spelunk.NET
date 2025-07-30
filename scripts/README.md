# Scripts Directory

This directory contains utility scripts for running and testing the MCP Roslyn Server.

## Directory Structure

### `run/` - Server Launch Scripts

- **`run-stdio-server.sh`** - Launches the standard STDIO MCP server (primary mode)
- **`run-server-debug.sh`** - Launches server with debug settings
- **`run-sse-server.sh`** - Launches the Server-Sent Events (SSE) variant

### `test/` - Testing Scripts

- **`test-server.sh`** - Runs server with predefined test requests from `test-requests.jsonl`
- **`test-mcp-server.sh`** - Interactive MCP protocol test with named pipes

## Usage

### Running the Server

The primary way to run the server:
```bash
./scripts/run/run-stdio-server.sh
```

For debugging:
```bash
./scripts/run/run-server-debug.sh
```

### Testing

Run predefined tests:
```bash
./scripts/test/test-server.sh
```

Interactive protocol testing:
```bash
./scripts/test/test-mcp-server.sh
```

## Script Details

### Server Run Scripts

All server scripts:
- Set the `--allowed-path` to the project root
- Change to the appropriate project directory
- Use `dotnet run` to start the server

### Test Scripts

**`test-server.sh`**
- Requires `test-requests.jsonl` file with predefined JSON-RPC requests
- Useful for regression testing

**`test-mcp-server.sh`**
- Creates named pipes for bidirectional communication
- Sends initialize, list tools, and tool call requests
- Logs server output to `mcp-server.log`
- Includes cleanup of pipes and processes

## Notes

- All scripts use absolute paths (should be updated if project moves)
- The SSE server is experimental and may not be fully functional
- Test scripts are useful for debugging protocol issues
- Python tests in `/tests/` provide more comprehensive testing