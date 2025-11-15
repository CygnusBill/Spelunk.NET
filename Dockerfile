# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Copy project files
COPY src/McpRoslyn.Server/*.csproj src/McpRoslyn.Server/
COPY McpRoslyn.sln .

# Restore dependencies
RUN dotnet restore src/McpRoslyn.Server/McpRoslyn.Server.csproj

# Copy source code
COPY src/McpRoslyn.Server/ src/McpRoslyn.Server/

# Build and publish
RUN dotnet publish src/McpRoslyn.Server/McpRoslyn.Server.csproj \
    --configuration Release \
    --no-restore \
    --output /app

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app

# Copy build artifacts
COPY --from=build /app .

# Set environment variables
# MCP_ROSLYN_ALLOWED_PATHS - comma-separated list of paths the server can access
# Default to /workspace to allow mounting code there
ENV MCP_ROSLYN_ALLOWED_PATHS=/workspace

# MCP_ROSLYN_LOGGING__MINIMUMLEVEL - Set log level (Trace, Debug, Information, Warning, Error, Critical)
ENV MCP_ROSLYN__LOGGING__MINIMUMLEVEL=Information

# Create workspace directory
RUN mkdir -p /workspace

# Set working directory for code operations
WORKDIR /workspace

# The server communicates over stdin/stdout using JSON-RPC
# No ports to expose - it's stdio based

# Health check is not applicable for stdio server
# HEALTHCHECK NONE

ENTRYPOINT ["dotnet", "/app/McpRoslyn.Server.dll"]

# Optional: Allow passing additional arguments
# CMD []
