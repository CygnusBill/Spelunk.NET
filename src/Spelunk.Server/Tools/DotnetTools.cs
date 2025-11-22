using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;
using Spelunk.Server;
using Spelunk.Server.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LanguageExt;

namespace Spelunk.Server.Tools;

// Error response helper for MCP tools
file static class ToolError
{
    public static string Create(string code, string message) =>
        JsonSerializer.Serialize(new { error = new { code, message } });
}

[McpServerToolType]
public static class DotnetTools
{
    private static DotnetWorkspaceManager? _workspaceManager;
    private static IOptionsMonitor<SpelunkOptions>? _optionsMonitor;
    private static ILogger? _logger;
    private static IDisposable? _optionsChangeToken;

    public static void Initialize(IOptionsMonitor<SpelunkOptions> optionsMonitor, ILogger logger)
    {
        _logger = logger;
        _optionsMonitor = optionsMonitor;

        // Create a typed logger for DotnetWorkspaceManager
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var workspaceLogger = loggerFactory.CreateLogger<DotnetWorkspaceManager>();

        // Create IOptions<SpelunkOptions> wrapper from current value
        var optionsWrapper = Options.Create(optionsMonitor.CurrentValue);
        _workspaceManager = new DotnetWorkspaceManager(workspaceLogger, optionsWrapper);

        // Subscribe to configuration changes using IOptionsMonitor
        _optionsChangeToken = optionsMonitor.OnChange(options =>
        {
            _logger?.LogInformation("Configuration changed. Allowed paths: {Paths}",
                string.Join(", ", options.AllowedPaths));
        });
    }

    [McpServerTool(Name = "spelunk-load-workspace"), Description(ToolDescriptions.LoadWorkspace)]
    public static async Task<string> DotnetLoadWorkspace(
        [Description("Full path to the solution (.sln) or project file (relative paths are not supported)")] string path,
        [Description("Optional workspace ID")] string? workspaceId = null)
    {
        if (_workspaceManager == null)
        {
            throw new InvalidOperationException("DotnetTools not initialized");
        }

        try
        {
            // Validate path is allowed
            var fullPath = Path.GetFullPath(path);
            if (!IsPathAllowed(fullPath))
            {
                throw new UnauthorizedAccessException($"Path '{fullPath}' is not in allowed paths");
            }
            
            var (success, message, workspace, actualWorkspaceId) = await _workspaceManager.LoadWorkspaceAsync(path, workspaceId);

            if (!success || workspace == null)
            {
                return ToolError.Create("WORKSPACE_NOT_FOUND", $"Failed to load workspace: {message}");
            }

            // Count projects in the workspace
            var projectCount = workspace.CurrentSolution.Projects.Count();
            var fileType = path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ? "Solution" : "Project";

            return $"{fileType} loaded successfully\nWorkspace ID: {actualWorkspaceId}\nProjects: {projectCount}\nPath: {path}";
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load workspace from path: {Path}", path);
            return ToolError.Create("WORKSPACE_NOT_FOUND", $"Failed to load workspace: {ex.Message}");
        }
    }

