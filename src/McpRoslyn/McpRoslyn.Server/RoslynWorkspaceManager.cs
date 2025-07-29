using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace McpRoslyn.Server;

public class RoslynWorkspaceManager
{
    private readonly ILogger<RoslynWorkspaceManager> _logger;
    private readonly Dictionary<string, Workspace> _workspaces = new();
    private static bool _msBuildRegistered = false;
    
    public RoslynWorkspaceManager(ILogger<RoslynWorkspaceManager> logger)
    {
        _logger = logger;
        
        // Register MSBuild once per process
        if (!_msBuildRegistered)
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
    }
    
    public async Task<(bool success, string message, Workspace? workspace)> LoadWorkspaceAsync(string path)
    {
        try
        {
            _logger.LogInformation("Loading workspace from: {Path}", path);
            
            // Check if already loaded
            if (_workspaces.TryGetValue(path, out var existingWorkspace))
            {
                return (true, "Workspace already loaded", existingWorkspace);
            }
            
            // Determine workspace type
            if (path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
            {
                return await LoadSolutionAsync(path);
            }
            else if (path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) || 
                     path.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase) ||
                     path.EndsWith(".fsproj", StringComparison.OrdinalIgnoreCase))
            {
                return await LoadProjectAsync(path);
            }
            else
            {
                return (false, "Unsupported file type. Please provide a .sln or project file.", null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load workspace");
            return (false, $"Failed to load workspace: {ex.Message}", null);
        }
    }
    
    private async Task<(bool success, string message, Workspace? workspace)> LoadSolutionAsync(string solutionPath)
    {
        var workspace = MSBuildWorkspace.Create();
        
        workspace.WorkspaceFailed += (sender, args) =>
        {
            _logger.LogWarning("Workspace diagnostic: {Kind} - {Message}", args.Diagnostic.Kind, args.Diagnostic.Message);
        };
        
        var solution = await workspace.OpenSolutionAsync(solutionPath);
        _workspaces[solutionPath] = workspace;
        
        var projectCount = solution.Projects.Count();
        _logger.LogInformation("Loaded solution with {Count} projects", projectCount);
        
        return (true, $"Solution loaded successfully with {projectCount} projects", workspace);
    }
    
    private async Task<(bool success, string message, Workspace? workspace)> LoadProjectAsync(string projectPath)
    {
        var workspace = MSBuildWorkspace.Create();
        
        workspace.WorkspaceFailed += (sender, args) =>
        {
            _logger.LogWarning("Workspace diagnostic: {Kind} - {Message}", args.Diagnostic.Kind, args.Diagnostic.Message);
        };
        
        var project = await workspace.OpenProjectAsync(projectPath);
        _workspaces[projectPath] = workspace;
        
        _logger.LogInformation("Loaded project: {Name}", project.Name);
        
        return (true, $"Project '{project.Name}' loaded successfully", workspace);
    }
    
    public WorkspaceStatus GetStatus()
    {
        var statuses = new List<object>();
        
        foreach (var (path, workspace) in _workspaces)
        {
            var solution = workspace.CurrentSolution;
            var projects = solution.Projects.Select(p => new
            {
                name = p.Name,
                language = p.Language,
                documentCount = p.Documents.Count(),
                hasCompilationErrors = false // Will be implemented later
            }).ToList();
            
            statuses.Add(new
            {
                path,
                projectCount = projects.Count,
                projects
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
        return _workspaces.Values;
    }
    
    public Workspace? GetWorkspace(string path)
    {
        return _workspaces.TryGetValue(path, out var workspace) ? workspace : null;
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
            workspacesToSearch.AddRange(_workspaces.Values);
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
                        var typeDeclarations = root.DescendantNodes()
                            .Where(node => node is Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax ||
                                         node is Microsoft.CodeAnalysis.CSharp.Syntax.EnumDeclarationSyntax);
                        
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
                        
                        // Find all method declarations
                        var methodDeclarations = root.DescendantNodes()
                            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>();
                        
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
                        
                        // Find all property declarations
                        var propertyDeclarations = root.DescendantNodes()
                            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.PropertyDeclarationSyntax>();
                        
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
                            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.FieldDeclarationSyntax>();
                        
                        foreach (var fieldDecl in fieldDeclarations)
                        {
                            foreach (var variable in fieldDecl.Declaration.Variables)
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
    
    private List<Workspace> GetWorkspacesToSearch(string? workspacePath)
    {
        var workspacesToSearch = new List<Workspace>();
        if (!string.IsNullOrEmpty(workspacePath))
        {
            var workspace = GetWorkspace(workspacePath);
            if (workspace != null)
                workspacesToSearch.Add(workspace);
        }
        else
        {
            workspacesToSearch.AddRange(_workspaces.Values);
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
        var invocations = methodBody.DescendantNodes().OfType<InvocationExpressionSyntax>();
        
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
                                .OfType<MethodDeclarationSyntax>()
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
                            .OfType<MethodDeclarationSyntax>()
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
                var doc = ws.CurrentSolution.Projects
                    .SelectMany(p => p.Documents)
                    .FirstOrDefault(d => d.FilePath == filePath);
                if (doc != null)
                {
                    targetWorkspace = ws;
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
            .OfType<ClassDeclarationSyntax>()
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
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault();
        
        if (parsedMethod == null)
        {
            // Try parsing as a member declaration
            var member = SyntaxFactory.ParseMemberDeclaration(methodCode);
            if (member is MethodDeclarationSyntax method)
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
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.Text == className);
        
        if (classDeclaration == null)
        {
            result.Error = $"Class '{className}' not found";
            result.Success = false;
            return (null, result);
        }
        
        // Parse the property code
        var parsedProperty = SyntaxFactory.ParseMemberDeclaration(propertyCode) as PropertyDeclarationSyntax;
        
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
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.Text == className);
        
        if (classDeclaration == null)
        {
            result.Error = $"Class '{className}' not found";
            result.Success = false;
            return (null, result);
        }
        
        // Find the method
        var methodDeclaration = classDeclaration.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
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
        if (returnType is PredefinedTypeSyntax predefined && predefined.Keyword.IsKind(SyntaxKind.VoidKeyword))
        {
            // void -> Task
            newMethod = newMethod.WithReturnType(SyntaxFactory.ParseTypeName("Task"));
        }
        else if (returnType is not GenericNameSyntax generic || generic.Identifier.Text != "Task")
        {
            // T -> Task<T>
            newMethod = newMethod.WithReturnType(
                SyntaxFactory.GenericName("Task")
                    .WithTypeArgumentList(
                        SyntaxFactory.TypeArgumentList(
                            SyntaxFactory.SingletonSeparatedList<TypeSyntax>(returnType))));
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
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
        
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
        // Find async methods without await
        if (findPattern == "async-without-await")
        {
            var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
                .Where(m => m.Modifiers.Any(mod => mod.IsKind(SyntaxKind.AsyncKeyword)));
            
            foreach (var method in methods)
            {
                var hasAwait = method.DescendantNodes().OfType<AwaitExpressionSyntax>().Any();
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
        // Find missing await on async calls
        else if (findPattern == "missing-await")
        {
            var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
            
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
    }
    
    private async Task FindNullCheckPatterns(SyntaxNode root, SemanticModel semanticModel, SourceText sourceText, string filePath, string findPattern, string replacePattern, List<PatternFix> fixes)
    {
        // Find old-style null checks and replace with null-conditional operator
        if (findPattern == "if-null-check")
        {
            var ifStatements = root.DescendantNodes().OfType<IfStatementSyntax>();
            
            foreach (var ifStatement in ifStatements)
            {
                // Look for pattern: if (x != null) x.Method()
                if (ifStatement.Condition is BinaryExpressionSyntax binary &&
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
    }
    
    private async Task FindStringFormatPatterns(SyntaxNode root, SemanticModel semanticModel, SourceText sourceText, string filePath, string findPattern, string replacePattern, List<PatternFix> fixes)
    {
        // Find string.Format and replace with interpolation
        if (findPattern == "string.Format")
        {
            var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Where(i => i.Expression.ToString() == "string.Format");
            
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
        
        // Get all statements using Roslyn's built-in traversal
        var statements = root.DescendantNodes()
            .Where(n => n is StatementSyntax)
            .Cast<StatementSyntax>();
        
        // Filter by scope if specified
        if (scope?.ContainsKey("className") == true || scope?.ContainsKey("methodName") == true)
        {
            statements = statements.Where(stmt => IsInScope(stmt, scope));
        }
        
        // Filter nested statements if requested
        if (!includeNestedStatements)
        {
            statements = statements.Where(stmt => !IsNestedStatement(stmt));
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
                var lineSpan = sourceText.Lines.GetLinePositionSpan(statement.Span);
                var containingMethod = statement.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
                var containingClass = statement.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
                
                var statementInfo = new StatementInfo
                {
                    StatementId = $"stmt-{++statementIdCounter.Value}",
                    Type = statement.GetType().Name,
                    Text = statementText,
                    Location = new Location
                    {
                        File = document.FilePath ?? "",
                        Line = lineSpan.Start.Line + 1,
                        Column = lineSpan.Start.Character + 1
                    },
                    ContainingMethod = containingMethod?.Identifier.Text ?? "",
                    ContainingClass = containingClass?.Identifier.Text ?? "",
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
        }
    }
    
    private bool IsInScope(StatementSyntax statement, Dictionary<string, string> scope)
    {
        if (scope.ContainsKey("methodName"))
        {
            var method = statement.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
            if (method?.Identifier.Text != scope["methodName"])
                return false;
        }
        
        if (scope.ContainsKey("className"))
        {
            var type = statement.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
            if (type?.Identifier.Text != scope["className"])
                return false;
        }
        
        return true;
    }
    
    private bool IsNestedStatement(StatementSyntax statement)
    {
        // Check if this statement is inside another statement (like inside if/while/for body)
        var parent = statement.Parent;
        while (parent != null)
        {
            if (parent is BlockSyntax block && block.Parent is StatementSyntax)
                return true;
            if (parent is StatementSyntax && !(parent is BlockSyntax))
                return true;
            parent = parent.Parent;
        }
        return false;
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