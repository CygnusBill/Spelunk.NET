using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using McpRoslyn.Server.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using CS = Microsoft.CodeAnalysis.CSharp.Syntax;
using McpRoslyn.Server.FSharp;

namespace McpRoslyn.Server;

public class McpJsonRpcServer
{
    private readonly ILogger<McpJsonRpcServer> _logger;
    private readonly Dictionary<string, WorkspaceInfo> _workspaces = new();
    private readonly JsonSerializerOptions _jsonOptions;
    private List<string> _allowedPaths;
    private readonly RoslynWorkspaceManager _workspaceManager;
    private readonly FSharpWorkspaceManager? _fsharpWorkspaceManager;
    private string? _initialWorkspace;
    private readonly object _configLock = new();
    
    public McpJsonRpcServer(
        ILogger<McpJsonRpcServer> logger,
        IOptions<McpRoslynOptions> options,
        RoslynWorkspaceManager workspaceManager,
        FSharpWorkspaceManager? fsharpWorkspaceManager = null
        )
    {
        _logger = logger;
        _workspaceManager = workspaceManager;
        _fsharpWorkspaceManager = fsharpWorkspaceManager;
        
        var optionsValue = options.Value;
        _allowedPaths = new List<string>(optionsValue.AllowedPaths);
        _initialWorkspace = optionsValue.InitialWorkspace;
        
        _jsonOptions = new JsonSerializerOptions 
        { 
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false 
        };
        
        // Pre-load initial workspace if provided
        if (!string.IsNullOrEmpty(_initialWorkspace))
        {
            Task.Run(async () =>
            {
                try
                {
                    await LoadWorkspaceAsync(_initialWorkspace);
                    _logger.LogInformation("Pre-loaded workspace: {Path}", _initialWorkspace);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to pre-load workspace: {Path}", _initialWorkspace);
                }
            });
        }
    }
    
    public void UpdateConfiguration(McpRoslynOptions options)
    {
        lock (_configLock)
        {
            _allowedPaths = new List<string>(options.AllowedPaths);
            _logger.LogInformation("Configuration updated. New allowed paths: {Paths}", 
                string.Join(", ", _allowedPaths));
        }
    }
    
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MCP Roslyn Server started - listening on stdio");
        
        var reader = Console.In;
        var writer = Console.Out;
        
        // Read JSON-RPC messages from stdin
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var line = await reader.ReadLineAsync();
                if (line == null) break;
                
                cancellationToken.ThrowIfCancellationRequested();
                
                // Skip empty lines
                if (string.IsNullOrWhiteSpace(line)) continue;
                
                _logger.LogDebug("Received: {Message}", line);
                
                // Parse JSON-RPC request
                var request = JsonSerializer.Deserialize<JsonRpcRequest>(line, _jsonOptions);
                if (request == null) continue;
                
                // Process request
                _logger.LogInformation("Processing request: {Method}, ID: {Id}", request.Method, request.Id);
                var response = await ProcessRequestAsync(request);
                _logger.LogInformation("ProcessRequestAsync completed, response type: {Type}, ID: {Id}", response?.GetType().Name ?? "null", response?.Id);
                
                // Send response
                if (response != null)
                {
                    _logger.LogInformation("Calling SendResponseAsync for ID: {Id}", response.Id);
                    await SendResponseAsync(writer, response);
                    _logger.LogInformation("SendResponseAsync completed for ID: {Id}", response.Id);
                }
                else
                {
                    _logger.LogWarning("Null response returned from ProcessRequestAsync");
                }
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
    
    private Task<JsonRpcResponse> HandleInitializeAsync(JsonRpcRequest request)
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
        
