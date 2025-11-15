# F# Implementation Guide for Spelunk.NET

## Introduction

This guide provides detailed implementation guidance for F# support in the Spelunk.NET. It covers the technical details developers need to understand, extend, or maintain F# functionality.

## Core Components

### 1. FSharpWorkspaceManager

The `FSharpWorkspaceManager` is the central component for F# project management, parallel to `DotnetWorkspaceManager`.

```csharp
public class FSharpWorkspaceManager
{
    private readonly FSharpChecker _checker;
    private readonly Dictionary<string, FSharpProject> _projects;
    private readonly ILogger<FSharpWorkspaceManager> _logger;
    
    public FSharpWorkspaceManager(ILogger<FSharpWorkspaceManager> logger)
    {
        _logger = logger;
        _checker = FSharpChecker.Create(
            projectCacheSize: 200,
            keepAssemblyContents: true,
            keepAllBackgroundResolutions: true
        );
        _projects = new Dictionary<string, FSharpProject>();
    }
    
    public async Task<FSharpProject> LoadProjectAsync(string projectPath)
    {
        // Implementation details below
    }
}
```

### 2. FSharpProjectTracker

Tracks F# projects that couldn't be loaded by MSBuildWorkspace:

```csharp
public class FSharpProjectTracker
{
    private readonly ConcurrentDictionary<string, SkippedProjectInfo> _skippedProjects;
    
    public void OnProjectLoadFailed(Project project, Exception error)
    {
        if (IsFSharpProject(project.FilePath))
        {
            _skippedProjects[project.FilePath] = new SkippedProjectInfo
            {
                ProjectPath = project.FilePath,
                Name = project.Name,
                FailureReason = error.Message,
                DetectedAt = DateTime.UtcNow
            };
        }
    }
    
    private bool IsFSharpProject(string path)
    {
        return path?.EndsWith(".fsproj", StringComparison.OrdinalIgnoreCase) ?? false;
    }
}
```

## FSharpPath Query Language

### Parser Implementation

FSharpPath uses a similar grammar to SpelunkPath but with F#-specific node types:

```csharp
public class FSharpPathParser
{
    private static readonly Dictionary<string, FSharpNodeType> NodeTypes = new()
    {
        ["module"] = FSharpNodeType.Module,
        ["function"] = FSharpNodeType.LetBinding,
        ["type"] = FSharpNodeType.TypeDefinition,
        ["union"] = FSharpNodeType.UnionCase,
        ["record"] = FSharpNodeType.RecordDefinition,
        ["match"] = FSharpNodeType.MatchExpression,
        ["computation"] = FSharpNodeType.ComputationExpression
    };
    
    public FSharpPathExpression Parse(string path)
    {
        // Parse XPath-style expression
        // Handle F#-specific attributes like @recursive, @inline, @mutable
    }
}
```

### Evaluator Implementation

```csharp
public class FSharpPathEvaluator
{
    private readonly FSharpParseFileResults _parseResults;
    private readonly FSharpCheckFileResults _checkResults;
    
    public IEnumerable<FSharpSymbol> Evaluate(FSharpPathExpression expression)
    {
        var visitor = new FSharpAstVisitor(expression);
        visitor.Visit(_parseResults.ParseTree);
        return visitor.Matches;
    }
    
    private class FSharpAstVisitor
    {
        // Traverse F# AST and match against path expression
    }
}
```

### F#-Specific Predicates

```csharp
// Predicate examples
"@recursive"      // Recursive functions
"@inline"         // Inline functions
"@mutable"        // Mutable bindings
"@async"          // Async computations
"@discriminated"  // Discriminated unions
"@active"         // Active patterns
```

## Loading F# Projects

### Project File Parsing

```csharp
public async Task<FSharpProject> LoadProjectAsync(string projectPath)
{
    // 1. Parse .fsproj file
    var projectOptions = ParseProjectFile(projectPath);
    
    // 2. Resolve dependencies
    var references = await ResolveReferencesAsync(projectOptions);
    
    // 3. Create FSharpProjectOptions
    var fsharpOptions = FSharpProjectOptions.Create(
        projectFileName: projectPath,
        projectId: Guid.NewGuid().ToString(),
        sourceFiles: projectOptions.SourceFiles.ToArray(),
        otherOptions: BuildCompilerOptions(projectOptions, references),
        referencedProjects: references.ProjectReferences.ToArray()
    );
    
    // 4. Create and cache project
    var project = new FSharpProject(fsharpOptions, _checker);
    _projects[projectPath] = project;
    
    return project;
}
```

### Handling F# Script Files

```csharp
public async Task<FSharpScriptContext> LoadScriptAsync(string scriptPath)
{
    var source = await File.ReadAllTextAsync(scriptPath);
    
    var projOptions = _checker.GetProjectOptionsFromScript(
        fileName: scriptPath,
        sourceText: SourceText.ofString(source),
        previewEnabled: true,
        loadedTimeStamp: DateTime.Now,
        otherFlags: null,
        useFsiAuxLib: true,
        useSdkRefs: true,
        assumeDotNetFramework: false
    );
    
    return new FSharpScriptContext(scriptPath, projOptions, _checker);
}
```

