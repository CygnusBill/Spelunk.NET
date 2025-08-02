using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.CSharp;
using CS = Microsoft.CodeAnalysis.CSharp.Syntax;
using VB = Microsoft.CodeAnalysis.VisualBasic.Syntax;
using VBSyntaxKind = Microsoft.CodeAnalysis.VisualBasic.SyntaxKind;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;
using McpRoslyn.Server.RoslynPath;
using McpRoslyn.Server.LanguageHandlers;
using McpRoslyn.Server.FSharp;

namespace McpRoslyn.Server;

public class RoslynWorkspaceManager : IDisposable
{
    private readonly ILogger<RoslynWorkspaceManager> _logger;
    private readonly Dictionary<string, WorkspaceEntry> _workspaces = new();
    private readonly Dictionary<string, string> _workspaceHistory = new(); // ID -> Path mapping for recently cleaned workspaces
    private readonly MarkerManager _markerManager = new();
    private readonly FSharpProjectTracker _fsharpTracker = new();
    private readonly Timer _cleanupTimer;
    private readonly TimeSpan _workspaceTimeout = TimeSpan.FromMinutes(15);
    private readonly TimeSpan _historyTimeout = TimeSpan.FromHours(1);
    private static bool _msBuildRegistered = false;
    private bool _disposed = false;
    
    public RoslynWorkspaceManager(ILogger<RoslynWorkspaceManager> logger)
    {
        _logger = logger;
        
        // Register MSBuild once per process
        if (!MSBuildLocator.IsRegistered && !_msBuildRegistered)
        {
            try
            {
                MSBuildLocator.RegisterDefaults();
                _msBuildRegistered = true;
                _logger.LogInformation("MSBuild registered successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register MSBuild");
            }
        }
        else if (MSBuildLocator.IsRegistered)
        {
            _logger.LogInformation("MSBuild already registered, skipping registration");
        }
        
        // Start cleanup timer (runs every 15 minutes for debugging, should be 5 minutes for production)
        _cleanupTimer = new Timer(CleanupStaleWorkspaces, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(15));
    }
    