    [McpServerTool(Name = "spelunk-workspace-status"), Description(ToolDescriptions.WorkspaceStatus)]
    public static string DotnetWorkspaceStatus(
        [Description("Optional workspace ID to get specific status")] string? workspaceId = null)
    {
        if (_workspaceManager == null)
        {
            throw new InvalidOperationException("DotnetTools not initialized");
        }

        var status = _workspaceManager.GetStatus();
        return JsonSerializer.Serialize(status, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool(Name = "spelunk-analyze-syntax"), Description(ToolDescriptions.AnalyzeSyntax)]
    public static async Task<string> DotnetAnalyzeSyntax(
        [Description("Path to the source file")] string filePath,
        [Description("Workspace ID for context")] string? workspaceId = null)
    {
        if (_workspaceManager == null)
        {
            throw new InvalidOperationException("DotnetTools not initialized");
        }

        try
        {
            var result = await _workspaceManager.AnalyzeSyntaxTreeAsync(filePath, includeTrivia: false, workspaceId);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to analyze syntax for file: {FilePath}", filePath);
            return ToolError.Create("CODE_EDIT_FAILED", $"Failed to analyze syntax: {ex.Message}");
        }
    }

    [McpServerTool(Name = "spelunk-get-symbols"), Description(ToolDescriptions.GetSymbols)]
    public static async Task<string> DotnetGetSymbols(
        [Description("Path to the source file")] string filePath,
        [Description("Workspace ID for context")] string? workspaceId = null,
        [Description("Optional symbol name to search for")] string? symbolName = null)
    {
        if (_workspaceManager == null)
        {
            throw new InvalidOperationException("DotnetTools not initialized");
        }

        try
        {
            object result;
            if (!string.IsNullOrEmpty(symbolName))
            {
                result = await _workspaceManager.GetSymbolsByNameAsync(filePath, symbolName, workspaceId);
            }
            else
            {
                result = await _workspaceManager.GetAllSymbolsAsync(filePath, workspaceId);
            }
            
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get symbols from file: {FilePath}", filePath);
            return ToolError.Create("CODE_EDIT_FAILED", $"Failed to get symbols: {ex.Message}");
        }
    }

    [McpServerTool(Name = "spelunk-find-class"), Description(ToolDescriptions.FindClass)]
    public static async Task<string> DotnetFindClass(
        [Description("Pattern to match class names (supports wildcards like User*)")] string pattern,
        [Description("Workspace ID for context")] string? workspaceId = null)
    {
        if (_workspaceManager == null)
        {
            throw new InvalidOperationException("DotnetTools not initialized");
        }

        try
        {
            var result = await _workspaceManager.FindClassesAsync(pattern, workspaceId);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to find classes with pattern: {Pattern}", pattern);
            return ToolError.Create("INVALID_PATTERN", $"Failed to find classes: {ex.Message}");
        }
    }

    [McpServerTool(Name = "spelunk-find-method"), Description(ToolDescriptions.FindMethod)]
    public static async Task<string> DotnetFindMethod(
        [Description("Pattern to match method names (supports wildcards like Get*)")] string methodPattern,
        [Description("Optional pattern to match class names")] string? classPattern = null,
        [Description("Workspace ID for context")] string? workspaceId = null)
    {
        if (_workspaceManager == null)
        {
            throw new InvalidOperationException("DotnetTools not initialized");
        }

        try
        {
            var result = await _workspaceManager.FindMethodsAsync(methodPattern, classPattern, workspaceId);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to find methods with pattern: {MethodPattern}, class: {ClassPattern}", methodPattern, classPattern);
            return ToolError.Create("INVALID_PATTERN", $"Failed to find methods: {ex.Message}");
        }
    }

    [McpServerTool(Name = "spelunk-find-property"), Description(ToolDescriptions.FindProperty)]
    public static async Task<string> DotnetFindProperty(
        [Description("Pattern to match property names (supports wildcards like Name*)")] string propertyPattern,
        [Description("Optional pattern to match class names")] string? classPattern = null,
        [Description("Workspace ID for context")] string? workspaceId = null)
    {
        if (_workspaceManager == null)
        {
            throw new InvalidOperationException("DotnetTools not initialized");
        }

        try
        {
            var result = await _workspaceManager.FindPropertiesAsync(propertyPattern, classPattern, workspaceId);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to find properties with pattern: {PropertyPattern}, class: {ClassPattern}", propertyPattern, classPattern);
            return ToolError.Create("INVALID_PATTERN", $"Failed to find properties: {ex.Message}");
        }
    }

    [McpServerTool(Name = "spelunk-find-method-calls"), Description(ToolDescriptions.FindMethodCalls)]
    public static async Task<string> DotnetFindMethodCalls(
        [Description("Method name to find calls to")] string methodName,
        [Description("Optional class name that contains the method")] string? className = null,
        [Description("Workspace ID for context")] string? workspaceId = null)
    {
        if (_workspaceManager == null)
        {
            throw new InvalidOperationException("DotnetTools not initialized");
        }

        try
        {
            var result = await _workspaceManager.FindMethodCallsAsync(methodName, className, workspaceId);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to find calls for method: {MethodName} in class: {ClassName}", methodName, className);
            return ToolError.Create("SYMBOL_NOT_FOUND", $"Failed to find method calls: {ex.Message}");
        }
    }

    [McpServerTool(Name = "spelunk-find-method-callers"), Description(ToolDescriptions.FindMethodCallers)]
    public static async Task<string> DotnetFindMethodCallers(
        [Description("Method name to find callers of")] string methodName,
        [Description("Optional class name that contains the method")] string? className = null,
        [Description("Workspace ID for context")] string? workspaceId = null)
    {
        if (_workspaceManager == null)
        {
            throw new InvalidOperationException("DotnetTools not initialized");
        }

        try
        {
            var result = await _workspaceManager.FindMethodCallersAsync(methodName, className, workspaceId);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to find callers for method: {MethodName} in class: {ClassName}", methodName, className);
            return ToolError.Create("SYMBOL_NOT_FOUND", $"Failed to find method callers: {ex.Message}");
        }
    }

    [McpServerTool(Name = "spelunk-find-references"), Description(ToolDescriptions.FindReferences)]
    public static async Task<string> DotnetFindReferences(
        [Description("Symbol name to find references to")] string symbolName,
        [Description("Type of symbol (class, method, property, field, etc.)")] string? symbolType = null,
        [Description("Optional container name (class/namespace)")] string? containerName = null,
        [Description("Workspace ID for context")] string? workspaceId = null)
    {
        if (_workspaceManager == null)
        {
            throw new InvalidOperationException("DotnetTools not initialized");
        }

        try
        {
            var result = await _workspaceManager.FindReferencesAsync(symbolName, symbolType, containerName, workspaceId);
            return result.Match(
                Right: optRefs => optRefs.Match(
                    Some: refs => JsonSerializer.Serialize(refs, new JsonSerializerOptions { WriteIndented = true }),
                    None: () => JsonSerializer.Serialize(new { message = $"Symbol '{symbolName}' not found", references = Array.Empty<object>() })
                ),
                Left: error => ToolError.Create(error.Code, error.Message)
            );
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to find references for symbol: {SymbolName}, type: {SymbolType}, container: {ContainerName}",
                symbolName, symbolType, containerName);
            return ToolError.Create("UNEXPECTED_ERROR", $"Failed to find references: {ex.Message}");
        }
    }

