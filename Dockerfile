# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Copy project files
COPY src/Spelunk.Server/*.csproj src/Spelunk.Server/
COPY Spelunk.NET.sln .

# Restore dependencies
RUN dotnet restore src/Spelunk.Server/Spelunk.Server.csproj

# Copy source code
COPY src/Spelunk.Server/ src/Spelunk.Server/

# Build and publish
RUN dotnet publish src/Spelunk.Server/Spelunk.Server.csproj \
    --configuration Release \
    --no-restore \
    --output /app

# Runtime stage - SDK is required for MSBuild.Locator
FROM mcr.microsoft.com/dotnet/sdk:10.0
WORKDIR /app

# Copy build artifacts
COPY --from=build /app .

# Set environment variables
# SPELUNK_ALLOWED_PATHS - comma-separated list of paths the server can access
# Default to /workspace to allow mounting code there
ENV SPELUNK_ALLOWED_PATHS=/workspace

# SPELUNK__LOGGING__MINIMUMLEVEL - Set log level (Trace, Debug, Information, Warning, Error, Critical)
ENV SPELUNK__LOGGING__MINIMUMLEVEL=Information

# Create workspace directory
RUN mkdir -p /workspace

# Set working directory for code operations
WORKDIR /workspace

# The server communicates over stdin/stdout using JSON-RPC
# No ports to expose - it's stdio based

# Health check is not applicable for stdio server
# HEALTHCHECK NONE

ENTRYPOINT ["dotnet", "/app/Spelunk.Server.dll"]

# Optional: Allow passing additional arguments
# CMD []