## Symbol Analysis

### Finding Symbols

```csharp
public async Task<IEnumerable<FSharpSymbolUse>> FindSymbolsAsync(
    string filePath, 
    FSharpPathExpression query)
{
    var project = GetProjectForFile(filePath);
    var parseResults = await _checker.ParseFileInProject(filePath, project.Options);
    var checkResults = await _checker.CheckFileInProject(parseResults, project.Options);
    
    // Use FSharpPathEvaluator to find matching symbols
    var evaluator = new FSharpPathEvaluator(parseResults, checkResults);
    var matches = evaluator.Evaluate(query);
    
    // Convert to unified format
    return matches.Select(ConvertToUnifiedFormat);
}
```

### Type Information Extraction

```csharp
private object ExtractTypeInfo(FSharpMemberOrFunctionOrValue symbol)
{
    return new
    {
        FullType = symbol.FullType.Format(FSharpDisplayContext.Empty),
        GenericParameters = symbol.GenericParameters.Select(p => p.Name),
        IsFunction = symbol.IsFunction,
        IsMutable = symbol.IsMutable,
        IsModule = symbol.IsModule,
        Accessibility = MapAccessibility(symbol.Accessibility),
        Attributes = symbol.Attributes.Select(attr => attr.AttributeType.TypeDefinition.DisplayName)
    };
}
```

## Cross-Language Integration

### Symbol Mapping

Map F# symbols to Roslyn-compatible format:

```csharp
public static class FSharpSymbolMapper
{
    public static UnifiedSymbol MapToUnified(FSharpSymbol fsharpSymbol)
    {
        return fsharpSymbol switch
        {
            FSharpMemberOrFunctionOrValue func when func.IsFunction =>
                new UnifiedSymbol
                {
                    Name = func.DisplayName,
                    Kind = func.IsMember ? SymbolKind.Method : SymbolKind.Function,
                    Type = FormatFunctionType(func),
                    Location = GetLocation(func)
                },
                
            FSharpEntity entity when entity.IsClass =>
                new UnifiedSymbol
                {
                    Name = entity.DisplayName,
                    Kind = SymbolKind.Class,
                    Type = entity.QualifiedName,
                    Location = GetLocation(entity)
                },
                
            // More mappings...
            _ => throw new NotSupportedException($"Unknown F# symbol type: {fsharpSymbol}")
        };
    }
}
```

### Cross-Language References

Finding references from F# to C#/VB.NET:

```csharp
public async Task<IEnumerable<ReferenceLocation>> FindCrossLanguageReferences(
    FSharpSymbol symbol)
{
    // 1. Get the .NET type/member info
    var dotnetInfo = ExtractDotNetInfo(symbol);
    
    // 2. Search in Roslyn workspaces
    var roslynRefs = await _roslynWorkspace.FindReferencesAsync(
        dotnetInfo.AssemblyQualifiedName);
    
    // 3. Search in F# projects
    var fsharpRefs = await FindFSharpReferencesAsync(symbol);
    
    // 4. Merge results
    return MergeReferences(roslynRefs, fsharpRefs);
}
```

## Code Generation

### F# Code Templates

```csharp
public static class FSharpCodeTemplates
{
    public static string GenerateFunction(string name, string[] parameters, string body)
    {
        var paramList = string.Join(" ", parameters);
        return $"let {name} {paramList} =\n    {IndentBody(body)}";
    }
    
    public static string GenerateType(string name, TypeKind kind, string definition)
    {
        return kind switch
        {
            TypeKind.Record => $"type {name} = {{\n{definition}\n}}",
            TypeKind.Union => $"type {name} =\n{definition}",
            TypeKind.Class => $"type {name}() =\n{definition}",
            _ => throw new NotSupportedException()
        };
    }
}
```

### Statement-Level Operations

Adapting statement-level operations for F#'s expression-based nature:

```csharp
public class FSharpStatementAdapter
{
    public FSharpExpr ExtractExpression(FSharpAst ast, Location location)
    {
        // F# doesn't have statements, find the containing expression
        var expr = FindContainingExpression(ast, location);
        
        // For let bindings, include the entire binding
        if (expr.Parent is FSharpLetBinding)
            return expr.Parent;
            
        return expr;
    }
    
    public FSharpAst ReplaceExpression(FSharpAst ast, FSharpExpr oldExpr, FSharpExpr newExpr)
    {
        // Immutable transformation
        return ast.Transform(node =>
            node.Equals(oldExpr) ? newExpr : node
        );
    }
}
```

## Testing F# Support

### Unit Tests