    public async Task<(bool success, string message, Workspace? workspace, string workspaceId)> LoadWorkspaceAsync(string path, string? workspaceId = null)
    {
        try
        {
            _logger.LogInformation("Loading workspace from: {Path}", path);
            
            // Generate workspace ID if not provided
            if (string.IsNullOrEmpty(workspaceId))
            {
                var fileName = Path.GetFileNameWithoutExtension(path);
                var hash = path.GetHashCode().ToString("x8");
                workspaceId = $"{fileName}_{hash}";
            }
            
            // Check if already loaded by ID
            if (_workspaces.TryGetValue(workspaceId, out var existingEntry))
            {
                existingEntry.Touch(); // Update access time
                return (true, "Workspace already loaded", existingEntry.Workspace, workspaceId);
            }
            
            // Check if path is already loaded under different ID (shouldn't happen but be safe)
            var existingByPath = _workspaces.Values.FirstOrDefault(e => e.Path == path);
            if (existingByPath != null)
            {
                existingByPath.Touch();
                var existingId = _workspaces.FirstOrDefault(kvp => kvp.Value == existingByPath).Key;
                return (true, $"Workspace already loaded with ID: {existingId}", existingByPath.Workspace, existingId);
            }
            
            // Determine workspace type and load
            if (path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
            {
                return await LoadSolutionAsync(path, workspaceId);
            }
            else if (path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) || 
                     path.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase) ||
                     path.EndsWith(".fsproj", StringComparison.OrdinalIgnoreCase))
            {
                return await LoadProjectAsync(path, workspaceId);
            }
            else
            {
                return (false, "Unsupported file type. Please provide a .sln or project file.", null, workspaceId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load workspace");
            return (false, $"Failed to load workspace: {ex.Message}", null, workspaceId ?? "");
        }
    }
    
    private async Task<(bool success, string message, Workspace? workspace, string workspaceId)> LoadSolutionAsync(string solutionPath, string workspaceId)
    {
        var workspace = MSBuildWorkspace.Create();
        
        workspace.WorkspaceFailed += (sender, args) =>
        {
            _logger.LogWarning("Workspace diagnostic: {Kind} - {Message}", args.Diagnostic.Kind, args.Diagnostic.Message);
            
            // Detect F# project loading failures
            if (args.Diagnostic.Message.Contains(".fsproj", StringComparison.OrdinalIgnoreCase) &&
                (args.Diagnostic.Message.Contains("Could not load project", StringComparison.OrdinalIgnoreCase) ||
                 args.Diagnostic.Message.Contains("not supported", StringComparison.OrdinalIgnoreCase)))
            {
                // Extract project path from the diagnostic message
                var match = Regex.Match(args.Diagnostic.Message, @"'([^']+\.fsproj)'");
                if (match.Success)
                {
                    var projectPath = match.Groups[1].Value;
                    var projectName = Path.GetFileNameWithoutExtension(projectPath);
                    _fsharpTracker.AddSkippedProject(projectPath, projectName, workspaceId);
                    _logger.LogInformation("Detected F# project: {Path}", projectPath);
                }
            }
        };
        
        var solution = await workspace.OpenSolutionAsync(solutionPath);
        
        // Create workspace entry
        var entry = new WorkspaceEntry
        {
            Workspace = workspace,
            Path = solutionPath
        };
        
        _workspaces[workspaceId] = entry;
        
        var projectCount = solution.Projects.Count();
        _logger.LogInformation("Loaded solution with {Count} projects, ID: {WorkspaceId}", projectCount, workspaceId);
        
        return (true, $"Solution loaded successfully with {projectCount} projects", workspace, workspaceId);
    }
    
    private async Task<(bool success, string message, Workspace? workspace, string workspaceId)> LoadProjectAsync(string projectPath, string workspaceId)
    {
        var workspace = MSBuildWorkspace.Create();
        
        workspace.WorkspaceFailed += (sender, args) =>
        {
            _logger.LogWarning("Workspace diagnostic: {Kind} - {Message}", args.Diagnostic.Kind, args.Diagnostic.Message);
            
            // Detect F# project loading failures
            if (args.Diagnostic.Message.Contains(".fsproj", StringComparison.OrdinalIgnoreCase) &&
                (args.Diagnostic.Message.Contains("Could not load project", StringComparison.OrdinalIgnoreCase) ||
                 args.Diagnostic.Message.Contains("not supported", StringComparison.OrdinalIgnoreCase)))
            {
                // Extract project path from the diagnostic message
                var match = Regex.Match(args.Diagnostic.Message, @"'([^']+\.fsproj)'");
                if (match.Success)
                {
                    var projectPath = match.Groups[1].Value;
                    var projectName = Path.GetFileNameWithoutExtension(projectPath);
                    _fsharpTracker.AddSkippedProject(projectPath, projectName, workspaceId);
                    _logger.LogInformation("Detected F# project: {Path}", projectPath);
                }
            }
        };
        
        var project = await workspace.OpenProjectAsync(projectPath);
        
        // Create workspace entry
        var entry = new WorkspaceEntry
        {
            Workspace = workspace,
            Path = projectPath
        };
        
        _workspaces[workspaceId] = entry;
        
        _logger.LogInformation("Loaded project: {Name}, ID: {WorkspaceId}", project.Name, workspaceId);
        
        return (true, $"Project '{project.Name}' loaded successfully", workspace, workspaceId);
    }
    
    public IReadOnlyList<FSharpProjectInfo> GetFSharpProjects(string? workspaceId = null)
    {
        return _fsharpTracker.GetAllProjects(workspaceId);
    }
    
    public IReadOnlyList<FSharpProjectInfo> GetSkippedFSharpProjects(string? workspaceId = null)
    {
        return _fsharpTracker.GetSkippedProjects(workspaceId);
    }
    
    public WorkspaceStatus GetStatus()
    {
        var statuses = new List<object>();
        
        foreach (var (workspaceId, entry) in _workspaces)
        {
            entry.Touch(); // Update access time when status is checked
            
            var solution = entry.Workspace.CurrentSolution;
            var projects = solution.Projects.Select(p => new
            {
                name = p.Name,
                language = p.Language,
                documentCount = p.Documents.Count(),
                hasCompilationErrors = false // Will be implemented later
            }).ToList();
            
            var fsharpProjects = _fsharpTracker.GetAllProjects(workspaceId);
            
            statuses.Add(new
            {
                id = workspaceId,
                path = entry.Path,
                loadedAt = entry.LoadedAt,
                lastAccessTime = entry.LastAccessTime,
                projectCount = projects.Count,
                projects,
                fsharpProjects = fsharpProjects.Select(fp => new
                {
                    path = fp.ProjectPath,
                    name = fp.ProjectName,
                    isLoaded = fp.IsLoaded,
                    loadError = fp.LoadError
                }).ToList()
            });
        }
        
        return new WorkspaceStatus
        {
            IsLoaded = _workspaces.Count > 0,
            WorkspaceCount = _workspaces.Count,
            Workspaces = statuses
        };
    }
    
    public IEnumerable<Workspace> GetLoadedWorkspaces()
    {
        // Update access time for all workspaces when accessed
        foreach (var entry in _workspaces.Values)
        {
            entry.Touch();
        }
        return _workspaces.Values.Select(e => e.Workspace);
    }
    
    public Workspace? GetWorkspace(string workspaceId)
    {
        if (_workspaces.TryGetValue(workspaceId, out var entry))
        {
            entry.Touch(); // Update access time
            return entry.Workspace;
        }
        return null;
    }
    
    public WorkspaceEntry? GetWorkspaceEntry(string workspaceId)
    {
        if (_workspaces.TryGetValue(workspaceId, out var entry))
        {
            entry.Touch();
            return entry;
        }
        return null;
    }
    
    public async Task<List<ClassSearchResult>> FindClassesAsync(string pattern, string? workspacePath = null)
    {
        var results = new List<ClassSearchResult>();
        
        // Convert wildcard pattern to regex (support * and ?)
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".")
            + "$";
        var regex = new Regex(regexPattern, RegexOptions.IgnoreCase);
        
        // Get workspaces to search
        var workspacesToSearch = new List<Workspace>();
        if (!string.IsNullOrEmpty(workspacePath))
        {
            var workspace = GetWorkspace(workspacePath);
            if (workspace != null)
                workspacesToSearch.Add(workspace);
        }
        else
        {
            workspacesToSearch.AddRange(_workspaces.Values.Select(entry => entry.Workspace));
        }
        
        // Search each workspace
        foreach (var workspace in workspacesToSearch)
        {
            var solution = workspace.CurrentSolution;
            
            // Search each project
            foreach (var project in solution.Projects)
            {
                try
                {
                    // Find all type declarations in the project
                    // First get all type symbols in the project
                    var compilation = await project.GetCompilationAsync();
                    if (compilation == null) continue;
                    
                    var allSymbols = new List<ISymbol>();
                    
                    // Use visitor pattern to find all type symbols
                    foreach (var tree in compilation.SyntaxTrees)
                    {
                        var semanticModel = compilation.GetSemanticModel(tree);
                        var root = await tree.GetRootAsync();
                        
                        // Determine language from the tree
                        var document = project.Documents.FirstOrDefault(d => d.FilePath == tree.FilePath);
                        if (document == null) continue;
                        
                        var language = LanguageDetector.GetLanguageFromDocument(document);
                        var handler = LanguageHandlerFactory.GetHandler(language);
                        
                        if (handler == null)
                        {
                            _logger.LogWarning("No language handler found for {Language} in file {FilePath}", language, tree.FilePath);
                            continue;
                        }
                        
                        // Find type declarations using language handler
                        var typeDeclarations = root.DescendantNodes()
                            .Where(node => handler.IsTypeDeclaration(node));
                        
                        foreach (var typeDecl in typeDeclarations)
                        {
                            var symbol = semanticModel.GetDeclaredSymbol(typeDecl);
                            if (symbol != null && regex.IsMatch(symbol.Name))
                            {
                                allSymbols.Add(symbol);
                            }
                        }
                    }
                    
                    var symbols = allSymbols;
                    
                    foreach (var symbol in symbols)
                    {
                        if (symbol is INamedTypeSymbol namedType && 
                            (namedType.TypeKind == TypeKind.Class || 
                             namedType.TypeKind == TypeKind.Interface || 
                             namedType.TypeKind == TypeKind.Struct ||
                             namedType.TypeKind == TypeKind.Enum))
                        {
                            var location = namedType.Locations.FirstOrDefault();
                            if (location != null && location.IsInSource)
                            {
                                var lineSpan = location.GetLineSpan();
                                results.Add(new ClassSearchResult
                                {
                                    Name = namedType.Name,
                                    FullyQualifiedName = namedType.ToDisplayString(),
                                    TypeKind = namedType.TypeKind.ToString(),
                                    FilePath = lineSpan.Path,
                                    Line = lineSpan.StartLinePosition.Line + 1,
                                    Column = lineSpan.StartLinePosition.Character + 1,
                                    ProjectName = project.Name,
                                    Namespace = namedType.ContainingNamespace?.ToDisplayString()
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to search in project {ProjectName}", project.Name);
                }
            }
        }
        
        return results.OrderBy(r => r.FullyQualifiedName).ToList();
    }
    
    public async Task<List<MemberSearchResult>> FindMethodsAsync(string methodPattern, string? classPattern = null, string? workspacePath = null)
    {
        var results = new List<MemberSearchResult>();
        
        // Convert wildcard patterns to regex
        var methodRegex = ConvertWildcardToRegex(methodPattern);
        var classRegex = !string.IsNullOrEmpty(classPattern) ? ConvertWildcardToRegex(classPattern) : null;
        
        // Get workspaces to search
        var workspacesToSearch = GetWorkspacesToSearch(workspacePath);
        
        // Search each workspace
        foreach (var workspace in workspacesToSearch)
        {
            var solution = workspace.CurrentSolution;
            
            foreach (var project in solution.Projects)
            {
                try
                {
                    var compilation = await project.GetCompilationAsync();
                    if (compilation == null) continue;
                    
                    foreach (var tree in compilation.SyntaxTrees)
                    {
                        var semanticModel = compilation.GetSemanticModel(tree);
                        var root = await tree.GetRootAsync();
                        
                        // Determine language from the tree
                        var document = project.Documents.FirstOrDefault(d => d.FilePath == tree.FilePath);
                        if (document == null) continue;
                        
                        var language = LanguageDetector.GetLanguageFromDocument(document);
                        var handler = LanguageHandlerFactory.GetHandler(language);
                        
                        if (handler == null)
                        {
                            _logger.LogWarning("No language handler found for {Language} in file {FilePath}", language, tree.FilePath);
                            continue;
                        }
                        
                        // Find all method declarations using language handler
                        var methodDeclarations = root.DescendantNodes()
                            .Where(node => handler.IsMethodDeclaration(node));
                        
                        foreach (var methodDecl in methodDeclarations)
                        {
                            var methodSymbol = semanticModel.GetDeclaredSymbol(methodDecl) as IMethodSymbol;
                            if (methodSymbol == null) continue;
                            
                            // Check if method name matches pattern
                            if (!methodRegex.IsMatch(methodSymbol.Name)) continue;
                            
                            // Check if class name matches pattern (if specified)
                            if (classRegex != null && !classRegex.IsMatch(methodSymbol.ContainingType.Name)) continue;
                            
                            var location = methodSymbol.Locations.FirstOrDefault();
                            if (location != null && location.IsInSource)
                            {
                                var lineSpan = location.GetLineSpan();
                                results.Add(new MemberSearchResult
                                {
                                    MemberName = methodSymbol.Name,
                                    MemberType = "Method",
                                    ClassName = methodSymbol.ContainingType.Name,
                                    FullyQualifiedClassName = methodSymbol.ContainingType.ToDisplayString(),
                                    ReturnType = methodSymbol.ReturnType.ToDisplayString(),
                                    Parameters = GetMethodParameters(methodSymbol),
                                    FilePath = lineSpan.Path,
                                    Line = lineSpan.StartLinePosition.Line + 1,
                                    Column = lineSpan.StartLinePosition.Character + 1,
                                    ProjectName = project.Name,
                                    IsStatic = methodSymbol.IsStatic,
                                    IsAsync = methodSymbol.IsAsync,
                                    AccessModifier = GetAccessModifier(methodSymbol)
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to search methods in project {ProjectName}", project.Name);
                }
            }
        }
        
        return results.OrderBy(r => r.FullyQualifiedClassName).ThenBy(r => r.MemberName).ToList();
    }
    
    public async Task<List<MemberSearchResult>> FindPropertiesAsync(string propertyPattern, string? classPattern = null, string? workspacePath = null)
    {
        var results = new List<MemberSearchResult>();
        
        // Convert wildcard patterns to regex
        var propertyRegex = ConvertWildcardToRegex(propertyPattern);
        var classRegex = !string.IsNullOrEmpty(classPattern) ? ConvertWildcardToRegex(classPattern) : null;
        
        // Get workspaces to search
        var workspacesToSearch = GetWorkspacesToSearch(workspacePath);
        
        // Search each workspace
        foreach (var workspace in workspacesToSearch)
        {
            var solution = workspace.CurrentSolution;
            
            foreach (var project in solution.Projects)
            {
                try
                {
                    var compilation = await project.GetCompilationAsync();
                    if (compilation == null) continue;
                    
                    foreach (var tree in compilation.SyntaxTrees)
                    {
                        var semanticModel = compilation.GetSemanticModel(tree);
                        var root = await tree.GetRootAsync();
                        
                        // Determine language from the tree
                        var document = project.Documents.FirstOrDefault(d => d.FilePath == tree.FilePath);
                        if (document == null) continue;
                        
                        var language = LanguageDetector.GetLanguageFromDocument(document);
                        var handler = LanguageHandlerFactory.GetHandler(language);
                        
                        if (handler == null)
                        {
                            _logger.LogWarning("No language handler found for {Language} in file {FilePath}", language, tree.FilePath);
                            continue;
                        }
                        
                        // Find all property declarations using language handler
                        var propertyDeclarations = root.DescendantNodes()
                            .Where(node => handler.IsPropertyDeclaration(node));
                        
                        foreach (var propDecl in propertyDeclarations)
                        {
                            var propSymbol = semanticModel.GetDeclaredSymbol(propDecl) as IPropertySymbol;
                            if (propSymbol == null) continue;
                            
                            // Check if property name matches pattern
                            if (!propertyRegex.IsMatch(propSymbol.Name)) continue;
                            
                            // Check if class name matches pattern (if specified)
                            if (classRegex != null && !classRegex.IsMatch(propSymbol.ContainingType.Name)) continue;
                            
                            var location = propSymbol.Locations.FirstOrDefault();
                            if (location != null && location.IsInSource)
                            {
                                var lineSpan = location.GetLineSpan();
                                results.Add(new MemberSearchResult
                                {
                                    MemberName = propSymbol.Name,
                                    MemberType = "Property",
                                    ClassName = propSymbol.ContainingType.Name,
                                    FullyQualifiedClassName = propSymbol.ContainingType.ToDisplayString(),
                                    ReturnType = propSymbol.Type.ToDisplayString(),
                                    FilePath = lineSpan.Path,
                                    Line = lineSpan.StartLinePosition.Line + 1,
                                    Column = lineSpan.StartLinePosition.Character + 1,
                                    ProjectName = project.Name,
                                    IsStatic = propSymbol.IsStatic,
                                    AccessModifier = GetAccessModifier(propSymbol),
                                    HasGetter = propSymbol.GetMethod != null,
                                    HasSetter = propSymbol.SetMethod != null
                                });
                            }
                        }
                        
                        // Also find fields
                        var fieldDeclarations = root.DescendantNodes()
                            .Where(node => handler.IsFieldDeclaration(node));
                        
                        foreach (var fieldDecl in fieldDeclarations)
                        {
                            // For C#, handle FieldDeclarationSyntax
                            if (fieldDecl is CS.FieldDeclarationSyntax csField)
                            {
                                foreach (var variable in csField.Declaration.Variables)
                            {
                                var fieldSymbol = semanticModel.GetDeclaredSymbol(variable) as IFieldSymbol;
                                if (fieldSymbol == null) continue;
                                
                                // Check if field name matches pattern
                                if (!propertyRegex.IsMatch(fieldSymbol.Name)) continue;
                                
                                // Check if class name matches pattern (if specified)
                                if (classRegex != null && !classRegex.IsMatch(fieldSymbol.ContainingType.Name)) continue;
                                
                                var location = fieldSymbol.Locations.FirstOrDefault();
                                if (location != null && location.IsInSource)
                                {
                                    var lineSpan = location.GetLineSpan();
                                    results.Add(new MemberSearchResult
                                    {
                                        MemberName = fieldSymbol.Name,
                                        MemberType = "Field",
                                        ClassName = fieldSymbol.ContainingType.Name,
                                        FullyQualifiedClassName = fieldSymbol.ContainingType.ToDisplayString(),
                                        ReturnType = fieldSymbol.Type.ToDisplayString(),
                                        FilePath = lineSpan.Path,
                                        Line = lineSpan.StartLinePosition.Line + 1,
                                        Column = lineSpan.StartLinePosition.Character + 1,
                                        ProjectName = project.Name,
                                        IsStatic = fieldSymbol.IsStatic,
                                        AccessModifier = GetAccessModifier(fieldSymbol),
                                        IsReadOnly = fieldSymbol.IsReadOnly
                                    });
                                }
                            }
                            }
                            // For VB.NET, handle FieldDeclarationSyntax
                            else if (fieldDecl is VB.FieldDeclarationSyntax vbField)
                            {
                                foreach (var declarator in vbField.Declarators)
                                {
                                    foreach (var name in declarator.Names)
                                    {
                                        var fieldSymbol = semanticModel.GetDeclaredSymbol(name) as IFieldSymbol;
                                        if (fieldSymbol == null) continue;
                                        
                                        // Check if field name matches pattern
                                        if (!propertyRegex.IsMatch(fieldSymbol.Name)) continue;
                                        
                                        // Check if class name matches pattern (if specified)
                                        if (classRegex != null && !classRegex.IsMatch(fieldSymbol.ContainingType.Name)) continue;
                                        
                                        var location = fieldSymbol.Locations.FirstOrDefault();
                                        if (location != null && location.IsInSource)
                                        {
                                            var lineSpan = location.GetLineSpan();
                                            results.Add(new MemberSearchResult
                                            {
                                                MemberName = fieldSymbol.Name,
                                                MemberType = "Field",
                                                ClassName = fieldSymbol.ContainingType.Name,
                                                FullyQualifiedClassName = fieldSymbol.ContainingType.ToDisplayString(),
                                                ReturnType = fieldSymbol.Type.ToDisplayString(),
                                                FilePath = lineSpan.Path,
                                                Line = lineSpan.StartLinePosition.Line + 1,
                                                Column = lineSpan.StartLinePosition.Character + 1,
                                                ProjectName = project.Name,
                                                IsStatic = fieldSymbol.IsStatic,
                                                AccessModifier = GetAccessModifier(fieldSymbol),
                                                IsReadOnly = fieldSymbol.IsReadOnly
                                            });
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to search properties in project {ProjectName}", project.Name);
                }
            }
        }
        
        return results.OrderBy(r => r.FullyQualifiedClassName).ThenBy(r => r.MemberName).ToList();
    }
    
    private Regex ConvertWildcardToRegex(string pattern)
    {
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".")
            + "$";
        return new Regex(regexPattern, RegexOptions.IgnoreCase);
    }
    
    private bool IsVariableDeclarator(SyntaxNode node, string language)
    {
        return language switch
        {
            LanguageNames.CSharp => node is CS.VariableDeclaratorSyntax,
            LanguageNames.VisualBasic => node is VB.ModifiedIdentifierSyntax,
            _ => false
        };
    }
    
    private List<Workspace> GetWorkspacesToSearch(string? workspaceId)
    {
        var workspacesToSearch = new List<Workspace>();
        if (!string.IsNullOrEmpty(workspaceId))
        {
            var workspace = GetWorkspace(workspaceId);
            if (workspace != null)
                workspacesToSearch.Add(workspace);
        }
        else
        {
            workspacesToSearch.AddRange(_workspaces.Values.Select(e => e.Workspace));
        }
        return workspacesToSearch;
    }
    
    private string GetMethodParameters(IMethodSymbol method)
    {
        var parameters = method.Parameters.Select(p => $"{p.Type.ToDisplayString()} {p.Name}");
        return $"({string.Join(", ", parameters)})";
    }
    
    private string GetAccessModifier(ISymbol symbol)
    {
        switch (symbol.DeclaredAccessibility)
        {
            case Accessibility.Public: return "public";
            case Accessibility.Private: return "private";
            case Accessibility.Protected: return "protected";
            case Accessibility.Internal: return "internal";
            case Accessibility.ProtectedOrInternal: return "protected internal";
            case Accessibility.ProtectedAndInternal: return "private protected";
            default: return "unknown";
        }
    }
    
    public async Task<MethodCallAnalysis> FindMethodCallsAsync(string methodName, string className, string? workspacePath = null)
    {
        var result = new MethodCallAnalysis
        {
            TargetMethod = $"{className}.{methodName}",
            DirectCalls = new List<MethodCallInfo>(),
            CallTree = new Dictionary<string, List<MethodCallInfo>>()
        };
        
        var workspacesToSearch = GetWorkspacesToSearch(workspacePath);
        
        foreach (var workspace in workspacesToSearch)
        {
            var solution = workspace.CurrentSolution;
            
            // First, find the target method
            IMethodSymbol? targetMethod = null;
            SyntaxNode? targetMethodBody = null;
            
            foreach (var project in solution.Projects)
            {
                if (targetMethod != null) break;
                
                var compilation = await project.GetCompilationAsync();
                if (compilation == null) continue;
                
                var classSymbol = compilation.GetTypeByMetadataName($"{project.DefaultNamespace}.{className}") 
                                ?? compilation.Assembly.GetTypeByMetadataName(className);
                
                if (classSymbol == null)
                {
                    // Try to find in all namespaces
                    foreach (var type in compilation.Assembly.GetAllTypes())
                    {
                        if (type.Name == className)
                        {
                            classSymbol = type;
                            break;
                        }
                    }
                }
                
                if (classSymbol != null)
                {
                    targetMethod = classSymbol.GetMembers(methodName).OfType<IMethodSymbol>().FirstOrDefault();
                    
                    if (targetMethod != null)
                    {
                        // Get the method body
                        var location = targetMethod.Locations.FirstOrDefault();
                        if (location != null && location.IsInSource)
                        {
                            var tree = location.SourceTree;
                            var root = await tree.GetRootAsync();
                            targetMethodBody = root.FindNode(location.SourceSpan);
                        }
                        break;
                    }
                }
            }
            
            if (targetMethod == null || targetMethodBody == null)
            {
                result.Error = $"Method {className}.{methodName} not found";
                return result;
            }
            
            // Analyze the method body for calls
            var targetProject = solution.Projects.FirstOrDefault(p => p.Documents.Any(d => d.FilePath == targetMethod.Locations.First().SourceTree?.FilePath));
            if (targetProject != null)
            {
                var compilation = await targetProject.GetCompilationAsync();
                var semanticModel = compilation?.GetSemanticModel(targetMethod.Locations.First().SourceTree);
                
                if (semanticModel != null)
                {
                    // Find direct calls
                    await FindDirectCallsAsync(targetMethodBody, semanticModel, result.DirectCalls, targetProject.Name);
                    
                    // Build call tree
                    var visited = new HashSet<string>();
                    await BuildCallTreeAsync(targetMethod, solution, result.CallTree, visited, 0, 5);
                }
            }
        }
        
        return result;
    }
    
    private async Task FindDirectCallsAsync(SyntaxNode methodBody, SemanticModel semanticModel, List<MethodCallInfo> calls, string projectName)
    {
        var invocations = methodBody.DescendantNodes().OfType<CS.InvocationExpressionSyntax>();
        
        foreach (var invocation in invocations)
        {
            var symbolInfo = semanticModel.GetSymbolInfo(invocation);
            if (symbolInfo.Symbol is IMethodSymbol calledMethod)
            {
                var location = invocation.GetLocation();
                var lineSpan = location.GetLineSpan();
                
                calls.Add(new MethodCallInfo
                {
                    MethodSignature = GetFullMethodSignature(calledMethod),
                    ClassName = calledMethod.ContainingType.ToDisplayString(),
                    MethodName = calledMethod.Name,
                    FilePath = lineSpan.Path,
                    Line = lineSpan.StartLinePosition.Line + 1,
                    Column = lineSpan.StartLinePosition.Character + 1,
                    ProjectName = projectName,
                    IsExternal = calledMethod.ContainingAssembly.Name != semanticModel.Compilation.AssemblyName
                });
            }
        }
    }
    
    private async Task BuildCallTreeAsync(IMethodSymbol method, Solution solution, Dictionary<string, List<MethodCallInfo>> callTree, HashSet<string> visited, int depth, int maxDepth)
    {
        if (depth >= maxDepth) return;
        
        var methodKey = GetFullMethodSignature(method);
        if (visited.Contains(methodKey)) return;
        visited.Add(methodKey);
        
        var calls = new List<MethodCallInfo>();
        
        var location = method.Locations.FirstOrDefault();
        if (location != null && location.IsInSource)
        {
            var document = solution.GetDocument(location.SourceTree);
            if (document != null)
            {
                var semanticModel = await document.GetSemanticModelAsync();
                var root = await location.SourceTree.GetRootAsync();
                var methodNode = root.FindNode(location.SourceSpan);
                
                if (semanticModel != null)
                {
                    await FindDirectCallsAsync(methodNode, semanticModel, calls, document.Project.Name);
                }
            }
        }
        
        if (calls.Any())
        {
            callTree[methodKey] = calls;
            
            // Recursively analyze called methods
            foreach (var call in calls.Where(c => !c.IsExternal))
            {
                // Find the called method symbol
                foreach (var project in solution.Projects)
                {
                    var compilation = await project.GetCompilationAsync();
                    if (compilation != null)
                    {
                        var type = compilation.GetTypeByMetadataName(call.ClassName);
                        if (type != null)
                        {
                            var calledMethod = type.GetMembers(call.MethodName).OfType<IMethodSymbol>().FirstOrDefault();
                            if (calledMethod != null)
                            {
                                await BuildCallTreeAsync(calledMethod, solution, callTree, visited, depth + 1, maxDepth);
                            }
                        }
                    }
                }
            }
        }
    }
    
    public async Task<MethodCallerAnalysis> FindMethodCallersAsync(string methodName, string className, string? workspacePath = null)
    {
        var result = new MethodCallerAnalysis
        {
            TargetMethod = $"{className}.{methodName}",
            DirectCallers = new List<MethodCallInfo>(),
            CallerTree = new Dictionary<string, List<MethodCallInfo>>()
        };
        
        var workspacesToSearch = GetWorkspacesToSearch(workspacePath);
        
        foreach (var workspace in workspacesToSearch)
        {
            var solution = workspace.CurrentSolution;
            
            // Find the target method symbol
            IMethodSymbol? targetMethod = null;
            
            foreach (var project in solution.Projects)
            {
                if (targetMethod != null) break;
                
                var compilation = await project.GetCompilationAsync();
                if (compilation == null) continue;
                
                // Try to find the class
                INamedTypeSymbol? classSymbol = null;
                foreach (var type in compilation.Assembly.GetAllTypes())
                {
                    if (type.Name == className || type.ToDisplayString() == className)
                    {
                        classSymbol = type;
                        break;
                    }
                }
                
                if (classSymbol != null)
                {
                    targetMethod = classSymbol.GetMembers(methodName).OfType<IMethodSymbol>().FirstOrDefault();
                    if (targetMethod != null) break;
                }
            }
            
            if (targetMethod == null)
            {
                result.Error = $"Method {className}.{methodName} not found";
                return result;
            }
            
            // Find all references to this method
            var references = await SymbolFinder.FindReferencesAsync(targetMethod, solution);
            
            foreach (var reference in references)
            {
                foreach (var location in reference.Locations)
                {
                    var document = solution.GetDocument(location.Document.Id);
                    if (document != null)
                    {
                        var semanticModel = await document.GetSemanticModelAsync();
                        var root = await document.GetSyntaxRootAsync();
                        
                        if (semanticModel != null && root != null)
                        {
                            var node = root.FindNode(location.Location.SourceSpan);
                            
                            // Find the containing method
                            var containingMethod = node.AncestorsAndSelf()
                                .OfType<CS.MethodDeclarationSyntax>()
                                .FirstOrDefault();
                            
                            if (containingMethod != null)
                            {
                                var methodSymbol = semanticModel.GetDeclaredSymbol(containingMethod);
                                if (methodSymbol != null)
                                {
                                    var lineSpan = location.Location.GetLineSpan();
                                    
                                    result.DirectCallers.Add(new MethodCallInfo
                                    {
                                        MethodSignature = GetFullMethodSignature(methodSymbol),
                                        ClassName = methodSymbol.ContainingType.ToDisplayString(),
                                        MethodName = methodSymbol.Name,
                                        FilePath = lineSpan.Path,
                                        Line = lineSpan.StartLinePosition.Line + 1,
                                        Column = lineSpan.StartLinePosition.Character + 1,
                                        ProjectName = document.Project.Name,
                                        IsExternal = false
                                    });
                                }
                            }
                        }
                    }
                }
            }
            
            // Build caller tree
            var visited = new HashSet<string>();
            await BuildCallerTreeAsync(targetMethod, solution, result.CallerTree, visited, 0, 5);
        }
        
        // Remove duplicates
        result.DirectCallers = result.DirectCallers
            .GroupBy(c => c.MethodSignature)
            .Select(g => g.First())
            .ToList();
        
        return result;
    }
    
    private async Task BuildCallerTreeAsync(IMethodSymbol method, Solution solution, Dictionary<string, List<MethodCallInfo>> callerTree, HashSet<string> visited, int depth, int maxDepth)
    {
        if (depth >= maxDepth) return;
        
        var methodKey = GetFullMethodSignature(method);
        if (visited.Contains(methodKey)) return;
        visited.Add(methodKey);
        
        var callers = new List<MethodCallInfo>();
        
        // Find references to this method
        var references = await SymbolFinder.FindReferencesAsync(method, solution);
        
        foreach (var reference in references)
        {
            foreach (var location in reference.Locations)
            {
                var document = solution.GetDocument(location.Document.Id);
                if (document != null)
                {
                    var semanticModel = await document.GetSemanticModelAsync();
                    var root = await document.GetSyntaxRootAsync();
                    
                    if (semanticModel != null && root != null)
                    {
                        var node = root.FindNode(location.Location.SourceSpan);
                        var containingMethod = node.AncestorsAndSelf()
                            .OfType<CS.MethodDeclarationSyntax>()
                            .FirstOrDefault();
                        
                        if (containingMethod != null)
                        {
                            var callerSymbol = semanticModel.GetDeclaredSymbol(containingMethod);
                            if (callerSymbol != null)
                            {
                                var lineSpan = location.Location.GetLineSpan();
                                
                                var callInfo = new MethodCallInfo
                                {
                                    MethodSignature = GetFullMethodSignature(callerSymbol),
                                    ClassName = callerSymbol.ContainingType.ToDisplayString(),
                                    MethodName = callerSymbol.Name,
                                    FilePath = lineSpan.Path,
                                    Line = lineSpan.StartLinePosition.Line + 1,
                                    Column = lineSpan.StartLinePosition.Character + 1,
                                    ProjectName = document.Project.Name,
                                    IsExternal = false
                                };
                                
                                if (!callers.Any(c => c.MethodSignature == callInfo.MethodSignature))
                                {
                                    callers.Add(callInfo);
                                }
                            }
                        }
                    }
                }
            }
        }
        
        if (callers.Any())
        {
            callerTree[methodKey] = callers;
            
            // Recursively find callers of the callers
            foreach (var caller in callers)
            {
                // Find the caller method symbol
                foreach (var project in solution.Projects)
                {
                    var compilation = await project.GetCompilationAsync();
                    if (compilation != null)
                    {
                        var type = compilation.GetTypeByMetadataName(caller.ClassName);
                        if (type != null)
                        {
                            var callerMethod = type.GetMembers(caller.MethodName).OfType<IMethodSymbol>().FirstOrDefault();
                            if (callerMethod != null)
                            {
                                await BuildCallerTreeAsync(callerMethod, solution, callerTree, visited, depth + 1, maxDepth);
                            }
                        }
                    }
                }
            }
        }
    }
    
    private string GetFullMethodSignature(IMethodSymbol method)
    {
        var parameters = string.Join(", ", method.Parameters.Select(p => $"{p.Type.ToDisplayString()} {p.Name}"));
        return $"{method.ContainingType.ToDisplayString()}.{method.Name}({parameters})";
    }
    
    public async Task<List<ReferenceInfo>> FindReferencesAsync(string symbolName, string symbolType, string? containerName = null, string? workspacePath = null)
    {
        var results = new List<ReferenceInfo>();
        var workspacesToSearch = GetWorkspacesToSearch(workspacePath);
        
        foreach (var workspace in workspacesToSearch)
        {
            var solution = workspace.CurrentSolution;
            
            // Find the symbol
            ISymbol? targetSymbol = null;
            
            foreach (var project in solution.Projects)
            {
                if (targetSymbol != null) break;
                
                var compilation = await project.GetCompilationAsync();
                if (compilation == null) continue;
                
                targetSymbol = await FindSymbolAsync(compilation, symbolName, symbolType, containerName);
                if (targetSymbol != null) break;
            }
            
            if (targetSymbol == null) continue;
            
            // Find all references
            var references = await SymbolFinder.FindReferencesAsync(targetSymbol, solution);
            
            foreach (var reference in references)
            {
                foreach (var location in reference.Locations)
                {
                    var document = solution.GetDocument(location.Document.Id);
                    if (document != null)
                    {
                        var lineSpan = location.Location.GetLineSpan();
                        var sourceText = await document.GetTextAsync();
                        var line = sourceText.Lines[lineSpan.StartLinePosition.Line];
                        
                        results.Add(new ReferenceInfo
                        {
                            FilePath = lineSpan.Path,
                            Line = lineSpan.StartLinePosition.Line + 1,
                            Column = lineSpan.StartLinePosition.Character + 1,
                            Context = line.ToString().Trim(),
                            ProjectName = document.Project.Name
                        });
                    }
                }
            }
        }
        
        return results;
    }
    
    public async Task<CodeEditResult> EditCodeAsync(string filePath, string operation, string className, string? methodName, string? code, JsonElement? parameters, bool preview)
    {
        var result = new CodeEditResult { Success = true };
        
        try
        {
            // Find the workspace containing this file
            Workspace? targetWorkspace = null;
            foreach (var ws in _workspaces.Values)
            {
                var doc = ws.Workspace.CurrentSolution.Projects
                    .SelectMany(p => p.Documents)
                    .FirstOrDefault(d => d.FilePath == filePath);
                if (doc != null)
                {
                    targetWorkspace = ws.Workspace;
                    break;
                }
            }
            
            if (targetWorkspace == null)
            {
                result.Error = $"File '{filePath}' not found in any loaded workspace";
                result.Success = false;
                return result;
            }
            
            var document = targetWorkspace.CurrentSolution.Projects
                .SelectMany(p => p.Documents)
                .First(d => d.FilePath == filePath);
            
            var root = await document.GetSyntaxRootAsync();
            if (root == null)
            {
                result.Error = "Failed to parse file";
                result.Success = false;
                return result;
            }
            
            // Perform the operation
            SyntaxNode? modifiedRoot = null;
            
            switch (operation.ToLower())
            {
                case "add-method":
                    (modifiedRoot, result) = await AddMethodToClass(root, className, code ?? "", document);
                    break;
                    
                case "add-property":
                    (modifiedRoot, result) = await AddPropertyToClass(root, className, code ?? "", document);
                    break;
                    
                case "make-async":
                    (modifiedRoot, result) = await MakeMethodAsync(root, className, methodName ?? "", document);
                    break;
                    
                default:
                    result.Error = $"Unknown operation: {operation}";
                    result.Success = false;
                    return result;
            }
            
            if (!result.Success || modifiedRoot == null)
            {
                return result;
            }
            
            // Format the code
            modifiedRoot = Formatter.Format(modifiedRoot, targetWorkspace);
            
            if (preview)
            {
                result.ModifiedCode = modifiedRoot.ToFullString();
            }
            else
            {
                // Apply the changes
                var newDocument = document.WithSyntaxRoot(modifiedRoot);
                var newSolution = newDocument.Project.Solution;
                
                if (!targetWorkspace.TryApplyChanges(newSolution))
                {
                    result.Error = "Failed to apply changes to workspace";
                    result.Success = false;
                    return result;
                }
                
                // Write to file
                var modifiedText = modifiedRoot.ToFullString();
                await File.WriteAllTextAsync(filePath, modifiedText);
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
        }
        
        return result;
    }
    
    private async Task<(SyntaxNode? modifiedRoot, CodeEditResult result)> AddMethodToClass(
        SyntaxNode root, string className, string methodCode, Document document)
    {
        var result = new CodeEditResult { Success = true };
        
        // Find the class
        var classDeclaration = root.DescendantNodes()
            .OfType<CS.ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.Text == className);
        
        if (classDeclaration == null)
        {
            result.Error = $"Class '{className}' not found";
            result.Success = false;
            return (null, result);
        }
        
        // Parse the method code
        var parsedMethod = SyntaxFactory.ParseCompilationUnit(methodCode)
            .DescendantNodes()
            .OfType<CS.MethodDeclarationSyntax>()
            .FirstOrDefault();
        
        if (parsedMethod == null)
        {
            // Try parsing as a member declaration
            var member = SyntaxFactory.ParseMemberDeclaration(methodCode);
            if (member is CS.MethodDeclarationSyntax method)
            {
                parsedMethod = method;
            }
            else
            {
                result.Error = "Invalid method syntax";
                result.Success = false;
                return (null, result);
            }
        }
        
        // Add the method to the class
        var newClass = classDeclaration.AddMembers(parsedMethod);
        var modifiedRoot = root.ReplaceNode(classDeclaration, newClass);
        
        result.Description = $"Added method '{parsedMethod.Identifier.Text}' to class '{className}'";
        
        return (modifiedRoot, result);
    }
    
    private async Task<(SyntaxNode? modifiedRoot, CodeEditResult result)> AddPropertyToClass(
        SyntaxNode root, string className, string propertyCode, Document document)
    {
        var result = new CodeEditResult { Success = true };
        
        // Find the class
        var classDeclaration = root.DescendantNodes()
            .OfType<CS.ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.Text == className);
        
        if (classDeclaration == null)
        {
            result.Error = $"Class '{className}' not found";
            result.Success = false;
            return (null, result);
        }
        
        // Parse the property code
        var parsedProperty = SyntaxFactory.ParseMemberDeclaration(propertyCode) as CS.PropertyDeclarationSyntax;
        
        if (parsedProperty == null)
        {
            result.Error = "Invalid property syntax";
            result.Success = false;
            return (null, result);
        }
        
        // Add the property to the class
        var newClass = classDeclaration.AddMembers(parsedProperty);
        var modifiedRoot = root.ReplaceNode(classDeclaration, newClass);
        
        result.Description = $"Added property '{parsedProperty.Identifier.Text}' to class '{className}'";
        
        return (modifiedRoot, result);
    }
    
    private async Task<(SyntaxNode? modifiedRoot, CodeEditResult result)> MakeMethodAsync(
        SyntaxNode root, string className, string methodName, Document document)
    {
        var result = new CodeEditResult { Success = true };
        
        // Find the class
        var classDeclaration = root.DescendantNodes()
            .OfType<CS.ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.Text == className);
        
        if (classDeclaration == null)
        {
            result.Error = $"Class '{className}' not found";
            result.Success = false;
            return (null, result);
        }
        
        // Find the method
        var methodDeclaration = classDeclaration.DescendantNodes()
            .OfType<CS.MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.Text == methodName);
        
        if (methodDeclaration == null)
        {
            result.Error = $"Method '{methodName}' not found in class '{className}'";
            result.Success = false;
            return (null, result);
        }
        
        // Check if already async
        if (methodDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword)))
        {
            result.Error = $"Method '{methodName}' is already async";
            result.Success = false;
            return (null, result);
        }
        
        // Add async modifier
        var newMethod = methodDeclaration.AddModifiers(SyntaxFactory.Token(SyntaxKind.AsyncKeyword));
        
        // Change return type if needed
        var returnType = methodDeclaration.ReturnType;
        if (returnType is CS.PredefinedTypeSyntax predefined && predefined.Keyword.IsKind(SyntaxKind.VoidKeyword))
        {
            // void -> Task
            newMethod = newMethod.WithReturnType(SyntaxFactory.ParseTypeName("Task"));
        }
        else if (returnType is not CS.GenericNameSyntax generic || generic.Identifier.Text != "Task")
        {
            // T -> Task<T>
            newMethod = newMethod.WithReturnType(
                SyntaxFactory.GenericName("Task")
                    .WithTypeArgumentList(
                        SyntaxFactory.TypeArgumentList(
                            SyntaxFactory.SingletonSeparatedList<CS.TypeSyntax>(returnType))));
        }
        
        var modifiedRoot = root.ReplaceNode(methodDeclaration, newMethod);
        
        result.Description = $"Made method '{methodName}' async in class '{className}'";
        
        return (modifiedRoot, result);
    }
    
    private async Task<ISymbol?> FindSymbolAsync(Compilation compilation, string symbolName, string symbolType, string? containerName)
    {
        switch (symbolType.ToLower())
        {
            case "type":
                // Search for type
                foreach (var type in compilation.Assembly.GetAllTypes())
                {
                    if (type.Name == symbolName)
                        return type;
                }
                break;
                
            case "method":
                // Search for method
                if (!string.IsNullOrEmpty(containerName))
                {
                    var containingType = compilation.Assembly.GetAllTypes()
                        .FirstOrDefault(t => t.Name == containerName);
                    
                    if (containingType != null)
                    {
                        return containingType.GetMembers(symbolName).OfType<IMethodSymbol>().FirstOrDefault();
                    }
                }
                break;
                
            case "property":
                // Search for property
                if (!string.IsNullOrEmpty(containerName))
                {
                    var containingType = compilation.Assembly.GetAllTypes()
                        .FirstOrDefault(t => t.Name == containerName);
                    
                    if (containingType != null)
                    {
                        return containingType.GetMembers(symbolName).OfType<IPropertySymbol>().FirstOrDefault();
                    }
                }
                break;
                
            case "field":
                // Search for field
                if (!string.IsNullOrEmpty(containerName))
                {
                    var containingType = compilation.Assembly.GetAllTypes()
                        .FirstOrDefault(t => t.Name == containerName);
                    
                    if (containingType != null)
                    {
                        return containingType.GetMembers(symbolName).OfType<IFieldSymbol>().FirstOrDefault();
                    }
                }
                break;
        }
        
        return null;
    }
    
    public async Task<List<ImplementationInfo>> FindImplementationsAsync(string interfaceName, string? workspacePath = null)
    {
        var results = new List<ImplementationInfo>();
        var workspacesToSearch = GetWorkspacesToSearch(workspacePath);
        
        foreach (var workspace in workspacesToSearch)
        {
            var solution = workspace.CurrentSolution;
            
            // Find the interface symbol
            INamedTypeSymbol? interfaceSymbol = null;
            
            foreach (var project in solution.Projects)
            {
                if (interfaceSymbol != null) break;
                
                var compilation = await project.GetCompilationAsync();
                if (compilation == null) continue;
                
                interfaceSymbol = compilation.Assembly.GetAllTypes()
                    .FirstOrDefault(t => t.Name == interfaceName && 
                                        (t.TypeKind == TypeKind.Interface || 
                                         (t.TypeKind == TypeKind.Class && t.IsAbstract)));
                
                if (interfaceSymbol != null) break;
            }
            
            if (interfaceSymbol == null) continue;
            
            // Find implementations
            var implementations = await SymbolFinder.FindImplementationsAsync(interfaceSymbol, solution);
            
            foreach (var impl in implementations.OfType<INamedTypeSymbol>())
            {
                var location = impl.Locations.FirstOrDefault();
                if (location != null && location.IsInSource)
                {
                    var lineSpan = location.GetLineSpan();
                    
                    // Get other implemented interfaces
                    var otherInterfaces = impl.AllInterfaces
                        .Where(i => i.Name != interfaceName)
                        .Select(i => i.Name);
                    
                    results.Add(new ImplementationInfo
                    {
                        ImplementingType = impl.ToDisplayString(),
                        FilePath = lineSpan.Path,
                        Line = lineSpan.StartLinePosition.Line + 1,
                        Column = lineSpan.StartLinePosition.Character + 1,
                        ProjectName = solution.GetDocument(location.SourceTree)?.Project.Name ?? "",
                        BaseTypes = otherInterfaces.Any() ? string.Join(", ", otherInterfaces) : null
                    });
                }
            }
        }
        
        return results;
    }
    
    public async Task<List<OverrideInfo>> FindOverridesAsync(string methodName, string className, string? workspacePath = null)
    {
        var results = new List<OverrideInfo>();
        var workspacesToSearch = GetWorkspacesToSearch(workspacePath);
        
        foreach (var workspace in workspacesToSearch)
        {
            var solution = workspace.CurrentSolution;
            
            // Find the base method
            IMethodSymbol? baseMethod = null;
            
            foreach (var project in solution.Projects)
            {
                if (baseMethod != null) break;
                
                var compilation = await project.GetCompilationAsync();
                if (compilation == null) continue;
                
                var classSymbol = compilation.Assembly.GetAllTypes()
                    .FirstOrDefault(t => t.Name == className);
                
                if (classSymbol != null)
                {
                    baseMethod = classSymbol.GetMembers(methodName)
                        .OfType<IMethodSymbol>()
                        .FirstOrDefault(m => m.IsVirtual || m.IsAbstract || m.IsOverride);
                    
                    if (baseMethod != null) break;
                }
            }
            
            if (baseMethod == null) continue;
            
            // Find overrides
            var overrides = await SymbolFinder.FindOverridesAsync(baseMethod, solution);
            
            foreach (var ovr in overrides.OfType<IMethodSymbol>())
            {
                var location = ovr.Locations.FirstOrDefault();
                if (location != null && location.IsInSource)
                {
                    var lineSpan = location.GetLineSpan();
                    
                    results.Add(new OverrideInfo
                    {
                        OverridingType = ovr.ContainingType.ToDisplayString(),
                        MethodName = ovr.Name,
                        Parameters = GetMethodParameters(ovr),
                        FilePath = lineSpan.Path,
                        Line = lineSpan.StartLinePosition.Line + 1,
                        Column = lineSpan.StartLinePosition.Character + 1,
                        ProjectName = solution.GetDocument(location.SourceTree)?.Project.Name ?? "",
                        AccessModifier = GetAccessModifier(ovr),
                        IsSealed = ovr.IsSealed
                    });
                }
            }
        }
        
        return results;
    }
    
    public async Task<List<DerivedTypeInfo>> FindDerivedTypesAsync(string baseClassName, string? workspacePath = null)
    {
        var results = new List<DerivedTypeInfo>();
        var workspacesToSearch = GetWorkspacesToSearch(workspacePath);
        
        foreach (var workspace in workspacesToSearch)
        {
            var solution = workspace.CurrentSolution;
            
            // Find the base class symbol
            INamedTypeSymbol? baseClassSymbol = null;
            
            foreach (var project in solution.Projects)
            {
                if (baseClassSymbol != null) break;
                
                var compilation = await project.GetCompilationAsync();
                if (compilation == null) continue;
                
                baseClassSymbol = compilation.Assembly.GetAllTypes()
                    .FirstOrDefault(t => t.Name == baseClassName && 
                                        (t.TypeKind == TypeKind.Class || t.TypeKind == TypeKind.Interface));
                
                if (baseClassSymbol != null) break;
            }
            
            if (baseClassSymbol == null) continue;
            
            // Find derived types
            var derivedTypes = await SymbolFinder.FindDerivedClassesAsync(baseClassSymbol, solution);
            
            foreach (var derived in derivedTypes.OfType<INamedTypeSymbol>())
            {
                var location = derived.Locations.FirstOrDefault();
                if (location != null && location.IsInSource)
                {
                    var lineSpan = location.GetLineSpan();
                    
                    results.Add(new DerivedTypeInfo
                    {
                        DerivedType = derived.ToDisplayString(),
                        BaseType = derived.BaseType?.ToDisplayString() ?? baseClassName,
                        FilePath = lineSpan.Path,
                        Line = lineSpan.StartLinePosition.Line + 1,
                        Column = lineSpan.StartLinePosition.Character + 1,
                        ProjectName = solution.GetDocument(location.SourceTree)?.Project.Name ?? "",
                        IsAbstract = derived.IsAbstract,
                        IsSealed = derived.IsSealed
                    });
                }
            }
            
            // Also check all types manually to handle generic base types
            foreach (var project in solution.Projects)
            {
                var compilation = await project.GetCompilationAsync();
                if (compilation == null) continue;
                
                foreach (var type in compilation.Assembly.GetAllTypes())
                {
                    if (type.BaseType != null && 
                        (type.BaseType.Name == baseClassName || 
                         (type.BaseType.IsGenericType && type.BaseType.ConstructUnboundGenericType().Name == baseClassName)))
                    {
                        var location = type.Locations.FirstOrDefault();
                        if (location != null && location.IsInSource)
                        {
                            var lineSpan = location.GetLineSpan();
                            
                            // Check if we already added this type
                            if (!results.Any(r => r.DerivedType == type.ToDisplayString()))
                            {
                                results.Add(new DerivedTypeInfo
                                {
                                    DerivedType = type.ToDisplayString(),
                                    BaseType = type.BaseType.ToDisplayString(),
                                    FilePath = lineSpan.Path,
                                    Line = lineSpan.StartLinePosition.Line + 1,
                                    Column = lineSpan.StartLinePosition.Character + 1,
                                    ProjectName = project.Name,
                                    IsAbstract = type.IsAbstract,
                                    IsSealed = type.IsSealed
                                });
                            }
                        }
                    }
                }
            }
        }
        
        // Also find all derived types recursively to build the full hierarchy
        var allDerived = new HashSet<string>(results.Select(r => r.DerivedType));
        var toProcess = new Queue<DerivedTypeInfo>(results);
        
        while (toProcess.Count > 0)
        {
            var current = toProcess.Dequeue();
            
            // Find types that derive from this one
            var children = results.Where(r => r.BaseType == current.DerivedType).ToList();
            
            foreach (var child in children)
            {
                if (!allDerived.Contains(child.DerivedType))
                {
                    allDerived.Add(child.DerivedType);
                    toProcess.Enqueue(child);
                }
            }
        }
        
        return results;
    }
    
    public async Task<RenameResult> RenameSymbolAsync(string oldName, string newName, string symbolType, string? containerName, string? workspacePath, bool preview)
    {
        var result = new RenameResult { Success = true };
        var workspacesToSearch = GetWorkspacesToSearch(workspacePath);
        
        // Validate new name
        if (string.IsNullOrWhiteSpace(newName))
        {
            result.Error = "New name cannot be empty";
            result.Success = false;
            return result;
        }
        
        // Check if new name is a C# keyword without @ prefix
        var keywords = new HashSet<string> 
        { 
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
            "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
            "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
            "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
            "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
            "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
            "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this",
            "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort",
            "using", "virtual", "void", "volatile", "while"
        };
        
        // Allow keywords with @ prefix (e.g., @class is valid)
        if (!newName.StartsWith("@") && keywords.Contains(newName))
        {
            result.Error = $"'{newName}' is a reserved C# keyword. Use @{newName} if you need this name.";
            result.Success = false;
            return result;
        }
        
        // Validate identifier (with or without @)
        var identifierToValidate = newName.StartsWith("@") ? newName.Substring(1) : newName;
        if (identifierToValidate.Length == 0)
        {
            result.Error = "Identifier cannot be empty after @ prefix";
            result.Success = false;
            return result;
        }
        
        // Check for valid C# identifier characters
        if (!char.IsLetter(identifierToValidate[0]) && identifierToValidate[0] != '_')
        {
            result.Error = $"'{newName}' is not a valid C# identifier. Identifiers must start with a letter or underscore.";
            result.Success = false;
            return result;
        }
        
        for (int i = 1; i < identifierToValidate.Length; i++)
        {
            if (!char.IsLetterOrDigit(identifierToValidate[i]) && identifierToValidate[i] != '_')
            {
                result.Error = $"'{newName}' is not a valid C# identifier. Identifiers can only contain letters, digits, and underscores.";
                result.Success = false;
                return result;
            }
        }
        
        try
        {
            foreach (var workspace in workspacesToSearch)
            {
                var solution = workspace.CurrentSolution;
                
                // Find the symbol to rename
                ISymbol? targetSymbol = null;
                
                foreach (var project in solution.Projects)
                {
                    if (targetSymbol != null) break;
                    
                    var compilation = await project.GetCompilationAsync();
                    if (compilation == null) continue;
                    
                    targetSymbol = await FindSymbolAsync(compilation, oldName, symbolType, containerName);
                    if (targetSymbol != null) break;
                }
                
                if (targetSymbol == null)
                {
                    result.Error = $"Symbol '{oldName}' not found";
                    result.Success = false;
                    return result;
                }
                
                // Safety check: Is this a system/framework type?
                var assemblyName = targetSymbol.ContainingAssembly?.Name ?? "";
                var dangerousAssemblies = new HashSet<string> 
                { 
                    "mscorlib", "System", "System.Core", "System.Runtime", "System.Collections",
                    "System.Linq", "System.Threading", "System.IO", "System.Net", "Microsoft.CSharp",
                    "netstandard", "System.Private.CoreLib"
                };
                
                if (dangerousAssemblies.Any(a => assemblyName.StartsWith(a)))
                {
                    result.Error = $"Cannot rename '{oldName}' - it's a system/framework symbol from {assemblyName}";
                    result.Success = false;
                    result.Warning = "Renaming system types could break your entire application";
                    return result;
                }
                
                // Check if it's a public API member that might be used externally
                if (targetSymbol.DeclaredAccessibility == Accessibility.Public)
                {
                    result.Warning = $"Warning: '{oldName}' is a public {symbolType}. This change may break external consumers.";
                }
                
                // Count references to assess impact
                var references = await SymbolFinder.FindReferencesAsync(targetSymbol, solution);
                var referenceCount = references.Sum(r => r.Locations.Count());
                var affectedFiles = references.SelectMany(r => r.Locations)
                    .Select(l => l.Document?.FilePath)
                    .Where(f => f != null)
                    .Distinct()
                    .Count();
                
                result.ImpactSummary = $"This rename will affect {referenceCount} references across {affectedFiles} files";
                
                // Warn if large impact
                if (referenceCount > 100)
                {
                    result.Warning = (result.Warning ?? "") + $"\nLarge impact detected: {referenceCount} references will be updated!";
                }
                
                // Check for naming conflicts in the same scope
                if (containerName != null && targetSymbol.ContainingType != null)
                {
                    var existingMember = targetSymbol.ContainingType.GetMembers(newName).FirstOrDefault();
                    if (existingMember != null && !SymbolEqualityComparer.Default.Equals(existingMember, targetSymbol))
                    {
                        result.Error = $"A {existingMember.Kind} named '{newName}' already exists in {containerName}";
                        result.Success = false;
                        return result;
                    }
                }
                
                // Use Roslyn's Renamer API
                var newSolution = await Renamer.RenameSymbolAsync(solution, targetSymbol, newName, solution.Options);
                
                // Get the changes
                var changes = newSolution.GetChanges(solution);
                
                foreach (var projectChange in changes.GetProjectChanges())
                {
                    foreach (var docId in projectChange.GetChangedDocuments())
                    {
                        var oldDoc = solution.GetDocument(docId);
                        var newDoc = newSolution.GetDocument(docId);
                        
                        if (oldDoc != null && newDoc != null)
                        {
                            var oldText = await oldDoc.GetTextAsync();
                            var newText = await newDoc.GetTextAsync();
                            var textChanges = newText.GetTextChanges(oldText);
                            
                            if (textChanges.Any())
                            {
                                var fileChange = new FileChangeInfo
                                {
                                    FilePath = oldDoc.FilePath ?? ""
                                };
                                
                                foreach (var change in textChanges)
                                {
                                    var linePos = oldText.Lines.GetLinePosition(change.Span.Start);
                                    fileChange.Edits.Add(new TextEdit
                                    {
                                        Line = linePos.Line + 1,
                                        Column = linePos.Character + 1,
                                        OldText = oldText.GetSubText(change.Span).ToString(),
                                        NewText = change.NewText ?? ""
                                    });
                                }
                                
                                result.Changes.Add(fileChange);
                            }
                        }
                    }
                }
                
                // Apply changes if not preview
                if (!preview && result.Changes.Any())
                {
                    foreach (var change in result.Changes)
                    {
                        var filePath = change.FilePath;
                        if (File.Exists(filePath))
                        {
                            var content = await File.ReadAllTextAsync(filePath);
                            
                            // Apply edits in reverse order to maintain positions
                            foreach (var edit in change.Edits.OrderByDescending(e => e.Line).ThenByDescending(e => e.Column))
                            {
                                // Simple text replacement - in production you'd want more sophisticated editing
                                content = content.Replace(edit.OldText, edit.NewText);
                            }
                            
                            await File.WriteAllTextAsync(filePath, content);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
        }
        
        return result;
    }
    
    public async Task<PatternFixResult> FixPatternAsync(string findPattern, string replacePattern, string patternType, string? workspacePath, bool preview)
    {
        var result = new PatternFixResult { Success = true };
        var workspacesToSearch = GetWorkspacesToSearch(workspacePath);
        
        try
        {
            foreach (var workspace in workspacesToSearch)
            {
                var solution = workspace.CurrentSolution;
                
                foreach (var project in solution.Projects)
                {
                    foreach (var document in project.Documents)
                    {
                        var root = await document.GetSyntaxRootAsync();
                        if (root == null) continue;
                        
                        var semanticModel = await document.GetSemanticModelAsync();
                        if (semanticModel == null) continue;
                        
                        var sourceText = await document.GetTextAsync();
                        
                        // Find patterns based on type
                        switch (patternType.ToLower())
                        {
                            case "method-call":
                                await FindMethodCallPatterns(root, semanticModel, sourceText, document.FilePath ?? "", findPattern, replacePattern, result.Fixes);
                                break;
                                
                            case "async-usage":
                                await FindAsyncPatterns(root, semanticModel, sourceText, document.FilePath ?? "", findPattern, replacePattern, result.Fixes);
                                break;
                                
                            case "null-check":
                                await FindNullCheckPatterns(root, semanticModel, sourceText, document.FilePath ?? "", findPattern, replacePattern, result.Fixes);
                                break;
                                
                            case "string-format":
                                await FindStringFormatPatterns(root, semanticModel, sourceText, document.FilePath ?? "", findPattern, replacePattern, result.Fixes);
                                break;
                        }
                    }
                }
                
                // Apply fixes if not preview
                if (!preview && result.Fixes.Any())
                {
                    var fileGroups = result.Fixes.GroupBy(f => f.FilePath);
                    
                    foreach (var fileGroup in fileGroups)
                    {
                        var filePath = fileGroup.Key;
                        if (File.Exists(filePath))
                        {
                            var content = await File.ReadAllTextAsync(filePath);
                            
                            // Apply fixes in reverse order
                            foreach (var fix in fileGroup.OrderByDescending(f => f.Line).ThenByDescending(f => f.Column))
                            {
                                content = content.Replace(fix.OriginalCode, fix.FixedCode);
                            }
                            
                            await File.WriteAllTextAsync(filePath, content);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
        }
        
        return result;
    }
    
    private async Task FindMethodCallPatterns(SyntaxNode root, SemanticModel semanticModel, SourceText sourceText, string filePath, string findPattern, string replacePattern, List<PatternFix> fixes)
    {
        // Find invocations based on language
        var invocations = root.Language switch
        {
            LanguageNames.CSharp => root.DescendantNodes().OfType<CS.InvocationExpressionSyntax>().Cast<SyntaxNode>(),
            LanguageNames.VisualBasic => root.DescendantNodes().OfType<VB.InvocationExpressionSyntax>().Cast<SyntaxNode>(),
            _ => Enumerable.Empty<SyntaxNode>()
        };
        
        foreach (var invocation in invocations)
        {
            var methodSymbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
            if (methodSymbol == null) continue;
            
            var methodName = methodSymbol.Name;
            
            // Check if method matches pattern
            if (MatchesWildcardPattern(methodName, findPattern))
            {
                var lineSpan = invocation.GetLocation().GetLineSpan();
                var line = sourceText.Lines[lineSpan.StartLinePosition.Line];
                
                // Create replacement
                var originalCode = invocation.ToString();
                var newMethodName = ApplyReplacePattern(methodName, findPattern, replacePattern);
                var fixedCode = originalCode.Replace(methodName, newMethodName);
                
                fixes.Add(new PatternFix
                {
                    FilePath = filePath,
                    Line = lineSpan.StartLinePosition.Line + 1,
                    Column = lineSpan.StartLinePosition.Character + 1,
                    OriginalCode = originalCode,
                    FixedCode = fixedCode,
                    Description = $"Rename method call from {methodName} to {newMethodName}"
                });
            }
        }
    }
    
    private async Task FindAsyncPatterns(SyntaxNode root, SemanticModel semanticModel, SourceText sourceText, string filePath, string findPattern, string replacePattern, List<PatternFix> fixes)
    {
        var language = root.Language;
        
        // Find async methods without await
        if (findPattern == "async-without-await")
        {
            if (language == LanguageNames.CSharp)
            {
                var methods = root.DescendantNodes().OfType<CS.MethodDeclarationSyntax>()
                    .Where(m => m.Modifiers.Any(mod => mod.IsKind(SyntaxKind.AsyncKeyword)));
                
                foreach (var method in methods)
                {
                    var hasAwait = method.DescendantNodes().OfType<CS.AwaitExpressionSyntax>().Any();
                    if (!hasAwait)
                    {
                        var lineSpan = method.Identifier.GetLocation().GetLineSpan();
                        
                        fixes.Add(new PatternFix
                        {
                            FilePath = filePath,
                            Line = lineSpan.StartLinePosition.Line + 1,
                            Column = lineSpan.StartLinePosition.Character + 1,
                            OriginalCode = "async",
                            FixedCode = "",
                            Description = $"Remove unnecessary async modifier from {method.Identifier.Text}"
                        });
                    }
                }
            }
            else if (language == LanguageNames.VisualBasic)
            {
                var methods = root.DescendantNodes().OfType<VB.MethodBlockSyntax>()
                    .Where(m => m.SubOrFunctionStatement.Modifiers.Any(mod => mod.IsKind(VBSyntaxKind.AsyncKeyword)));
                
                foreach (var method in methods)
                {
                    var hasAwait = method.DescendantNodes().OfType<VB.AwaitExpressionSyntax>().Any();
                    if (!hasAwait)
                    {
                        var lineSpan = method.SubOrFunctionStatement.Identifier.GetLocation().GetLineSpan();
                        
                        fixes.Add(new PatternFix
                        {
                            FilePath = filePath,
                            Line = lineSpan.StartLinePosition.Line + 1,
                            Column = lineSpan.StartLinePosition.Character + 1,
                            OriginalCode = "Async",
                            FixedCode = "",
                            Description = $"Remove unnecessary Async modifier from {method.SubOrFunctionStatement.Identifier.Text}"
                        });
                    }
                }
            }
        }
        // Find missing await on async calls
        else if (findPattern == "missing-await")
        {
            if (language == LanguageNames.CSharp)
            {
                var invocations = root.DescendantNodes().OfType<CS.InvocationExpressionSyntax>();
                
                foreach (var invocation in invocations)
                {
                    var symbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                    if (symbol != null && symbol.ReturnType.Name == "Task" && 
                        !invocation.Parent.IsKind(SyntaxKind.AwaitExpression))
                    {
                        var lineSpan = invocation.GetLocation().GetLineSpan();
                        
                        fixes.Add(new PatternFix
                        {
                            FilePath = filePath,
                            Line = lineSpan.StartLinePosition.Line + 1,
                            Column = lineSpan.StartLinePosition.Character + 1,
                            OriginalCode = invocation.ToString(),
                            FixedCode = $"await {invocation}",
                            Description = $"Add missing await to async call"
                        });
                    }
                }
            }
            else if (language == LanguageNames.VisualBasic)
            {
                var invocations = root.DescendantNodes().OfType<VB.InvocationExpressionSyntax>();
                
                foreach (var invocation in invocations)
                {
                    var symbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                    if (symbol != null && symbol.ReturnType.Name == "Task" && 
                        !invocation.Parent.IsKind(VBSyntaxKind.AwaitExpression))
                    {
                        var lineSpan = invocation.GetLocation().GetLineSpan();
                        
                        fixes.Add(new PatternFix
                        {
                            FilePath = filePath,
                            Line = lineSpan.StartLinePosition.Line + 1,
                            Column = lineSpan.StartLinePosition.Character + 1,
                            OriginalCode = invocation.ToString(),
                            FixedCode = $"Await {invocation}",
                            Description = $"Add missing Await to async call"
                        });
                    }
                }
            }
        }
    }
    
    private async Task FindNullCheckPatterns(SyntaxNode root, SemanticModel semanticModel, SourceText sourceText, string filePath, string findPattern, string replacePattern, List<PatternFix> fixes)
    {
        var language = root.Language;
        
        // Find old-style null checks and replace with null-conditional operator
        if (findPattern == "if-null-check")
        {
            if (language == LanguageNames.CSharp)
            {
                var ifStatements = root.DescendantNodes().OfType<CS.IfStatementSyntax>();
                
                foreach (var ifStatement in ifStatements)
                {
                    // Look for pattern: if (x != null) x.Method()
                    if (ifStatement.Condition is CS.BinaryExpressionSyntax binary &&
                        binary.IsKind(SyntaxKind.NotEqualsExpression) &&
                        binary.Right.IsKind(SyntaxKind.NullLiteralExpression))
                    {
                        var identifier = binary.Left.ToString();
                        var lineSpan = ifStatement.GetLocation().GetLineSpan();
                        
                        // Check if the body uses the same identifier
                        var bodyText = ifStatement.Statement.ToString();
                        if (bodyText.Contains($"{identifier}."))
                        {
                            fixes.Add(new PatternFix
                            {
                                FilePath = filePath,
                                Line = lineSpan.StartLinePosition.Line + 1,
                                Column = lineSpan.StartLinePosition.Character + 1,
                                OriginalCode = ifStatement.ToString(),
                                FixedCode = bodyText.Replace($"{identifier}.", $"{identifier}?.").Trim('{', '}', ' ', '\n', '\r'),
                                Description = "Replace null check with null-conditional operator"
                            });
                        }
                    }
                }
            }
            else if (language == LanguageNames.VisualBasic)
            {
                var ifStatements = root.DescendantNodes().OfType<VB.MultiLineIfBlockSyntax>();
                
                foreach (var ifStatement in ifStatements)
                {
                    // Look for pattern: If x IsNot Nothing Then x.Method()
                    if (ifStatement.IfStatement.Condition is VB.BinaryExpressionSyntax binary &&
                        binary.IsKind(VBSyntaxKind.IsNotExpression) &&
                        binary.Right.IsKind(VBSyntaxKind.NothingLiteralExpression))
                    {
                        var identifier = binary.Left.ToString();
                        var lineSpan = ifStatement.GetLocation().GetLineSpan();
                        
                        // Check if the body uses the same identifier
                        var bodyText = string.Join(" ", ifStatement.Statements.Select(s => s.ToString()));
                        if (bodyText.Contains($"{identifier}."))
                        {
                            fixes.Add(new PatternFix
                            {
                                FilePath = filePath,
                                Line = lineSpan.StartLinePosition.Line + 1,
                                Column = lineSpan.StartLinePosition.Character + 1,
                                OriginalCode = ifStatement.ToString(),
                                FixedCode = bodyText.Replace($"{identifier}.", $"{identifier}?.").Trim(),
                                Description = "Replace null check with null-conditional operator"
                            });
                        }
                    }
                }
                
                // Also check single-line If statements
                var singleLineIfs = root.DescendantNodes().OfType<VB.SingleLineIfStatementSyntax>();
                
                foreach (var ifStatement in singleLineIfs)
                {
                    // Look for pattern: If x IsNot Nothing Then x.Method()
                    if (ifStatement.Condition is VB.BinaryExpressionSyntax binary &&
                        binary.IsKind(VBSyntaxKind.IsNotExpression) &&
                        binary.Right.IsKind(VBSyntaxKind.NothingLiteralExpression))
                    {
                        var identifier = binary.Left.ToString();
                        var lineSpan = ifStatement.GetLocation().GetLineSpan();
                        
                        // Check if the statements use the same identifier
                        var bodyText = string.Join(" ", ifStatement.Statements.Select(s => s.ToString()));
                        if (bodyText.Contains($"{identifier}."))
                        {
                            fixes.Add(new PatternFix
                            {
                                FilePath = filePath,
                                Line = lineSpan.StartLinePosition.Line + 1,
                                Column = lineSpan.StartLinePosition.Character + 1,
                                OriginalCode = ifStatement.ToString(),
                                FixedCode = bodyText.Replace($"{identifier}.", $"{identifier}?."),
                                Description = "Replace null check with null-conditional operator"
                            });
                        }
                    }
                }
            }
        }
    }
    
    private async Task FindStringFormatPatterns(SyntaxNode root, SemanticModel semanticModel, SourceText sourceText, string filePath, string findPattern, string replacePattern, List<PatternFix> fixes)
    {
        var language = root.Language;
        
        // Find string.Format and replace with interpolation
        if (findPattern == "string.Format" || findPattern == "String.Format")
        {
            if (language == LanguageNames.CSharp)
            {
                var invocations = root.DescendantNodes().OfType<CS.InvocationExpressionSyntax>()
                    .Where(i => i.Expression.ToString() == "string.Format" || i.Expression.ToString() == "String.Format");
                
                foreach (var invocation in invocations)
                {
                    if (invocation.ArgumentList.Arguments.Count >= 2)
                    {
                        var formatString = invocation.ArgumentList.Arguments[0].ToString().Trim('"');
                        var args = invocation.ArgumentList.Arguments.Skip(1).Select(a => a.ToString()).ToList();
                        
                        // Simple conversion to string interpolation
                        var interpolated = formatString;
                        for (int i = 0; i < args.Count; i++)
                        {
                            interpolated = interpolated.Replace($"{{{i}}}", $"{{{args[i]}}}");
                        }
                        
                        var lineSpan = invocation.GetLocation().GetLineSpan();
                        
                        fixes.Add(new PatternFix
                        {
                            FilePath = filePath,
                            Line = lineSpan.StartLinePosition.Line + 1,
                            Column = lineSpan.StartLinePosition.Character + 1,
                            OriginalCode = invocation.ToString(),
                            FixedCode = $"$\"{interpolated}\"",
                            Description = "Convert string.Format to string interpolation"
                        });
                    }
                }
            }
            else if (language == LanguageNames.VisualBasic)
            {
                var invocations = root.DescendantNodes().OfType<VB.InvocationExpressionSyntax>()
                    .Where(i => i.Expression.ToString() == "String.Format");
                
                foreach (var invocation in invocations)
                {
                    if (invocation.ArgumentList?.Arguments.Count >= 2)
                    {
                        var formatString = invocation.ArgumentList.Arguments[0].ToString().Trim('"');
                        var args = invocation.ArgumentList.Arguments.Skip(1).Select(a => a.ToString()).ToList();
                        
                        // Simple conversion to string interpolation
                        var interpolated = formatString;
                        for (int i = 0; i < args.Count; i++)
                        {
                            interpolated = interpolated.Replace($"{{{i}}}", $"{{{args[i]}}}");
                        }
                        
                        var lineSpan = invocation.GetLocation().GetLineSpan();
                        
                        fixes.Add(new PatternFix
                        {
                            FilePath = filePath,
                            Line = lineSpan.StartLinePosition.Line + 1,
                            Column = lineSpan.StartLinePosition.Character + 1,
                            OriginalCode = invocation.ToString(),
                            FixedCode = $"$\"{interpolated}\"",
                            Description = "Convert String.Format to string interpolation"
                        });
                    }
                }
            }
        }
    }
    
    private bool MatchesWildcardPattern(string text, string pattern)
    {
        var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
        return Regex.IsMatch(text, regexPattern, RegexOptions.IgnoreCase);
    }
    
    private string ApplyReplacePattern(string text, string findPattern, string replacePattern)
    {
        // Simple replacement - in production you'd want more sophisticated pattern matching
        if (findPattern.Contains("*"))
        {
            var prefix = findPattern.Split('*')[0];
            var suffix = findPattern.Contains("*") && findPattern.Split('*').Length > 1 ? findPattern.Split('*')[1] : "";
            
            if (text.StartsWith(prefix) && text.EndsWith(suffix))
            {
                var middle = text.Substring(prefix.Length, text.Length - prefix.Length - suffix.Length);
                return replacePattern.Replace("*", middle);
            }
        }
        
        return replacePattern;
    }
    
    public async Task<FindStatementsResult> FindStatementsAsync(
        string pattern, 
        Dictionary<string, string>? scope,
        string patternType = "text",
        bool includeNestedStatements = false,
        bool groupRelated = false,
        string? workspacePath = null)
    {
        var result = new FindStatementsResult { Success = true };
        var workspacesToSearch = GetWorkspacesToSearch(workspacePath);
        var statementIdCounter = new StatementIdCounter();
        
        try
        {
            foreach (var workspace in workspacesToSearch)
            {
                var solution = workspace.CurrentSolution;
                
                foreach (var project in solution.Projects)
                {
                    // Apply scope filters
                    if (scope?.ContainsKey("file") == true)
                    {
                        var document = project.Documents.FirstOrDefault(d => d.FilePath == scope["file"]);
                        if (document != null)
                        {
                            await ProcessDocumentForStatements(document, pattern, patternType, 
                                includeNestedStatements, scope, result, statementIdCounter);
                        }
                    }
                    else
                    {
                        foreach (var document in project.Documents)
                        {
                            await ProcessDocumentForStatements(document, pattern, patternType, 
                                includeNestedStatements, scope, result, statementIdCounter);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
        }
        
        return result;
    }
    
    private class StatementIdCounter
    {
        public int Value { get; set; } = 0;
    }
    
    // Track statement information for the session - removed as find is read-only
    
    private async Task ProcessDocumentForStatements(
        Document document,
        string pattern,
        string patternType,
        bool includeNestedStatements,
        Dictionary<string, string>? scope,
        FindStatementsResult result,
        StatementIdCounter statementIdCounter)
    {
        var root = await document.GetSyntaxRootAsync();
        if (root == null) return;
        
        var semanticModel = await document.GetSemanticModelAsync();
        if (semanticModel == null) return;
        
        var sourceText = await document.GetTextAsync();
        var language = LanguageDetector.GetLanguageFromDocument(document);
        var handler = LanguageHandlerFactory.GetHandler(language);
        
        if (handler == null)
        {
            _logger.LogWarning($"No language handler available for {language}");
            return;
        }
        
        // Handle RoslynPath pattern type
        if (patternType.ToLower() == "roslynpath")
        {
            try
            {
                var syntaxTree = await document.GetSyntaxTreeAsync();
                if (syntaxTree == null) return;
                
                // Use RoslynPath to find nodes
                var matchingNodes = RoslynPath.RoslynPath.Find(syntaxTree, pattern, semanticModel);
                
                // Filter to only statements
                var statements = matchingNodes.Where(n => handler.IsStatement(n));
                
                // Apply scope filter if specified
                if (scope?.ContainsKey("className") == true || scope?.ContainsKey("methodName") == true)
                {
                    statements = statements.Where(stmt => IsInScope(stmt, scope, language));
                }
                
                // Filter nested statements if requested
                if (!includeNestedStatements)
                {
                    statements = statements.Where(stmt => !IsNestedStatement(stmt, language));
                }
                
                foreach (var statement in statements)
                {
                    await AddStatementToResult(statement, sourceText, semanticModel, 
                        document.FilePath ?? "", result, statementIdCounter, language);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"RoslynPath query failed: {ex.Message}. Falling back to text search.");
                // Fall back to text search
                patternType = "text";
            }
        }
        
        // Handle text and regex pattern types
        if (patternType.ToLower() != "roslynpath")
        {
            // Get all statements using language handler
            var statements = root.DescendantNodes()
                .Where(n => handler.IsStatement(n));
            
            // Filter by scope if specified
            if (scope?.ContainsKey("className") == true || scope?.ContainsKey("methodName") == true)
            {
                statements = statements.Where(stmt => IsInScope(stmt, scope, language));
            }
            
            // Filter nested statements if requested
            if (!includeNestedStatements)
            {
                statements = statements.Where(stmt => !IsNestedStatement(stmt, language));
            }
            
            foreach (var statement in statements)
            {
                var statementText = statement.ToString();
                
                // Apply pattern matching
                bool matches = patternType switch
                {
                    "regex" => System.Text.RegularExpressions.Regex.IsMatch(statementText, pattern),
                    _ => statementText.Contains(pattern, StringComparison.OrdinalIgnoreCase)
                };
                
                if (matches)
                {
                    await AddStatementToResult(statement, sourceText, semanticModel, 
                        document.FilePath ?? "", result, statementIdCounter, language);
                }
            }
        }
    }
    
    private async Task AddStatementToResult(
        SyntaxNode statement,
        SourceText sourceText,
        SemanticModel semanticModel,
        string filePath,
        FindStatementsResult result,
        StatementIdCounter statementIdCounter,
        string language)
    {
        var handler = LanguageHandlerFactory.GetHandler(language);
        if (handler == null) return;
        
        var lineSpan = sourceText.Lines.GetLinePositionSpan(statement.Span);
        var containingMethod = statement.Ancestors().FirstOrDefault(n => handler.IsMethodDeclaration(n));
        var containingClass = statement.Ancestors().FirstOrDefault(n => handler.IsTypeDeclaration(n));
        
        var statementId = $"stmt-{++statementIdCounter.Value}";
        
        var statementInfo = new StatementInfo
        {
            StatementId = statementId,
            Type = statement.GetType().Name,
            Text = statement.ToString(),
            Location = new Location
            {
                File = filePath,
                Line = lineSpan.Start.Line + 1,
                Column = lineSpan.Start.Character + 1
            },
            ContainingMethod = containingMethod != null ? handler.GetMethodDeclarationName(containingMethod) ?? "" : "",
            ContainingClass = containingClass != null ? handler.GetTypeDeclarationName(containingClass) ?? "" : "",
            SyntaxTag = $"syntax-{statementIdCounter.Value}"
        };
        
        // Add semantic tags for symbols in the statement
        var symbols = statement.DescendantNodes()
            .Select(node => semanticModel.GetSymbolInfo(node).Symbol)
            .Where(symbol => symbol != null)
            .Select(symbol => $"symbol-{symbol!.Name}")
            .Distinct();
        
        statementInfo.SemanticTags.AddRange(symbols);
        
        result.Statements.Add(statementInfo);
    }
    
    private bool IsInScope(SyntaxNode statement, Dictionary<string, string> scope, string language)
    {
        var handler = LanguageHandlerFactory.GetHandler(language);
        if (handler == null) return false;
        
        if (scope.ContainsKey("methodName"))
        {
            var method = statement.Ancestors().FirstOrDefault(n => handler.IsMethodDeclaration(n));
            if (method != null)
            {
                var methodName = handler.GetMethodDeclarationName(method);
                if (methodName != scope["methodName"])
                    return false;
            }
            else
            {
                return false;
            }
        }
        
        if (scope.ContainsKey("className"))
        {
            var type = statement.Ancestors().FirstOrDefault(n => handler.IsTypeDeclaration(n));
            if (type != null)
            {
                var typeName = handler.GetTypeDeclarationName(type);
                if (typeName != scope["className"])
                    return false;
            }
            else
            {
                return false;
            }
        }
        
        return true;
    }
    
    private bool IsNestedStatement(SyntaxNode statement, string language)
    {
        var handler = LanguageHandlerFactory.GetHandler(language);
        if (handler == null) return false;
        
        // Check if this statement is inside another statement (like inside if/while/for body)
        var parent = statement.Parent;
        while (parent != null)
        {
            if (language == LanguageNames.CSharp)
            {
                if (parent is CS.BlockSyntax block && handler.IsStatement(block.Parent))
                    return true;
                if (handler.IsStatement(parent) && !(parent is CS.BlockSyntax))
                    return true;
            }
            else if (language == LanguageNames.VisualBasic)
            {
                // In VB.NET, statements can be nested in different ways
                if (handler.IsStatement(parent) && parent != statement)
                    return true;
            }
            parent = parent.Parent;
        }
        return false;
    }
    
    public async Task<ReplaceStatementResult> ReplaceStatementAsync(
        string filePath,
        int line,
        int column,
        string newStatement,
        bool preserveComments = true,
        string? workspacePath = null)
    {
        var result = new ReplaceStatementResult { Success = true };
        
        try
        {
            // Find the document
            var workspacesToSearch = GetWorkspacesToSearch(workspacePath);
            Document? targetDocument = null;
            
            foreach (var ws in workspacesToSearch)
            {
                var solution = ws.CurrentSolution;
                targetDocument = solution.Projects
                    .SelectMany(p => p.Documents)
                    .FirstOrDefault(d => d.FilePath == filePath);
                    
                if (targetDocument != null) break;
            }
            
            if (targetDocument == null)
            {
                result.Success = false;
                result.Error = $"File not found in workspace: {filePath}";
                return result;
            }
            
            // Get syntax tree
            var root = await targetDocument.GetSyntaxRootAsync();
            if (root == null)
            {
                result.Success = false;
                result.Error = "Failed to get syntax root";
                return result;
            }
            
            var sourceText = await targetDocument.GetTextAsync();
            var language = LanguageDetector.GetLanguageFromDocument(targetDocument);
            var handler = LanguageHandlerFactory.GetHandler(language);
            
            if (handler == null)
            {
                result.Success = false;
                result.Error = $"No language handler available for {language}";
                return result;
            }
            
            // Find the statement at the specified location
            var position = sourceText.Lines.GetPosition(new LinePosition(line - 1, column - 1));
            var token = root.FindToken(position);
            
            // Find the containing statement
            var statement = token.Parent?.AncestorsAndSelf()
                .FirstOrDefault(n => handler.IsStatement(n));
                
            if (statement == null)
            {
                result.Success = false;
                result.Error = $"No statement found at {filePath}:{line}:{column}";
                return result;
            }
            
            // Parse the new statement
            var newStatementSyntax = handler.ParseStatement(newStatement);
            if (newStatementSyntax.ContainsDiagnostics)
            {
                result.Success = false;
                result.Error = "New statement contains syntax errors";
                return result;
            }
            
            // Preserve leading trivia (comments, whitespace) if requested
            if (preserveComments)
            {
                newStatementSyntax = newStatementSyntax
                    .WithLeadingTrivia(statement.GetLeadingTrivia())
                    .WithTrailingTrivia(statement.GetTrailingTrivia());
            }
            else
            {
                // At least preserve indentation
                var leadingWhitespace = statement.GetLeadingTrivia()
                    .Where(t => handler.IsWhitespaceTrivia(t))
                    .LastOrDefault();
                if (leadingWhitespace != default)
                {
                    newStatementSyntax = newStatementSyntax.WithLeadingTrivia(leadingWhitespace);
                }
                
                // Preserve line ending
                var trailingTrivia = statement.GetTrailingTrivia()
                    .Where(t => handler.IsEndOfLineTrivia(t))
                    .LastOrDefault();
                if (trailingTrivia != default)
                {
                    newStatementSyntax = newStatementSyntax.WithTrailingTrivia(trailingTrivia);
                }
            }
            
            // Replace the statement
            var newRoot = root.ReplaceNode(statement, newStatementSyntax);
            
            // Update the document
            var newDocument = targetDocument.WithSyntaxRoot(newRoot);
            
            // Apply the changes
            var workspace = targetDocument.Project.Solution.Workspace;
            var success = workspace.TryApplyChanges(newDocument.Project.Solution);
            
            if (!success)
            {
                result.Success = false;
                result.Error = "Failed to apply changes to workspace";
                return result;
            }
            
            result.ModifiedFile = filePath;
            result.OriginalStatement = statement.ToString();
            result.NewStatement = newStatementSyntax.ToString();
            
            // Generate preview
            var statementLineSpan = sourceText.Lines.GetLinePositionSpan(statement.Span);
            var startLine = Math.Max(0, statementLineSpan.Start.Line - 2);
            var endLine = Math.Min(sourceText.Lines.Count - 1, statementLineSpan.End.Line + 2);
            
            var preview = new System.Text.StringBuilder();
            preview.AppendLine("Before:");
            for (int i = startLine; i <= endLine; i++)
            {
                var lineText = sourceText.Lines[i].ToString();
                preview.AppendLine($"  {i + 1}: {lineText}");
            }
            
            var newText = newRoot.GetText();
            preview.AppendLine("\nAfter:");
            for (int i = startLine; i <= endLine; i++)
            {
                if (i < newText.Lines.Count)
                {
                    var lineText = newText.Lines[i].ToString();
                    preview.AppendLine($"  {i + 1}: {lineText}");
                }
            }
            
            result.Preview = preview.ToString();
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
        }
        
        return result;
    }
    
    public async Task<InsertStatementResult> InsertStatementAsync(
        string position,
        string filePath,
        int line,
        int column,
        string statement,
        string? workspacePath = null)
    {
        var result = new InsertStatementResult { Success = true };
        
        try
        {
            // Find the document
            var workspacesToSearch = GetWorkspacesToSearch(workspacePath);
            Document? targetDocument = null;
            
            foreach (var ws in workspacesToSearch)
            {
                var solution = ws.CurrentSolution;
                targetDocument = solution.Projects
                    .SelectMany(p => p.Documents)
                    .FirstOrDefault(d => d.FilePath == filePath);
                    
                if (targetDocument != null) break;
            }
            
            if (targetDocument == null)
            {
                result.Success = false;
                result.Error = $"File not found in workspace: {filePath}";
                return result;
            }
            
            // Get syntax tree
            var root = await targetDocument.GetSyntaxRootAsync();
            if (root == null)
            {
                result.Success = false;
                result.Error = "Failed to get syntax root";
                return result;
            }
            
            var sourceText = await targetDocument.GetTextAsync();
            var language = LanguageDetector.GetLanguageFromDocument(targetDocument);
            var handler = LanguageHandlerFactory.GetHandler(language);
            
            if (handler == null)
            {
                result.Success = false;
                result.Error = $"No language handler available for {language}";
                return result;
            }
            
            // Find the reference statement at the specified location
            var pos = sourceText.Lines.GetPosition(new LinePosition(line - 1, column - 1));
            var token = root.FindToken(pos);
            
            // Find the containing statement
            var referenceStatement = token.Parent?.AncestorsAndSelf()
                .FirstOrDefault(n => handler.IsStatement(n));
                
            if (referenceStatement == null)
            {
                result.Success = false;
                result.Error = $"No statement found at {filePath}:{line}:{column}";
                return result;
            }
            
            // Parse the new statement
            var newStatementSyntax = handler.ParseStatement(statement);
            if (newStatementSyntax.ContainsDiagnostics)
            {
                result.Success = false;
                result.Error = "New statement contains syntax errors";
                return result;
            }
            
            // Get the parent that contains the reference statement
            var parent = referenceStatement.Parent;
            if (parent == null)
            {
                result.Success = false;
                result.Error = "Cannot determine where to insert the statement";
                return result;
            }
            
            // Apply indentation from the reference statement
            newStatementSyntax = handler.ApplyIndentation(newStatementSyntax, referenceStatement);
            
            // Add end-of-line trivia
            var eolTrivia = handler.CreateEndOfLineTrivia();
            newStatementSyntax = newStatementSyntax.WithTrailingTrivia(
                newStatementSyntax.GetTrailingTrivia().Add(eolTrivia));
            
            SyntaxNode newRoot;
            
            if (handler.IsBlock(parent))
            {
                // Insert within a block
                var statements = handler.GetBlockStatements(parent).ToList();
                var index = statements.IndexOf(referenceStatement);
                
                if (index == -1)
                {
                    result.Success = false;
                    result.Error = "Failed to find statement in parent block";
                    return result;
                }
                
                // Insert at the appropriate position
                var newBlock = position.ToLower() == "before"
                    ? handler.InsertIntoBlock(parent, index, newStatementSyntax)
                    : handler.InsertIntoBlock(parent, index + 1, newStatementSyntax);
                    
                newRoot = root.ReplaceNode(parent, newBlock);
            }
            else
            {
                // Handle other cases - language-specific behavior
                if (language == LanguageNames.CSharp)
                {
                    // For C#, wrap in a block
                    var blockStatements = position.ToLower() == "before"
                        ? new[] { newStatementSyntax, referenceStatement }
                        : new[] { referenceStatement, newStatementSyntax };
                        
                    var newBlock = handler.CreateBlock(blockStatements)
                        .WithLeadingTrivia(referenceStatement.GetLeadingTrivia())
                        .WithTrailingTrivia(referenceStatement.GetTrailingTrivia());
                        
                    newRoot = root.ReplaceNode(referenceStatement, newBlock);
                }
                else
                {
                    // For VB.NET, this is more complex as statements are typically in method blocks
                    result.Success = false;
                    result.Error = "Cannot insert statement outside of a block in VB.NET";
                    return result;
                }
            }
            
            // Update the document
            var newDocument = targetDocument.WithSyntaxRoot(newRoot);
            
            // Apply the changes
            var workspace = targetDocument.Project.Solution.Workspace;
            var success = workspace.TryApplyChanges(newDocument.Project.Solution);
            
            if (!success)
            {
                result.Success = false;
                result.Error = "Failed to apply changes to workspace";
                return result;
            }
            
            result.ModifiedFile = filePath;
            result.InsertedStatement = newStatementSyntax.ToString();
            
            // Calculate where it was inserted
            var newText = newRoot.GetText();
            var insertedNode = newRoot.DescendantNodes()
                .OfType<CS.StatementSyntax>()
                .FirstOrDefault(s => s.ToString().Trim() == statement.Trim());
                
            if (insertedNode != null)
            {
                var insertedLineSpan = newText.Lines.GetLinePositionSpan(insertedNode.Span);
                result.InsertedAt = new Location
                {
                    File = filePath,
                    Line = insertedLineSpan.Start.Line + 1,
                    Column = insertedLineSpan.Start.Character + 1
                };
            }
            
            // Generate preview
            var referenceLineSpan = sourceText.Lines.GetLinePositionSpan(referenceStatement.Span);
            var startLine = Math.Max(0, referenceLineSpan.Start.Line - 2);
            var endLine = Math.Min(newText.Lines.Count - 1, referenceLineSpan.End.Line + 3);
            
            var preview = new System.Text.StringBuilder();
            preview.AppendLine($"Inserted {position} line {line}:");
            for (int i = startLine; i <= endLine && i < newText.Lines.Count; i++)
            {
                var lineText = newText.Lines[i].ToString();
                var marker = (insertedNode != null && i >= result.InsertedAt.Line - 1 && 
                             i < result.InsertedAt.Line - 1 + newStatementSyntax.ToString().Split('\n').Length) 
                    ? " <<<" : "";
                preview.AppendLine($"  {i + 1}: {lineText}{marker}");
            }
            
            result.Preview = preview.ToString();
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
        }
        
        return result;
    }
    
    public async Task<RemoveStatementResult> RemoveStatementAsync(
        string filePath,
        int line,
        int column,
        bool preserveComments = true,
        string? workspacePath = null)
    {
        var result = new RemoveStatementResult { Success = true };
        var location = new Location { File = filePath, Line = line, Column = column };
        
        try
        {
            // Find the document
            var workspacesToSearch = GetWorkspacesToSearch(workspacePath);
            Document? targetDocument = null;
            
            foreach (var ws in workspacesToSearch)
            {
                var solution = ws.CurrentSolution;
                targetDocument = solution.Projects
                    .SelectMany(p => p.Documents)
                    .FirstOrDefault(d => d.FilePath == filePath);
                    
                if (targetDocument != null) break;
            }
            
            if (targetDocument == null)
            {
                result.Success = false;
                result.Error = $"File not found in workspace: {filePath}";
                return result;
            }
            
            // Get syntax tree
            var syntaxTree = await targetDocument.GetSyntaxTreeAsync();
            if (syntaxTree == null)
            {
                result.Success = false;
                result.Error = $"Could not parse file: {filePath}";
                return result;
            }
            
            var root = await syntaxTree.GetRootAsync();
            var sourceText = await targetDocument.GetTextAsync();
            
            // Get language handler
            var language = targetDocument.Project.Language;
            var handler = LanguageHandlerFactory.GetHandler(language);
            
            // Find the statement at the location
            var position = sourceText.Lines.GetPosition(new LinePosition(line - 1, column - 1));
            var token = root.FindToken(position);
            var statement = token.Parent?.AncestorsAndSelf().FirstOrDefault(n => handler.IsStatement(n));
            
            if (statement == null)
            {
                result.Success = false;
                result.Error = $"No statement found at {filePath}:{line}:{column}";
                return result;
            }
            
            // Store the statement details before removal
            var removedStatement = statement.ToString();
            var statementLineSpan = statement.GetLocation().GetLineSpan();
            var statementLocation = new Location 
            { 
                File = statementLineSpan.Path, 
                Line = statementLineSpan.StartLinePosition.Line + 1, 
                Column = statementLineSpan.StartLinePosition.Character + 1 
            };
            
            // Check if this is the only statement in a block
            var parentBlock = statement.Parent;
            var isOnlyStatement = false;
            if (parentBlock != null && handler.IsBlock(parentBlock))
            {
                var blockStatements = handler.GetBlockStatements(parentBlock).ToList();
                isOnlyStatement = blockStatements.Count == 1;
            }
            
            // Get the trivia to preserve
            var leadingTrivia = preserveComments ? statement.GetLeadingTrivia() : new SyntaxTriviaList();
            var trailingTrivia = statement.GetTrailingTrivia();
            
            // Create new root with statement removed
            SyntaxNode newRoot;
            if (isOnlyStatement && parentBlock != null)
            {
                // If it's the only statement in a block, we might want to preserve the block structure
                // by adding an empty line or comment
                SyntaxNode emptyStatement;
                if (language == LanguageNames.CSharp)
                {
                    emptyStatement = Microsoft.CodeAnalysis.CSharp.SyntaxFactory.EmptyStatement()
                        .WithLeadingTrivia(leadingTrivia)
                        .WithTrailingTrivia(trailingTrivia);
                }
                else
                {
                    emptyStatement = Microsoft.CodeAnalysis.VisualBasic.SyntaxFactory.EmptyStatement()
                        .WithLeadingTrivia(leadingTrivia)
                        .WithTrailingTrivia(trailingTrivia);
                }
                    
                newRoot = root.ReplaceNode(statement, emptyStatement);
            }
            else
            {
                // Remove the statement entirely
                newRoot = root.RemoveNode(statement, SyntaxRemoveOptions.KeepNoTrivia);
                
                // If we preserved comments, we might need to attach them to the next statement
                if (preserveComments && leadingTrivia.Any(t => handler.IsCommentTrivia(t)))
                {
                    // Find the next statement and attach the comments
                    var nodeAfterRemoval = newRoot.FindToken(position).Parent?.AncestorsAndSelf()
                        .FirstOrDefault(n => handler.IsStatement(n));
                        
                    if (nodeAfterRemoval != null)
                    {
                        var newNodeWithComments = nodeAfterRemoval.WithLeadingTrivia(
                            leadingTrivia.AddRange(nodeAfterRemoval.GetLeadingTrivia()));
                        newRoot = newRoot.ReplaceNode(nodeAfterRemoval, newNodeWithComments);
                    }
                }
            }
            
            // Update the document
            var newDocument = targetDocument.WithSyntaxRoot(newRoot);
            var workspace = targetDocument.Project.Solution.Workspace;
            var success = workspace.TryApplyChanges(newDocument.Project.Solution);
            
            if (!success)
            {
                result.Success = false;
                result.Error = "Failed to apply changes to workspace";
                return result;
            }
            
            // Generate preview
            var preview = GenerateRemovalPreview(root, newRoot, statement, syntaxTree);
            
            result.ModifiedFile = filePath;
            result.RemovedStatement = removedStatement.Trim();
            result.RemovedFrom = statementLocation;
            result.Preview = preview;
            result.WasOnlyStatementInBlock = isOnlyStatement;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = $"Exception during statement removal: {ex.Message}";
        }
        
        return result;
    }
    
    private string GenerateRemovalPreview(SyntaxNode oldRoot, SyntaxNode newRoot, SyntaxNode removedStatement, SyntaxTree syntaxTree)
    {
        var sb = new System.Text.StringBuilder();
        var removedLineSpan = removedStatement.GetLocation().GetLineSpan();
        var removedLocation = new Location 
        { 
            File = removedLineSpan.Path, 
            Line = removedLineSpan.StartLinePosition.Line + 1, 
            Column = removedLineSpan.StartLinePosition.Character + 1 
        };
        
        sb.AppendLine($"Removed from line {removedLocation.Line}:");
        
        // Get surrounding context
        var startLine = Math.Max(1, removedLocation.Line - 3);
        var endLine = removedLocation.Line + 3;
        
        var lines = newRoot.ToString().Split('\n');
        for (int i = startLine - 1; i < Math.Min(lines.Length, endLine); i++)
        {
            var lineNum = i + 1;
            if (lineNum == removedLocation.Line)
            {
                sb.AppendLine($"  {lineNum}: --- removed: {removedStatement.ToString().Trim()} ---");
            }
            else
            {
                sb.AppendLine($"  {lineNum}: {lines[i]}");
            }
        }
        
        return sb.ToString();
    }
    
    public async Task<MarkStatementResult> MarkStatementAsync(
        string filePath,
        int line,
        int column,
        string? label = null,
        string? workspacePath = null)
    {
        var result = new MarkStatementResult { Success = true };
        
        try
        {
            if (!_markerManager.HasCapacity)
            {
                result.Success = false;
                result.Error = $"Maximum number of markers (100) reached. Clear some markers first.";
                return result;
            }
            
            // Find the document
            var workspacesToSearch = GetWorkspacesToSearch(workspacePath);
            Document? targetDocument = null;
            
            foreach (var ws in workspacesToSearch)
            {
                var solution = ws.CurrentSolution;
                targetDocument = solution.Projects
                    .SelectMany(p => p.Documents)
                    .FirstOrDefault(d => d.FilePath == filePath);
                    
                if (targetDocument != null) break;
            }
            
            if (targetDocument == null)
            {
                result.Success = false;
                result.Error = $"File not found in workspace: {filePath}";
                return result;
            }
            
            // Check if we have a marked version of this document
            var markedDoc = _markerManager.GetMarkedDocument(filePath);
            if (markedDoc != null)
            {
                targetDocument = markedDoc;
            }
            
            // Get syntax tree
            var syntaxTree = await targetDocument.GetSyntaxTreeAsync();
            if (syntaxTree == null)
            {
                result.Success = false;
                result.Error = $"Could not parse file: {filePath}";
                return result;
            }
            
            var root = await syntaxTree.GetRootAsync();
            var sourceText = await targetDocument.GetTextAsync();
            
            // Get language handler
            var language = targetDocument.Project.Language;
            var handler = LanguageHandlerFactory.GetHandler(language);
            
            // Find the statement at the location
            var position = sourceText.Lines.GetPosition(new LinePosition(line - 1, column - 1));
            var token = root.FindToken(position);
            var statement = token.Parent?.AncestorsAndSelf().FirstOrDefault(n => handler.IsStatement(n));
            
            if (statement == null)
            {
                result.Success = false;
                result.Error = $"No statement found at {filePath}:{line}:{column}";
                return result;
            }
            
            // Create marker
            var markerId = _markerManager.CreateMarkerId(label);
            var annotation = _markerManager.CreateMarker(markerId, label);
            
            // Annotate the statement
            var annotatedStatement = statement.WithAdditionalAnnotations(annotation);
            var newRoot = root.ReplaceNode(statement, annotatedStatement);
            
            // Create new document with marked statement
            var newDocument = targetDocument.WithSyntaxRoot(newRoot);
            
            // Store the marked document
            _markerManager.StoreMarkedDocument(filePath, newDocument);
            
            // Get location info
            var statementLineSpan = statement.GetLocation().GetLineSpan();
            var location = new Location
            {
                File = statementLineSpan.Path,
                Line = statementLineSpan.StartLinePosition.Line + 1,
                Column = statementLineSpan.StartLinePosition.Character + 1
            };
            
            result.MarkerId = markerId;
            result.Label = label;
            result.MarkedStatement = statement.ToString().Trim();
            result.Location = location;
            result.Context = GetStatementContext(statement);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = $"Exception during statement marking: {ex.Message}";
        }
        
        return result;
    }
    
    public async Task<FindMarkedStatementsResult> FindMarkedStatementsAsync(
        string? markerId = null,
        string? filePath = null)
    {
        var result = new FindMarkedStatementsResult { Success = true };
        
        try
        {
            result.MarkedStatements = _markerManager.FindMarkedStatements(markerId, filePath);
            result.TotalMarkers = _markerManager.MarkerCount;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = $"Exception finding marked statements: {ex.Message}";
        }
        
        return result;
    }
    
    public async Task<UnmarkStatementResult> UnmarkStatementAsync(string markerId)
    {
        var result = new UnmarkStatementResult { Success = true };
        
        try
        {
            if (_markerManager.RemoveMarker(markerId))
            {
                result.Message = $"Marker '{markerId}' removed successfully";
                result.RemainingMarkers = _markerManager.MarkerCount;
            }
            else
            {
                result.Success = false;
                result.Error = $"Marker '{markerId}' not found";
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = $"Exception removing marker: {ex.Message}";
        }
        
        return result;
    }
    
    public async Task<ClearMarkersResult> ClearAllMarkersAsync()
    {
        var result = new ClearMarkersResult { Success = true };
        
        try
        {
            var previousCount = _markerManager.MarkerCount;
            _markerManager.ClearAllMarkers();
            result.ClearedCount = previousCount;
            result.Message = $"Cleared {previousCount} marker(s)";
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = $"Exception clearing markers: {ex.Message}";
        }
        
        return result;
    }
    
    private string GetStatementContext(SyntaxNode statement)
    {
        // Try to find the containing method/property/constructor
        var containingMember = statement.Ancestors().FirstOrDefault(n => 
            n is CS.MethodDeclarationSyntax || n is CS.ConstructorDeclarationSyntax || n is CS.PropertyDeclarationSyntax ||
            n is VB.MethodBlockSyntax || n is VB.MethodStatementSyntax || n is VB.PropertyBlockSyntax || n is VB.SubNewStatementSyntax);
            
        if (containingMember != null)
        {
            string? memberName = null;
            string? className = null;
            
            // C# cases
            if (containingMember is CS.MethodDeclarationSyntax csMethod)
            {
                memberName = csMethod.Identifier.Text;
                className = (csMethod.Parent as CS.TypeDeclarationSyntax)?.Identifier.Text;
            }
            else if (containingMember is CS.ConstructorDeclarationSyntax csConstructor)
            {
                memberName = ".ctor";
                className = (csConstructor.Parent as CS.TypeDeclarationSyntax)?.Identifier.Text;
            }
            else if (containingMember is CS.PropertyDeclarationSyntax csProperty)
            {
                memberName = csProperty.Identifier.Text;
                className = (csProperty.Parent as CS.TypeDeclarationSyntax)?.Identifier.Text;
            }
            // VB.NET cases
            else if (containingMember is VB.MethodBlockSyntax vbMethodBlock)
            {
                memberName = vbMethodBlock.SubOrFunctionStatement.Identifier.Text;
                var containingType = vbMethodBlock.Ancestors().OfType<VB.TypeBlockSyntax>().FirstOrDefault();
                className = containingType?.BlockStatement.Identifier.Text;
            }
            else if (containingMember is VB.PropertyBlockSyntax vbPropertyBlock)
            {
                memberName = vbPropertyBlock.PropertyStatement.Identifier.Text;
                var containingType = vbPropertyBlock.Ancestors().OfType<VB.TypeBlockSyntax>().FirstOrDefault();
                className = containingType?.BlockStatement.Identifier.Text;
            }
            else if (containingMember is VB.SubNewStatementSyntax vbConstructor)
            {
                memberName = "New";
                var containingType = vbConstructor.Ancestors().OfType<VB.TypeBlockSyntax>().FirstOrDefault();
                className = containingType?.BlockStatement.Identifier.Text;
            }
            
            if (className != null && memberName != null)
            {
                return $"{className}.{memberName}";
            }
            else if (memberName != null)
            {
                return memberName;
            }
        }
        
        return statement.Parent?.GetType().Name.Replace("Syntax", "") ?? "Unknown";
    }
    
    private Document? GetDocumentByPath(Solution solution, string filePath)
    {
        var normalizedPath = Path.GetFullPath(filePath);
        return solution.Projects
            .SelectMany(p => p.Documents)
            .FirstOrDefault(d => string.Equals(Path.GetFullPath(d.FilePath ?? ""), normalizedPath, StringComparison.OrdinalIgnoreCase));
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
    
    public async Task<object> AnalyzeSyntaxTreeAsync(string filePath, bool includeTrivia, string? workspaceId)
    {
        var workspacesToSearch = GetWorkspacesToSearch(workspaceId);
        if (!workspacesToSearch.Any())
            return CreateErrorResponseWithReloadInstructions("No workspace loaded", workspaceId);
        
        var workspace = workspacesToSearch.First();
        var solution = workspace.CurrentSolution;
        var document = GetDocumentByPath(solution, filePath);
        
        if (document == null)
            return CreateErrorResponse($"File not found: {filePath}");
            
        var syntaxTree = await document.GetSyntaxTreeAsync();
        if (syntaxTree == null)
            return CreateErrorResponse("Could not parse syntax tree");
            
        var root = await syntaxTree.GetRootAsync();
        var analysis = AnalyzeNode(root, includeTrivia, 0);
        
        return new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = JsonSerializer.Serialize(new
                    {
                        filePath = filePath,
                        language = root.Language,
                        syntaxTree = analysis
                    }, new JsonSerializerOptions { WriteIndented = true })
                }
            }
        };
    }
    
    private object AnalyzeNode(SyntaxNode node, bool includeTrivia, int depth)
    {
        var nodeInfo = new Dictionary<string, object>
        {
            ["kind"] = node.Kind().ToString(),
            ["type"] = node.GetType().Name,
            ["span"] = $"{node.Span.Start}-{node.Span.End}",
            ["text"] = node.Span.Length < 100 ? node.ToString() : node.ToString().Substring(0, 100) + "..."
        };
        
        // Add location info
        if (node.SyntaxTree != null)
        {
            var lineSpan = node.SyntaxTree.GetLineSpan(node.Span);
            nodeInfo["location"] = $"{lineSpan.StartLinePosition.Line + 1}:{lineSpan.StartLinePosition.Character + 1}";
        }
        
        // Add specific node properties
        switch (node)
        {
            // C# nodes
            case CS.ClassDeclarationSyntax cls:
                nodeInfo["name"] = cls.Identifier.Text;
                nodeInfo["modifiers"] = cls.Modifiers.ToString();
                break;
            case CS.MethodDeclarationSyntax method:
                nodeInfo["name"] = method.Identifier.Text;
                nodeInfo["returnType"] = method.ReturnType.ToString();
                nodeInfo["modifiers"] = method.Modifiers.ToString();
                break;
            case CS.PropertyDeclarationSyntax prop:
                nodeInfo["name"] = prop.Identifier.Text;
                nodeInfo["type"] = prop.Type.ToString();
                nodeInfo["modifiers"] = prop.Modifiers.ToString();
                break;
            case CS.VariableDeclaratorSyntax var:
                nodeInfo["name"] = var.Identifier.Text;
                break;
            case CS.ParameterSyntax param:
                nodeInfo["name"] = param.Identifier.Text;
                nodeInfo["type"] = param.Type?.ToString() ?? "var";
                break;
                
            // VB.NET nodes
            case VB.ClassBlockSyntax vbCls:
                nodeInfo["name"] = vbCls.ClassStatement.Identifier.Text;
                nodeInfo["modifiers"] = vbCls.ClassStatement.Modifiers.ToString();
                break;
            case VB.MethodBlockSyntax vbMethod:
                nodeInfo["name"] = vbMethod.SubOrFunctionStatement.Identifier.Text;
                if (vbMethod.SubOrFunctionStatement is VB.MethodStatementSyntax methodStmt && 
                    methodStmt.AsClause is VB.SimpleAsClauseSyntax simpleAs)
                    nodeInfo["returnType"] = simpleAs.Type.ToString();
                nodeInfo["modifiers"] = vbMethod.SubOrFunctionStatement.Modifiers.ToString();
                break;
            case VB.PropertyBlockSyntax vbProp:
                nodeInfo["name"] = vbProp.PropertyStatement.Identifier.Text;
                if (vbProp.PropertyStatement.AsClause is VB.SimpleAsClauseSyntax propAs)
                    nodeInfo["type"] = propAs.Type.ToString();
                nodeInfo["modifiers"] = vbProp.PropertyStatement.Modifiers.ToString();
                break;
            case VB.ModifiedIdentifierSyntax vbVar:
                nodeInfo["name"] = vbVar.Identifier.Text;
                break;
            case VB.ParameterSyntax vbParam:
                nodeInfo["name"] = vbParam.Identifier.Identifier.Text;
                if (vbParam.AsClause is VB.SimpleAsClauseSyntax paramAs)
                    nodeInfo["type"] = paramAs.Type.ToString();
                break;
        }
        
        // Add trivia if requested
        if (includeTrivia)
        {
            var leadingTrivia = node.GetLeadingTrivia().Select(t => new { kind = t.Kind().ToString(), text = t.ToString() }).ToList();
            var trailingTrivia = node.GetTrailingTrivia().Select(t => new { kind = t.Kind().ToString(), text = t.ToString() }).ToList();
            
            if (leadingTrivia.Any())
                nodeInfo["leadingTrivia"] = leadingTrivia;
            if (trailingTrivia.Any())
                nodeInfo["trailingTrivia"] = trailingTrivia;
        }
        
        // Add children (limit depth to avoid huge output)
        if (depth < 5)
        {
            var children = node.ChildNodes().Select(child => AnalyzeNode(child, includeTrivia, depth + 1)).ToList();
            if (children.Any())
                nodeInfo["children"] = children;
        }
        else if (node.ChildNodes().Any())
        {
            nodeInfo["childCount"] = node.ChildNodes().Count();
            nodeInfo["truncated"] = true;
        }
        
        return nodeInfo;
    }
    
    public async Task<object> GetSymbolsByNameAsync(string filePath, string symbolName, string? workspaceId)
    {
        var workspacesToSearch = GetWorkspacesToSearch(workspaceId);
        if (!workspacesToSearch.Any())
            return CreateErrorResponseWithReloadInstructions("No workspace loaded", workspaceId);
        
        var workspace = workspacesToSearch.First();
        var solution = workspace.CurrentSolution;
        var document = GetDocumentByPath(solution, filePath);
        
        if (document == null)
            return CreateErrorResponse($"File not found: {filePath}");
            
        var syntaxTree = await document.GetSyntaxTreeAsync();
        if (syntaxTree == null)
            return CreateErrorResponse("Could not parse syntax tree");
            
        var root = await syntaxTree.GetRootAsync();
        var semanticModel = await document.GetSemanticModelAsync();
        if (semanticModel == null)
            return CreateErrorResponse("Could not get semantic model");
            
        // Use RoslynPath to find symbols
        var roslynPath = $"//*[@name='{symbolName}']";
        var nodes = RoslynPath.RoslynPath.Find(syntaxTree, roslynPath, semanticModel);
        
        var symbols = new List<object>();
        foreach (var node in nodes)
        {
            var symbol = semanticModel.GetDeclaredSymbol(node) ?? semanticModel.GetSymbolInfo(node).Symbol;
            if (symbol != null)
            {
                symbols.Add(FormatSymbolInfo(symbol, node, syntaxTree));
            }
        }
        
        return new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = symbols.Count > 0 
                        ? $"Found {symbols.Count} symbols matching '{symbolName}':\n\n" + 
                          string.Join("\n\n", symbols.Select(s => JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true })))
                        : $"No symbols found matching '{symbolName}'"
                }
            }
        };
    }
    
    public async Task<object> GetSymbolAtPositionAsync(string filePath, int line, int column, string? workspaceId)
    {
        var workspacesToSearch = GetWorkspacesToSearch(workspaceId);
        if (!workspacesToSearch.Any())
            return CreateErrorResponseWithReloadInstructions("No workspace loaded", workspaceId);
        
        var workspace = workspacesToSearch.First();
        var solution = workspace.CurrentSolution;
        var document = GetDocumentByPath(solution, filePath);
        
        if (document == null)
            return CreateErrorResponse($"File not found: {filePath}");
            
        var syntaxTree = await document.GetSyntaxTreeAsync();
        if (syntaxTree == null)
            return CreateErrorResponse("Could not parse syntax tree");
            
        var sourceText = await document.GetTextAsync();
        var position = sourceText.Lines.GetPosition(new LinePosition(line - 1, column - 1));
        
        var root = await syntaxTree.GetRootAsync();
        var node = root.FindToken(position).Parent;
        
        if (node == null)
            return CreateErrorResponse($"No symbol at position {line}:{column}");
            
        var semanticModel = await document.GetSemanticModelAsync();
        if (semanticModel == null)
            return CreateErrorResponse("Could not get semantic model");
            
        var symbolInfo = semanticModel.GetSymbolInfo(node);
        var symbol = symbolInfo.Symbol ?? semanticModel.GetDeclaredSymbol(node);
        
        if (symbol == null)
            return CreateErrorResponse($"No symbol information at position {line}:{column}");
            
        var info = FormatSymbolInfo(symbol, node, syntaxTree);
        
        return new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true })
                }
            }
        };
    }
    
    public async Task<object> GetAllSymbolsAsync(string filePath, string? workspaceId)
    {
        var workspacesToSearch = GetWorkspacesToSearch(workspaceId);
        if (!workspacesToSearch.Any())
            return CreateErrorResponseWithReloadInstructions("No workspace loaded", workspaceId);
        
        var workspace = workspacesToSearch.First();
        var solution = workspace.CurrentSolution;
        var document = GetDocumentByPath(solution, filePath);
        
        if (document == null)
            return CreateErrorResponse($"File not found: {filePath}");
            
        var syntaxTree = await document.GetSyntaxTreeAsync();
        if (syntaxTree == null)
            return CreateErrorResponse("Could not parse syntax tree");
            
        var root = await syntaxTree.GetRootAsync();
        var semanticModel = await document.GetSemanticModelAsync();
        if (semanticModel == null)
            return CreateErrorResponse("Could not get semantic model");
            
        // Get language handler
        var language = LanguageDetector.GetLanguageFromDocument(document);
        var handler = LanguageHandlerFactory.GetHandler(language);
        
        if (handler == null)
        {
            return CreateErrorResponse($"No language handler found for {language}");
        }
        
        // Find all declaration nodes using language handler
        var declarations = root.DescendantNodes()
            .Where(n => handler.IsTypeDeclaration(n) || 
                       handler.IsMethodDeclaration(n) || 
                       handler.IsPropertyDeclaration(n) || 
                       handler.IsFieldDeclaration(n) ||
                       handler.IsEventDeclaration(n) ||
                       handler.IsConstructor(n) ||
                       IsVariableDeclarator(n, language));
                       
        var symbols = new List<object>();
        foreach (var node in declarations)
        {
            var symbol = semanticModel.GetDeclaredSymbol(node);
            if (symbol != null)
            {
                symbols.Add(FormatSymbolInfo(symbol, node, syntaxTree));
            }
        }
        
        return new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = $"Found {symbols.Count} symbols in {filePath}:\n\n" + 
                          string.Join("\n\n", symbols.Select(s => JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true })))
                }
            }
        };
    }
    
    private object FormatSymbolInfo(ISymbol symbol, SyntaxNode node, SyntaxTree syntaxTree)
    {
        var lineSpan = syntaxTree.GetLineSpan(node.Span);
        var location = $"{syntaxTree.FilePath}:{lineSpan.StartLinePosition.Line + 1}:{lineSpan.StartLinePosition.Character + 1}";
        
        return new
        {
            name = symbol.Name,
            kind = symbol.Kind.ToString(),
            type = symbol.GetType().Name.Replace("Symbol", ""),
            containingType = symbol.ContainingType?.ToDisplayString(),
            containingNamespace = symbol.ContainingNamespace?.ToDisplayString(),
            location = location,
            accessibility = symbol.DeclaredAccessibility.ToString(),
            isStatic = symbol.IsStatic,
            isAbstract = symbol.IsAbstract,
            isVirtual = symbol.IsVirtual,
            isOverride = symbol.IsOverride,
            documentation = GetDocumentationComment(symbol),
            signature = GetSymbolSignature(symbol)
        };
    }
    
    private string? GetDocumentationComment(ISymbol symbol)
    {
        var xml = symbol.GetDocumentationCommentXml();
        if (string.IsNullOrWhiteSpace(xml))
            return null;
            
        // Simple extraction of summary tag
        var start = xml.IndexOf("<summary>");
        var end = xml.IndexOf("</summary>");
        if (start >= 0 && end > start)
        {
            var summary = xml.Substring(start + 9, end - start - 9).Trim();
            // Remove extra whitespace and newlines
            summary = System.Text.RegularExpressions.Regex.Replace(summary, @"\s+", " ");
            return summary;
        }
        
        return null;
    }
    
    private string GetSymbolSignature(ISymbol symbol)
    {
        switch (symbol)
        {
            case IMethodSymbol method:
                return method.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            case IPropertySymbol property:
                return $"{property.Type.ToDisplayString()} {property.Name}";
            case IFieldSymbol field:
                return $"{field.Type.ToDisplayString()} {field.Name}";
            case ITypeSymbol type:
                return type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            default:
                return symbol.ToDisplayString();
        }
    }
    
    /// <summary>
    /// Cleanup timer callback that removes stale workspaces
    /// </summary>
    private void CleanupStaleWorkspaces(object? state)
    {
        if (_disposed) return;
        
        var now = DateTime.UtcNow;
        var staleWorkspaces = _workspaces
            .Where(kvp => kvp.Value.IsStale(_workspaceTimeout))
            .ToList();
            
        foreach (var (workspaceId, entry) in staleWorkspaces)
        {
            try
            {
                _logger.LogInformation("Cleaning up stale workspace: {WorkspaceId} (last accessed: {LastAccess})", 
                    workspaceId, entry.LastAccessTime);
                
                // Store in history for helpful error messages
                _workspaceHistory[workspaceId] = entry.Path;
                
                // Dispose the workspace
                entry.Workspace.Dispose();
                
                // Remove from active workspaces
                _workspaces.Remove(workspaceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up workspace {WorkspaceId}", workspaceId);
            }
        }
        
        // Clean up old history entries (older than 1 hour)
        var staleHistory = _workspaceHistory
            .Where(_ => now - DateTime.UtcNow > _historyTimeout) // This is a simple cleanup, could be more sophisticated
            .ToList();
            
        foreach (var (workspaceId, _) in staleHistory)
        {
            _workspaceHistory.Remove(workspaceId);
        }
        
        if (staleWorkspaces.Any())
        {
            _logger.LogInformation("Cleaned up {Count} stale workspaces", staleWorkspaces.Count);
        }
    }
    
    /// <summary>
    /// Create improved error response with reload instructions
    /// </summary>
    public object CreateErrorResponseWithReloadInstructions(string message, string? workspaceId = null)
    {
        var errorMessage = $"Error: {message}";
        
        if (!string.IsNullOrEmpty(workspaceId) && _workspaceHistory.TryGetValue(workspaceId, out var path))
        {
            errorMessage += $"\n\nWorkspace '{workspaceId}' was previously loaded but has been unloaded due to inactivity.";
            errorMessage += $"\nTo reload it, use: dotnet-load-workspace with path: {path}";
        }
        else if (!_workspaces.Any())
        {
            errorMessage += "\n\nNo workspaces are currently loaded.";
            errorMessage += "\nPlease load a workspace first using: dotnet-load-workspace";
        }
        
        errorMessage += "\n\nTo see available workspaces, use: dotnet-workspace-status";
        
        return new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = errorMessage
                }
            }
        };
    }
    
    /// <summary>
    /// Dispose resources
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        
        _disposed = true;
        
        try
        {
            _cleanupTimer?.Dispose();
            
            // Dispose all workspaces
            foreach (var entry in _workspaces.Values)
            {
                try
                {
                    entry.Workspace.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disposing workspace");
                }
            }
            
            _workspaces.Clear();
            _workspaceHistory.Clear();
            _fsharpTracker.Clear();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during RoslynWorkspaceManager disposal");
        }
    }
}

public class ClassSearchResult
{
    public string Name { get; set; } = "";
    public string FullyQualifiedName { get; set; } = "";
    public string TypeKind { get; set; } = "";
    public string FilePath { get; set; } = "";
    public int Line { get; set; }
    public int Column { get; set; }
    public string ProjectName { get; set; } = "";
    public string? Namespace { get; set; }
}

public class MemberSearchResult
{
    public string MemberName { get; set; } = "";
    public string MemberType { get; set; } = ""; // Method, Property, Field
    public string ClassName { get; set; } = "";
    public string FullyQualifiedClassName { get; set; } = "";
    public string ReturnType { get; set; } = "";
    public string? Parameters { get; set; } // For methods
    public string FilePath { get; set; } = "";
    public int Line { get; set; }
    public int Column { get; set; }
    public string ProjectName { get; set; } = "";
    public bool IsStatic { get; set; }
    public bool IsAsync { get; set; } // For methods
    public string AccessModifier { get; set; } = "";
    public bool? HasGetter { get; set; } // For properties
    public bool? HasSetter { get; set; } // For properties
    public bool? IsReadOnly { get; set; } // For fields
}

public class WorkspaceStatus
{
    public bool IsLoaded { get; set; }
    public int WorkspaceCount { get; set; }
    public List<object> Workspaces { get; set; } = new();
}

public class MethodCallInfo
{
    public string MethodSignature { get; set; } = "";
    public string ClassName { get; set; } = "";
    public string MethodName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public int Line { get; set; }
    public int Column { get; set; }
    public string ProjectName { get; set; } = "";
    public bool IsExternal { get; set; }
}

public class MethodCallAnalysis
{
    public string TargetMethod { get; set; } = "";
    public List<MethodCallInfo> DirectCalls { get; set; } = new();
    public Dictionary<string, List<MethodCallInfo>> CallTree { get; set; } = new();
    public string? Error { get; set; }
}

public class MethodCallerAnalysis
{
    public string TargetMethod { get; set; } = "";
    public List<MethodCallInfo> DirectCallers { get; set; } = new();
    public Dictionary<string, List<MethodCallInfo>> CallerTree { get; set; } = new();
    public string? Error { get; set; }
}

public class ReferenceInfo
{
    public string FilePath { get; set; } = "";
    public int Line { get; set; }
    public int Column { get; set; }
    public string Context { get; set; } = "";
    public string ProjectName { get; set; } = "";
}

public class ImplementationInfo
{
    public string ImplementingType { get; set; } = "";
    public string FilePath { get; set; } = "";
    public int Line { get; set; }
    public int Column { get; set; }
    public string ProjectName { get; set; } = "";
    public string? BaseTypes { get; set; }
}

public class OverrideInfo
{
    public string OverridingType { get; set; } = "";
    public string MethodName { get; set; } = "";
    public string Parameters { get; set; } = "";
    public string FilePath { get; set; } = "";
    public int Line { get; set; }
    public int Column { get; set; }
    public string ProjectName { get; set; } = "";
    public string AccessModifier { get; set; } = "";
    public bool IsSealed { get; set; }
}

public class DerivedTypeInfo
{
    public string DerivedType { get; set; } = "";
    public string BaseType { get; set; } = "";
    public string FilePath { get; set; } = "";
    public int Line { get; set; }
    public int Column { get; set; }
    public string ProjectName { get; set; } = "";
    public bool IsAbstract { get; set; }
    public bool IsSealed { get; set; }
}

public class RenameResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? Warning { get; set; }
    public string? ImpactSummary { get; set; }
    public List<FileChangeInfo> Changes { get; set; } = new();
}


public class FileChangeInfo
{
    public string FilePath { get; set; } = "";
    public List<TextEdit> Edits { get; set; } = new();
}

public class TextEdit
{
    public int Line { get; set; }
    public int Column { get; set; }
    public string OldText { get; set; } = "";
    public string NewText { get; set; } = "";
}

public class PatternFixResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<PatternFix> Fixes { get; set; } = new();
}

public class CodeEditResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? Description { get; set; }
    public string? ModifiedCode { get; set; }
}