        return Task.FromResult(new JsonRpcResponse
        {
            JsonRpc = "2.0",
            Id = request.Id,
            Result = result
        });
    }
    
    private JsonRpcResponse HandleToolsList(JsonRpcRequest request)
    {
        var tools = new object[]
        {
            new
            {
                name = "dotnet-load-workspace",
                description = ToolDescriptions.LoadWorkspace,
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
                description = ToolDescriptions.AnalyzeSyntax,
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
                description = ToolDescriptions.GetSymbols,
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
                description = ToolDescriptions.WorkspaceStatus,
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
                description = ToolDescriptions.FindClass,
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
                description = ToolDescriptions.FindMethod,
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        pattern = new { type = "string", description = "Method name pattern (e.g., 'Get*', '*Async', 'Load?')" },
                        classPattern = new { type = "string", description = "Optional class name pattern to filter by (e.g., '*Controller', 'Base*')" },
                        workspacePath = new { type = "string", description = "Optional workspace path to search in" }
                    },
                    required = new[] { "pattern" }
                }
            },
            new
            {
                name = "dotnet-find-property",
                description = ToolDescriptions.FindProperty,
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        pattern = new { type = "string", description = "Property/field name pattern (e.g., 'Is*', '*Count', '_*')" },
                        classPattern = new { type = "string", description = "Optional class name pattern to filter by" },
                        workspacePath = new { type = "string", description = "Optional workspace path to search in" }
                    },
                    required = new[] { "pattern" }
                }
            },
            new
            {
                name = "dotnet-find-method-calls",
                description = ToolDescriptions.FindMethodCalls,
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
                description = ToolDescriptions.FindMethodCallers,
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
                description = ToolDescriptions.FindReferences,
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
                description = ToolDescriptions.FindImplementations,
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
                description = ToolDescriptions.FindOverrides,
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
                description = ToolDescriptions.FindDerivedTypes,
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
                description = ToolDescriptions.RenameSymbol,
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
                description = ToolDescriptions.EditCode,
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
                description = ToolDescriptions.FixPattern,
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
                description = ToolDescriptions.FindStatements,
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
                description = ToolDescriptions.ReplaceStatement,
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
                description = ToolDescriptions.InsertStatement,
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
                description = ToolDescriptions.RemoveStatement,
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
                description = ToolDescriptions.MarkStatement,
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
                description = ToolDescriptions.FindMarkedStatements,
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
                description = ToolDescriptions.UnmarkStatement,
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
                description = ToolDescriptions.ClearMarkers,
                inputSchema = new
                {
                    type = "object",
                    properties = new { }
                }
            },
            new
            {
                name = "dotnet-get-statement-context",
                description = ToolDescriptions.GetStatementContext,
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
                        workspacePath = new { type = "string", description = "Optional workspace path" }
                    }
                }
            },
            new
            {
                name = "dotnet-get-data-flow",
                description = ToolDescriptions.GetDataFlow,
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        file = new { type = "string", description = "File path to analyze" },
                        startLine = new { type = "number", description = "Start line of region (1-based)" },
                        startColumn = new { type = "number", description = "Start column of region (1-based)" },
                        endLine = new { type = "number", description = "End line of region (1-based)" },
                        endColumn = new { type = "number", description = "End column of region (1-based)" },
                        includeControlFlow = new { type = "boolean", description = "Include control flow analysis (default: true)" },
                        workspacePath = new { type = "string", description = "Optional workspace path" }
                    },
                    required = new[] { "file", "startLine", "startColumn", "endLine", "endColumn" }
                }
            },
            new
            {
                name = "dotnet-fsharp-projects",
                description = "Get information about F# projects in the workspace (detected but not loaded by MSBuild)",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        workspaceId = new { type = "string", description = "Optional workspace ID to filter by" },
                        includeLoaded = new { type = "boolean", description = "Include successfully loaded F# projects (default: false)" }
                    }
                }
            },
            new
            {
                name = "dotnet-fsharp-load-project",
                description = "Load an F# project using FSharp.Compiler.Service (separate from MSBuild workspaces)",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        projectPath = new { type = "string", description = "Path to the .fsproj file to load" }
                    },
                    required = new[] { "projectPath" }
                }
            },
            new
            {
                name = "dotnet-fsharp-find-symbols",
                description = "Find symbols in F# code using FSharpPath queries",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        filePath = new { type = "string", description = "Path to the F# source file" },
                        query = new { type = "string", description = "FSharpPath query (e.g., '//function[startsWith(name, \"process\")]')" }
                    },
                    required = new[] { "filePath", "query" }
                }
            },
            new
            {
                name = "dotnet-fsharp-query",
                description = "Query F# code using FSharpPath syntax",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        fsharpPath = new { type = "string", description = "FSharpPath query (e.g., '//function[@recursive]', '//type[union]')" },
                        file = new { type = "string", description = "Path to the F# source file" },
                        includeContext = new { type = "boolean", description = "Include surrounding context in results" },
                        contextLines = new { type = "integer", description = "Number of context lines to include (default: 2)" }
                    },
                    required = new[] { "fsharpPath", "file" }
                }
            },
            new
            {
                name = "dotnet-fsharp-get-ast",
                description = "Get AST structure for F# code",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        filePath = new { type = "string", description = "Path to the F# source file" },
                        root = new { type = "string", description = "Optional FSharpPath to specify root node" },
                        depth = new { type = "integer", description = "Maximum depth to traverse (default: 3)" },
                        includeRange = new { type = "boolean", description = "Include source location information" }
                    },
                    required = new[] { "filePath" }
                }
            },
            new
            {
                name = "dotnet-query-syntax",
                description = ToolDescriptions.QuerySyntax,
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        roslynPath = new { type = "string", description = "RoslynPath query (e.g., '//binary-expression[@operator=\"==\" and @right-text=\"null\"]')" },
                        file = new { type = "string", description = "Optional: specific file to search in" },
                        workspacePath = new { type = "string", description = "Optional: workspace to search in" },
                        includeContext = new { type = "boolean", description = "Include surrounding context lines" },
                        contextLines = new { type = "integer", description = "Number of context lines (default: 2)" },
                        includeSemanticInfo = new { type = "boolean", description = "Include semantic information (types, symbols, project context)" }
                    },
                    required = new[] { "roslynPath" }
                }
            },
            new
            {
                name = "dotnet-navigate",
                description = ToolDescriptions.Navigate,
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        from = new
                        {
                            type = "object",
                            properties = new
                            {
                                file = new { type = "string", description = "File path" },
                                line = new { type = "integer", description = "Line number (1-based)" },
                                column = new { type = "integer", description = "Column number (1-based)" }
                            },
                            required = new[] { "file", "line", "column" }
                        },
                        path = new { type = "string", description = "Navigation path (e.g., 'ancestor::method[1]/following-sibling::method[1]')" },
                        returnPath = new { type = "boolean", description = "Return the RoslynPath of the target node" },
                        includeSemanticInfo = new { type = "boolean", description = "Include semantic information (types, symbols, project context)" }
                    },
                    required = new[] { "from", "path" }
                }
            },
            new
            {
                name = "dotnet-get-ast",
                description = ToolDescriptions.GetAst,
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        file = new { type = "string", description = "File path to analyze" },
                        root = new { type = "string", description = "Optional: RoslynPath to root node (e.g., '//method[Process]')" },
                        depth = new { type = "integer", description = "Tree depth to include (default: 3)" },
                        includeTokens = new { type = "boolean", description = "Include syntax tokens" },
                        format = new { type = "string", description = "Output format: 'tree' (default)" },
                        includeSemanticInfo = new { type = "boolean", description = "Include semantic information (types, symbols, project context)" }
                    },
                    required = new[] { "file" }
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
                
            case "dotnet-get-statement-context":
                result = await GetStatementContextAsync(toolCallParams.Arguments);
                break;
                
            case "dotnet-get-data-flow":
                result = await GetDataFlowAsync(toolCallParams.Arguments);
                break;
                
            case "dotnet-query-syntax":
                result = await QuerySyntaxAsync(toolCallParams.Arguments);
                break;
                
            case "dotnet-navigate":
                result = await NavigateAsync(toolCallParams.Arguments);
                break;
                
            case "dotnet-get-ast":
                result = await GetAstAsync(toolCallParams.Arguments);
                break;
                
            case "dotnet-fsharp-projects":
                result = await GetFSharpProjectsAsync(toolCallParams.Arguments);
                break;
                
            case "dotnet-fsharp-load-project":
                result = await LoadFSharpProjectAsync(toolCallParams.Arguments);
                break;
                
            case "dotnet-fsharp-find-symbols":
                result = await FindFSharpSymbolsAsync(toolCallParams.Arguments);
                break;
                
            case "dotnet-fsharp-query":
                result = await QueryFSharpAsync(toolCallParams.Arguments);
                break;
                
            case "dotnet-fsharp-get-ast":
                result = await GetFSharpAstAsync(toolCallParams.Arguments);
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
        // Support both "workspacePath" and "path" for consistency
        string? path = null;
        if (args?.TryGetProperty("workspacePath", out var wp) == true)
        {
            path = wp.GetString();
        }
        else if (args?.TryGetProperty("path", out var p) == true)
        {
            path = p.GetString();
        }
        if (string.IsNullOrEmpty(path))
        {
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = "Error: 'workspacePath' or 'path' parameter is required"
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
                        text = JsonSerializer.Serialize(result, _jsonOptions)
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
        lock (_configLock)
        {
            return _allowedPaths.Any(allowed => 
                normalizedPath.StartsWith(Path.GetFullPath(allowed), StringComparison.OrdinalIgnoreCase));
        }
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
        _logger.LogInformation("About to send JSON response, length: {Length}, ID: {Id}", json.Length, response.Id);
        _logger.LogInformation("JSON preview: {Preview}...", json.Length > 200 ? json.Substring(0, 200) : json);
        
        await writer.WriteLineAsync(json);
        await writer.FlushAsync();
        
        _logger.LogInformation("JSON sent and flushed successfully for ID: {Id}", response.Id);
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
        var methodPattern = args?.GetProperty("pattern").GetString();
        if (string.IsNullOrEmpty(methodPattern))
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
        var propertyPattern = args?.GetProperty("pattern").GetString();
        if (string.IsNullOrEmpty(propertyPattern))
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
                    report += $"  After:  {fix.ReplacementCode}\n";
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
        
        // Check for direct file parameter first (convenience)
        if (args?.TryGetProperty("file", out var directFile) == true)
        {
            scope = new Dictionary<string, string>();
            scope["file"] = directFile.GetString() ?? "";
        }
        
        // Then check for scope object (overrides direct file if present)
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
                response.AppendLine($"Path: {stmt.Path}");
                response.AppendLine($"Type: {stmt.Type}");
                response.AppendLine($"Depth: {stmt.Depth}");
                response.AppendLine($"Location: {stmt.Location.File}:{stmt.Location.Line}:{stmt.Location.Column}");
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
            if (string.IsNullOrEmpty(statementId))
            {
                return new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = "Error: Statement ID cannot be empty"
                        }
                    }
                };
            }

            // Look up the statement by ID
            var (node, statementFilePath, workspaceId) = _workspaceManager.GetStatementById(statementId);
            if (node == null || string.IsNullOrEmpty(statementFilePath))
            {
                return new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = $"Error: Statement ID '{statementId}' not found. It may have been cleared or was from a previous session."
                        }
                    }
                };
            }

            // Use the statement's location
            filePath = statementFilePath;
            var sourceText = await node.SyntaxTree.GetTextAsync();
            var lineSpan = sourceText.Lines.GetLinePositionSpan(node.Span);
            line = lineSpan.Start.Line + 1;
            column = lineSpan.Start.Character + 1;
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
    
    private async Task<object> GetStatementContextAsync(JsonElement? args)
    {
        if (args == null)
            return CreateErrorResponse("No arguments provided");
            
        try
        {
            // Get location either from statementId or direct location
            string? filePath = null;
            int line = 0;
            int column = 0;
            string? workspaceId = args.Value.TryGetProperty("workspacePath", out var wsPath) ? wsPath.GetString() : null;
            
            if (args.Value.TryGetProperty("statementId", out var idElement))
            {
                var statementId = idElement.GetString();
                if (string.IsNullOrEmpty(statementId))
                    return CreateErrorResponse("Statement ID is empty");
                    
                // Look up the statement by ID
                var (node, statementFilePath, statementWorkspaceId) = _workspaceManager.GetStatementById(statementId);
                if (node == null || string.IsNullOrEmpty(statementFilePath))
                {
                    return CreateErrorResponse($"Statement with ID '{statementId}' not found");
                }
                
                filePath = statementFilePath;
                workspaceId = statementWorkspaceId;
                
                // Get line and column from the node
                var syntaxTree = node.SyntaxTree;
                var lineSpan = syntaxTree.GetLineSpan(node.Span);
                line = lineSpan.StartLinePosition.Line + 1;
                column = lineSpan.StartLinePosition.Character + 1;
            }
            else if (args.Value.TryGetProperty("location", out var locElement))
            {
                filePath = locElement.GetProperty("file").GetString();
                line = locElement.GetProperty("line").GetInt32();
                column = locElement.GetProperty("column").GetInt32();
            }
            else
            {
                return CreateErrorResponse("Either statementId or location is required");
            }
            
            if (string.IsNullOrEmpty(filePath))
                return CreateErrorResponse("File path is required");
                
            var result = await _workspaceManager.GetStatementContextAsync(filePath, line, column, workspaceId);
            
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
            _logger.LogError(ex, "Failed to get statement context");
            return CreateErrorResponse($"Error getting statement context: {ex.Message}");
        }
    }
    
    private async Task<object> GetDataFlowAsync(JsonElement? args)
    {
        if (args == null)
            return CreateErrorResponse("No arguments provided");
            
        try
        {
            // Extract required parameters
            if (!args.Value.TryGetProperty("file", out var fileElement))
                return CreateErrorResponse("File path is required");
                
            var filePath = fileElement.GetString();
            if (string.IsNullOrEmpty(filePath))
                return CreateErrorResponse("File path cannot be empty");
                
            if (!args.Value.TryGetProperty("startLine", out var startLineElement))
                return CreateErrorResponse("Start line is required");
            var startLine = startLineElement.GetInt32();
            
            if (!args.Value.TryGetProperty("startColumn", out var startColumnElement))
                return CreateErrorResponse("Start column is required");
            var startColumn = startColumnElement.GetInt32();
            
            if (!args.Value.TryGetProperty("endLine", out var endLineElement))
                return CreateErrorResponse("End line is required");
            var endLine = endLineElement.GetInt32();
            
            if (!args.Value.TryGetProperty("endColumn", out var endColumnElement))
                return CreateErrorResponse("End column is required");
            var endColumn = endColumnElement.GetInt32();
            
            // Optional parameters
            var includeControlFlow = true;
            if (args.Value.TryGetProperty("includeControlFlow", out var cfElement))
                includeControlFlow = cfElement.GetBoolean();
                
            string? workspaceId = args.Value.TryGetProperty("workspacePath", out var wsPath) ? wsPath.GetString() : null;
            
            // Call the workspace manager
            var result = await _workspaceManager.GetDataFlowAnalysisAsync(
                filePath, startLine, startColumn, endLine, endColumn, includeControlFlow, workspaceId);
            
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
            _logger.LogError(ex, "Failed to get data flow analysis");
            return CreateErrorResponse($"Error getting data flow analysis: {ex.Message}");
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
    
    private async Task<object> QuerySyntaxAsync(JsonElement? args)
    {
        if (args == null)
            return CreateErrorResponse("No arguments provided");
            
        try
        {
            // Extract required parameters
            if (!args.Value.TryGetProperty("roslynPath", out var pathElement))
                return CreateErrorResponse("RoslynPath query is required");
                
            var roslynPath = pathElement.GetString();
            if (string.IsNullOrEmpty(roslynPath))
                return CreateErrorResponse("RoslynPath cannot be empty");
                
            // No restrictions needed - RoslynPath follows XPath semantics where
            // the descendant axis (//) finds all descendants regardless of nesting
                
            // Extract optional parameters
            string? filePath = null;
            if (args.Value.TryGetProperty("file", out var fileElement))
                filePath = fileElement.GetString();
                
            // Check if this is an F# file
            if (!string.IsNullOrEmpty(filePath) && FSharpFileDetector.IsFSharpFile(filePath))
            {
                _logger.LogInformation("F# file detected in query-syntax: {File}", filePath);
                // For now, return a simple F# not supported message directly
                return new
                {
                    success = false,
                    message = FSharpFileDetector.GetFSharpNotSupportedMessage("query-syntax", filePath),
                    info = new
                    {
                        requestedFile = filePath,
                        requestedQuery = roslynPath,
                        note = "F# will use FSharpPath syntax instead of RoslynPath",
                        documentationLink = "docs/design/FSHARP_IMPLEMENTATION_GUIDE.md#fsharppath-query-language"
                    }
                };
            }
                
            string? workspaceId = null;
            if (args.Value.TryGetProperty("workspacePath", out var workspaceElement))
                workspaceId = workspaceElement.GetString();
                
            bool includeContext = false;
            if (args.Value.TryGetProperty("includeContext", out var contextElement))
                includeContext = contextElement.GetBoolean();
                
            int contextLines = 2;
            if (args.Value.TryGetProperty("contextLines", out var contextLinesElement))
                contextLines = contextLinesElement.GetInt32();
                
            bool includeSemanticInfo = false;
            if (args.Value.TryGetProperty("includeSemanticInfo", out var semanticElement))
                includeSemanticInfo = semanticElement.GetBoolean();
                
            // Get documents to search
            var documentsToSearch = new List<Document>();
            
            if (!string.IsNullOrEmpty(filePath))
            {
                // Search specific file
                if (_workspaces.Count == 0)
                    return CreateErrorResponse("No workspace loaded");
                    
                var workspaceInfo = !string.IsNullOrEmpty(workspaceId) 
                    ? _workspaces.Values.FirstOrDefault(w => w.Id == workspaceId)
                    : _workspaces.Values.First();
                    
                if (workspaceInfo == null)
                    return CreateErrorResponse("Workspace not found");
                    
                var workspace = _workspaceManager.GetWorkspace(workspaceInfo.Id);
                if (workspace == null)
                    return CreateErrorResponse("Workspace not found");
                    
                // Convert to absolute path if relative
                var absolutePath = Path.IsPathRooted(filePath) 
                    ? filePath 
                    : Path.GetFullPath(filePath);
                    
                var document = workspace.CurrentSolution.Projects
                    .SelectMany(p => p.Documents)
                    .FirstOrDefault(d => d.FilePath == absolutePath);
                    
                if (document != null)
                    documentsToSearch.Add(document);
            }
            else
            {
                // Search all documents in workspace
                if (_workspaces.Count == 0)
                    return CreateErrorResponse("No workspace loaded");
                    
                var workspaceInfo = !string.IsNullOrEmpty(workspaceId) 
                    ? _workspaces.Values.FirstOrDefault(w => w.Id == workspaceId)
                    : _workspaces.Values.First();
                    
                if (workspaceInfo == null)
                    return CreateErrorResponse("Workspace not found");
                    
                var workspace = _workspaceManager.GetWorkspace(workspaceInfo.Id);
                if (workspace == null)
                    return CreateErrorResponse("Workspace not found");
                    
                documentsToSearch = workspace.CurrentSolution.Projects
                    .SelectMany(p => p.Documents)
                    .ToList();
            }
            
            // Search documents using RoslynPath
            var matches = new List<object>();
            
            foreach (var document in documentsToSearch)
            {
                var tree = await document.GetSyntaxTreeAsync();
                if (tree == null) continue;
                
                var semanticModel = await document.GetSemanticModelAsync();
                var evaluator = new RoslynPath.RoslynPathEvaluator(tree);
                
                try
                {
                    var results = evaluator.Evaluate(roslynPath);
                    
                    foreach (var node in results)
                    {
                        var lineSpan = node.GetLocation().GetLineSpan();
                        var nodeType = RoslynPath.EnhancedNodeTypes.GetDetailedNodeTypeName(node);
                        
                        object? semanticInfo = null;
                        if (includeSemanticInfo && semanticModel != null)
                        {
                            semanticInfo = GetSemanticInfo(node, semanticModel, document);
                        }
                        
                        var match = new
                        {
                            nodeType = nodeType,
                            path = RoslynPath.RoslynPath.GetNodePath(node),
                            location = new
                            {
                                file = document.FilePath,
                                startLine = lineSpan.StartLinePosition.Line + 1,
                                startColumn = lineSpan.StartLinePosition.Character + 1,
                                endLine = lineSpan.EndLinePosition.Line + 1,
                                endColumn = lineSpan.EndLinePosition.Character + 1
                            },
                            text = node.ToString(),
                            context = includeContext ? await GetContextLines(document, lineSpan.StartLinePosition.Line, contextLines) : null,
                            semanticInfo = semanticInfo
                        };
                        
                        matches.Add(match);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Error evaluating RoslynPath in {document.FilePath}");
                }
            }
            
            return new { nodes = matches };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in QuerySyntaxAsync");
            return CreateErrorResponse($"Error querying syntax: {ex.Message}");
        }
    }
    
    private async Task<object?> GetContextLines(Document document, int centerLine, int contextLines)
    {
        var text = await document.GetTextAsync();
        var lines = text.Lines;
        
        var startLine = Math.Max(0, centerLine - contextLines);
        var endLine = Math.Min(lines.Count - 1, centerLine + contextLines);
        
        var before = new List<string>();
        var after = new List<string>();
        
        for (int i = startLine; i < centerLine; i++)
        {
            before.Add(lines[i].ToString());
        }
        
        for (int i = centerLine + 1; i <= endLine; i++)
        {
            after.Add(lines[i].ToString());
        }
        
        return new { before, after };
    }
    
    private async Task<object> NavigateAsync(JsonElement? args)
    {
        if (args == null)
            return CreateErrorResponse("No arguments provided");
            
        try
        {
            // Extract required from position
            if (!args.Value.TryGetProperty("from", out var fromElement))
                return CreateErrorResponse("From position is required");
                
            if (!fromElement.TryGetProperty("file", out var fileElement))
                return CreateErrorResponse("File path is required in from position");
            var filePath = fileElement.GetString();
            
            if (!fromElement.TryGetProperty("line", out var lineElement))
                return CreateErrorResponse("Line is required in from position");
            var line = lineElement.GetInt32();
            
            if (!fromElement.TryGetProperty("column", out var columnElement))
                return CreateErrorResponse("Column is required in from position");
            var column = columnElement.GetInt32();
            
            // Extract navigation path
            if (!args.Value.TryGetProperty("path", out var pathElement))
                return CreateErrorResponse("Navigation path is required");
            var navPath = pathElement.GetString();
            
            bool returnPath = false;
            if (args.Value.TryGetProperty("returnPath", out var returnPathElement))
                returnPath = returnPathElement.GetBoolean();
                
            bool includeSemanticInfo = false;
            if (args.Value.TryGetProperty("includeSemanticInfo", out var semanticElement))
                includeSemanticInfo = semanticElement.GetBoolean();
                
            // Get the node at the starting position
            if (_workspaces.Count == 0)
                return CreateErrorResponse("No workspace loaded");
                
            var workspaceInfo = _workspaces.Values.First();
            var workspace = _workspaceManager.GetWorkspace(workspaceInfo.Id);
            if (workspace == null)
                return CreateErrorResponse("Workspace not found");
                
            var solution = workspace.CurrentSolution;
            var document = solution.Projects
                .SelectMany(p => p.Documents)
                .FirstOrDefault(d => d.FilePath == filePath);
                
            if (document == null)
                return CreateErrorResponse($"Document not found: {filePath}");
                
            var root = await document.GetSyntaxRootAsync();
            if (root == null)
                return CreateErrorResponse("Failed to get syntax root");
                
            var sourceText = await document.GetTextAsync();
            var position = sourceText.Lines.GetPosition(new LinePosition(line - 1, column - 1));
            var startNode = root.FindNode(new TextSpan(position, 0));
            
            // Navigate from the starting node using the specified path
            var targetNode = NavigateFromNode(startNode, navPath);
            
            if (targetNode == null)
                return new { navigatedTo = (object?)null };
                
            var lineSpan = targetNode.GetLocation().GetLineSpan();
            
            // Get semantic info if requested
            object? semanticInfo = null;
            if (includeSemanticInfo)
            {
                var semanticModel = await document.GetSemanticModelAsync();
                if (semanticModel != null)
                {
                    semanticInfo = GetSemanticInfo(targetNode, semanticModel, document);
                }
            }
            
            var target = new
            {
                nodeType = RoslynPath.EnhancedNodeTypes.GetDetailedNodeTypeName(targetNode),
                name = GetNodeName(targetNode),
                location = new
                {
                    file = filePath,
                    startLine = lineSpan.StartLinePosition.Line + 1,
                    startColumn = lineSpan.StartLinePosition.Character + 1,
                    endLine = lineSpan.EndLinePosition.Line + 1,
                    endColumn = lineSpan.EndLinePosition.Character + 1
                },
                path = returnPath ? BuildNodePath(targetNode) : null,
                text = targetNode.ToString(),
                semanticInfo = semanticInfo
            };
            
            return new { target };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in NavigateAsync");
            return CreateErrorResponse($"Error navigating: {ex.Message}");
        }
    }
    
    private SyntaxNode? NavigateFromNode(SyntaxNode startNode, string path)
    {
        var current = startNode;
        
        // Parse the navigation path (simple implementation)
        var parts = path.Split(new[] { "::" }, StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var part in parts)
        {
            if (current == null) return null;
            
            // Parse axis and node test
            var match = System.Text.RegularExpressions.Regex.Match(part, @"^(\w+)(?:\[(\d+)\])?$");
            if (!match.Success) continue;
            
            var axis = match.Groups[1].Value;
            var index = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 1;
            
            current = axis switch
            {
                "ancestor" => GetAncestor(current, index),
                "parent" => current.Parent,
                "child" => GetChild(current, index),
                "descendant" => GetDescendant(current, index),
                "following-sibling" => GetFollowingSibling(current, index),
                "preceding-sibling" => GetPrecedingSibling(current, index),
                "self" => current,
                _ => null
            };
        }
        
        return current;
    }
    
    private SyntaxNode? GetAncestor(SyntaxNode node, int index)
    {
        var current = node.Parent;
        for (int i = 1; i < index && current != null; i++)
        {
            current = current.Parent;
        }
        return current;
    }
    
    private SyntaxNode? GetChild(SyntaxNode node, int index)
    {
        var children = node.ChildNodes().ToList();
        return index > 0 && index <= children.Count ? children[index - 1] : null;
    }
    
    private SyntaxNode? GetDescendant(SyntaxNode node, int index)
    {
        var descendants = node.DescendantNodes().ToList();
        return index > 0 && index <= descendants.Count ? descendants[index - 1] : null;
    }
    
    private SyntaxNode? GetFollowingSibling(SyntaxNode node, int index)
    {
        if (node.Parent == null) return null;
        
        var siblings = node.Parent.ChildNodes().ToList();
        var currentIndex = siblings.IndexOf(node);
        var targetIndex = currentIndex + index;
        
        return targetIndex < siblings.Count ? siblings[targetIndex] : null;
    }
    
    private SyntaxNode? GetPrecedingSibling(SyntaxNode node, int index)
    {
        if (node.Parent == null) return null;
        
        var siblings = node.Parent.ChildNodes().ToList();
        var currentIndex = siblings.IndexOf(node);
        var targetIndex = currentIndex - index;
        
        return targetIndex >= 0 ? siblings[targetIndex] : null;
    }
    
    private string BuildNodePath(SyntaxNode node)
    {
        var pathParts = new List<string>();
        var current = node;
        
        while (current != null)
        {
            var nodeType = RoslynPath.EnhancedNodeTypes.GetDetailedNodeTypeName(current);
            var name = GetNodeName(current);
            
            if (!string.IsNullOrEmpty(name))
            {
                pathParts.Insert(0, $"{nodeType}[@name='{name}']");
            }
            else
            {
                pathParts.Insert(0, nodeType);
            }
            
            current = current.Parent;
        }
        
        return "/" + string.Join("/", pathParts);
    }
    
    private async Task<object> GetAstAsync(JsonElement? args)
    {
        if (args == null)
            return CreateErrorResponse("No arguments provided");
            
        try
        {
            // Extract required parameters
            if (!args.Value.TryGetProperty("file", out var fileElement))
                return CreateErrorResponse("File path is required");
            var filePath = fileElement.GetString();
            
            // Extract optional root path
            string? rootPath = null;
            if (args.Value.TryGetProperty("root", out var rootElement))
                rootPath = rootElement.GetString();
                
            int depth = 3;
            if (args.Value.TryGetProperty("depth", out var depthElement))
                depth = depthElement.GetInt32();
                
            bool includeTokens = false;
            if (args.Value.TryGetProperty("includeTokens", out var tokensElement))
                includeTokens = tokensElement.GetBoolean();
                
            string format = "tree";
            if (args.Value.TryGetProperty("format", out var formatElement))
                format = formatElement.GetString() ?? "tree";
                
            bool includeSemanticInfo = false;
            if (args.Value.TryGetProperty("includeSemanticInfo", out var semanticElement))
                includeSemanticInfo = semanticElement.GetBoolean();
                
            // Get the document
            if (_workspaces.Count == 0)
                return CreateErrorResponse("No workspace loaded");
                
            var workspaceInfo = _workspaces.Values.First();
            var workspace = _workspaceManager.GetWorkspace(workspaceInfo.Id);
            if (workspace == null)
                return CreateErrorResponse("Workspace not found");
                
            var solution = workspace.CurrentSolution;
            var document = solution.Projects
                .SelectMany(p => p.Documents)
                .FirstOrDefault(d => d.FilePath == filePath);
                
            if (document == null)
                return CreateErrorResponse($"Document not found: {filePath}");
                
            var root = await document.GetSyntaxRootAsync();
            if (root == null)
                return CreateErrorResponse("Failed to get syntax root");
                
            SyntaxNode targetNode = root;
            
            // If a root path is specified, find that node first
            if (!string.IsNullOrEmpty(rootPath))
            {
                var tree = await document.GetSyntaxTreeAsync();
                if (tree == null)
                    return CreateErrorResponse("Failed to get syntax tree");
                    
                var evaluator = new RoslynPath.RoslynPathEvaluator(tree);
                var results = evaluator.Evaluate(rootPath).ToList();
                
                if (results.Count == 0)
                    return CreateErrorResponse($"No nodes found matching path: {rootPath}");
                    
                targetNode = results.First();
            }
            
            // Get semantic model if needed
            SemanticModel? semanticModel = null;
            if (includeSemanticInfo)
            {
                semanticModel = await document.GetSemanticModelAsync();
            }
            
            // Build the AST representation
            var ast = includeSemanticInfo && semanticModel != null
                ? await BuildAstNodeAsync(targetNode, depth, includeTokens, semanticModel, document)
                : BuildAstNode(targetNode, depth, includeTokens, null, null);
            
            return new { ast };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetAstAsync");
            return CreateErrorResponse($"Error getting AST: {ex.Message}");
        }
    }
    
    private async Task<object> BuildAstNodeAsync(SyntaxNode node, int remainingDepth, bool includeTokens, SemanticModel semanticModel, Document document)
    {
        var nodeType = RoslynPath.EnhancedNodeTypes.GetDetailedNodeTypeName(node);
        var result = new Dictionary<string, object>
        {
            ["nodeType"] = nodeType,
            ["kind"] = node.Kind().ToString()
        };
        
        // Add name if available
        var name = GetNodeName(node);
        if (!string.IsNullOrEmpty(name))
            result["name"] = name;
            
        // Add semantic info
        var semanticInfo = GetSemanticInfo(node, semanticModel, document);
        if (semanticInfo != null)
            result["semanticInfo"] = semanticInfo;
            
        // Add specific properties based on node type
        if (node is CS.BinaryExpressionSyntax binary)
        {
            result["operator"] = RoslynPath.EnhancedNodeTypes.GetBinaryOperator(node) ?? "";
            if (remainingDepth > 0)
            {
                result["left"] = await BuildAstNodeAsync(binary.Left, remainingDepth - 1, includeTokens, semanticModel, document);
                result["right"] = await BuildAstNodeAsync(binary.Right, remainingDepth - 1, includeTokens, semanticModel, document);
            }
        }
        else if (node is CS.LiteralExpressionSyntax literal)
        {
            result["value"] = RoslynPath.EnhancedNodeTypes.GetLiteralValue(node) ?? "";
        }
        else if (remainingDepth > 0)
        {
            // Add children for other node types
            var children = new List<object>();
            foreach (var child in node.ChildNodes())
            {
                children.Add(await BuildAstNodeAsync(child, remainingDepth - 1, includeTokens, semanticModel, document));
            }
            
            if (children.Count > 0)
                result["children"] = children;
        }
        
        if (includeTokens && remainingDepth > 0)
        {
            var tokens = node.ChildTokens().Select(t => new
            {
                kind = t.Kind().ToString(),
                text = t.Text,
                value = t.Value?.ToString()
            }).ToList();
            
            if (tokens.Count > 0)
                result["tokens"] = tokens;
        }
        
        return result;
    }
    
    private object BuildAstNode(SyntaxNode node, int remainingDepth, bool includeTokens, SemanticModel? semanticModel = null, string? projectName = null)
    {
        var nodeType = RoslynPath.EnhancedNodeTypes.GetDetailedNodeTypeName(node);
        var result = new Dictionary<string, object>
        {
            ["type"] = nodeType,
            ["kind"] = node.Kind().ToString()
        };
        
        // Add name if available
        var name = GetNodeName(node);
        if (!string.IsNullOrEmpty(name))
            result["name"] = name;
            
        // Semantic info is only available in the async version (BuildAstNodeAsync)
        // which properly passes the Document parameter needed for GetSemanticInfo.
        // This synchronous version is kept for backward compatibility when semantic info is not needed.
            
        // Add specific properties based on node type
        if (node is CS.BinaryExpressionSyntax binary)
        {
            result["operator"] = RoslynPath.EnhancedNodeTypes.GetBinaryOperator(node) ?? "";
            if (remainingDepth > 0)
            {
                result["left"] = BuildAstNode(binary.Left, remainingDepth - 1, includeTokens, semanticModel, projectName);
                result["right"] = BuildAstNode(binary.Right, remainingDepth - 1, includeTokens, semanticModel, projectName);
            }
        }
        else if (node is CS.LiteralExpressionSyntax literal)
        {
            result["value"] = RoslynPath.EnhancedNodeTypes.GetLiteralValue(node) ?? "";
        }
        else if (remainingDepth > 0)
        {
            // Add children for other node types
            var children = new List<object>();
            foreach (var child in node.ChildNodes())
            {
                children.Add(BuildAstNode(child, remainingDepth - 1, includeTokens, semanticModel, projectName));
            }
            
            if (children.Count > 0)
                result["children"] = children;
        }
        
        if (includeTokens && remainingDepth > 0)
        {
            var tokens = node.ChildTokens().Select(t => new
            {
                kind = t.Kind().ToString(),
                text = t.Text,
                value = t.Value?.ToString()
            }).ToList();
            
            if (tokens.Count > 0)
                result["tokens"] = tokens;
        }
        
        return result;
    }
    
    private string? GetNodeName(SyntaxNode node)
    {
        return node switch
        {
            CS.ClassDeclarationSyntax classDecl => classDecl.Identifier.Text,
            CS.MethodDeclarationSyntax methodDecl => methodDecl.Identifier.Text,
            CS.PropertyDeclarationSyntax propDecl => propDecl.Identifier.Text,
            CS.FieldDeclarationSyntax fieldDecl => fieldDecl.Declaration.Variables.FirstOrDefault()?.Identifier.Text,
            CS.ParameterSyntax param => param.Identifier.Text,
            CS.VariableDeclaratorSyntax variable => variable.Identifier.Text,
            CS.IdentifierNameSyntax identifier => identifier.Identifier.Text,
            _ => null
        };
    }
    
    private async Task<object> GetFSharpProjectsAsync(JsonElement? args)
    {
        try
        {
            var workspaceId = args?.TryGetProperty("workspaceId", out var wsId) == true ? wsId.GetString() : null;
            var includeLoaded = args?.TryGetProperty("includeLoaded", out var incLoaded) == true ? incLoaded.GetBoolean() : false;
            
            var projects = includeLoaded 
                ? _workspaceManager.GetFSharpProjects(workspaceId)
                : _workspaceManager.GetSkippedFSharpProjects(workspaceId);
            
            var response = new System.Text.StringBuilder();
            response.AppendLine($"F# Projects{(includeLoaded ? "" : " (Skipped)")}: {projects.Count}");
            response.AppendLine();
            
            // F# project enumeration disabled for PoC
            if (projects.Count == 0)
            {
                response.AppendLine("F# project listing is disabled for diagnostic PoC");
            }
            
            if (projects.Count == 0)
            {
                response.AppendLine("No F# projects found.");
                response.AppendLine();
            }
            
            response.AppendLine("Note: F# projects are not fully supported by MSBuildWorkspace.");
            response.AppendLine("To work with F# projects, use FSharp.Compiler.Service directly.");
            
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
            _logger.LogError(ex, "Failed to get F# project information");
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = $"Error getting F# project information: {ex.Message}"
                    }
                }
            };
        }
    }
    
    private async Task<object> LoadFSharpProjectAsync(JsonElement? args)
    {
        try
        {
            if (_fsharpWorkspaceManager == null)
            {
                return new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = "F# support is not configured. FSharpWorkspaceManager is not available."
                        }
                    }
                };
            }
            
            var projectPath = args?.GetProperty("projectPath").GetString();
            if (string.IsNullOrEmpty(projectPath))
            {
                throw new ArgumentException("projectPath is required");
            }
            
            // Validate path
            if (!IsPathAllowed(projectPath))
            {
                throw new UnauthorizedAccessException($"Path '{projectPath}' is not in allowed paths");
            }
            
            // Create and use F# tool
            var loggerFactory = _logger.BeginScope("FSharpLoadProject") as ILoggerFactory ?? LoggerFactory.Create(builder => {});
            var toolLogger = _logger as ILogger<FSharp.Tools.FSharpLoadProjectTool> ?? loggerFactory.CreateLogger<FSharp.Tools.FSharpLoadProjectTool>();
            var fsharpLoadTool = new FSharp.Tools.FSharpLoadProjectTool(_fsharpWorkspaceManager, toolLogger);
            return await fsharpLoadTool.ExecuteAsync(projectPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading F# project");
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = $"Error loading F# project: {ex.Message}"
                    }
                }
            };
        }
    }
    
    private async Task<object> FindFSharpSymbolsAsync(JsonElement? args)
    {
        try
        {
            if (_fsharpWorkspaceManager == null)
            {
                return new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = "F# support is not configured. FSharpWorkspaceManager is not available."
                        }
                    }
                };
            }
            
            var filePath = args?.GetProperty("filePath").GetString();
            var query = args?.GetProperty("query").GetString();
            
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException("filePath is required");
            }
            if (string.IsNullOrEmpty(query))
            {
                throw new ArgumentException("query is required");
            }
            
            // Validate path
            if (!IsPathAllowed(filePath))
            {
                throw new UnauthorizedAccessException($"Path '{filePath}' is not in allowed paths");
            }
            
            // For now, use pattern-based search (FSharpPath will be implemented later)
            // The 'query' parameter in the tool definition suggests FSharpPath, but we'll use pattern for now
            var pattern = query; // Extract pattern from FSharpPath query
            if (query.Contains("//"))
            {
                // Simple extraction - in full implementation, parse FSharpPath properly
                pattern = "*"; // Default to all symbols for now
            }
            
            // Create and use F# tool
            var loggerFactory = LoggerFactory.Create(builder => {});
            var toolLogger = loggerFactory.CreateLogger<FSharp.Tools.FSharpFindSymbolsTool>();
            var fsharpFindTool = new FSharp.Tools.FSharpFindSymbolsTool(_fsharpWorkspaceManager, toolLogger);
            return await fsharpFindTool.ExecuteAsync(pattern, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding F# symbols");
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = $"Error finding F# symbols: {ex.Message}"
                    }
                }
            };
        }
    }
    
    private async Task<object> QueryFSharpAsync(JsonElement? args)
    {
        try
        {
            if (_fsharpWorkspaceManager == null)
            {
                return new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = "F# support is not configured. FSharpWorkspaceManager is not available."
                        }
                    }
                };
            }

            if (!args.HasValue)
            {
                throw new ArgumentException("Missing required arguments");
            }

            var fsharpPath = args.Value.GetProperty("fsharpPath").GetString() 
                ?? throw new ArgumentException("fsharpPath is required");
            var file = args.Value.GetProperty("file").GetString() 
                ?? throw new ArgumentException("file is required");
            var includeContext = args.Value.TryGetProperty("includeContext", out var ctxProp) && ctxProp.GetBoolean();
            var contextLines = args.Value.TryGetProperty("contextLines", out var linesProp) ? linesProp.GetInt32() : 2;

            // Create and use F# query tool
            var loggerFactory = LoggerFactory.Create(builder => {});
            var toolLogger = loggerFactory.CreateLogger<FSharp.Tools.FSharpQueryTool>();
            var fsharpQueryTool = new FSharp.Tools.FSharpQueryTool(_fsharpWorkspaceManager, toolLogger);
            return await fsharpQueryTool.ExecuteAsync(fsharpPath, file, includeContext, contextLines);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying F# code");
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = $"Error querying F# code: {ex.Message}"
                    }
                }
            };
        }
    }
    
    private async Task<object> GetFSharpAstAsync(JsonElement? args)
    {
        try
        {
            if (_fsharpWorkspaceManager == null)
            {
                return new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = "F# support is not configured. FSharpWorkspaceManager is not available."
                        }
                    }
                };
            }

            if (!args.HasValue)
            {
                throw new ArgumentException("Missing required arguments");
            }

            var filePath = args.Value.GetProperty("filePath").GetString() 
                ?? throw new ArgumentException("filePath is required");
            var root = args.Value.TryGetProperty("root", out var rootProp) ? rootProp.GetString() : null;
            var depth = args.Value.TryGetProperty("depth", out var depthProp) ? depthProp.GetInt32() : 3;
            var includeRange = args.Value.TryGetProperty("includeRange", out var rangeProp) && rangeProp.GetBoolean();

            // Create and use F# AST tool
            var loggerFactory = LoggerFactory.Create(builder => {});
            var toolLogger = loggerFactory.CreateLogger<FSharp.Tools.FSharpGetAstTool>();
            var fsharpGetAstTool = new FSharp.Tools.FSharpGetAstTool(_fsharpWorkspaceManager, toolLogger);
            return await fsharpGetAstTool.ExecuteAsync(filePath, root, depth, includeRange);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting F# AST");
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = $"Error getting F# AST: {ex.Message}"
                    }
                }
            };
        }
    }
    
    private static object? GetSemanticInfo(SyntaxNode node, SemanticModel semanticModel, Document document)
    {
        try
        {
            var extractor = new SemanticInfoExtractor(semanticModel, document);
            var info = extractor.ExtractSemanticInfo(node);
            
            return info.Count > 0 ? info : null;
        }
        catch (Exception ex)
        {
            // Return error info if semantic analysis fails
            return new
            {
                error = "Semantic analysis failed",
                message = ex.Message
            };
        }
    }}
