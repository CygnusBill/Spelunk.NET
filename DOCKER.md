# Docker Guide for MCP Roslyn Server

This guide explains how to build and run the MCP Roslyn Server in a Docker container.

## Quick Start

### Build the Image

```bash
docker build -t mcp-roslyn:latest .
```

### Run the Container

The MCP Roslyn Server uses stdin/stdout for communication, so you need to run it interactively:

```bash
docker run -i \
  -v /path/to/your/code:/workspace \
  -e MCP_ROSLYN_ALLOWED_PATHS=/workspace \
  mcp-roslyn:latest
```

## Usage Patterns

### 1. Interactive Testing

Test the server interactively by piping JSON-RPC requests:

```bash
echo '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}}}' | \
docker run -i \
  -v $(pwd):/workspace \
  -e MCP_ROSLYN_ALLOWED_PATHS=/workspace \
  mcp-roslyn:latest
```

### 2. With Claude Desktop

Add to your Claude Desktop MCP configuration (`~/Library/Application Support/Claude/claude_desktop_config.json` on macOS):

```json
{
  "mcpServers": {
    "roslyn": {
      "command": "docker",
      "args": [
        "run",
        "-i",
        "--rm",
        "-v", "/path/to/your/projects:/workspace",
        "-e", "MCP_ROSLYN_ALLOWED_PATHS=/workspace",
        "mcp-roslyn:latest"
      ]
    }
  }
}
```

### 3. With Multiple Workspaces

Mount multiple directories and grant access to all:

```bash
docker run -i \
  -v /path/to/project1:/workspace/project1:ro \
  -v /path/to/project2:/workspace/project2 \
  -e MCP_ROSLYN_ALLOWED_PATHS=/workspace/project1:/workspace/project2 \
  mcp-roslyn:latest
```

### 4. Development Mode with Hot Reload

For development, mount the source and rebuild on change:

```bash
docker run -i \
  -v $(pwd)/src:/source/src:ro \
  -v /path/to/code:/workspace \
  -e MCP_ROSLYN_ALLOWED_PATHS=/workspace \
  mcp-roslyn:latest
```

## Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `MCP_ROSLYN_ALLOWED_PATHS` | Colon-separated paths the server can access | `/workspace` |
| `MCP_ROSLYN__LOGGING__MINIMUMLEVEL` | Log level (Trace, Debug, Information, Warning, Error, Critical) | `Information` |

**Note**: Environment variable names use double underscores (`__`) to represent nested configuration sections.

## Volume Mounts

The container expects code to be mounted at `/workspace` by default. You can mount:

- **Read-only** (`ro`): For analyzing code without modification
- **Read-write**: For operations that modify code (refactoring, editing)

```bash
# Read-only for analysis
-v /path/to/code:/workspace:ro

# Read-write for modifications
-v /path/to/code:/workspace
```

## Build Options

### Multi-Architecture Build

Build for different architectures:

```bash
# For ARM64 (Apple Silicon, ARM servers)
docker buildx build --platform linux/arm64 -t mcp-roslyn:arm64 .

# For AMD64 (Intel/AMD)
docker buildx build --platform linux/amd64 -t mcp-roslyn:amd64 .

# Multi-platform
docker buildx build --platform linux/amd64,linux/arm64 -t mcp-roslyn:latest .
```

### Development Build

Build with debug configuration:

```dockerfile
# Modify Dockerfile to use Debug instead of Release
RUN dotnet publish src/McpRoslyn.Server/McpRoslyn.Server.csproj \
    --configuration Debug \
    --no-restore \
    --output /app
```

### Smaller Image

The current image uses `mcr.microsoft.com/dotnet/runtime:10.0` (~200MB). For even smaller images, use Alpine:

```dockerfile
FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine
```

## Security Considerations

### Path Restrictions

The server enforces path restrictions via `MCP_ROSLYN_ALLOWED_PATHS`. Only paths in this list can be accessed:

```bash
# Restrict to specific project
-e MCP_ROSLYN_ALLOWED_PATHS=/workspace/myproject

# Multiple paths (colon-separated)
-e MCP_ROSLYN_ALLOWED_PATHS=/workspace/project1:/workspace/project2
```

### Read-Only Mounts

For analysis-only workloads, use read-only mounts:

```bash
docker run -i \
  -v /path/to/code:/workspace:ro \
  -e MCP_ROSLYN_ALLOWED_PATHS=/workspace \
  mcp-roslyn:latest
```

### Network Isolation