public class PatternFix
{
    public string FilePath { get; set; } = "";
    public int Line { get; set; }
    public int Column { get; set; }
    public string OriginalCode { get; set; } = "";
    public string FixedCode { get; set; } = "";
    public string? Description { get; set; }
}

// Statement-level operation classes
public class StatementInfo
{
    public string StatementId { get; set; } = "";
    public string Type { get; set; } = "";
    public string Text { get; set; } = "";
    public Location Location { get; set; } = new();
    public string ContainingMethod { get; set; } = "";
    public string ContainingClass { get; set; } = "";
    public string? SyntaxTag { get; set; }
    public List<string> SemanticTags { get; set; } = new();
    public string? GroupId { get; set; }
}

public class Location
{
    public string File { get; set; } = "";
    public int Line { get; set; }
    public int Column { get; set; }
}

public class FindStatementsResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<StatementInfo> Statements { get; set; } = new();
    public StatementGroup? StatementGroup { get; set; }
}

public class StatementGroup
{
    public string GroupId { get; set; } = "";
    public string Reason { get; set; } = "";
    public List<GroupedStatement> Statements { get; set; } = new();
    public CrossReferences CrossReferences { get; set; } = new();
}

public class GroupedStatement
{
    public string Id { get; set; } = "";
    public string GroupTag { get; set; } = ""; // start, middle, end
    public string Type { get; set; } = "";
    public string Text { get; set; } = "";
    public string? SyntaxTag { get; set; }
    public List<string> SemanticTags { get; set; } = new();
}

