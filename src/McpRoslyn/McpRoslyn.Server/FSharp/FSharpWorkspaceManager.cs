using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.FSharp.Compiler;
using Microsoft.FSharp.Compiler.CodeAnalysis;
using Microsoft.FSharp.Compiler.Diagnostics;
using Microsoft.FSharp.Compiler.EditorServices;
using Microsoft.FSharp.Compiler.Syntax;
using Microsoft.FSharp.Compiler.Text;
using Microsoft.FSharp.Core;
using FSharpFunc = Microsoft.FSharp.Core.FSharpFunc<Microsoft.FSharp.Core.Unit, Microsoft.FSharp.Compiler.Diagnostics.FSharpDiagnostic[]>;

namespace McpRoslyn.Server.FSharp
{
    /// <summary>
    /// Manages F# project loading and analysis using FSharp.Compiler.Service
    /// </summary>
    public class FSharpWorkspaceManager
    {
        private readonly ILogger<FSharpWorkspaceManager> _logger;
        private readonly FSharpChecker _checker;
        private readonly Dictionary<string, FSharpProjectInfo> _projects = new();
        private readonly Dictionary<string, FSharpProjectOptions> _projectOptions = new();

        public FSharpWorkspaceManager(ILogger<FSharpWorkspaceManager> logger)
        {
            _logger = logger;
            _checker = FSharpChecker.Create(
                projectCacheSize: FSharpOption<int>.Some(200),
                keepAssemblyContents: FSharpOption<bool>.Some(false),
                keepAllBackgroundResolutions: FSharpOption<bool>.Some(false),
                legacyReferenceResolver: null,
                tryGetMetadataSnapshot: null,
                suggestNamesForErrors: FSharpOption<bool>.Some(true),
                keepAllBackgroundSymbolUses: FSharpOption<bool>.Some(false),
                enableBackgroundItemKeyStoreAndSemanticClassification: FSharpOption<bool>.Some(false),
                enablePartialTypeChecking: FSharpOption<bool>.Some(true),
                parallelReferenceResolution: FSharpOption<bool>.Some(true),
                captureIdentifiersWhenParsing: FSharpOption<bool>.Some(false),
                documentSource: null,
                useSyntaxTreeCache: null,
                useTransparentCompiler: FSharpOption<bool>.Some(false)
            );
        }

        /// <summary>
        /// Load an F# project from a .fsproj file
        /// </summary>
        public async Task<FSharpProjectInfo> LoadProjectAsync(string projectPath)
        {
            try
            {
                _logger.LogInformation("Loading F# project: {Path}", projectPath);

                var projectInfo = new FSharpProjectInfo
                {
                    ProjectPath = projectPath,
                    ProjectName = Path.GetFileNameWithoutExtension(projectPath),
                    DetectedAt = DateTime.UtcNow
                };

                // Create project options from the project file
                var projectOptions = await CreateProjectOptionsAsync(projectPath);
                if (projectOptions == null)
                {
                    projectInfo.LoadError = "Failed to create project options";
                    _logger.LogError("Failed to create project options for {Path}", projectPath);
                    return projectInfo;
                }

                _projectOptions[projectPath] = projectOptions;
                projectInfo.SourceFiles = projectOptions.SourceFiles.ToList();
                projectInfo.References = projectOptions.OtherOptions
                    .Where(opt => opt.StartsWith("-r:"))
                    .Select(opt => opt.Substring(3))
                    .ToList();
                projectInfo.IsLoaded = true;

                _projects[projectPath] = projectInfo;
                _logger.LogInformation("Successfully loaded F# project: {Path}", projectPath);

                return projectInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading F# project: {Path}", projectPath);
                var projectInfo = new FSharpProjectInfo
                {
                    ProjectPath = projectPath,
                    ProjectName = Path.GetFileNameWithoutExtension(projectPath),
                    DetectedAt = DateTime.UtcNow,
                    IsLoaded = false,
                    LoadError = ex.Message
                };
                _projects[projectPath] = projectInfo;
                return projectInfo;
            }
        }

