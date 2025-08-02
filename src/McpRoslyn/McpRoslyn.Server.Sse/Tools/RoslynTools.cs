using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;
using McpRoslyn.Server;
using Microsoft.Extensions.Logging;

namespace McpRoslyn.Server.Sse.Tools;

[McpServerToolType]
public static class RoslynTools
{
    private static RoslynWorkspaceManager? _workspaceManager;
    private static readonly List<string> _allowedPaths = new();
    private static ILogger<Program>? _logger;

    public static void Initialize(List<string> allowedPaths, ILogger<Program> logger)
    {
        _logger = logger;
        _allowedPaths.Clear();
        _allowedPaths.AddRange(allowedPaths);
        
        // Create a typed logger for RoslynWorkspaceManager
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var workspaceLogger = loggerFactory.CreateLogger<RoslynWorkspaceManager>();
        
        _workspaceManager = new RoslynWorkspaceManager(workspaceLogger);
    }

    [McpServerTool(Name = "dotnet-load-workspace"), Description("Load a .NET solution or project into the workspace")]
    public static async Task<string> DotnetLoadWorkspace(
        [Description("Full path to the solution (.sln) or project file (relative paths are not supported)")] string path,
        [Description("Optional workspace ID")] string? workspaceId = null)
    {
        if (_workspaceManager == null)
        {
            throw new InvalidOperationException("RoslynTools not initialized");
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
                throw new InvalidOperationException($"Failed to load workspace: {message}");
            }

            // Count projects in the workspace  
            var projectCount = workspace.CurrentSolution.Projects.Count();
            var fileType = path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ? "Solution" : "Project";
            
            return $"{fileType} loaded successfully\nWorkspace ID: {actualWorkspaceId}\nProjects: {projectCount}\nPath: {path}";
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load workspace");
            throw new InvalidOperationException($"Failed to load workspace: {ex.Message}", ex);
        }
    }

    [McpServerTool(Name = "dotnet-workspace-status"), Description("Get the status of loaded workspaces")]
    public static string DotnetWorkspaceStatus(
        [Description("Optional workspace ID to get specific status")] string? workspaceId = null)
    {
        if (_workspaceManager == null)
        {
            throw new InvalidOperationException("RoslynTools not initialized");
        }

        var status = _workspaceManager.GetStatus();
        return JsonSerializer.Serialize(status, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool(Name = "dotnet-analyze-syntax"), Description("Analyze the syntax tree of a C# file")]
    public static async Task<string> DotnetAnalyzeSyntax(
        [Description("Path to the source file")] string filePath,
        [Description("Workspace ID for context")] string? workspaceId = null)
    {
        if (_workspaceManager == null)
        {
            throw new InvalidOperationException("RoslynTools not initialized");
        }

        try
        {
            var result = await _workspaceManager.AnalyzeSyntaxTreeAsync(filePath, includeTrivia: false, workspaceId);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to analyze syntax");
            throw new InvalidOperationException($"Failed to analyze syntax: {ex.Message}", ex);
        }
    }

    [McpServerTool(Name = "dotnet-get-symbols"), Description("Get symbol information from code")]
    public static async Task<string> DotnetGetSymbols(
        [Description("Path to the source file")] string filePath,
        [Description("Workspace ID for context")] string? workspaceId = null,
        [Description("Optional symbol name to search for")] string? symbolName = null)
    {
        if (_workspaceManager == null)
        {
            throw new InvalidOperationException("RoslynTools not initialized");
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
            _logger?.LogError(ex, "Failed to get symbols");
            throw new InvalidOperationException($"Failed to get symbols: {ex.Message}", ex);
        }
    }

    [McpServerTool(Name = "dotnet-find-class"), Description("Find classes in the workspace")]
    public static async Task<string> DotnetFindClass(
        [Description("Pattern to match class names (supports wildcards like User*)")] string pattern,
        [Description("Workspace ID for context")] string? workspaceId = null)
    {
        if (_workspaceManager == null)
        {
            throw new InvalidOperationException("RoslynTools not initialized");
        }

        try
        {
            var result = await _workspaceManager.FindClassesAsync(pattern, workspaceId);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to find classes");
            throw new InvalidOperationException($"Failed to find classes: {ex.Message}", ex);
        }
    }

    [McpServerTool(Name = "dotnet-find-method"), Description("Find methods in the workspace")]
    public static async Task<string> DotnetFindMethod(
        [Description("Pattern to match method names (supports wildcards like Get*)")] string methodPattern,
        [Description("Optional pattern to match class names")] string? classPattern = null,
        [Description("Workspace ID for context")] string? workspaceId = null)
    {
        if (_workspaceManager == null)
        {
            throw new InvalidOperationException("RoslynTools not initialized");
        }

        try
        {
            var result = await _workspaceManager.FindMethodsAsync(methodPattern, classPattern, workspaceId);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to find methods");
            throw new InvalidOperationException($"Failed to find methods: {ex.Message}", ex);
        }
    }

    [McpServerTool(Name = "dotnet-find-property"), Description("Find properties in the workspace")]
    public static async Task<string> DotnetFindProperty(
        [Description("Pattern to match property names (supports wildcards like Name*)")] string propertyPattern,
        [Description("Optional pattern to match class names")] string? classPattern = null,
        [Description("Workspace ID for context")] string? workspaceId = null)
    {
        if (_workspaceManager == null)
        {
            throw new InvalidOperationException("RoslynTools not initialized");
        }

        try
        {
            var result = await _workspaceManager.FindPropertiesAsync(propertyPattern, classPattern, workspaceId);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to find properties");
            throw new InvalidOperationException($"Failed to find properties: {ex.Message}", ex);
        }
    }

    [McpServerTool(Name = "dotnet-find-method-calls"), Description("Find calls to a specific method")]
    public static async Task<string> DotnetFindMethodCalls(
        [Description("Method name to find calls to")] string methodName,
        [Description("Optional class name that contains the method")] string? className = null,
        [Description("Workspace ID for context")] string? workspaceId = null)
    {
        if (_workspaceManager == null)
        {
            throw new InvalidOperationException("RoslynTools not initialized");
        }

        try
        {
            var result = await _workspaceManager.FindMethodCallsAsync(methodName, className, workspaceId);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to find method calls");
            throw new InvalidOperationException($"Failed to find method calls: {ex.Message}", ex);
        }
    }

    [McpServerTool(Name = "dotnet-find-method-callers"), Description("Find what methods call a specific method")]
    public static async Task<string> DotnetFindMethodCallers(
        [Description("Method name to find callers of")] string methodName,
        [Description("Optional class name that contains the method")] string? className = null,
        [Description("Workspace ID for context")] string? workspaceId = null)
    {
        if (_workspaceManager == null)
        {
            throw new InvalidOperationException("RoslynTools not initialized");
        }

        try
        {
            var result = await _workspaceManager.FindMethodCallersAsync(methodName, className, workspaceId);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to find method callers");
            throw new InvalidOperationException($"Failed to find method callers: {ex.Message}", ex);
        }
    }

    [McpServerTool(Name = "dotnet-find-references"), Description("Find all references to a symbol")]
    public static async Task<string> DotnetFindReferences(
        [Description("Symbol name to find references to")] string symbolName,
        [Description("Type of symbol (class, method, property, field, etc.)")] string? symbolType = null,
        [Description("Optional container name (class/namespace)")] string? containerName = null,
        [Description("Workspace ID for context")] string? workspaceId = null)
    {
        if (_workspaceManager == null)
        {
            throw new InvalidOperationException("RoslynTools not initialized");
        }

        try
        {
            var result = await _workspaceManager.FindReferencesAsync(symbolName, symbolType, containerName, workspaceId);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to find references");
            throw new InvalidOperationException($"Failed to find references: {ex.Message}", ex);
        }
    }

    [McpServerTool(Name = "dotnet-find-implementations"), Description("Find implementations of an interface")]
    public static async Task<string> DotnetFindImplementations(
        [Description("Interface name to find implementations of")] string interfaceName,
        [Description("Workspace ID for context")] string? workspaceId = null)
    {
        if (_workspaceManager == null)
        {
            throw new InvalidOperationException("RoslynTools not initialized");
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

    [McpServerTool(Name = "dotnet-find-overrides"), Description("Find overrides of a virtual/abstract method")]
    public static async Task<string> DotnetFindOverrides(
        [Description("Method name to find overrides of")] string methodName,
        [Description("Class name that contains the base method")] string className,
        [Description("Workspace ID for context")] string? workspaceId = null)
    {
        if (_workspaceManager == null)
        {
            throw new InvalidOperationException("RoslynTools not initialized");
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

    [McpServerTool(Name = "dotnet-find-derived-types"), Description("Find types that derive from a base class")]
    public static async Task<string> DotnetFindDerivedTypes(
        [Description("Base class name to find derived types of")] string baseClassName,
        [Description("Workspace ID for context")] string? workspaceId = null)
    {
        if (_workspaceManager == null)
        {
            throw new InvalidOperationException("RoslynTools not initialized");
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

    [McpServerTool(Name = "dotnet-rename-symbol"), Description("Rename a symbol throughout the workspace")]
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
            throw new InvalidOperationException("RoslynTools not initialized");
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

    [McpServerTool(Name = "dotnet-edit-code"), Description("Add, modify, or remove code in a class")]
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
            throw new InvalidOperationException("RoslynTools not initialized");
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

    [McpServerTool(Name = "dotnet-fix-pattern"), Description("Find and replace code patterns")]
    public static async Task<string> DotnetFixPattern(
        [Description("Pattern to find (supports regex)")] string findPattern,
        [Description("Replacement pattern")] string replacePattern,
        [Description("Pattern type: text, regex")] string patternType = "text",
        [Description("Workspace ID for context")] string? workspaceId = null,
        [Description("Preview changes without applying them")] bool preview = false)
    {
        if (_workspaceManager == null)
        {
            throw new InvalidOperationException("RoslynTools not initialized");
        }

        try
        {
            var result = await _workspaceManager.FixPatternAsync(findPattern, replacePattern, patternType, workspaceId, preview);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to fix pattern");
            throw new InvalidOperationException($"Failed to fix pattern: {ex.Message}", ex);
        }
    }

    [McpServerTool(Name = "dotnet-find-statements"), Description("Find statements in code using text, regex, or RoslynPath queries")]
    public static async Task<string> DotnetFindStatements(
        [Description("Text, regex, or RoslynPath pattern to match in statements. RoslynPath allows XPath-style queries like '//method[Get*]//statement[@type=ThrowStatement]'")] string pattern,
        [Description("Pattern type: 'text' (default), 'regex', or 'roslynpath' for XPath-style queries")] string patternType = "text",
        [Description("Optional file path to search in")] string? filePath = null,
        [Description("Include nested statements in blocks")] bool includeNestedStatements = true,
        [Description("Group related statements together")] bool groupRelated = false,
        [Description("Workspace ID for context")] string? workspaceId = null)
    {
        if (_workspaceManager == null)
        {
            throw new InvalidOperationException("RoslynTools not initialized");
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

    [McpServerTool(Name = "dotnet-replace-statement"), Description("Replace a specific statement with new code")]
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
            throw new InvalidOperationException("RoslynTools not initialized");
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

    [McpServerTool(Name = "dotnet-insert-statement"), Description("Insert a new statement before or after an existing statement")]
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
            throw new InvalidOperationException("RoslynTools not initialized");
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

    [McpServerTool(Name = "dotnet-remove-statement"), Description("Remove a specific statement")]
    public static async Task<string> DotnetRemoveStatement(
        [Description("File path containing the statement")] string filePath,
        [Description("Line number (1-based)")] int line,
        [Description("Column number (1-based)")] int column,
        [Description("Preserve comments attached to the statement")] bool preserveComments = true,
        [Description("Workspace ID for context")] string? workspaceId = null)
    {
        if (_workspaceManager == null)
        {
            throw new InvalidOperationException("RoslynTools not initialized");
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

    [McpServerTool(Name = "dotnet-mark-statement"), Description("Mark a statement with an ephemeral marker for tracking")]
    public static async Task<string> DotnetMarkStatement(
        [Description("File path containing the statement")] string filePath,
        [Description("Line number (1-based)")] int line,
        [Description("Column number (1-based)")] int column,
        [Description("Optional label for the marker")] string? label = null,
        [Description("Workspace ID for context")] string? workspaceId = null)
    {
        if (_workspaceManager == null)
        {
            throw new InvalidOperationException("RoslynTools not initialized");
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

    [McpServerTool(Name = "dotnet-find-marked-statements"), Description("Find statements with specific markers")]
    public static async Task<string> DotnetFindMarkedStatements(
        [Description("Marker ID to search for (optional - finds all if not specified)")] string? markerId = null,
        [Description("Optional file path to search in")] string? filePath = null)
    {
        if (_workspaceManager == null)
        {
            throw new InvalidOperationException("RoslynTools not initialized");
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

    [McpServerTool(Name = "dotnet-unmark-statement"), Description("Remove a marker from statements")]
    public static async Task<string> DotnetUnmarkStatement(
        [Description("Marker ID to remove")] string markerId)
    {
        if (_workspaceManager == null)
        {
            throw new InvalidOperationException("RoslynTools not initialized");
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

    [McpServerTool(Name = "dotnet-clear-markers"), Description("Clear all ephemeral markers")]
    public static async Task<string> DotnetClearMarkers()
    {
        if (_workspaceManager == null)
        {
            throw new InvalidOperationException("RoslynTools not initialized");
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

    private static bool IsPathAllowed(string path)
    {
        var normalizedPath = Path.GetFullPath(path);
        return _allowedPaths.Any(allowed => 
            normalizedPath.StartsWith(Path.GetFullPath(allowed), StringComparison.OrdinalIgnoreCase));
    }
}