public class CrossReferences
{
    public List<SyntaxNodeReference> SyntaxNodes { get; set; } = new();
    public List<SemanticSymbolReference> SemanticSymbols { get; set; } = new();
}

public class SyntaxNodeReference
{
    public string Tag { get; set; } = "";
    public string Type { get; set; } = "";
    public TextSpanInfo Span { get; set; } = new();
}

public class TextSpanInfo
{
    public int Start { get; set; }
    public int Length { get; set; }
}

public class SemanticSymbolReference
{
    public string Tag { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Type { get; set; } = "";
    public string DeclaredAt { get; set; } = "";
    public List<string> UsedAt { get; set; } = new();
}

public class ReplaceStatementResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? ModifiedFile { get; set; }
    public string? OriginalStatement { get; set; }
    public string? NewStatement { get; set; }
    public string? Preview { get; set; }
}

public class InsertStatementResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? ModifiedFile { get; set; }
    public string? InsertedStatement { get; set; }
    public Location? InsertedAt { get; set; }
    public string? Preview { get; set; }
}

public class RemoveStatementResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? ModifiedFile { get; set; }
    public string? RemovedStatement { get; set; }
    public Location? RemovedFrom { get; set; }
    public string? Preview { get; set; }
    public bool WasOnlyStatementInBlock { get; set; }
}