    [McpServerTool(Name = "spelunk-find-implementations"), Description(ToolDescriptions.FindImplementations)]
    public static async Task<string> DotnetFindImplementations(
        [Description("Interface name to find implementations of")] string interfaceName,
        [Description("Workspace ID for context")] string? workspaceId = null)
    {
        if (_workspaceManager == null)
        {
            throw new InvalidOperationException("DotnetTools not initialized");
        }

        try
        {
            var result = await _workspaceManager.FindImplementationsAsync(interfaceName, workspaceId);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to find implementations");
            throw new InvalidOperationException($"Failed to find implementations: {ex.Message}", ex);
        }
    }

    [McpServerTool(Name = "spelunk-find-overrides"), Description(ToolDescriptions.FindOverrides)]
    public static async Task<string> DotnetFindOverrides(
        [Description("Method name to find overrides of")] string methodName,
        [Description("Class name that contains the base method")] string className,
        [Description("Workspace ID for context")] string? workspaceId = null)
    {
        if (_workspaceManager == null)
        {
            throw new InvalidOperationException("DotnetTools not initialized");
        }

        try
        {
            var result = await _workspaceManager.FindOverridesAsync(methodName, className, workspaceId);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to find overrides");
            throw new InvalidOperationException($"Failed to find overrides: {ex.Message}", ex);
        }
    }

    [McpServerTool(Name = "spelunk-find-derived-types"), Description(ToolDescriptions.FindDerivedTypes)]
    public static async Task<string> DotnetFindDerivedTypes(
        [Description("Base class name to find derived types of")] string baseClassName,
        [Description("Workspace ID for context")] string? workspaceId = null)
    {
        if (_workspaceManager == null)
        {
            throw new InvalidOperationException("DotnetTools not initialized");
        }

        try
        {
            var result = await _workspaceManager.FindDerivedTypesAsync(baseClassName, workspaceId);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to find derived types");
            throw new InvalidOperationException($"Failed to find derived types: {ex.Message}", ex);
        }
    }