The stdio server doesn't require network access. Run with `--network none` for maximum isolation:

```bash
docker run -i --network none \
  -v /path/to/code:/workspace \
  -e MCP_ROSLYN_ALLOWED_PATHS=/workspace \
  mcp-roslyn:latest
```

## Troubleshooting

### Permission Errors

If you encounter permission errors accessing mounted code:

```bash
# Run as current user
docker run -i --user $(id -u):$(id -g) \
  -v /path/to/code:/workspace \
  -e MCP_ROSLYN_ALLOWED_PATHS=/workspace \
  mcp-roslyn:latest
```

### Can't Access Files

Ensure paths in `MCP_ROSLYN_ALLOWED_PATHS` match the container's mount points:

```bash
# Host path: /Users/you/code
# Container path: /workspace
# So use: MCP_ROSLYN_ALLOWED_PATHS=/workspace
```

### Logs Not Appearing

Logs go to stderr by default. To see them:

```bash
docker run -i \
  -v /path/to/code:/workspace \
  -e MCP_ROSLYN_ALLOWED_PATHS=/workspace \
  -e MCP_ROSLYN__LOGGING__MINIMUMLEVEL=Debug \
  mcp-roslyn:latest 2>logs.txt
```

### MSBuild Not Found

If you see "MSBuild not found" errors, the image might need the SDK instead of runtime. Modify Dockerfile:

```dockerfile
# Change runtime to SDK for full MSBuild support
FROM mcr.microsoft.com/dotnet/sdk:10.0
```

## Docker Compose

For easier management, use Docker Compose:

```yaml
# docker-compose.yml
version: '3.8'

services:
  mcp-roslyn:
    build: .
    image: mcp-roslyn:latest
    stdin_open: true
    volumes:
      - /path/to/your/code:/workspace:ro
    environment:
      - MCP_ROSLYN_ALLOWED_PATHS=/workspace
      - MCP_ROSLYN__LOGGING__MINIMUMLEVEL=Information
    network_mode: none
```

Run with:

```bash
docker-compose run --rm mcp-roslyn
```

## Image Size

Expected image sizes:
- **Build stage**: ~2.5 GB (includes .NET SDK)
- **Final runtime image**: ~220 MB (runtime only)

Check your image size:

```bash
docker images mcp-roslyn
```

## Advanced Configuration

### Custom Configuration File

Mount a custom `appsettings.json`:

```bash
docker run -i \
  -v /path/to/code:/workspace \
  -v $(pwd)/appsettings.json:/app/appsettings.json:ro \
  -e MCP_ROSLYN_ALLOWED_PATHS=/workspace \
  mcp-roslyn:latest
```

### Workspace Pre-loading

To preload a specific workspace on startup, modify the Dockerfile:

```dockerfile
ENV MCP_ROSLYN__INITIALWORKSPACE=/workspace/MySolution.sln
```

## Performance Considerations

### Build Cache

Docker caches layers. To optimize builds:

1. Package restore happens before copying source (cache friendly)
2. Only changed files trigger rebuild
3. Use `.dockerignore` to exclude unnecessary files

### Runtime Performance

- The container starts fresh each time (no persistent cache)
- For better performance with repeated use, consider mounting a cache volume:

```bash
-v mcp-roslyn-cache:/root/.nuget
```

## CI/CD Integration

### GitHub Actions

```yaml
- name: Build MCP Roslyn Docker Image
  run: docker build -t mcp-roslyn:${{ github.sha }} .

- name: Test MCP Server
  run: |
    echo '{"jsonrpc":"2.0","id":1,"method":"ping"}' | \
    docker run -i mcp-roslyn:${{ github.sha }}
```

### GitLab CI

```yaml
build:
  script:
    - docker build -t $CI_REGISTRY_IMAGE:$CI_COMMIT_SHA .
    - docker push $CI_REGISTRY_IMAGE:$CI_COMMIT_SHA
```

## Publishing to Container Registry

### Docker Hub

```bash
docker tag mcp-roslyn:latest yourusername/mcp-roslyn:latest
docker push yourusername/mcp-roslyn:latest
```

### GitHub Container Registry

```bash
docker tag mcp-roslyn:latest ghcr.io/yourusername/mcp-roslyn:latest
docker push ghcr.io/yourusername/mcp-roslyn:latest
```

## Updating

To update to the latest version:

```bash
# Rebuild the image
docker build -t mcp-roslyn:latest .

# Or pull if published
docker pull yourusername/mcp-roslyn:latest
```

## License

See the main project LICENSE file for licensing information.
