using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace McpRoslyn.Server;

public class McpJsonRpcServer
{
    private readonly ILogger _logger;
    private readonly Dictionary<string, WorkspaceInfo> _workspaces = new();
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly List<string> _allowedPaths;
    private readonly RoslynWorkspaceManager _workspaceManager;
    
    public McpJsonRpcServer(ILogger logger, List<string> allowedPaths, string? initialWorkspace)
    {
        _logger = logger;
        _allowedPaths = allowedPaths;
        _jsonOptions = new JsonSerializerOptions 
        { 
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false 
        };
        
        // Create logger factory for workspace manager
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .SetMinimumLevel(LogLevel.Information)
                .AddConsole(options =>
                {
                    options.LogToStandardErrorThreshold = LogLevel.Trace;
                });
        });
        
        _workspaceManager = new RoslynWorkspaceManager(loggerFactory.CreateLogger<RoslynWorkspaceManager>());
        
        // Pre-load initial workspace if provided
        if (!string.IsNullOrEmpty(initialWorkspace))
        {
            Task.Run(async () =>
            {
                try
                {
                    await LoadWorkspaceAsync(initialWorkspace);
                    _logger.LogInformation("Pre-loaded workspace: {Path}", initialWorkspace);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to pre-load workspace: {Path}", initialWorkspace);
                }
            });
        }
    }
    
    public async Task RunAsync()
    {
        _logger.LogInformation("MCP Roslyn Server started - listening on stdio");
        
        var reader = Console.In;
        var writer = Console.Out;
        
        // Read JSON-RPC messages from stdin
        while (true)
        {
            try
            {
                var line = await reader.ReadLineAsync();
                if (line == null) break;
                
                // Skip empty lines
                if (string.IsNullOrWhiteSpace(line)) continue;
                
                _logger.LogDebug("Received: {Message}", line);
                
                // Parse JSON-RPC request
                var request = JsonSerializer.Deserialize<JsonRpcRequest>(line, _jsonOptions);
                if (request == null) continue;
                
                // Process request
                var response = await ProcessRequestAsync(request);
                
                // Send response
                await SendResponseAsync(writer, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing request");
                // Send error response
                await SendResponseAsync(writer, new JsonRpcResponse
                {
                    JsonRpc = "2.0",
                    Error = new JsonRpcError
                    {
                        Code = -32603,
                        Message = "Internal error",
                        Data = ex.Message
                    }
                });
            }
        }
        
        _logger.LogInformation("MCP Roslyn Server shutting down");
    }
    
    public async Task<JsonRpcResponse> ProcessRequestAsync(JsonRpcRequest request)
    {
        _logger.LogInformation("Processing method: {Method}", request.Method);
        
        switch (request.Method)
        {
            case "initialize":
                return await HandleInitializeAsync(request);
                
            case "tools/list":
                return HandleToolsList(request);
                
            case "tools/call":
                return await HandleToolCallAsync(request);
                
            case "workspaces/list":
                return HandleWorkspacesList(request);
                
            default:
                return new JsonRpcResponse
                {
                    JsonRpc = "2.0",
                    Id = request.Id,
                    Error = new JsonRpcError
                    {
                        Code = -32601,
                        Message = "Method not found"
                    }
                };
        }
    }
    
    private async Task<JsonRpcResponse> HandleInitializeAsync(JsonRpcRequest request)
    {
        var result = new
        {
            protocolVersion = "2024-11-05",
            capabilities = new
            {
                tools = new { }
            },
            serverInfo = new
            {
                name = "mcp-roslyn",
                version = "0.1.0"
            }
        };
        
        return new JsonRpcResponse
        {
            JsonRpc = "2.0",
            Id = request.Id,
            Result = result
        };
    }
    
    private JsonRpcResponse HandleToolsList(JsonRpcRequest request)
    {
        var tools = new object[]
        {
            new
            {
                name = "dotnet-load-workspace",
                description = "Load a .NET solution or project into the workspace",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "Full path to .sln or .csproj file (relative paths are not supported)" },
                        workspaceId = new { type = "string", description = "Optional workspace ID (auto-generated if not provided)" }
                    },
                    required = new[] { "path" }
                }
            },
            new
            {
                name = "dotnet-analyze-syntax",
                description = "Analyzes the syntax tree of a C# or VB.NET file",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        filePath = new { type = "string", description = "Path to the source file" },
                        workspaceId = new { type = "string", description = "Workspace ID for context" },
                        includeTrivia = new { type = "boolean", description = "Include whitespace and comments" }
                    },
                    required = new[] { "filePath" }
                }
            },
            new
            {
                name = "dotnet-get-symbols",
                description = "Retrieves symbol information from code",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        filePath = new { type = "string", description = "Path to the source file" },
                        workspaceId = new { type = "string", description = "Workspace ID for context" },
                        position = new
                        {
                            type = "object",
                            properties = new
                            {
                                line = new { type = "number" },
                                column = new { type = "number" }
                            }
                        },
                        symbolName = new { type = "string", description = "Specific symbol to find" }
                    },
                    required = new[] { "filePath" }
                }
            },
            new
            {
                name = "dotnet-workspace-status",
                description = "Get loading progress and workspace info",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        workspaceId = new { type = "string", description = "Specific workspace ID (or all if not specified)" }
                    }
                }
            },
            new
            {
                name = "dotnet-find-class",
                description = "Find classes, interfaces, structs, or enums by name pattern (supports * and ? wildcards)",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        pattern = new { type = "string", description = "Name pattern to search for (e.g., '*Controller', 'I*Service', 'User?')" },
                        workspacePath = new { type = "string", description = "Optional workspace path to search in (searches all if not specified)" }
                    },
                    required = new[] { "pattern" }
                }
            },
            new
            {
                name = "dotnet-find-method",
                description = "Find methods by name pattern with optional class pattern filter (supports * and ? wildcards)",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        methodPattern = new { type = "string", description = "Method name pattern (e.g., 'Get*', '*Async', 'Load?')" },
                        classPattern = new { type = "string", description = "Optional class name pattern to filter by (e.g., '*Controller', 'Base*')" },
                        workspacePath = new { type = "string", description = "Optional workspace path to search in" }
                    },
                    required = new[] { "methodPattern" }
                }
            },
            new
            {
                name = "dotnet-find-property",
                description = "Find properties and fields by name pattern with optional class pattern filter (supports * and ? wildcards)",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        propertyPattern = new { type = "string", description = "Property/field name pattern (e.g., 'Is*', '*Count', '_*')" },
                        classPattern = new { type = "string", description = "Optional class name pattern to filter by" },
                        workspacePath = new { type = "string", description = "Optional workspace path to search in" }
                    },
                    required = new[] { "propertyPattern" }
                }
            },
            new
            {
                name = "dotnet-find-method-calls",
                description = "Find all methods called by a specific method (call tree analysis)",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        methodName = new { type = "string", description = "Name of the method to analyze" },
                        className = new { type = "string", description = "Name of the class containing the method" },
                        workspacePath = new { type = "string", description = "Optional workspace path to search in" }
                    },
                    required = new[] { "methodName", "className" }
                }
            },
            new
            {
                name = "dotnet-find-method-callers",
                description = "Find all methods that call a specific method (caller tree analysis)",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        methodName = new { type = "string", description = "Name of the method to find callers for" },
                        className = new { type = "string", description = "Name of the class containing the method" },
                        workspacePath = new { type = "string", description = "Optional workspace path to search in" }
                    },
                    required = new[] { "methodName", "className" }
                }
            },
            new
            {
                name = "dotnet-find-references",
                description = "Find all references to a type, method, property, or field",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        symbolName = new { type = "string", description = "Name of the symbol to find references for" },
                        symbolType = new { type = "string", description = "Type of symbol: 'type', 'method', 'property', 'field'" },
                        containerName = new { type = "string", description = "Optional: containing type name (for methods/properties/fields)" },
                        workspacePath = new { type = "string", description = "Optional workspace path to search in" }
                    },
                    required = new[] { "symbolName", "symbolType" }
                }
            },
            new
            {
                name = "dotnet-find-implementations",
                description = "Find all implementations of an interface or abstract class",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        interfaceName = new { type = "string", description = "Name of the interface or abstract class" },
                        workspacePath = new { type = "string", description = "Optional workspace path to search in" }
                    },
                    required = new[] { "interfaceName" }
                }
            },
            new
            {
                name = "dotnet-find-overrides",
                description = "Find all overrides of a virtual or abstract method",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        methodName = new { type = "string", description = "Name of the virtual/abstract method" },
                        className = new { type = "string", description = "Name of the class containing the method" },
                        workspacePath = new { type = "string", description = "Optional workspace path to search in" }
                    },
                    required = new[] { "methodName", "className" }
                }
            },
            new
            {
                name = "dotnet-find-derived-types",
                description = "Find all types that derive from a base class",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        baseClassName = new { type = "string", description = "Name of the base class" },
                        workspacePath = new { type = "string", description = "Optional workspace path to search in" }
                    },
                    required = new[] { "baseClassName" }
                }
            },
            new
            {
                name = "dotnet-rename-symbol",
                description = "Rename a symbol (type, method, property, field) and update all references",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        oldName = new { type = "string", description = "Current name of the symbol" },
                        newName = new { type = "string", description = "New name for the symbol" },
                        symbolType = new { type = "string", description = "Type of symbol: 'type', 'method', 'property', 'field'" },
                        containerName = new { type = "string", description = "Optional: containing type name (for methods/properties/fields)" },
                        workspacePath = new { type = "string", description = "Optional workspace path to apply rename in" },
                        preview = new { type = "boolean", description = "If true, only preview changes without applying them" }
                    },
                    required = new[] { "oldName", "newName", "symbolType" }
                }
            },
            new
            {
                name = "dotnet-edit-code",
                description = "Perform surgical code edits using Roslyn. Operations: add-method, add-property, make-async, add-parameter, wrap-try-catch",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        file = new { type = "string", description = "Path to the source file to edit" },
                        operation = new { type = "string", description = "Operation type: add-method, add-property, make-async, add-parameter, wrap-try-catch" },
                        className = new { type = "string", description = "Name of the class to modify" },
                        methodName = new { type = "string", description = "Method name (for method operations)" },
                        code = new { type = "string", description = "Code to add (for add operations)" },
                        parameters = new { type = "object", description = "Operation-specific parameters" },
                        preview = new { type = "boolean", description = "If true, show changes without applying" }
                    },
                    required = new[] { "file", "operation", "className" }
                }
            },
            new
            {
                name = "dotnet-fix-pattern",
                description = "Find code matching a pattern and transform it to a new pattern",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        findPattern = new { type = "string", description = "Pattern to search for (supports wildcards)" },
                        replacePattern = new { type = "string", description = "Pattern to replace with" },
                        patternType = new { type = "string", description = "Type of pattern: 'method-call', 'property-access', 'async-usage', 'null-check', 'string-format'" },
                        workspacePath = new { type = "string", description = "Optional workspace path to apply fixes in" },
                        preview = new { type = "boolean", description = "If true, only preview changes without applying them" }
                    },
                    required = new[] { "findPattern", "replacePattern", "patternType" }
                }
            },
            new
            {
                name = "dotnet-find-statements",
                description = "Find statements in code matching a pattern. Supports text search, regex, and RoslynPath (XPath-style queries for C# AST). Returns statement IDs for use with other operations.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        pattern = new { type = "string", description = "Text, regex, or RoslynPath pattern to match in statements. RoslynPath allows XPath-style queries like '//method[Get*]//statement[@type=ThrowStatement]'" },
                        scope = new 
                        { 
                            type = "object", 
                            description = "Optional scope to limit search",
                            properties = new
                            {
                                file = new { type = "string", description = "File path to search in" },
                                className = new { type = "string", description = "Class name to search within" },
                                methodName = new { type = "string", description = "Method name to search within" }
                            }
                        },
                        patternType = new { type = "string", description = "Pattern type: 'text' (default), 'regex', or 'roslynpath' for XPath-style queries", @enum = new[] { "text", "regex", "roslynpath" } },
                        includeNestedStatements = new { type = "boolean", description = "Include statements inside blocks (if/while/for bodies)" },
                        groupRelated = new { type = "boolean", description = "Group statements that share data flow or are in sequence" },
                        workspacePath = new { type = "string", description = "Optional workspace path to search in" }
                    },
                    required = new[] { "pattern" }
                }
            },
            new
            {
                name = "dotnet-replace-statement",
                description = "Replace a statement with new code. The statement is identified by its location from find-statements. Preserves indentation and formatting context.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        statementId = new { type = "string", description = "Statement ID from find-statements (e.g., 'stmt-123')" },
                        location = new 
                        { 
                            type = "object", 
                            description = "Alternative to statementId - direct location",
                            properties = new
                            {
                                file = new { type = "string", description = "File path" },
                                line = new { type = "number", description = "Line number (1-based)" },
                                column = new { type = "number", description = "Column number (1-based)" }
                            },
                            required = new[] { "file", "line", "column" }
                        },
                        newStatement = new { type = "string", description = "The new statement code (including semicolon)" },
                        preserveComments = new { type = "boolean", description = "Keep existing comments attached to the statement", @default = true },
                        workspacePath = new { type = "string", description = "Optional workspace path" }
                    },
                    required = new[] { "newStatement" }
                }
            },
            new
            {
                name = "dotnet-insert-statement",
                description = "Insert a new statement before or after an existing statement. The reference statement is identified by its location from find-statements. Preserves indentation and formatting context.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        position = new { type = "string", description = "Where to insert: 'before' or 'after' the reference statement", @enum = new[] { "before", "after" } },
                        location = new 
                        { 
                            type = "object", 
                            description = "Location of the reference statement",
                            properties = new
                            {
                                file = new { type = "string", description = "File path" },
                                line = new { type = "number", description = "Line number (1-based)" },
                                column = new { type = "number", description = "Column number (1-based)" }
                            },
                            required = new[] { "file", "line", "column" }
                        },
                        statement = new { type = "string", description = "The new statement to insert (including semicolon)" },
                        workspacePath = new { type = "string", description = "Optional workspace path" }
                    },
                    required = new[] { "position", "location", "statement" }
                }
            },
            new
            {
                name = "dotnet-remove-statement",
                description = "Remove a statement from the code. The statement is identified by its location from find-statements. Can preserve comments attached to the statement.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        location = new 
                        { 
                            type = "object", 
                            description = "Location of the statement to remove",
                            properties = new
                            {
                                file = new { type = "string", description = "File path" },
                                line = new { type = "number", description = "Line number (1-based)" },
                                column = new { type = "number", description = "Column number (1-based)" }
                            },
                            required = new[] { "file", "line", "column" }
                        },
                        preserveComments = new { type = "boolean", description = "Preserve comments attached to the statement", @default = true },
                        workspacePath = new { type = "string", description = "Optional workspace path" }
                    },
                    required = new[] { "location" }
                }
            },
            new
            {
                name = "dotnet-mark-statement",
                description = "Mark a statement with an ephemeral marker for later reference. Markers are session-scoped and not persisted.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        location = new 
                        { 
                            type = "object", 
                            description = "Location of the statement to mark",
                            properties = new
                            {
                                file = new { type = "string", description = "File path" },
                                line = new { type = "number", description = "Line number (1-based)" },
                                column = new { type = "number", description = "Column number (1-based)" }
                            },
                            required = new[] { "file", "line", "column" }
                        },
                        label = new { type = "string", description = "Optional label for the marker" },
                        workspacePath = new { type = "string", description = "Optional workspace path" }
                    },
                    required = new[] { "location" }
                }
            },
            new
            {
                name = "dotnet-find-marked-statements",
                description = "Find all marked statements or specific markers. Returns current locations which may have changed due to edits.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        markerId = new { type = "string", description = "Optional marker ID to find specific marker" },
                        filePath = new { type = "string", description = "Optional file path to search within" }
                    }
                }
            },
            new
            {
                name = "dotnet-unmark-statement",
                description = "Remove a specific marker from a statement.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        markerId = new { type = "string", description = "The marker ID to remove" }
                    },
                    required = new[] { "markerId" }
                }
            },
            new
            {
                name = "dotnet-clear-markers",
                description = "Clear all markers from the current session.",
                inputSchema = new
                {
                    type = "object",
                    properties = new { }
                }
            }
        };
        
        return new JsonRpcResponse
        {
            JsonRpc = "2.0",
            Id = request.Id,
            Result = new { tools }
        };
    }
    
    private async Task<JsonRpcResponse> HandleToolCallAsync(JsonRpcRequest request)
    {
        var toolCallParams = request.Params?.Deserialize<ToolCallParams>(_jsonOptions);
        if (toolCallParams == null)
        {
            return new JsonRpcResponse
            {
                JsonRpc = "2.0",
                Id = request.Id,
                Error = new JsonRpcError
                {
                    Code = -32602,
                    Message = "Invalid params"
                }
            };
        }
        
        object result;
        switch (toolCallParams.Name)
        {
            case "dotnet-load-workspace":
                result = await LoadWorkspaceToolAsync(toolCallParams.Arguments);
                break;
                
            case "dotnet-analyze-syntax":
                result = await AnalyzeSyntaxAsync(toolCallParams.Arguments);
                break;
                
            case "dotnet-get-symbols":
                result = await GetSymbolsAsync(toolCallParams.Arguments);
                break;
                
            case "dotnet-workspace-status":
                result = await GetWorkspaceStatusAsync(toolCallParams.Arguments);
                break;
                
            case "dotnet-find-class":
                result = await FindClassAsync(toolCallParams.Arguments);
                break;
                
            case "dotnet-find-method":
                result = await FindMethodAsync(toolCallParams.Arguments);
                break;
                
            case "dotnet-find-property":
                result = await FindPropertyAsync(toolCallParams.Arguments);
                break;
                
            case "dotnet-find-method-calls":
                result = await FindMethodCallsAsync(toolCallParams.Arguments);
                break;
                
            case "dotnet-find-method-callers":
                result = await FindMethodCallersAsync(toolCallParams.Arguments);
                break;
                
            case "dotnet-find-references":
                result = await FindReferencesAsync(toolCallParams.Arguments);
                break;
                
            case "dotnet-find-implementations":
                result = await FindImplementationsAsync(toolCallParams.Arguments);
                break;
                
            case "dotnet-find-overrides":
                result = await FindOverridesAsync(toolCallParams.Arguments);
                break;
                
            case "dotnet-find-derived-types":
                result = await FindDerivedTypesAsync(toolCallParams.Arguments);
                break;
                
            case "dotnet-rename-symbol":
                result = await RenameSymbolAsync(toolCallParams.Arguments);
                break;
                
            case "dotnet-edit-code":
                result = await EditCodeAsync(toolCallParams.Arguments);
                break;
                
            case "dotnet-fix-pattern":
                result = await FixPatternAsync(toolCallParams.Arguments);
                break;
                
            case "dotnet-find-statements":
                result = await FindStatementsAsync(toolCallParams.Arguments);
                break;
                
            case "dotnet-replace-statement":
                result = await ReplaceStatementAsync(toolCallParams.Arguments);
                break;
                
            case "dotnet-insert-statement":
                result = await InsertStatementAsync(toolCallParams.Arguments);
                break;
                
            case "dotnet-remove-statement":
                result = await RemoveStatementAsync(toolCallParams.Arguments);
                break;
                
            case "dotnet-mark-statement":
                result = await MarkStatementAsync(toolCallParams.Arguments);
                break;
                
            case "dotnet-find-marked-statements":
                result = await FindMarkedStatementsAsync(toolCallParams.Arguments);
                break;
                
            case "dotnet-unmark-statement":
                result = await UnmarkStatementAsync(toolCallParams.Arguments);
                break;
                
            case "dotnet-clear-markers":
                result = await ClearMarkersAsync(toolCallParams.Arguments);
                break;
                
            default:
                return new JsonRpcResponse
                {
                    JsonRpc = "2.0",
                    Id = request.Id,
                    Error = new JsonRpcError
                    {
                        Code = -32602,
                        Message = $"Unknown tool: {toolCallParams.Name}"
                    }
                };
        }
        
        return new JsonRpcResponse
        {
            JsonRpc = "2.0",
            Id = request.Id,
            Result = result
        };
    }
    
    private async Task<object> AnalyzeSyntaxAsync(JsonElement? args)
    {
        if (args == null)
            return CreateErrorResponse("No arguments provided");
            
        var filePath = args.Value.GetProperty("filePath").GetString();
        if (string.IsNullOrEmpty(filePath))
            return CreateErrorResponse("File path is required");
            
        var workspaceId = args.Value.TryGetProperty("workspaceId", out var wsId) ? wsId.GetString() : null;
        var includeTrivia = args.Value.TryGetProperty("includeTrivia", out var trivia) ? trivia.GetBoolean() : false;
        
        try
        {
            return await _workspaceManager.AnalyzeSyntaxTreeAsync(filePath, includeTrivia, workspaceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing syntax");
            return CreateErrorResponse($"Error analyzing syntax: {ex.Message}");
        }
    }
    
    private async Task<object> GetSymbolsAsync(JsonElement? args)
    {
        if (args == null)
            return CreateErrorResponse("No arguments provided");
            
        var filePath = args.Value.GetProperty("filePath").GetString();
        if (string.IsNullOrEmpty(filePath))
            return CreateErrorResponse("File path is required");
            
        var workspaceId = args.Value.TryGetProperty("workspaceId", out var wsId) ? wsId.GetString() : null;
        var symbolName = args.Value.TryGetProperty("symbolName", out var sym) ? sym.GetString() : null;
        var hasPosition = args.Value.TryGetProperty("position", out var pos);
        
        try
        {
            if (!string.IsNullOrEmpty(symbolName))
            {
                // Use RoslynPath to find symbols by name
                return await _workspaceManager.GetSymbolsByNameAsync(filePath, symbolName, workspaceId);
            }
            else if (hasPosition)
            {
                // Get symbol at specific position
                var line = pos.GetProperty("line").GetInt32();
                var column = pos.GetProperty("column").GetInt32();
                return await _workspaceManager.GetSymbolAtPositionAsync(filePath, line, column, workspaceId);
            }
            else
            {
                // List all symbols in the file
                return await _workspaceManager.GetAllSymbolsAsync(filePath, workspaceId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving symbols");
            return CreateErrorResponse($"Error retrieving symbols: {ex.Message}");
        }
    }
    
    private async Task<object> GetWorkspaceStatusAsync(JsonElement? args)
    {
        var workspaceId = args?.TryGetProperty("workspaceId", out var wsId) == true ? wsId.GetString() : null;
        
        // Get detailed status from Roslyn workspace manager
        var roslynStatus = _workspaceManager.GetStatus();
        
        // Synchronize local workspace tracking with RoslynWorkspaceManager
        SynchronizeWorkspaceTracking(roslynStatus);
        
        if (!string.IsNullOrEmpty(workspaceId))
        {
            // Return specific workspace info
            if (_workspaces.TryGetValue(workspaceId, out var workspace))
            {
                // Find corresponding Roslyn workspace details
                var roslynWorkspaceInfo = roslynStatus.Workspaces
                    .FirstOrDefault(w => ((dynamic)w).path == workspace.Path);
                
                var detailedInfo = new
                {
                    workspace.Id,
                    workspace.Path,
                    workspace.Type,
                    workspace.Status,
                    workspace.LoadedAt,
                    workspace.ProjectCount,
                    projects = roslynWorkspaceInfo != null ? ((dynamic)roslynWorkspaceInfo).projects : null
                };
                
                return new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = JsonSerializer.Serialize(detailedInfo, new JsonSerializerOptions { WriteIndented = true })
                        }
                    }
                };
            }
            else
            {
                return _workspaceManager.CreateErrorResponseWithReloadInstructions("Workspace not found", workspaceId);
            }
        }
        
        // Return all workspaces with Roslyn details
        var allWorkspaces = _workspaces.Select(kvp => 
        {
            var roslynWorkspaceInfo = roslynStatus.Workspaces
                .FirstOrDefault(w => ((dynamic)w).path == kvp.Value.Path);
                
            return new
            {
                id = kvp.Key,
                path = kvp.Value.Path,
                type = kvp.Value.Type,
                status = kvp.Value.Status,
                loadedAt = kvp.Value.LoadedAt,
                projectCount = kvp.Value.ProjectCount,
                projects = roslynWorkspaceInfo != null ? ((dynamic)roslynWorkspaceInfo).projects : null
            };
        }).ToArray();
        
        return new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = JsonSerializer.Serialize(new
                    {
                        workspaceCount = _workspaces.Count,
                        totalProjectCount = roslynStatus.Workspaces.Sum(w => ((dynamic)w).projectCount),
                        workspaces = allWorkspaces
                    }, new JsonSerializerOptions { WriteIndented = true })
                }
            }
        };
    }
    
    private async Task<object> LoadWorkspaceToolAsync(JsonElement? args)
    {
        var path = args?.GetProperty("path").GetString();
        if (string.IsNullOrEmpty(path))
        {
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = "Error: 'path' parameter is required"
                    }
                }
            };
        }
        
        var workspaceId = args?.TryGetProperty("workspaceId", out var wsId) == true ? wsId.GetString() : null;
        
        try
        {
            var result = await LoadWorkspaceAsync(path, workspaceId);
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                    }
                }
            };
        }
        catch (Exception ex)
        {
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = $"Error loading workspace: {ex.Message}"
                    }
                }
            };
        }
    }
    
    private async Task<WorkspaceInfo> LoadWorkspaceAsync(string path, string? workspaceId = null)
    {
        // Validate path is allowed
        var fullPath = Path.GetFullPath(path);
        if (!IsPathAllowed(fullPath))
        {
            throw new UnauthorizedAccessException($"Path '{fullPath}' is not in allowed paths");
        }
        
        // Generate workspace ID if not provided
        if (string.IsNullOrEmpty(workspaceId))
        {
            workspaceId = Path.GetFileNameWithoutExtension(path) + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        }
        
        // Check if already loaded
        if (_workspaces.ContainsKey(workspaceId))
        {
            _logger.LogInformation("Workspace {Id} already loaded", workspaceId);
            return _workspaces[workspaceId];
        }
        
        // Load workspace using Roslyn
        _logger.LogInformation("Loading workspace {Id} from {Path} using Roslyn", workspaceId, path);
        
        var (success, message, workspace, actualWorkspaceId) = await _workspaceManager.LoadWorkspaceAsync(fullPath, workspaceId);
        
        if (!success || workspace == null)
        {
            throw new InvalidOperationException($"Failed to load workspace: {message}");
        }
        
        // Use the actual workspace ID returned by the manager
        workspaceId = actualWorkspaceId;
        
        // Count projects in the workspace
        var projectCount = workspace.CurrentSolution.Projects.Count();
        
        var workspaceInfo = new WorkspaceInfo
        {
            Id = workspaceId,
            Path = fullPath,
            Type = path.EndsWith(".sln") ? "Solution" : "Project",
            Status = "Loaded",
            LoadedAt = DateTime.UtcNow,
            ProjectCount = projectCount
        };
        
        _workspaces[workspaceId] = workspaceInfo;
        _logger.LogInformation("Loaded workspace {Id} from {Path} with {Count} projects", workspaceId, path, projectCount);
        
        return workspaceInfo;
    }
    
    private object CreateErrorResponse(string message)
    {
        return new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = $"Error: {message}"
                }
            }
        };
    }
    
    private bool IsPathAllowed(string path)
    {
        var normalizedPath = Path.GetFullPath(path);
        return _allowedPaths.Any(allowed => 
            normalizedPath.StartsWith(Path.GetFullPath(allowed), StringComparison.OrdinalIgnoreCase));
    }
    
    private JsonRpcResponse HandleWorkspacesList(JsonRpcRequest request)
    {
        var workspaces = _workspaces.Select(kvp => new
        {
            id = kvp.Key,
            path = kvp.Value.Path,
            type = kvp.Value.Type,
            status = kvp.Value.Status,
            loadedAt = kvp.Value.LoadedAt,
            projectCount = kvp.Value.ProjectCount
        }).ToArray();
        
        return new JsonRpcResponse
        {
            JsonRpc = "2.0",
            Id = request.Id,
            Result = new { workspaces }
        };
    }
    
    private async Task SendResponseAsync(TextWriter writer, JsonRpcResponse response)
    {
        var json = JsonSerializer.Serialize(response, _jsonOptions);
        await writer.WriteLineAsync(json);
        await writer.FlushAsync();
        _logger.LogDebug("Sent: {Response}", json);
    }
    
    private async Task<object> FindClassAsync(JsonElement? args)
    {
        var pattern = args?.GetProperty("pattern").GetString();
        if (string.IsNullOrEmpty(pattern))
        {
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = "Error: Pattern is required"
                    }
                }
            };
        }
        
        var workspacePath = args?.TryGetProperty("workspacePath", out var ws) == true ? ws.GetString() : null;
        
        try
        {
            var results = await _workspaceManager.FindClassesAsync(pattern, workspacePath);
            
            if (results.Count == 0)
            {
                return new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = $"No types found matching pattern '{pattern}'"
                        }
                    }
                };
            }
            
            // Format results as a detailed report
            var report = $"Found {results.Count} types matching pattern '{pattern}':\n\n";
            
            foreach (var result in results)
            {
                report += $"• {result.FullyQualifiedName}\n";
                report += $"  Type: {result.TypeKind}\n";
                report += $"  File: {result.FilePath}:{result.Line}:{result.Column}\n";
                report += $"  Project: {result.ProjectName}\n";
                if (!string.IsNullOrEmpty(result.Namespace))
                    report += $"  Namespace: {result.Namespace}\n";
                report += "\n";
            }
            
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = report
                    }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find classes with pattern {Pattern}", pattern);
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = $"Error searching for classes: {ex.Message}"
                    }
                }
            };
        }
    }
    
    private async Task<object> FindMethodAsync(JsonElement? args)
    {
        var methodPattern = args?.GetProperty("methodPattern").GetString();
        if (string.IsNullOrEmpty(methodPattern))
        {
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = "Error: Method pattern is required"
                    }
                }
            };
        }
        
        var classPattern = args?.TryGetProperty("classPattern", out var cp) == true ? cp.GetString() : null;
        var workspacePath = args?.TryGetProperty("workspacePath", out var ws) == true ? ws.GetString() : null;
        
        try
        {
            var results = await _workspaceManager.FindMethodsAsync(methodPattern, classPattern, workspacePath);
            
            if (results.Count == 0)
            {
                return new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = $"No methods found matching pattern '{methodPattern}'" + 
                                   (classPattern != null ? $" in classes matching '{classPattern}'" : "")
                        }
                    }
                };
            }
            
            // Format results
            var report = $"Found {results.Count} methods matching pattern '{methodPattern}'";
            if (classPattern != null) report += $" in classes matching '{classPattern}'";
            report += ":\n\n";
            
            foreach (var result in results)
            {
                report += $"• {result.ClassName}.{result.MemberName}{result.Parameters}\n";
                report += $"  Returns: {result.ReturnType}\n";
                report += $"  Access: {result.AccessModifier}";
                if (result.IsStatic) report += " static";
                if (result.IsAsync) report += " async";
                report += "\n";
                report += $"  File: {result.FilePath}:{result.Line}:{result.Column}\n";
                report += $"  Project: {result.ProjectName}\n\n";
            }
            
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = report
                    }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find methods with pattern {Pattern}", methodPattern);
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = $"Error searching for methods: {ex.Message}"
                    }
                }
            };
        }
    }
    
    private async Task<object> FindPropertyAsync(JsonElement? args)
    {
        var propertyPattern = args?.GetProperty("propertyPattern").GetString();
        if (string.IsNullOrEmpty(propertyPattern))
        {
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = "Error: Property pattern is required"
                    }
                }
            };
        }
        
        var classPattern = args?.TryGetProperty("classPattern", out var cp) == true ? cp.GetString() : null;
        var workspacePath = args?.TryGetProperty("workspacePath", out var ws) == true ? ws.GetString() : null;
        
        try
        {
            var results = await _workspaceManager.FindPropertiesAsync(propertyPattern, classPattern, workspacePath);
            
            if (results.Count == 0)
            {
                return new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = $"No properties/fields found matching pattern '{propertyPattern}'" + 
                                   (classPattern != null ? $" in classes matching '{classPattern}'" : "")
                        }
                    }
                };
            }
            
            // Format results
            var report = $"Found {results.Count} properties/fields matching pattern '{propertyPattern}'";
            if (classPattern != null) report += $" in classes matching '{classPattern}'";
            report += ":\n\n";
            
            // Group by type
            var grouped = results.GroupBy(r => r.MemberType);
            
            foreach (var group in grouped)
            {
                if (grouped.Count() > 1)
                    report += $"=== {group.Key}s ===\n";
                
                foreach (var result in group)
                {
                    report += $"• {result.ClassName}.{result.MemberName}\n";
                    report += $"  Type: {result.ReturnType}\n";
                    report += $"  Access: {result.AccessModifier}";
                    if (result.IsStatic) report += " static";
                    if (result.IsReadOnly == true) report += " readonly";
                    if (result.MemberType == "Property")
                    {
                        var accessors = new List<string>();
                        if (result.HasGetter == true) accessors.Add("get");
                        if (result.HasSetter == true) accessors.Add("set");
                        if (accessors.Any()) report += $" {{ {string.Join("; ", accessors)} }}";
                    }
                    report += "\n";
                    report += $"  File: {result.FilePath}:{result.Line}:{result.Column}\n";
                    report += $"  Project: {result.ProjectName}\n\n";
                }
            }
            
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = report
                    }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find properties with pattern {Pattern}", propertyPattern);
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = $"Error searching for properties: {ex.Message}"
                    }
                }
            };
        }
    }
    
    private async Task<object> FindMethodCallsAsync(JsonElement? args)
    {
        var methodName = args?.GetProperty("methodName").GetString();
        var className = args?.GetProperty("className").GetString();
        
        if (string.IsNullOrEmpty(methodName) || string.IsNullOrEmpty(className))
        {
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = "Error: Method name and class name are required"
                    }
                }
            };
        }
        
        var workspacePath = args?.TryGetProperty("workspacePath", out var ws) == true ? ws.GetString() : null;
        
        try
        {
            var analysis = await _workspaceManager.FindMethodCallsAsync(methodName, className, workspacePath);
            
            if (!string.IsNullOrEmpty(analysis.Error))
            {
                return new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = analysis.Error
                        }
                    }
                };
            }
            
            // Format results
            var report = $"Call analysis for {analysis.TargetMethod}:\n\n";
            
            // Direct calls
            if (analysis.DirectCalls.Any())
            {
                report += $"=== Direct Calls ({analysis.DirectCalls.Count}) ===\n";
                foreach (var call in analysis.DirectCalls.OrderBy(c => c.MethodSignature))
                {
                    report += $"• {call.MethodSignature}\n";
                    report += $"  Location: {call.FilePath}:{call.Line}:{call.Column}\n";
                    if (call.IsExternal) report += $"  External: Yes (from {call.ProjectName})\n";
                    report += "\n";
                }
            }
            else
            {
                report += "No direct method calls found.\n\n";
            }
            
            // Call tree
            if (analysis.CallTree.Any())
            {
                report += $"\n=== Call Tree (depth limit: 5) ===\n";
                var printed = new HashSet<string>();
                report += PrintCallTree(analysis.TargetMethod, analysis.CallTree, printed, 0);
            }
            
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = report
                    }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find method calls for {Method}", $"{className}.{methodName}");
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = $"Error analyzing method calls: {ex.Message}"
                    }
                }
            };
        }
    }
    
    private string PrintCallTree(string methodKey, Dictionary<string, List<MethodCallInfo>> tree, HashSet<string> printed, int depth)
    {
        if (printed.Contains(methodKey)) return "";
        printed.Add(methodKey);
        
        var result = "";
        var indent = new string(' ', depth * 2);
        
        if (tree.TryGetValue(methodKey, out var calls))
        {
            foreach (var call in calls)
            {
                result += $"{indent}└─ {call.MethodSignature}";
                if (call.IsExternal) result += " [External]";
                result += "\n";
                
                // Recursively print subcalls
                if (!call.IsExternal)
                {
                    result += PrintCallTree(call.MethodSignature, tree, printed, depth + 1);
                }
            }
        }
        
        return result;
    }
    
    private async Task<object> FindMethodCallersAsync(JsonElement? args)
    {
        var methodName = args?.GetProperty("methodName").GetString();
        var className = args?.GetProperty("className").GetString();
        
        if (string.IsNullOrEmpty(methodName) || string.IsNullOrEmpty(className))
        {
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = "Error: Method name and class name are required"
                    }
                }
            };
        }
        
        var workspacePath = args?.TryGetProperty("workspacePath", out var ws) == true ? ws.GetString() : null;
        
        try
        {
            var analysis = await _workspaceManager.FindMethodCallersAsync(methodName, className, workspacePath);
            
            if (!string.IsNullOrEmpty(analysis.Error))
            {
                return new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = analysis.Error
                        }
                    }
                };
            }
            
            // Format results
            var report = $"Caller analysis for {analysis.TargetMethod}:\n\n";
            
            // Direct callers
            if (analysis.DirectCallers.Any())
            {
                report += $"=== Direct Callers ({analysis.DirectCallers.Count}) ===\n";
                foreach (var caller in analysis.DirectCallers.OrderBy(c => c.MethodSignature))
                {
                    report += $"• {caller.MethodSignature}\n";
                    report += $"  Calls from: {caller.FilePath}:{caller.Line}:{caller.Column}\n";
                    report += $"  Project: {caller.ProjectName}\n\n";
                }
            }
            else
            {
                report += "No direct callers found.\n\n";
            }
            
            // Caller tree
            if (analysis.CallerTree.Any())
            {
                report += $"\n=== Caller Tree (who calls whom, depth limit: 5) ===\n";
                
                // Find root callers (methods that aren't called by others in the tree)
                var allCalledMethods = new HashSet<string>();
                foreach (var callers in analysis.CallerTree.Values)
                {
                    foreach (var caller in callers)
                    {
                        allCalledMethods.Add(caller.MethodSignature);
                    }
                }
                
                // Start from the target method
                report += $"{analysis.TargetMethod}\n";
                if (analysis.CallerTree.TryGetValue(analysis.TargetMethod, out var targetCallers))
                {
                    report += PrintCallerTree(targetCallers, analysis.CallerTree, new HashSet<string>(), 1);
                }
            }
            
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = report
                    }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find method callers for {Method}", $"{className}.{methodName}");
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = $"Error analyzing method callers: {ex.Message}"
                    }
                }
            };
        }
    }
    
    private string PrintCallerTree(List<MethodCallInfo> callers, Dictionary<string, List<MethodCallInfo>> tree, HashSet<string> printed, int depth)
    {
        var result = "";
        var indent = new string(' ', depth * 2);
        
        foreach (var caller in callers)
        {
            if (printed.Contains(caller.MethodSignature)) 
            {
                result += $"{indent}└─ {caller.MethodSignature} [Already shown]\n";
                continue;
            }
            
            printed.Add(caller.MethodSignature);
            result += $"{indent}└─ {caller.MethodSignature}\n";
            
            // Recursively print who calls this caller
            if (tree.TryGetValue(caller.MethodSignature, out var callerCallers))
            {
                result += PrintCallerTree(callerCallers, tree, printed, depth + 1);
            }
        }
        
        return result;
    }
    
    private async Task<object> FindReferencesAsync(JsonElement? args)
    {
        var symbolName = args?.GetProperty("symbolName").GetString();
        var symbolType = args?.GetProperty("symbolType").GetString();
        
        if (string.IsNullOrEmpty(symbolName) || string.IsNullOrEmpty(symbolType))
        {
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = "Error: Symbol name and type are required"
                    }
                }
            };
        }
        
        var containerName = args?.TryGetProperty("containerName", out var cn) == true ? cn.GetString() : null;
        var workspacePath = args?.TryGetProperty("workspacePath", out var ws) == true ? ws.GetString() : null;
        
        try
        {
            var references = await _workspaceManager.FindReferencesAsync(symbolName, symbolType, containerName, workspacePath);
            
            if (!references.Any())
            {
                return new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = $"No references found for {symbolType} '{symbolName}'"
                        }
                    }
                };
            }
            
            // Format results
            var report = $"Found {references.Count} references to {symbolType} '{symbolName}':\n\n";
            
            // Group by file
            var byFile = references.GroupBy(r => r.FilePath).OrderBy(g => g.Key);
            
            foreach (var fileGroup in byFile)
            {
                report += $"📄 {fileGroup.Key}\n";
                foreach (var reference in fileGroup.OrderBy(r => r.Line))
                {
                    report += $"  Line {reference.Line}:{reference.Column} - {reference.Context}\n";
                }
                report += "\n";
            }
            
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = report
                    }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find references for {SymbolType} {SymbolName}", symbolType, symbolName);
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = $"Error finding references: {ex.Message}"
                    }
                }
            };
        }
    }
    
    private async Task<object> FindImplementationsAsync(JsonElement? args)
    {
        var interfaceName = args?.GetProperty("interfaceName").GetString();
        
        if (string.IsNullOrEmpty(interfaceName))
        {
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = "Error: Interface name is required"
                    }
                }
            };
        }
        
        var workspacePath = args?.TryGetProperty("workspacePath", out var ws) == true ? ws.GetString() : null;
        
        try
        {
            var implementations = await _workspaceManager.FindImplementationsAsync(interfaceName, workspacePath);
            
            if (!implementations.Any())
            {
                return new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = $"No implementations found for interface '{interfaceName}'"
                        }
                    }
                };
            }
            
            // Format results
            var report = $"Found {implementations.Count} implementations of '{interfaceName}':\n\n";
            
            foreach (var impl in implementations.OrderBy(i => i.ImplementingType))
            {
                report += $"• {impl.ImplementingType}\n";
                report += $"  File: {impl.FilePath}:{impl.Line}:{impl.Column}\n";
                report += $"  Project: {impl.ProjectName}\n";
                if (!string.IsNullOrEmpty(impl.BaseTypes))
                {
                    report += $"  Also implements: {impl.BaseTypes}\n";
                }
                report += "\n";
            }
            
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = report
                    }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find implementations for {InterfaceName}", interfaceName);
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = $"Error finding implementations: {ex.Message}"
                    }
                }
            };
        }
    }
    
    private async Task<object> FindOverridesAsync(JsonElement? args)
    {
        var methodName = args?.GetProperty("methodName").GetString();
        var className = args?.GetProperty("className").GetString();
        
        if (string.IsNullOrEmpty(methodName) || string.IsNullOrEmpty(className))
        {
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = "Error: Method name and class name are required"
                    }
                }
            };
        }
        
        var workspacePath = args?.TryGetProperty("workspacePath", out var ws) == true ? ws.GetString() : null;
        
        try
        {
            var overrides = await _workspaceManager.FindOverridesAsync(methodName, className, workspacePath);
            
            if (!overrides.Any())
            {
                return new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = $"No overrides found for method '{className}.{methodName}'"
                        }
                    }
                };
            }
            
            // Format results
            var report = $"Found {overrides.Count} overrides of '{className}.{methodName}':\n\n";
            
            foreach (var ovr in overrides.OrderBy(o => o.OverridingType))
            {
                report += $"• {ovr.OverridingType}.{ovr.MethodName}{ovr.Parameters}\n";
                report += $"  File: {ovr.FilePath}:{ovr.Line}:{ovr.Column}\n";
                report += $"  Project: {ovr.ProjectName}\n";
                report += $"  Access: {ovr.AccessModifier}";
                if (ovr.IsSealed) report += " sealed";
                report += "\n\n";
            }
            
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = report
                    }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find overrides for {Method}", $"{className}.{methodName}");
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = $"Error finding overrides: {ex.Message}"
                    }
                }
            };
        }
    }
    
    private async Task<object> FindDerivedTypesAsync(JsonElement? args)
    {
        var baseClassName = args?.GetProperty("baseClassName").GetString();
        
        if (string.IsNullOrEmpty(baseClassName))
        {
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = "Error: Base class name is required"
                    }
                }
            };
        }
        
        var workspacePath = args?.TryGetProperty("workspacePath", out var ws) == true ? ws.GetString() : null;
        
        try
        {
            var derivedTypes = await _workspaceManager.FindDerivedTypesAsync(baseClassName, workspacePath);
            
            if (!derivedTypes.Any())
            {
                return new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = $"No derived types found for '{baseClassName}'"
                        }
                    }
                };
            }
            
            // Format results
            var report = $"Found {derivedTypes.Count} types derived from '{baseClassName}':\n\n";
            
            // Build hierarchy  
            var directDerived = derivedTypes.Where(d => 
                d.BaseType == baseClassName || 
                d.BaseType.Contains($".{baseClassName}<") ||
                d.BaseType.EndsWith($".{baseClassName}")).OrderBy(d => d.DerivedType);
            
            // If no direct derived found, show all
            if (!directDerived.Any() && derivedTypes.Any())
            {
                directDerived = derivedTypes.OrderBy(d => d.DerivedType);
            }
            
            foreach (var derived in directDerived)
            {
                report += FormatDerivedTypeHierarchy(derived, derivedTypes, 0);
            }
            
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = report
                    }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find derived types for {BaseClass}", baseClassName);
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = $"Error finding derived types: {ex.Message}"
                    }
                }
            };
        }
    }
    
    private string FormatDerivedTypeHierarchy(DerivedTypeInfo type, List<DerivedTypeInfo> allTypes, int depth)
    {
        var indent = new string(' ', depth * 2);
        var result = $"{indent}• {type.DerivedType}";
        if (type.IsAbstract) result += " (abstract)";
        if (type.IsSealed) result += " (sealed)";
        result += $"\n{indent}  File: {type.FilePath}:{type.Line}:{type.Column}\n";
        result += $"{indent}  Project: {type.ProjectName}\n\n";
        
        // Find children
        var children = allTypes.Where(t => t.BaseType == type.DerivedType).OrderBy(t => t.DerivedType);
        foreach (var child in children)
        {
            result += FormatDerivedTypeHierarchy(child, allTypes, depth + 1);
        }
        
        return result;
    }
    
    private async Task<object> RenameSymbolAsync(JsonElement? args)
    {
        var oldName = args?.GetProperty("oldName").GetString();
        var newName = args?.GetProperty("newName").GetString();
        var symbolType = args?.GetProperty("symbolType").GetString();
        
        if (string.IsNullOrEmpty(oldName) || string.IsNullOrEmpty(newName) || string.IsNullOrEmpty(symbolType))
        {
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = "Error: Old name, new name, and symbol type are required"
                    }
                }
            };
        }
        
        var containerName = args?.TryGetProperty("containerName", out var cn) == true ? cn.GetString() : null;
        var workspacePath = args?.TryGetProperty("workspacePath", out var ws) == true ? ws.GetString() : null;
        var preview = args?.TryGetProperty("preview", out var prev) == true && prev.GetBoolean();
        
        try
        {
            var result = await _workspaceManager.RenameSymbolAsync(oldName, newName, symbolType, containerName, workspacePath, preview);
            
            if (!result.Success)
            {
                return new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = $"Error: {result.Error}"
                        }
                    }
                };
            }
            
            // Format results
            var report = $"Rename '{oldName}' to '{newName}' ({symbolType})\n";
            report += $"Status: {(preview ? "Preview" : "Applied")}\n";
            
            // Show impact summary
            if (!string.IsNullOrEmpty(result.ImpactSummary))
            {
                report += $"\n⚡ Impact: {result.ImpactSummary}\n";
            }
            
            // Show warnings
            if (!string.IsNullOrEmpty(result.Warning))
            {
                report += $"\n⚠️  {result.Warning}\n";
            }
            
            report += "\n";
            
            if (result.Changes.Any())
            {
                report += $"Changes in {result.Changes.Count} files:\n\n";
                
                foreach (var change in result.Changes.OrderBy(c => c.FilePath))
                {
                    report += $"📄 {change.FilePath}\n";
                    foreach (var edit in change.Edits.OrderBy(e => e.Line))
                    {
                        report += $"  Line {edit.Line}: {edit.OldText} → {edit.NewText}\n";
                    }
                    report += "\n";
                }
                
                report += $"Total edits: {result.Changes.Sum(c => c.Edits.Count)}";
            }
            else
            {
                report += "No changes needed.";
            }
            
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = report
                    }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rename symbol {OldName} to {NewName}", oldName, newName);
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = $"Error renaming symbol: {ex.Message}"
                    }
                }
            };
        }
    }
    
    private async Task<object> EditCodeAsync(JsonElement? args)
    {
        var file = args?.GetProperty("file").GetString();
        var operation = args?.GetProperty("operation").GetString();
        var className = args?.GetProperty("className").GetString();
        
        if (string.IsNullOrEmpty(file) || string.IsNullOrEmpty(operation) || string.IsNullOrEmpty(className))
        {
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = "Error: File, operation, and className are required"
                    }
                }
            };
        }
        
        var methodName = args?.TryGetProperty("methodName", out var mn) == true ? mn.GetString() : null;
        var code = args?.TryGetProperty("code", out var c) == true ? c.GetString() : null;
        var preview = args?.TryGetProperty("preview", out var p) == true && p.GetBoolean();
        var parameters = args?.TryGetProperty("parameters", out var prm) == true ? (JsonElement?)prm : null;
        
        try
        {
            var result = await _workspaceManager.EditCodeAsync(file, operation, className, methodName, code, parameters, preview);
            
            if (!result.Success)
            {
                return new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = $"Error: {result.Error}"
                        }
                    }
                };
            }
            
            // Format results
            var report = $"Code edit: {operation} on {className}\n";
            report += $"File: {file}\n";
            report += $"Status: {(preview ? "Preview" : "Applied")}\n\n";
            
            if (!string.IsNullOrEmpty(result.Description))
            {
                report += $"Changes:\n{result.Description}\n\n";
            }
            
            if (preview && !string.IsNullOrEmpty(result.ModifiedCode))
            {
                report += "Modified code:\n";
                report += "```csharp\n";
                report += result.ModifiedCode;
                report += "\n```\n";
            }
            
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = report
                    }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to edit code in {File}", file);
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = $"Error editing code: {ex.Message}"
                    }
                }
            };
        }
    }
    
    private async Task<object> FixPatternAsync(JsonElement? args)
    {
        var findPattern = args?.GetProperty("findPattern").GetString();
        var replacePattern = args?.GetProperty("replacePattern").GetString();
        var patternType = args?.GetProperty("patternType").GetString();
        
        if (string.IsNullOrEmpty(findPattern) || string.IsNullOrEmpty(replacePattern) || string.IsNullOrEmpty(patternType))
        {
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = "Error: Find pattern, replace pattern, and pattern type are required"
                    }
                }
            };
        }
        
        var workspacePath = args?.TryGetProperty("workspacePath", out var ws) == true ? ws.GetString() : null;
        var preview = args?.TryGetProperty("preview", out var prev) == true && prev.GetBoolean();
        
        try
        {
            var result = await _workspaceManager.FixPatternAsync(findPattern, replacePattern, patternType, workspacePath, preview);
            
            if (!result.Success)
            {
                return new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = $"Error: {result.Error}"
                        }
                    }
                };
            }
            
            // Format results
            var report = $"Pattern Fix: {patternType}\n";
            report += $"Find: {findPattern}\n";
            report += $"Replace: {replacePattern}\n";
            report += $"Status: {(preview ? "Preview" : "Applied")}\n\n";
            
            if (result.Fixes.Any())
            {
                report += $"Found {result.Fixes.Count} matches:\n\n";
                
                foreach (var fix in result.Fixes.OrderBy(f => f.FilePath).ThenBy(f => f.Line))
                {
                    report += $"📄 {fix.FilePath}:{fix.Line}\n";
                    report += $"  Before: {fix.OriginalCode}\n";
                    report += $"  After:  {fix.FixedCode}\n";
                    if (!string.IsNullOrEmpty(fix.Description))
                    {
                        report += $"  Note:   {fix.Description}\n";
                    }
                    report += "\n";
                }
            }
            else
            {
                report += "No patterns found to fix.";
            }
            
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = report
                    }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fix pattern {Pattern}", findPattern);
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = $"Error fixing pattern: {ex.Message}"
                    }
                }
            };
        }
    }
    
    private async Task<object> FindStatementsAsync(JsonElement? args)
    {
        var pattern = args?.GetProperty("pattern").GetString();
        
        if (string.IsNullOrEmpty(pattern))
        {
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = "Error: Pattern is required"
                    }
                }
            };
        }
        
        // Parse optional parameters
        Dictionary<string, string>? scope = null;
        if (args?.TryGetProperty("scope", out var scopeElement) == true)
        {
            scope = new Dictionary<string, string>();
            if (scopeElement.TryGetProperty("file", out var file))
                scope["file"] = file.GetString() ?? "";
            if (scopeElement.TryGetProperty("className", out var className))
                scope["className"] = className.GetString() ?? "";
            if (scopeElement.TryGetProperty("methodName", out var methodName))
                scope["methodName"] = methodName.GetString() ?? "";
        }
        
        var patternType = args?.TryGetProperty("patternType", out var pt) == true ? pt.GetString() ?? "text" : "text";
        var includeNestedStatements = args?.TryGetProperty("includeNestedStatements", out var nested) == true && nested.GetBoolean();
        var groupRelated = args?.TryGetProperty("groupRelated", out var group) == true && group.GetBoolean();
        var workspacePath = args?.TryGetProperty("workspacePath", out var ws) == true ? ws.GetString() : null;
        
        try
        {
            var result = await _workspaceManager.FindStatementsAsync(
                pattern, scope, patternType, includeNestedStatements, groupRelated, workspacePath);
            
            if (!result.Success)
            {
                return new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = $"Error: {result.Error}"
                        }
                    }
                };
            }
            
            // Format the response
            var response = new System.Text.StringBuilder();
            response.AppendLine($"Found {result.Statements.Count} statements matching '{pattern}':\n");
            
            foreach (var stmt in result.Statements)
            {
                response.AppendLine($"Statement ID: {stmt.StatementId}");
                response.AppendLine($"Type: {stmt.Type}");
                response.AppendLine($"Location: {stmt.Location.File}:{stmt.Location.Line}:{stmt.Location.Column}");
                response.AppendLine($"Class: {stmt.ContainingClass}, Method: {stmt.ContainingMethod}");
                response.AppendLine($"Code: {stmt.Text}");
                if (stmt.SemanticTags.Any())
                {
                    response.AppendLine($"Semantic tags: {string.Join(", ", stmt.SemanticTags)}");
                }
                response.AppendLine();
            }
            
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = response.ToString()
                    }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find statements matching {Pattern}", pattern);
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = $"Error finding statements: {ex.Message}"
                    }
                }
            };
        }
    }
    
    private async Task<object> ReplaceStatementAsync(JsonElement? args)
    {
        var newStatement = args?.GetProperty("newStatement").GetString();
        
        if (string.IsNullOrEmpty(newStatement))
        {
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = "Error: New statement is required"
                    }
                }
            };
        }
        
        // Get location from either statementId or direct location
        string? filePath = null;
        int line = 0;
        int column = 0;
        
        // Check if statementId is provided (format: stmt-123)
        if (args?.TryGetProperty("statementId", out var stmtId) == true)
        {
            var statementId = stmtId.GetString();
            // For now, we require location since we're not persisting statement info
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = "Error: Statement IDs are ephemeral. Please provide location directly from find-statements output."
                    }
                }
            };
        }
        
        // Get direct location
        if (args?.TryGetProperty("location", out var loc) == true)
        {
            filePath = loc.GetProperty("file").GetString();
            line = loc.GetProperty("line").GetInt32();
            column = loc.GetProperty("column").GetInt32();
        }
        
        if (string.IsNullOrEmpty(filePath) || line == 0 || column == 0)
        {
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = "Error: Location (file, line, column) is required"
                    }
                }
            };
        }
        
        var preserveComments = args?.TryGetProperty("preserveComments", out var pc) == true ? pc.GetBoolean() : true;
        var workspacePath = args?.TryGetProperty("workspacePath", out var ws) == true ? ws.GetString() : null;
        
        try
        {
            var result = await _workspaceManager.ReplaceStatementAsync(
                filePath, line, column, newStatement, preserveComments, workspacePath);
            
            if (!result.Success)
            {
                return new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = $"Error: {result.Error}"
                        }
                    }
                };
            }
            
            // Format the response
            var response = new System.Text.StringBuilder();
            response.AppendLine($"Statement replaced successfully in {result.ModifiedFile}");
            response.AppendLine();
            response.AppendLine($"Original: {result.OriginalStatement}");
            response.AppendLine($"New: {result.NewStatement}");
            response.AppendLine();
            response.AppendLine("Preview:");
            response.AppendLine(result.Preview);
            
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = response.ToString()
                    }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to replace statement");
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = $"Error replacing statement: {ex.Message}"
                    }
                }
            };
        }
    }
    
    private async Task<object> InsertStatementAsync(JsonElement? args)
    {
        var statement = args?.GetProperty("statement").GetString();
        var position = args?.GetProperty("position").GetString();
        
        if (string.IsNullOrEmpty(statement))
        {
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = "Error: Statement is required"
                    }
                }
            };
        }
        
        if (string.IsNullOrEmpty(position) || (position != "before" && position != "after"))
        {
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = "Error: Position must be 'before' or 'after'"
                    }
                }
            };
        }
        
        // Get location
        string? filePath = null;
        int line = 0;
        int column = 0;
        
        if (args?.TryGetProperty("location", out var loc) == true)
        {
            filePath = loc.GetProperty("file").GetString();
            line = loc.GetProperty("line").GetInt32();
            column = loc.GetProperty("column").GetInt32();
        }
        
        if (string.IsNullOrEmpty(filePath) || line == 0 || column == 0)
        {
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = "Error: Location (file, line, column) is required"
                    }
                }
            };
        }
        
        var workspacePath = args?.TryGetProperty("workspacePath", out var ws) == true ? ws.GetString() : null;
        
        try
        {
            var result = await _workspaceManager.InsertStatementAsync(
                position, filePath, line, column, statement, workspacePath);
            
            if (!result.Success)
            {
                return new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = $"Error: {result.Error}"
                        }
                    }
                };
            }
            
            // Format the response
            var response = new System.Text.StringBuilder();
            response.AppendLine($"Statement inserted successfully in {result.ModifiedFile}");
            response.AppendLine();
            response.AppendLine($"Inserted: {result.InsertedStatement}");
            response.AppendLine($"Position: {position} {result.InsertedAt}");
            response.AppendLine();
            response.AppendLine("Preview:");
            response.AppendLine(result.Preview);
            
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = response.ToString()
                    }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to insert statement");
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = $"Error inserting statement: {ex.Message}"
                    }
                }
            };
        }
    }
    
    private async Task<object> RemoveStatementAsync(JsonElement? args)
    {
        // Get location
        string? filePath = null;
        int line = 0;
        int column = 0;
        
        if (args?.TryGetProperty("location", out var loc) == true)
        {
            filePath = loc.GetProperty("file").GetString();
            line = loc.GetProperty("line").GetInt32();
            column = loc.GetProperty("column").GetInt32();
        }
        
        if (string.IsNullOrEmpty(filePath) || line == 0 || column == 0)
        {
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = "Error: Location (file, line, column) is required"
                    }
                }
            };
        }
        
        var preserveComments = args?.TryGetProperty("preserveComments", out var pc) == true ? pc.GetBoolean() : true;
        var workspacePath = args?.TryGetProperty("workspacePath", out var ws) == true ? ws.GetString() : null;
        
        try
        {
            var result = await _workspaceManager.RemoveStatementAsync(
                filePath, line, column, preserveComments, workspacePath);
            
            if (!result.Success)
            {
                return new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = $"Error: {result.Error}"
                        }
                    }
                };
            }
            
            // Format the response
            var response = new System.Text.StringBuilder();
            response.AppendLine($"Statement removed successfully from {result.ModifiedFile}");
            response.AppendLine();
            response.AppendLine($"Removed: {result.RemovedStatement}");
            response.AppendLine($"From: {result.RemovedFrom}");
            if (result.WasOnlyStatementInBlock)
            {
                response.AppendLine("Note: This was the only statement in its block");
            }
            response.AppendLine();
            response.AppendLine("Preview:");
            response.AppendLine(result.Preview);
            
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = response.ToString()
                    }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove statement");
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = $"Error removing statement: {ex.Message}"
                    }
                }
            };
        }
    }
    
    private async Task<object> MarkStatementAsync(JsonElement? args)
    {
        // Get location
        string? filePath = null;
        int line = 0;
        int column = 0;
        
        if (args?.TryGetProperty("location", out var loc) == true)
        {
            filePath = loc.GetProperty("file").GetString();
            line = loc.GetProperty("line").GetInt32();
            column = loc.GetProperty("column").GetInt32();
        }
        
        if (string.IsNullOrEmpty(filePath) || line == 0 || column == 0)
        {
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = "Error: Location (file, line, column) is required"
                    }
                }
            };
        }
        
        var label = args?.TryGetProperty("label", out var lbl) == true ? lbl.GetString() : null;
        var workspacePath = args?.TryGetProperty("workspacePath", out var ws) == true ? ws.GetString() : null;
        
        try
        {
            var result = await _workspaceManager.MarkStatementAsync(
                filePath, line, column, label, workspacePath);
            
            if (!result.Success)
            {
                return new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = $"Error: {result.Error}"
                        }
                    }
                };
            }
            
            // Format the response
            var response = new System.Text.StringBuilder();
            response.AppendLine($"Statement marked successfully");
            response.AppendLine($"Marker ID: {result.MarkerId}");
            if (!string.IsNullOrEmpty(result.Label))
            {
                response.AppendLine($"Label: {result.Label}");
            }
            response.AppendLine($"Statement: {result.MarkedStatement}");
            response.AppendLine($"Location: {result.Location}");
            response.AppendLine($"Context: {result.Context}");
            
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = response.ToString()
                    }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark statement");
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = $"Error marking statement: {ex.Message}"
                    }
                }
            };
        }
    }
    
    private async Task<object> FindMarkedStatementsAsync(JsonElement? args)
    {
        var markerId = args?.TryGetProperty("markerId", out var mid) == true ? mid.GetString() : null;
        var filePath = args?.TryGetProperty("filePath", out var fp) == true ? fp.GetString() : null;
        
        try
        {
            var result = await _workspaceManager.FindMarkedStatementsAsync(markerId, filePath);
            
            if (!result.Success)
            {
                return new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = $"Error: {result.Error}"
                        }
                    }
                };
            }
            
            // Format the response
            var response = new System.Text.StringBuilder();
            response.AppendLine($"Found {result.MarkedStatements.Count} marked statement(s)");
            response.AppendLine($"Total markers in session: {result.TotalMarkers}");
            response.AppendLine();
            
            foreach (var marked in result.MarkedStatements)
            {
                response.AppendLine($"Marker ID: {marked.MarkerId}");
                if (!string.IsNullOrEmpty(marked.Label))
                {
                    response.AppendLine($"Label: {marked.Label}");
                }
                response.AppendLine($"Location: {marked.Location}");
                response.AppendLine($"Context: {marked.Context}");
                response.AppendLine($"Statement: {marked.Statement}");
                response.AppendLine();
            }
            
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = response.ToString()
                    }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find marked statements");
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = $"Error finding marked statements: {ex.Message}"
                    }
                }
            };
        }
    }
    
    private async Task<object> UnmarkStatementAsync(JsonElement? args)
    {
        var markerId = args?.GetProperty("markerId").GetString();
        
        if (string.IsNullOrEmpty(markerId))
        {
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = "Error: Marker ID is required"
                    }
                }
            };
        }
        
        try
        {
            var result = await _workspaceManager.UnmarkStatementAsync(markerId);
            
            if (!result.Success)
            {
                return new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = $"Error: {result.Error}"
                        }
                    }
                };
            }
            
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = $"{result.Message}\nRemaining markers: {result.RemainingMarkers}"
                    }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unmark statement");
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = $"Error unmarking statement: {ex.Message}"
                    }
                }
            };
        }
    }
    
    private async Task<object> ClearMarkersAsync(JsonElement? args)
    {
        try
        {
            var result = await _workspaceManager.ClearAllMarkersAsync();
            
            if (!result.Success)
            {
                return new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = $"Error: {result.Error}"
                        }
                    }
                };
            }
            
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = result.Message
                    }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear markers");
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = $"Error clearing markers: {ex.Message}"
                    }
                }
            };
        }
    }
    
    /// <summary>
    /// Synchronize local workspace tracking with RoslynWorkspaceManager
    /// This ensures consistency when workspaces are automatically cleaned up
    /// </summary>
    private void SynchronizeWorkspaceTracking(dynamic roslynStatus)
    {
        try
        {
            // Get the list of currently loaded workspace IDs from RoslynWorkspaceManager
            var activeWorkspaceIds = new HashSet<string>();
            
            if (roslynStatus.Workspaces != null)
            {
                foreach (var workspace in roslynStatus.Workspaces)
                {
                    if (workspace != null && workspace.id != null)
                    {
                        activeWorkspaceIds.Add((string)workspace.id);
                    }
                }
            }
            
            // Remove any workspaces from local tracking that are no longer active
            var toRemove = _workspaces.Keys.Where(id => !activeWorkspaceIds.Contains(id)).ToList();
            foreach (var id in toRemove)
            {
                _workspaces.Remove(id);
                _logger.LogInformation("Removed workspace {Id} from local tracking (cleaned up by RoslynWorkspaceManager)", id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to synchronize workspace tracking");
        }
    }
}