    [McpServerTool(Name = "spelunk-rename-symbol"), Description(ToolDescriptions.RenameSymbol)]
    public static async Task<string> DotnetRenameSymbol(
        [Description("Current name of the symbol")] string oldName,
        [Description("New name for the symbol")] string newName,
        [Description("Type of symbol (class, method, property, field, etc.)")] string? symbolType = null,
        [Description("Optional container name (class/namespace)")] string? containerName = null,
        [Description("Workspace ID for context")] string? workspaceId = null,
        [Description("Preview changes without applying them")] bool preview = false)
    {
        if (_workspaceManager == null)
        {
            throw new InvalidOperationException("DotnetTools not initialized");
        }

        try
        {
            var result = await _workspaceManager.RenameSymbolAsync(oldName, newName, symbolType, containerName, workspaceId, preview);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to rename symbol");
            throw new InvalidOperationException($"Failed to rename symbol: {ex.Message}", ex);
        }
    }

    [McpServerTool(Name = "spelunk-edit-code"), Description(ToolDescriptions.EditCode)]
    public static async Task<string> DotnetEditCode(
        [Description("File path to edit")] string file,
        [Description("Operation: add-method, modify-method, remove-method, add-property, etc.")] string operation,
        [Description("Target class name")] string? className = null,
        [Description("Target method name (for method operations)")] string? methodName = null,
        [Description("Code to add or replacement code")] string? code = null,
        [Description("Additional parameters as JSON")] string? parameters = null,
        [Description("Preview changes without applying them")] bool preview = false)
    {
        if (_workspaceManager == null)
        {
            throw new InvalidOperationException("DotnetTools not initialized");
        }

        try
        {
            // Parse parameters JSON if provided
            JsonElement? parsedParams = null;
            if (!string.IsNullOrEmpty(parameters))
            {
                parsedParams = JsonSerializer.Deserialize<JsonElement>(parameters);
            }

            var result = await _workspaceManager.EditCodeAsync(file, operation, className, methodName, code, parsedParams, preview);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to edit code");
            throw new InvalidOperationException($"Failed to edit code: {ex.Message}", ex);
        }
    }

    [McpServerTool(Name = "spelunk-find-statements"), Description(ToolDescriptions.FindStatements)]
    public static async Task<string> DotnetFindStatements(
        [Description("Text, regex, or SpelunkPath pattern to match in statements. SpelunkPath allows XPath-style queries like '//method[Get*]//statement[@type=ThrowStatement]'")] string pattern,
        [Description("Pattern type: 'text' (default), 'regex', or 'spelunkpath' for XPath-style queries")] string patternType = "text",
        [Description("Optional file path to search in")] string? filePath = null,
        [Description("Include nested statements in blocks")] bool includeNestedStatements = true,
        [Description("Group related statements together")] bool groupRelated = false,
        [Description("Workspace ID for context")] string? workspaceId = null)
    {
        if (_workspaceManager == null)
        {
            throw new InvalidOperationException("DotnetTools not initialized");
        }

        try
        {
            // Build scope dictionary if filePath is provided
            Dictionary<string, string>? scope = null;
            if (!string.IsNullOrEmpty(filePath))
            {
                scope = new Dictionary<string, string> { { "file", filePath } };
            }

            var result = await _workspaceManager.FindStatementsAsync(pattern, scope, patternType, includeNestedStatements, groupRelated, workspaceId);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to find statements");
            throw new InvalidOperationException($"Failed to find statements: {ex.Message}", ex);
        }
    }

    [McpServerTool(Name = "spelunk-replace-statement"), Description(ToolDescriptions.ReplaceStatement)]
    public static async Task<string> DotnetReplaceStatement(
        [Description("File path containing the statement")] string filePath,
        [Description("Line number (1-based)")] int line,
        [Description("Column number (1-based)")] int column,
        [Description("New statement code")] string newStatement,
        [Description("Preserve comments attached to the statement")] bool preserveComments = true,
        [Description("Workspace ID for context")] string? workspaceId = null)
    {
        if (_workspaceManager == null)
        {
            throw new InvalidOperationException("DotnetTools not initialized");
        }

        try
        {
            var result = await _workspaceManager.ReplaceStatementAsync(filePath, line, column, newStatement, preserveComments, workspaceId);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to replace statement");
            throw new InvalidOperationException($"Failed to replace statement: {ex.Message}", ex);
        }
    }

