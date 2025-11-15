# MCP Roslyn Server Configuration

The MCP Roslyn Server supports a flexible configuration system using .NET's IOptions pattern. Configuration can be provided through multiple sources with the following precedence (highest to lowest):

1. Command-line arguments
2. Environment variables
3. Configuration files
4. Default values

## Configuration Sources

### 1. Configuration Files

The server loads configuration from JSON files in the following order:
- `appsettings.json` - Base configuration
- `appsettings.{Environment}.json` - Environment-specific overrides (e.g., `appsettings.Development.json`)
- `mcp-dotnet.config.json` - User-specific configuration

All configuration files support hot reload - changes are detected automatically without restarting the server.

Example `mcp-dotnet.config.json`:
```json
{
  "McpDotnet": {
    "AllowedPaths": [
      "/path/to/project1",
      "/path/to/project2"
    ],
    "InitialWorkspace": "/path/to/workspace.sln",
    "Logging": {
      "MinimumLevel": "Information",
      "EnableDebugLogging": false
    },
    "Server": {
      "RequestTimeoutSeconds": 120,
      "MaxWorkspaces": 10,
      "EnableExperimentalFeatures": false
    }
  }
}
```

### 2. Environment Variables

Environment variables use the `MCP_DOTNET__` prefix with double underscores for hierarchy:

```bash
# Set allowed paths
export MCP_DOTNET__AllowedPaths__0=/path/to/project1
export MCP_DOTNET__AllowedPaths__1=/path/to/project2

# Set initial workspace
export MCP_DOTNET__InitialWorkspace=/path/to/workspace.sln

# Set logging level
export MCP_DOTNET__Logging__MinimumLevel=Debug

# Set server options
export MCP_DOTNET__Server__RequestTimeoutSeconds=300
export MCP_DOTNET__Server__MaxWorkspaces=20
```

### 3. Command-Line Arguments

Command-line arguments can override any configuration:

```bash
# Long form
dotnet run -- --workspace /path/to/workspace.sln --log-level Debug

# Short form
dotnet run -- -w /path/to/workspace.sln
```

Supported command-line arguments:
- `--workspace` or `-w`: Initial workspace path
- `--allowed-path`: Add an allowed path (first one only)
- `--log-level`: Set minimum log level
- `--config`: Load additional configuration file

### 4. Legacy Environment Variables

For backward compatibility, the following legacy environment variables are still supported:
- `MCP_DOTNET_ALLOWED_PATHS` - Colon-separated list of allowed paths
- `MCP_DOTNET_WORKSPACE` - Initial workspace path

## Configuration Options

### AllowedPaths (Required)
List of file system paths the server is allowed to access. At least one path must be specified.

**Default**: Current directory if none specified
**Type**: `List<string>`

### InitialWorkspace (Optional)
Path to a solution or project file to load on startup.

**Default**: `null`
**Type**: `string?`

### Logging Options

#### MinimumLevel
Minimum log level to output.

**Default**: `Information`
**Type**: `LogLevel` (Trace, Debug, Information, Warning, Error, Critical, None)

#### EnableDebugLogging
Enable verbose debug logging for troubleshooting.

**Default**: `false`
**Type**: `bool`

### Server Options

#### RequestTimeoutSeconds
Maximum time in seconds for a single request to complete.

**Default**: `120`
**Type**: `int` (Range: 1-3600)

#### MaxWorkspaces
Maximum number of concurrent workspaces that can be loaded.

**Default**: `10`
**Type**: `int` (Range: 1-100)

#### EnableExperimentalFeatures
Enable experimental features that may be unstable.

**Default**: `false`
**Type**: `bool`

## Configuration Priority Example

Given the following configuration sources:

1. `appsettings.json`:
```json
{
  "McpDotnet": {
    "AllowedPaths": ["/default/path"],
    "Logging": {
      "MinimumLevel": "Information"
    }
  }
}
```

2. Environment variable:
```bash
export MCP_DOTNET__Logging__MinimumLevel=Debug
```

3. Command line:
```bash
dotnet run -- --allowed-path /cmdline/path
```

The effective configuration would be:
- AllowedPaths: ["/cmdline/path"] (command line wins)
- Logging.MinimumLevel: Debug (environment variable wins)

## Hot Reload

Configuration files (`appsettings.json`, `mcp-dotnet.config.json`) support hot reload. When changes are detected:
1. The new configuration is validated
2. If valid, the server updates its settings without restart
3. A log message confirms the configuration update

Note: Only certain settings support hot reload. Changes to some settings may require a server restart.

## Validation

Configuration is validated on startup and during hot reload. Validation errors include:
- No allowed paths specified
- Invalid timeout values (must be 1-3600 seconds)
- Invalid max workspaces (must be 1-100)

If validation fails on startup, the server will exit with an error message.
If validation fails during hot reload, the change is rejected and the previous configuration remains active.