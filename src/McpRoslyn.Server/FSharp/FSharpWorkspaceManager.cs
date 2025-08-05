using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FSharp.Compiler.CodeAnalysis;
using FSharp.Compiler.Diagnostics;
using FSharp.Compiler.Symbols;
using FSharp.Compiler.Syntax;
using FSharp.Compiler.Text;
using Microsoft.Extensions.Logging;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Control;
using Microsoft.FSharp.Core;

namespace McpRoslyn.Server.FSharp;

/// <summary>
/// Manages F# workspaces using FSharp.Compiler.Service.
/// Provides parsing, type checking, and symbol analysis for F# code.
/// </summary>
public class FSharpWorkspaceManager : IDisposable
{
    private readonly ILogger<FSharpWorkspaceManager> _logger;
    private readonly FSharpChecker _checker;
    private readonly Dictionary<string, FSharpProjectOptions> _projectOptionsCache;
    private readonly Dictionary<string, (DateTime timestamp, FSharpParseFileResults parse, FSharpCheckFileResults check)> _fileCache;

    public FSharpWorkspaceManager(ILogger<FSharpWorkspaceManager> logger)
    {
        _logger = logger;
        _checker = FSharpChecker.Create(
            projectCacheSize: FSharpOption<int>.Some(200),
            keepAssemblyContents: FSharpOption<bool>.Some(true),
            keepAllBackgroundResolutions: FSharpOption<bool>.Some(true),
            legacyReferenceResolver: null,
            tryGetMetadataSnapshot: null,
            suggestNamesForErrors: FSharpOption<bool>.Some(true),
            keepAllBackgroundSymbolUses: FSharpOption<bool>.Some(true),
            enableBackgroundItemKeyStoreAndSemanticClassification: FSharpOption<bool>.Some(true),
            enablePartialTypeChecking: FSharpOption<bool>.Some(true),
            parallelReferenceResolution: FSharpOption<bool>.Some(true),
            captureIdentifiersWhenParsing: FSharpOption<bool>.Some(true),
            documentSource: null,
            useSyntaxTreeCache: FSharpOption<bool>.None,
            useTransparentCompiler: FSharpOption<bool>.Some(false));
            
        _projectOptionsCache = new Dictionary<string, FSharpProjectOptions>();
        _fileCache = new Dictionary<string, (DateTime, FSharpParseFileResults, FSharpCheckFileResults)>();
    }