    [McpServerTool(Name = "spelunk-insert-statement"), Description(ToolDescriptions.InsertStatement)]
    public static async Task<string> DotnetInsertStatement(
        [Description("Position: 'before' or 'after'")] string position,
        [Description("File path containing the reference statement")] string filePath,
        [Description("Line number (1-based)")] int line,
        [Description("Column number (1-based)")] int column,
        [Description("Statement code to insert")] string statement,
        [Description("Workspace ID for context")] string? workspaceId = null)
    {
        if (_workspaceManager == null)
        {
            throw new InvalidOperationException("DotnetTools not initialized");
        }

        try
        {
            var result = await _workspaceManager.InsertStatementAsync(position, filePath, line, column, statement, workspaceId);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to insert statement");
            throw new InvalidOperationException($"Failed to insert statement: {ex.Message}", ex);
        }
    }

    [McpServerTool(Name = "spelunk-remove-statement"), Description(ToolDescriptions.RemoveStatement)]
    public static async Task<string> DotnetRemoveStatement(
        [Description("File path containing the statement")] string filePath,
        [Description("Line number (1-based)")] int line,
        [Description("Column number (1-based)")] int column,
        [Description("Preserve comments attached to the statement")] bool preserveComments = true,
        [Description("Workspace ID for context")] string? workspaceId = null)
    {
        if (_workspaceManager == null)
        {
            throw new InvalidOperationException("DotnetTools not initialized");
        }

        try
        {
            var result = await _workspaceManager.RemoveStatementAsync(filePath, line, column, preserveComments, workspaceId);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to remove statement");
            throw new InvalidOperationException($"Failed to remove statement: {ex.Message}", ex);
        }
    }

    [McpServerTool(Name = "spelunk-mark-statement"), Description(ToolDescriptions.MarkStatement)]
    public static async Task<string> DotnetMarkStatement(
        [Description("File path containing the statement")] string filePath,
        [Description("Line number (1-based)")] int line,
        [Description("Column number (1-based)")] int column,
        [Description("Optional label for the marker")] string? label = null,
        [Description("Workspace ID for context")] string? workspaceId = null)
    {
        if (_workspaceManager == null)
        {
            throw new InvalidOperationException("DotnetTools not initialized");
        }

        try
        {
            var result = await _workspaceManager.MarkStatementAsync(filePath, line, column, label, workspaceId);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to mark statement");
            throw new InvalidOperationException($"Failed to mark statement: {ex.Message}", ex);
        }
    }

    [McpServerTool(Name = "spelunk-find-marked-statements"), Description(ToolDescriptions.FindMarkedStatements)]
    public static async Task<string> DotnetFindMarkedStatements(
        [Description("Marker ID to search for (optional - finds all if not specified)")] string? markerId = null,
        [Description("Optional file path to search in")] string? filePath = null)
    {
        if (_workspaceManager == null)
        {
            throw new InvalidOperationException("DotnetTools not initialized");
        }

        try
        {
            var result = await _workspaceManager.FindMarkedStatementsAsync(markerId, filePath);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to find marked statements");
            throw new InvalidOperationException($"Failed to find marked statements: {ex.Message}", ex);
        }
    }

    [McpServerTool(Name = "spelunk-unmark-statement"), Description(ToolDescriptions.UnmarkStatement)]
    public static async Task<string> DotnetUnmarkStatement(
        [Description("Marker ID to remove")] string markerId)
    {
        if (_workspaceManager == null)
        {
            throw new InvalidOperationException("DotnetTools not initialized");
        }

        try
        {
            var result = await _workspaceManager.UnmarkStatementAsync(markerId);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to unmark statement");
            throw new InvalidOperationException($"Failed to unmark statement: {ex.Message}", ex);
        }
    }

    [McpServerTool(Name = "spelunk-clear-markers"), Description(ToolDescriptions.ClearMarkers)]
    public static async Task<string> DotnetClearMarkers()
    {
        if (_workspaceManager == null)
        {
            throw new InvalidOperationException("DotnetTools not initialized");
        }

        try
        {
            var result = await _workspaceManager.ClearAllMarkersAsync();
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to clear markers");
            throw new InvalidOperationException($"Failed to clear markers: {ex.Message}", ex);
        }
    }