```csharp
[Fact]
public async Task FindRecursiveFunctions_ReturnsCorrectResults()
{
    // Arrange
    var fsharpCode = @"
    let rec factorial n =
        if n <= 1 then 1
        else n * factorial (n - 1)
        
    let iterative n = n * 2
    ";
    
    var manager = new FSharpWorkspaceManager(logger);
    var project = await manager.LoadScriptAsync("test.fsx", fsharpCode);
    
    // Act
    var results = await manager.FindSymbolsAsync(
        "test.fsx", 
        "//function[@recursive]"
    );
    
    // Assert
    Assert.Single(results);
    Assert.Equal("factorial", results.First().Name);
}
```

### Integration Tests

Test cross-language scenarios:

```csharp
[Fact]
public async Task RenameSymbol_UpdatesReferencesInBothLanguages()
{
    // Setup mixed solution with F# library and C# consumer
    var solution = await LoadMixedSolution();
    
    // Find F# symbol
    var fsharpSymbol = await FindSymbol(solution, "FSharpLib.calculate");
    
    // Rename
    var result = await RenameSymbolAsync(fsharpSymbol, "computeValue");
    
    // Verify updates in both F# and C# files
    Assert.Contains("computeValue", await ReadFile("Library.fs"));
    Assert.Contains("computeValue", await ReadFile("Program.cs"));
}
```

## Performance Optimizations

### Caching Strategies

```csharp
public class FSharpProjectCache
{
    private readonly MemoryCache _parseCache;
    private readonly MemoryCache _checkCache;
    
    public async Task<FSharpParseFileResults> GetOrParseAsync(
        string filePath, 
        FSharpProjectOptions options)
    {
        var key = $"{filePath}:{options.ProjectId}";
        
        if (_parseCache.TryGetValue(key, out FSharpParseFileResults cached))
            return cached;
            
        var results = await _checker.ParseFileInProject(filePath, options);
        _parseCache.Set(key, results, TimeSpan.FromMinutes(5));
        
        return results;
    }
}
```

### Incremental Processing

```csharp
public class IncrementalFSharpProcessor
{
    public async Task<FSharpCheckFileResults> CheckFileIncrementally(
        string filePath,
        TextChange change,
        FSharpProjectOptions options)
    {
        // Get previous results
        var previousResults = GetCachedResults(filePath);
        
        // Apply text change
        var newSource = ApplyChange(previousResults.Source, change);
        
        // Incremental check
        return await _checker.CheckFileInProject(
            filePath,
            newSource,
            options,
            cache: true,
            textVersionHash: ComputeHash(newSource)
        );
    }
}
```

## Error Handling

### F#-Specific Errors

```csharp
public class FSharpErrorHandler
{
    public static ErrorResponse HandleFSharpError(Exception ex)
    {
        return ex switch
        {
            FSharpCompilationException fex => new ErrorResponse
            {
                Code = "FS" + fex.ErrorCode,
                Message = fex.Message,
                Details = FormatFSharpErrors(fex.Errors)
            },
            
            TypeProviderException tpex => new ErrorResponse
            {
                Code = "FS_TYPE_PROVIDER",
                Message = "Type provider error: " + tpex.Message,
                Details = tpex.InnerException?.Message
            },
            
            _ => HandleGenericError(ex)
        };
    }
}
```

## Debugging Tips

### Logging F# Operations

```csharp
public class FSharpDiagnostics
{
    private readonly ILogger _logger;
    
    public void LogProjectLoad(string projectPath, FSharpProjectOptions options)
    {
        _logger.LogDebug("Loading F# project: {Path}", projectPath);
        _logger.LogDebug("Source files: {Files}", string.Join(", ", options.SourceFiles));
        _logger.LogDebug("References: {Refs}", string.Join(", ", options.OtherOptions.Where(o => o.StartsWith("-r:"))));
        _logger.LogDebug("Defines: {Defines}", string.Join(", ", options.OtherOptions.Where(o => o.StartsWith("--define:"))));
    }
    
    public void LogAstTraversal(string query, int nodeCount)
    {
        _logger.LogDebug("FSharpPath query: {Query}", query);
        _logger.LogDebug("Traversed {Count} AST nodes", nodeCount);
    }
}
```

### Common Issues

1. **Project Load Failures**
   - Check .NET SDK version compatibility
   - Verify all project references exist
   - Look for type provider initialization errors

2. **Symbol Resolution Issues**
   - Ensure file is part of compilation
   - Check for parse errors preventing semantic analysis
   - Verify FSharpChecker has current source

3. **Performance Problems**
   - Enable caching in FSharpChecker
   - Use incremental parsing when possible
   - Profile AST traversal for complex queries

## Summary

Implementing F# support requires understanding both the technical differences from Roslyn and the architectural patterns needed to provide a unified experience. The key is maintaining separate infrastructure while presenting consistent interfaces to MCP clients.

Focus areas for robust implementation:
- Reliable project loading outside MSBuildWorkspace
- Efficient AST querying with FSharpPath
- Accurate symbol mapping between F# and C#/VB.NET
- Performance optimization through caching
- Comprehensive error handling for F#-specific scenarios