    /// <summary>
    /// Loads an F# project from an .fsproj file.
    /// </summary>
    public async Task<(bool success, string message, FSharpProjectInfo? projectInfo)> LoadProjectAsync(string projectPath)
    {
        try
        {
            if (!File.Exists(projectPath))
            {
                return (false, $"Project file not found: {projectPath}", null);
            }

            if (!projectPath.EndsWith(".fsproj", StringComparison.OrdinalIgnoreCase))
            {
                return (false, $"Not an F# project file: {projectPath}", null);
            }

            _logger.LogInformation("Loading F# project: {ProjectPath}", projectPath);

            // Get project options from the project file
            var projectOptions = await GetProjectOptionsAsync(projectPath);
            if (projectOptions == null)
            {
                return (false, "Failed to get project options", null);
            }

            _projectOptionsCache[projectPath] = projectOptions;

            // Create project info
            var projectInfo = new FSharpProjectInfo
            {
                ProjectPath = projectPath,
                ProjectName = Path.GetFileNameWithoutExtension(projectPath),
                SourceFiles = projectOptions.SourceFiles.ToList(),
                References = projectOptions.OtherOptions
                    .Where(opt => opt.StartsWith("-r:"))
                    .Select(opt => opt.Substring(3))
                    .ToList(),
                TargetFramework = ExtractTargetFramework(projectOptions.OtherOptions)
            };

            _logger.LogInformation("Successfully loaded F# project with {FileCount} files", projectInfo.SourceFiles.Count);
            return (true, "Project loaded successfully", projectInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading F# project: {ProjectPath}", projectPath);
            return (false, $"Error loading project: {ex.Message}", null);
        }
    }

    /// <summary>
    /// Parses and type-checks an F# file.
    /// </summary>
    public async Task<(bool success, FSharpParseFileResults? parseResults, FSharpCheckFileResults? checkResults, FSharpDiagnostic[]? diagnostics)> 
        ParseAndCheckFileAsync(string filePath, string? sourceText = null)
    {
        try
        {
            if (!FSharpFileDetector.IsFSharpFile(filePath))
            {
                return (false, null, null, null);
            }

            // Get or read source text
            var source = sourceText ?? await File.ReadAllTextAsync(filePath);
            var sourceTextObj = SourceText.ofString(source);

            // Check cache
            if (_fileCache.TryGetValue(filePath, out var cached))
            {
                var fileInfo = new FileInfo(filePath);
                if (cached.timestamp >= fileInfo.LastWriteTime && sourceText == null)
                {
                    return (true, cached.parse, cached.check, Array.Empty<FSharpDiagnostic>());
                }
            }

            // Get project options for the file
            var projectOptions = GetProjectOptionsForFile(filePath);
            if (projectOptions == null)
            {
                // Create default options if no project
                projectOptions = CreateDefaultProjectOptions(filePath);
            }

            // Get parsing options from project options
            var parsingOptions = _checker.GetParsingOptionsFromProjectOptions(projectOptions).Item1;
            
            // Parse the file
            var parseResults = await FSharpAsync.StartAsTask(
                _checker.ParseFile(
                    filePath,
                    sourceTextObj,
                    parsingOptions,
                    cache: FSharpOption<bool>.Some(true),
                    userOpName: FSharpOption<string>.Some("Parse")
                ),
                null, null);

            // Type check the file
            var checkAnswer = await FSharpAsync.StartAsTask(
                _checker.CheckFileInProject(
                    parseResults, 
                    filePath, 
                    0, 
                    sourceTextObj, 
                    projectOptions,
                    userOpName: FSharpOption<string>.Some("Check")
                ),
                null, null);

            if (checkAnswer is FSharpCheckFileAnswer.Succeeded succeeded)
            {
                var checkResults = succeeded.Item;
                var diagnostics = checkResults.Diagnostics;

                // Update cache
                _fileCache[filePath] = (DateTime.Now, parseResults, checkResults);

                return (true, parseResults, checkResults, diagnostics);
            }
            else
            {
                _logger.LogWarning("Type checking aborted for file: {FilePath}", filePath);
                return (false, parseResults, null, null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing/checking F# file: {FilePath}", filePath);
            return (false, null, null, null);
        }
    }

    /// <summary>
    /// Finds symbols in an F# file matching a pattern.
    /// </summary>
    public async Task<List<FSharpSymbolInfo>> FindSymbolsAsync(string filePath, string pattern)
    {
        var symbols = new List<FSharpSymbolInfo>();

        var (success, _, checkResults, _) = await ParseAndCheckFileAsync(filePath);
        if (!success || checkResults == null)
        {
            return symbols;
        }

        // Get all symbols in the file
        var symbolUses = checkResults.GetAllUsesOfAllSymbolsInFile(FSharpOption<System.Threading.CancellationToken>.None);
        
        foreach (var symbolUse in symbolUses)
        {
            if (symbolUse.IsFromDefinition)
            {
                var symbol = symbolUse.Symbol;
                var displayName = symbol.DisplayName;

                // Check if symbol matches pattern (simple wildcard support)
                if (MatchesPattern(displayName, pattern))
                {
                    var range = symbolUse.Range;
                    symbols.Add(new FSharpSymbolInfo
                    {
                        Name = displayName,
                        FullName = symbol.FullName,
                        Kind = GetSymbolKind(symbol),
                        FilePath = filePath,
                        StartLine = range.StartLine,
                        StartColumn = range.StartColumn,
                        EndLine = range.EndLine,
                        EndColumn = range.EndColumn,
                        Documentation = GetSymbolDocumentation(symbol)
                    });
                }
            }
        }

        return symbols;
    }

    private Task<FSharpProjectOptions?> GetProjectOptionsAsync(string projectPath)
    {
        try
        {
            // For now, create basic project options
            // In a full implementation, this would parse the .fsproj file
            var projectDirectory = Path.GetDirectoryName(projectPath) ?? "";
            var sourceFiles = Directory.GetFiles(projectDirectory, "*.fs", SearchOption.AllDirectories)
                .Where(f => !f.Contains("obj") && !f.Contains("bin"))
                .OrderBy(f => f)
                .ToArray();

            var otherOptions = new[]
            {
                "--target:library",
                "--nowarn:52", // Defensive copy warnings
                "--nowarn:57", // Experimental syntax
                $"--out:{Path.Combine(projectDirectory, "bin", "Debug", Path.GetFileNameWithoutExtension(projectPath) + ".dll")}",
            };

            return Task.FromResult<FSharpProjectOptions?>(new FSharpProjectOptions(
                projectFileName: projectPath,
                projectId: null,
                sourceFiles: sourceFiles,
                otherOptions: otherOptions,
                referencedProjects: Array.Empty<FSharpReferencedProject>(),
                isIncompleteTypeCheckEnvironment: false,
                useScriptResolutionRules: false,
                loadTime: DateTime.Now,
                unresolvedReferences: null,
                originalLoadReferences: ListModule.Empty<Tuple<global::FSharp.Compiler.Text.Range, string, string>>(),
                stamp: null
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting project options for: {ProjectPath}", projectPath);
            return Task.FromResult<FSharpProjectOptions?>(null);
        }
    }

    private FSharpProjectOptions? GetProjectOptionsForFile(string filePath)
    {
        // Find project containing this file
        foreach (var (projectPath, options) in _projectOptionsCache)
        {
            if (options.SourceFiles.Contains(filePath))
            {
                return options;
            }
        }
        return null;
    }

    private FSharpProjectOptions CreateDefaultProjectOptions(string filePath)
    {
        return new FSharpProjectOptions(
            projectFileName: "temp.fsproj",
            projectId: null,
            sourceFiles: new[] { filePath },
            otherOptions: new[] { "--target:library", "--nowarn:52", "--nowarn:57" },
            referencedProjects: Array.Empty<FSharpReferencedProject>(),
            isIncompleteTypeCheckEnvironment: true,
            useScriptResolutionRules: Path.GetExtension(filePath).Equals(".fsx", StringComparison.OrdinalIgnoreCase),
            loadTime: DateTime.Now,
            unresolvedReferences: null,
            originalLoadReferences: ListModule.Empty<Tuple<global::FSharp.Compiler.Text.Range, string, string>>(),
            stamp: null
        );
    }

    private string ExtractTargetFramework(string[] otherOptions)
    {
        // Look for target framework in options
        var tfm = otherOptions.FirstOrDefault(opt => opt.Contains("netstandard") || opt.Contains("netcoreapp") || opt.Contains("net"));
        return tfm ?? "net8.0";
    }

    private bool MatchesPattern(string name, string pattern)
    {
        if (pattern == "*") return true;
        if (pattern.StartsWith("*") && pattern.EndsWith("*"))
        {
            return name.Contains(pattern.Trim('*'), StringComparison.OrdinalIgnoreCase);
        }
        if (pattern.StartsWith("*"))
        {
            return name.EndsWith(pattern.TrimStart('*'), StringComparison.OrdinalIgnoreCase);
        }
        if (pattern.EndsWith("*"))
        {
            return name.StartsWith(pattern.TrimEnd('*'), StringComparison.OrdinalIgnoreCase);
        }
        return name.Equals(pattern, StringComparison.OrdinalIgnoreCase);
    }

    private string GetSymbolKind(FSharpSymbol symbol)
    {
        return symbol switch
        {
            FSharpMemberOrFunctionOrValue m when m.IsModuleValueOrMember && !m.IsProperty => "Function",
            FSharpMemberOrFunctionOrValue m when m.IsProperty => "Property",
            FSharpMemberOrFunctionOrValue m when m.IsMember => "Method",
            FSharpMemberOrFunctionOrValue _ => "Value",
            FSharpEntity e when e.IsClass => "Class",
            FSharpEntity e when e.IsInterface => "Interface",
            FSharpEntity e when e.IsFSharpModule => "Module",
            FSharpEntity e when e.IsFSharpUnion => "Union",
            FSharpEntity e when e.IsFSharpRecord => "Record",
            FSharpEntity e when e.IsEnum => "Enum",
            FSharpEntity _ => "Type",
            FSharpGenericParameter _ => "TypeParameter",
            FSharpUnionCase _ => "UnionCase",
            FSharpField _ => "Field",
            _ => "Unknown"
        };
    }

    private string? GetSymbolDocumentation(FSharpSymbol symbol)
    {
        // F# documentation is not easily accessible through the API
        // In a full implementation, we would extract from XML doc files
        return null;
    }

    public void Dispose()
    {
        // FSharpChecker doesn't need explicit disposal
        _projectOptionsCache.Clear();
        _fileCache.Clear();
    }
}

/// <summary>
/// Information about an F# project.
/// </summary>
public class FSharpProjectInfo
{
    public string ProjectPath { get; set; } = "";
    public string ProjectName { get; set; } = "";
    public List<string> SourceFiles { get; set; } = new();
    public List<string> References { get; set; } = new();
    public string TargetFramework { get; set; } = "";
}

/// <summary>
/// Information about an F# symbol.
/// </summary>
public class FSharpSymbolInfo
{
    public string Name { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Kind { get; set; } = "";
    public string FilePath { get; set; } = "";
    public int StartLine { get; set; }
    public int StartColumn { get; set; }
    public int EndLine { get; set; }
    public int EndColumn { get; set; }
    public string? Documentation { get; set; }
}