    [McpServerTool(Name = "spelunk-get-statement-context"), Description(ToolDescriptions.GetStatementContext)]
    public static async Task<string> DotnetGetStatementContext(
        [Description("Statement ID from find-statements (e.g., 'stmt-123')")] string? statementId = null,
        [Description("File path (alternative to statementId)")] string? file = null,
        [Description("Line number (1-based, used with file)")] int? line = null,
        [Description("Column number (1-based, used with file)")] int? column = null,
        [Description("Optional workspace path")] string? workspacePath = null)
    {
        if (_workspaceManager == null)
        {
            throw new InvalidOperationException("DotnetTools not initialized");
        }

        try
        {
            // Validate input
            if (!string.IsNullOrEmpty(statementId))
            {
                // Look up by statement ID
                var (node, filePath, workspaceId) = _workspaceManager.GetStatementById(statementId);
                if (node == null || string.IsNullOrEmpty(filePath))
                {
                    throw new ArgumentException($"Statement with ID '{statementId}' not found");
                }
                
                var syntaxTree = node.SyntaxTree;
                var lineSpan = syntaxTree.GetLineSpan(node.Span);
                var result = await _workspaceManager.GetStatementContextAsync(
                    filePath,
                    lineSpan.StartLinePosition.Line + 1,
                    lineSpan.StartLinePosition.Character + 1,
                    workspaceId);

                return result.Match(
                    Right: ctx => JsonSerializer.Serialize(ctx, new JsonSerializerOptions { WriteIndented = true }),
                    Left: error => throw new InvalidOperationException($"Error getting statement context: {error.Message}")
                );
            }
            else if (!string.IsNullOrEmpty(file) && line.HasValue && column.HasValue)
            {
                // Direct location
                if (IsPathAllowed(file))
                {
                    var result = await _workspaceManager.GetStatementContextAsync(file, line.Value, column.Value, workspacePath);
                    return result.Match(
                        Right: ctx => JsonSerializer.Serialize(ctx, new JsonSerializerOptions { WriteIndented = true }),
                        Left: error => throw new InvalidOperationException($"Error getting statement context: {error.Message}")
                    );
                }
                else
                {
                    throw new UnauthorizedAccessException($"Access to path '{file}' is not allowed");
                }
            }
            else
            {
                throw new ArgumentException("Either statementId or (file, line, column) must be provided");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get statement context");
            throw new InvalidOperationException($"Failed to get statement context: {ex.Message}", ex);
        }
    }

    [McpServerTool(Name = "spelunk-get-data-flow"), Description(ToolDescriptions.GetDataFlow)]
    public static async Task<string> DotnetGetDataFlow(
        [Description("File path to analyze")] string file,
        [Description("Start line of region (1-based)")] int startLine,
        [Description("Start column of region (1-based)")] int startColumn,
        [Description("End line of region (1-based)")] int endLine,
        [Description("End column of region (1-based)")] int endColumn,
        [Description("Include control flow analysis (default: true)")] bool includeControlFlow = true,
        [Description("Optional workspace path")] string? workspacePath = null)
    {
        if (_workspaceManager == null)
        {
            throw new InvalidOperationException("DotnetTools not initialized");
        }

        try
        {
            // Validate path is allowed
            if (!IsPathAllowed(file))
            {
                throw new UnauthorizedAccessException($"Access to path '{file}' is not allowed");
            }
            
            var result = await _workspaceManager.GetDataFlowAnalysisAsync(
                file, startLine, startColumn, endLine, endColumn, includeControlFlow, workspacePath);

            return result.Match(
                Right: data => JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }),
                Left: error => throw new InvalidOperationException($"Failed to get data flow analysis: {error.Message}")
            );
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get data flow analysis");
            throw new InvalidOperationException($"Failed to get data flow analysis: {ex.Message}", ex);
        }
    }

    private static bool IsPathAllowed(string path)
    {
        if (_optionsMonitor == null)
        {
            throw new InvalidOperationException("DotnetTools not initialized");
        }

        var normalizedPath = Path.GetFullPath(path);
        var allowedPaths = _optionsMonitor.CurrentValue.AllowedPaths;

        return allowedPaths.Any(allowed =>
            normalizedPath.StartsWith(Path.GetFullPath(allowed), StringComparison.OrdinalIgnoreCase));
    }
}