        /// <summary>
        /// Parse an F# source file
        /// </summary>
        public async Task<FSharpParseFileResults?> ParseFileAsync(string filePath, string sourceText)
        {
            try
            {
                var sourceText2 = SourceText.ofString(sourceText);
                var options = GetParsingOptionsForFile(filePath);
                
                var parseResults = await FSharpAsync.StartAsTask(
                    _checker.ParseFile(filePath, sourceText2, options),
                    cancellationToken: default
                );

                if (parseResults.ParseHadErrors)
                {
                    _logger.LogWarning("Parse errors in {File}: {Errors}", 
                        filePath, 
                        string.Join(", ", parseResults.Diagnostics.Select(d => d.Message)));
                }

                return parseResults;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing F# file: {Path}", filePath);
                return null;
            }
        }

        /// <summary>
        /// Get type checking results for a file
        /// </summary>
        public async Task<FSharpCheckFileResults?> CheckFileAsync(string filePath, string sourceText)
        {
            try
            {
                var projectOptions = GetProjectOptionsForFile(filePath);
                if (projectOptions == null)
                {
                    _logger.LogWarning("No project options found for file: {Path}", filePath);
                    return null;
                }

                var sourceText2 = SourceText.ofString(sourceText);
                var parseResults = await FSharpAsync.StartAsTask(
                    _checker.ParseFile(filePath, sourceText2, GetParsingOptionsForFile(filePath)),
                    cancellationToken: default
                );

                if (parseResults.ParseHadErrors)
                {
                    _logger.LogWarning("Parse errors prevented type checking: {Path}", filePath);
                    return null;
                }

                var checkResults = await FSharpAsync.StartAsTask(
                    _checker.CheckFileInProject(parseResults, filePath, 0, sourceText2, projectOptions),
                    cancellationToken: default
                );

                return checkResults;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking F# file: {Path}", filePath);
                return null;
            }
        }

        /// <summary>
        /// Find symbols in an F# file using FSharpPath
        /// </summary>
        public async Task<IEnumerable<FSharpSymbolInfo>> FindSymbolsAsync(string filePath, string fsharpPath)
        {
            try
            {
                var sourceText = await File.ReadAllTextAsync(filePath);
                var parseResults = await ParseFileAsync(filePath, sourceText);
                
                if (parseResults?.ParseTree == null)
                {
                    _logger.LogWarning("No parse tree available for {Path}", filePath);
                    return Enumerable.Empty<FSharpSymbolInfo>();
                }

                var fsharpPathQuery = new FSharpPath(parseResults.ParseTree.Value, sourceText);
                var nodes = fsharpPathQuery.Evaluate(fsharpPath);

                var symbols = new List<FSharpSymbolInfo>();
                foreach (var node in nodes)
                {
                    var symbolInfo = CreateSymbolInfo(node, filePath);
                    if (symbolInfo != null)
                    {
                        symbols.Add(symbolInfo);
                    }
                }

                return symbols;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding symbols in {Path} with query {Query}", filePath, fsharpPath);
                return Enumerable.Empty<FSharpSymbolInfo>();
            }
        }

        /// <summary>
        /// Get all top-level symbols in a file
        /// </summary>
        public async Task<IEnumerable<FSharpSymbolInfo>> GetTopLevelSymbolsAsync(string filePath)
        {
            return await FindSymbolsAsync(filePath, "/module//*[self::function or self::type or self::value]");
        }