public class MarkerManager
{
    private readonly Dictionary<string, SyntaxAnnotation> _markers = new();
    private readonly Dictionary<string, Document> _markedDocuments = new();
    private int _markerCounter = 0;
    private const string MarkerKind = "MCP.Marker";
    private const int MaxMarkers = 100;
    
    public string CreateMarkerId(string? label = null)
    {
        _markerCounter++;
        return $"mark-{_markerCounter}";
    }
    
    public SyntaxAnnotation CreateMarker(string markerId, string? label = null)
    {
        var data = $"{markerId}|{label ?? ""}|{DateTime.UtcNow:o}";
        var annotation = new SyntaxAnnotation(MarkerKind, data);
        _markers[markerId] = annotation;
        return annotation;
    }
    
    public bool TryGetMarker(string markerId, out SyntaxAnnotation? annotation)
    {
        return _markers.TryGetValue(markerId, out annotation);
    }
    
    public void StoreMarkedDocument(string filePath, Document document)
    {
        _markedDocuments[filePath] = document;
    }
    
    public Document? GetMarkedDocument(string filePath)
    {
        return _markedDocuments.TryGetValue(filePath, out var doc) ? doc : null;
    }
    
    public List<MarkedStatement> FindMarkedStatements(string? markerId = null, string? filePath = null)
    {
        var results = new List<MarkedStatement>();
        
        var documentsToSearch = filePath != null && _markedDocuments.ContainsKey(filePath)
            ? new[] { _markedDocuments[filePath] }
            : _markedDocuments.Values.ToArray();
            
        foreach (var document in documentsToSearch)
        {
            var root = document.GetSyntaxRootAsync().Result;
            if (root == null) continue;
            
            var markedNodes = root.GetAnnotatedNodes(MarkerKind);
            
            foreach (var node in markedNodes.OfType<CS.StatementSyntax>())
            {
                var annotations = node.GetAnnotations(MarkerKind);
                foreach (var annotation in annotations)
                {
                    var data = annotation.Data?.Split('|');
                    if (data == null || data.Length < 1) continue;
                    
                    var nodeMarkerId = data[0];
                    if (markerId != null && nodeMarkerId != markerId) continue;
                    
                    var label = data.Length > 1 ? data[1] : null;
                    var location = node.GetLocation();
                    var lineSpan = location.GetLineSpan();
                    
                    results.Add(new MarkedStatement
                    {
                        MarkerId = nodeMarkerId,
                        Label = string.IsNullOrEmpty(label) ? null : label,
                        Statement = node.ToString(),
                        Location = new Location
                        {
                            File = lineSpan.Path,
                            Line = lineSpan.StartLinePosition.Line + 1,
                            Column = lineSpan.StartLinePosition.Character + 1
                        },
                        Context = GetStatementContext(node)
                    });
                }
            }
        }
        
        return results;
    }
    
