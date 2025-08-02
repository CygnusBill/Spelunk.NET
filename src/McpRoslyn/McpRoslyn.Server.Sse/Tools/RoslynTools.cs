using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace McpRoslyn.Server.Sse.Tools;

[McpServerToolType]
public static class RoslynTools
{
    private static readonly Dictionary<string, (MSBuildWorkspace workspace, DateTime loadedAt)> _workspaces = new();
    private static readonly List<string> _allowedPaths = new();
    private static ILogger<Program>? _logger;

    public static void Initialize(List<string> allowedPaths, ILogger<Program> logger)
    {
        _allowedPaths.Clear();
        _allowedPaths.AddRange(allowedPaths);
        _logger = logger;
    }

    [McpServerTool(Name = "dotnet-load-workspace"), Description("Load a .NET solution or project into the workspace")]
    public static async Task<string> DotnetLoadWorkspace(
        [Description("Path to the solution (.sln) or project file")] string path,
        [Description("Optional workspace ID")] string? workspaceId = null)
    {
        try
        {
            // Validate path
            var fullPath = Path.GetFullPath(path);
            if (!IsPathAllowed(fullPath))
            {
                throw new InvalidOperationException($"Path '{fullPath}' is not in allowed paths");
            }

            // Generate workspace ID if not provided
            workspaceId ??= Path.GetFileNameWithoutExtension(path) + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);

            // Check if already loaded
            if (_workspaces.ContainsKey(workspaceId))
            {
                return $"Workspace '{workspaceId}' already loaded";
            }

            // Create MSBuild workspace
            var workspace = MSBuildWorkspace.Create();
            
            workspace.WorkspaceFailed += (sender, args) =>
            {
                _logger?.LogWarning("Workspace diagnostic: {Kind} - {Message}", args.Diagnostic.Kind, args.Diagnostic.Message);
            };

            // Load solution or project
            if (path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
            {
                var solution = await workspace.OpenSolutionAsync(fullPath);
                var projectCount = solution.Projects.Count();
                
                _workspaces[workspaceId] = (workspace, DateTime.UtcNow);
                
                return $"Solution loaded successfully\nWorkspace ID: {workspaceId}\nProjects: {projectCount}\nPath: {fullPath}";
            }
            else if (path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) || 
                     path.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase) ||
                     path.EndsWith(".fsproj", StringComparison.OrdinalIgnoreCase))
            {
                var project = await workspace.OpenProjectAsync(fullPath);
                
                _workspaces[workspaceId] = (workspace, DateTime.UtcNow);
                
                return $"Project loaded successfully\nWorkspace ID: {workspaceId}\nProject: {project.Name}\nPath: {fullPath}";
            }
            else
            {
                throw new InvalidOperationException("Unsupported file type. Please provide a .sln or project file.");
            }
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
        if (!string.IsNullOrEmpty(workspaceId))
        {
            if (_workspaces.TryGetValue(workspaceId, out var ws))
            {
                var solution = ws.workspace.CurrentSolution;
                var projects = solution.Projects.Select(p => new
                {
                    Name = p.Name,
                    Language = p.Language,
                    DocumentCount = p.Documents.Count(),
                    AssemblyName = p.AssemblyName
                }).ToList();

                var status = new
                {
                    WorkspaceId = workspaceId,
                    LoadedAt = ws.loadedAt,
                    ProjectCount = projects.Count,
                    Projects = projects
                };

                return JsonSerializer.Serialize(status, new JsonSerializerOptions { WriteIndented = true });
            }
            else
            {
                throw new InvalidOperationException($"Workspace '{workspaceId}' not found");
            }
        }

        // Return all workspaces
        var allWorkspaces = _workspaces.Select(kvp =>
        {
            var solution = kvp.Value.workspace.CurrentSolution;
            return new
            {
                WorkspaceId = kvp.Key,
                LoadedAt = kvp.Value.loadedAt,
                ProjectCount = solution.Projects.Count()
            };
        }).ToList();

        var result = new
        {
            WorkspaceCount = _workspaces.Count,
            Workspaces = allWorkspaces
        };

        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool(Name = "dotnet-analyze-syntax"), Description("Analyze the syntax tree of a C# file")]
    public static string DotnetAnalyzeSyntax(
        [Description("Path to the source file")] string filePath,
        [Description("Workspace ID for context")] string? workspaceId = null)
    {
        // TODO: Implement syntax analysis
        return "Syntax analysis not yet implemented";
    }

    [McpServerTool(Name = "dotnet-get-symbols"), Description("Get symbol information from code")]
    public static string DotnetGetSymbols(
        [Description("Path to the source file")] string filePath,
        [Description("Workspace ID for context")] string? workspaceId = null,
        [Description("Optional symbol name to search for")] string? symbolName = null)
    {
        // TODO: Implement symbol retrieval
        return "Symbol retrieval not yet implemented";
    }

    private static bool IsPathAllowed(string path)
    {
        var normalizedPath = Path.GetFullPath(path);
        return _allowedPaths.Any(allowed => 
            normalizedPath.StartsWith(Path.GetFullPath(allowed), StringComparison.OrdinalIgnoreCase));
    }
}