        private async Task<FSharpProjectOptions?> CreateProjectOptionsAsync(string projectPath)
        {
            try
            {
                // Read .fsproj file and extract source files and references
                var projectDir = Path.GetDirectoryName(projectPath) ?? "";
                var sourceFiles = new List<string>();
                var references = new List<string>();

                // This is a simplified implementation - in a real scenario, you'd parse the .fsproj XML
                // and extract Compile items and PackageReference/Reference items
                
                // For now, find all .fs files in the project directory
                sourceFiles.AddRange(Directory.GetFiles(projectDir, "*.fs", SearchOption.AllDirectories)
                    .OrderBy(f => f)); // F# compilation order matters!

                // Add standard F# references
                references.Add("FSharp.Core");
                references.Add("mscorlib");
                references.Add("System");
                references.Add("System.Core");

                var otherOptions = new List<string>
                {
                    "--noframework",
                    "--debug:full",
                    "--define:DEBUG",
                    "--optimize-",
                    "--tailcalls-",
                    $"--out:{Path.ChangeExtension(projectPath, ".dll")}",
                    "--target:library",
                    "--warn:3",
                    "--warnaserror:76",
                    "--fullpaths",
                    "--flaterrors",
                    "--highentropyva+",
                    "--targetprofile:netcore"
                };

                // Add references
                foreach (var reference in references)
                {
                    otherOptions.Add($"-r:{reference}");
                }

                return new FSharpProjectOptions(
                    projectFileName: projectPath,
                    projectId: FSharpOption<string>.Some(Guid.NewGuid().ToString()),
                    sourceFiles: sourceFiles.ToArray(),
                    otherOptions: otherOptions.ToArray(),
                    referencedProjects: Array.Empty<FSharpReferencedProject>(),
                    isIncompleteTypeCheckEnvironment: false,
                    useScriptResolutionRules: false,
                    loadTime: DateTime.UtcNow,
                    unresolvedReferences: FSharpOption<FSharpUnresolvedReferencesSet>.None,
                    originalLoadReferences: Array.Empty<string>(),
                    stamp: FSharpOption<long>.Some(DateTime.UtcNow.Ticks)
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating project options for {Path}", projectPath);
                return null;
            }
        }

        private FSharpParsingOptions GetParsingOptionsForFile(string filePath)
        {
            return new FSharpParsingOptions(
                sourceFiles: new[] { filePath },
                conditionalDefines: new[] { "DEBUG" },
                diagnosticOptions: FSharpDiagnosticOptions.Default,
                langVersionText: FSharpOption<string>.None,
                isInteractive: false,
                lightSyntax: FSharpOption<bool>.Some(true),
                compilingFSharpCore: false,
                isExe: false
            );
        }

        private FSharpProjectOptions? GetProjectOptionsForFile(string filePath)
        {
            // Find the project that contains this file
            foreach (var (projectPath, options) in _projectOptions)
            {
                if (options.SourceFiles.Contains(filePath))
                {
                    return options;
                }
            }

            // Try to find project in the same directory hierarchy
            var dir = Path.GetDirectoryName(filePath);
            while (!string.IsNullOrEmpty(dir))
            {
                var fsprojFiles = Directory.GetFiles(dir, "*.fsproj");
                if (fsprojFiles.Length > 0)
                {
                    var projectPath = fsprojFiles[0];
                    if (_projectOptions.TryGetValue(projectPath, out var options))
                    {
                        return options;
                    }
                }
                dir = Path.GetDirectoryName(dir);
            }

            return null;
        }

        private FSharpSymbolInfo? CreateSymbolInfo(FSharpNode node, string filePath)
        {
            var name = node.GetName();
            if (string.IsNullOrEmpty(name))
                return null;

            return new FSharpSymbolInfo
            {
                Name = name,
                NodeType = node.NodeType,
                FilePath = filePath,
                IsAsync = node.IsAsync,
                IsStatic = node.IsStatic,
                IsMutable = node.IsMutable,
                IsRecursive = node.IsRecursive,
                IsInline = node.IsInline,
                Accessibility = node.GetAccessibility() ?? "public"
            };
        }

        public IReadOnlyDictionary<string, FSharpProjectInfo> GetProjects() => _projects;

        public FSharpChecker GetChecker() => _checker;
    }

    /// <summary>
    /// Information about an F# symbol
    /// </summary>
    public class FSharpSymbolInfo
    {
        public string Name { get; set; } = string.Empty;
        public FSharpNodeType NodeType { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public bool IsAsync { get; set; }
        public bool IsStatic { get; set; }
        public bool IsMutable { get; set; }
        public bool IsRecursive { get; set; }
        public bool IsInline { get; set; }
        public string Accessibility { get; set; } = "public";
    }
}