    private string GetStatementContext(SyntaxNode statement)
    {
        // Try to find the containing method
        var containingMethod = statement.Ancestors().FirstOrDefault(n => 
            n is CS.MethodDeclarationSyntax || 
            n is VB.MethodBlockSyntax || 
            n is VB.MethodStatementSyntax);
            
        if (containingMethod != null)
        {
            string? methodName = null;
            
            if (containingMethod is CS.MethodDeclarationSyntax csMethod)
            {
                methodName = csMethod.Identifier.Text;
            }
            else if (containingMethod is VB.MethodBlockSyntax vbMethodBlock)
            {
                methodName = vbMethodBlock.SubOrFunctionStatement.Identifier.Text;
            }
            else if (containingMethod is VB.MethodStatementSyntax vbMethodStmt)
            {
                methodName = vbMethodStmt.Identifier.Text;
            }
            
            if (methodName != null)
            {
                return $"Method: {methodName}";
            }
        }
        
        return statement.Parent?.GetType().Name ?? "Unknown";
    }
    
    public bool RemoveMarker(string markerId)
    {
        if (!_markers.TryGetValue(markerId, out var annotation))
            return false;
            
        _markers.Remove(markerId);
        
        // Update all documents to remove this marker
        foreach (var kvp in _markedDocuments.ToList())
        {
            var document = kvp.Value;
            var root = document.GetSyntaxRootAsync().Result;
            if (root == null) continue;
            
            var markedNodes = root.GetAnnotatedNodes(annotation);
            if (!markedNodes.Any()) continue;
            
            var newRoot = root.ReplaceNodes(
                markedNodes,
                (oldNode, newNode) => newNode.WithoutAnnotations(annotation));
                
            _markedDocuments[kvp.Key] = document.WithSyntaxRoot(newRoot);
        }
        
        return true;
    }
    
    public void ClearAllMarkers()
    {
        _markers.Clear();
        _markedDocuments.Clear();
        _markerCounter = 0;
    }
    
    public int MarkerCount => _markers.Count;
    public bool HasCapacity => _markers.Count < MaxMarkers;
}

public class MarkedStatement
{
    public string MarkerId { get; set; } = "";
    public string? Label { get; set; }
    public string Statement { get; set; } = "";
    public Location Location { get; set; } = new();
    public string? Context { get; set; }
}

public class MarkStatementResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? MarkerId { get; set; }
    public string? Label { get; set; }
    public string? MarkedStatement { get; set; }
    public Location? Location { get; set; }
    public string? Context { get; set; }
}

public class FindMarkedStatementsResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<MarkedStatement> MarkedStatements { get; set; } = new();
    public int TotalMarkers { get; set; }
}

public class UnmarkStatementResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? Message { get; set; }
    public int RemainingMarkers { get; set; }
}

public class ClearMarkersResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? Message { get; set; }
    public int ClearedCount { get; set; }
}