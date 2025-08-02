using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.FSharp.Compiler.Syntax;

namespace McpRoslyn.Server.FSharp
{
    /// <summary>
    /// Language handler for F# files
    /// </summary>
    public class FSharpLanguageHandler : ILanguageHandler
    {
        private readonly ILogger<FSharpLanguageHandler> _logger;
        private readonly FSharpWorkspaceManager _workspaceManager;

        public FSharpLanguageHandler(ILogger<FSharpLanguageHandler> logger, FSharpWorkspaceManager workspaceManager)
        {
            _logger = logger;
            _workspaceManager = workspaceManager;
        }

        public string Language => "F#";

        public bool CanHandle(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension == ".fs" || extension == ".fsi" || extension == ".fsx";
        }

        public async Task<IEnumerable<StatementInfo>> FindStatementsAsync(Document document, string pattern, string patternType)
        {
            if (patternType.Equals("fsharppath", StringComparison.OrdinalIgnoreCase))
            {
                return await FindStatementsWithFSharpPathAsync(document, pattern);
            }
            else
            {
                return await FindStatementsWithPatternAsync(document, pattern);
            }
        }

        private async Task<IEnumerable<StatementInfo>> FindStatementsWithFSharpPathAsync(Document document, string fsharpPath)
        {
            try
            {
                var symbols = await _workspaceManager.FindSymbolsAsync(document.FilePath, fsharpPath);
                var statements = new List<StatementInfo>();

                foreach (var symbol in symbols)
                {
                    // Convert F# symbols to statement info
                    var statement = new StatementInfo
                    {
                        Text = $"{symbol.NodeType}: {symbol.Name}",
                        Location = new Location
                        {
                            FilePath = symbol.FilePath,
                            StartLine = 0, // Would need range info from FSharpNode
                            StartColumn = 0,
                            EndLine = 0,
                            EndColumn = 0
                        },
                        Type = MapNodeTypeToStatementType(symbol.NodeType),
                        ParentContext = GetParentContext(symbol)
                    };
                    statements.Add(statement);
                }

                return statements;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding statements with FSharpPath in {Path}", document.FilePath);
                return Enumerable.Empty<StatementInfo>();
            }
        }

        private async Task<IEnumerable<StatementInfo>> FindStatementsWithPatternAsync(Document document, string pattern)
        {
            // Simple pattern matching implementation
            var sourceText = await File.ReadAllTextAsync(document.FilePath);
            var lines = sourceText.Split('\n');
            var statements = new List<StatementInfo>();

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains(pattern))
                {
                    statements.Add(new StatementInfo
                    {
                        Text = lines[i].Trim(),
                        Location = new Location
                        {
                            FilePath = document.FilePath,
                            StartLine = i + 1,
                            StartColumn = 0,
                            EndLine = i + 1,
                            EndColumn = lines[i].Length
                        },
                        Type = DetermineStatementType(lines[i]),
                        ParentContext = ""
                    });
                }
            }

            return statements;
        }

        public async Task<string> ReplaceStatementAsync(Document document, Location location, string newStatement)
        {
            try
            {
                var sourceText = await File.ReadAllTextAsync(document.FilePath);
                var lines = sourceText.Split('\n').ToList();

                if (location.StartLine > 0 && location.StartLine <= lines.Count)
                {
                    // Simple line replacement for now
                    lines[location.StartLine - 1] = newStatement;
                    var newText = string.Join('\n', lines);
                    
                    await File.WriteAllTextAsync(document.FilePath, newText);
                    return "Statement replaced successfully";
                }

                return "Invalid location";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error replacing statement in {Path}", document.FilePath);
                throw;
            }
        }

        public async Task<string> InsertStatementAsync(Document document, string locationPath, string position, string statement)
        {
            try
            {
                // Parse the location path to find insertion point
                var sourceText = await File.ReadAllTextAsync(document.FilePath);
                var lines = sourceText.Split('\n').ToList();

                // Simple implementation - insert at the beginning or end of file
                if (position.Equals("before", StringComparison.OrdinalIgnoreCase))
                {
                    lines.Insert(0, statement);
                }
                else
                {
                    lines.Add(statement);
                }

                var newText = string.Join('\n', lines);
                await File.WriteAllTextAsync(document.FilePath, newText);
                
                return "Statement inserted successfully";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inserting statement in {Path}", document.FilePath);
                throw;
            }
        }

        public async Task<string> RemoveStatementAsync(Document document, Location location)
        {
            try
            {
                var sourceText = await File.ReadAllTextAsync(document.FilePath);
                var lines = sourceText.Split('\n').ToList();

                if (location.StartLine > 0 && location.StartLine <= lines.Count)
                {
                    lines.RemoveAt(location.StartLine - 1);
                    var newText = string.Join('\n', lines);
                    
                    await File.WriteAllTextAsync(document.FilePath, newText);
                    return "Statement removed successfully";
                }

                return "Invalid location";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing statement in {Path}", document.FilePath);
                throw;
            }
        }

        public async Task<IEnumerable<MethodInfo>> FindMethodsAsync(Document document, string pattern)
        {
            try
            {
                var symbols = await _workspaceManager.GetTopLevelSymbolsAsync(document.FilePath);
                var methods = new List<MethodInfo>();

                foreach (var symbol in symbols.Where(s => s.NodeType == FSharpNodeType.Function))
                {
                    if (MatchesPattern(symbol.Name, pattern))
                    {
                        methods.Add(new MethodInfo
                        {
                            Name = symbol.Name,
                            FullName = symbol.Name,
                            Location = new Location
                            {
                                FilePath = symbol.FilePath,
                                StartLine = 0,
                                StartColumn = 0,
                                EndLine = 0,
                                EndColumn = 0
                            },
                            ReturnType = "unknown", // Would need type info
                            Parameters = new List<ParameterInfo>(),
                            IsAsync = symbol.IsAsync,
                            IsStatic = symbol.IsStatic,
                            AccessLevel = symbol.Accessibility
                        });
                    }
                }

                return methods;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding methods in {Path}", document.FilePath);
                return Enumerable.Empty<MethodInfo>();
            }
        }

        public async Task<IEnumerable<ClassInfo>> FindClassesAsync(Document document, string pattern)
        {
            try
            {
                var symbols = await _workspaceManager.GetTopLevelSymbolsAsync(document.FilePath);
                var classes = new List<ClassInfo>();

                foreach (var symbol in symbols.Where(s => s.NodeType == FSharpNodeType.Type))
                {
                    if (MatchesPattern(symbol.Name, pattern))
                    {
                        classes.Add(new ClassInfo
                        {
                            Name = symbol.Name,
                            FullName = symbol.Name,
                            Location = new Location
                            {
                                FilePath = symbol.FilePath,
                                StartLine = 0,
                                StartColumn = 0,
                                EndLine = 0,
                                EndColumn = 0
                            },
                            BaseTypes = new List<string>(),
                            IsAbstract = false,
                            IsStatic = symbol.IsStatic,
                            AccessLevel = symbol.Accessibility
                        });
                    }
                }

                return classes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding classes in {Path}", document.FilePath);
                return Enumerable.Empty<ClassInfo>();
            }
        }

        public async Task<IEnumerable<PropertyInfo>> FindPropertiesAsync(Document document, string pattern)
        {
            try
            {
                var symbols = await _workspaceManager.GetTopLevelSymbolsAsync(document.FilePath);
                var properties = new List<PropertyInfo>();

                foreach (var symbol in symbols.Where(s => s.NodeType == FSharpNodeType.Property || 
                                                         (s.NodeType == FSharpNodeType.Value && !s.IsMutable)))
                {
                    if (MatchesPattern(symbol.Name, pattern))
                    {
                        properties.Add(new PropertyInfo
                        {
                            Name = symbol.Name,
                            FullName = symbol.Name,
                            Location = new Location
                            {
                                FilePath = symbol.FilePath,
                                StartLine = 0,
                                StartColumn = 0,
                                EndLine = 0,
                                EndColumn = 0
                            },
                            Type = "unknown", // Would need type info
                            HasGetter = true,
                            HasSetter = symbol.IsMutable,
                            IsStatic = symbol.IsStatic,
                            AccessLevel = symbol.Accessibility
                        });
                    }
                }

                return properties;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding properties in {Path}", document.FilePath);
                return Enumerable.Empty<PropertyInfo>();
            }
        }

        public Task<string> AnalyzeSyntaxAsync(Document document)
        {
            // F# syntax analysis would be different from C#/VB
            return Task.FromResult("F# syntax analysis not yet implemented");
        }

        public async Task<IEnumerable<SymbolInfo>> GetSymbolsAsync(Document document)
        {
            try
            {
                var symbols = await _workspaceManager.GetTopLevelSymbolsAsync(document.FilePath);
                var result = new List<SymbolInfo>();

                foreach (var symbol in symbols)
                {
                    result.Add(new SymbolInfo
                    {
                        Name = symbol.Name,
                        FullName = symbol.Name,
                        Kind = MapNodeTypeToSymbolKind(symbol.NodeType),
                        Location = new Location
                        {
                            FilePath = symbol.FilePath,
                            StartLine = 0,
                            StartColumn = 0,
                            EndLine = 0,
                            EndColumn = 0
                        }
                    });
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting symbols from {Path}", document.FilePath);
                return Enumerable.Empty<SymbolInfo>();
            }
        }

        public Task<IEnumerable<FixPatternResult>> FixPatternAsync(Document document, string patternName, Dictionary<string, object>? options)
        {
            // F# pattern fixing would be implemented differently
            return Task.FromResult(Enumerable.Empty<FixPatternResult>());
        }

        public Task<string> GetEphemeralMarkerIdAsync(Document document, Location location)
        {
            // F# doesn't use the same marker system
            return Task.FromResult($"fsharp_{Guid.NewGuid():N}");
        }

        public Task<string> MarkStatementAsync(Document document, Location location, string markerId)
        {
            // F# marking would be different
            return Task.FromResult("F# statement marking not yet implemented");
        }

        public Task<IEnumerable<MarkedStatement>> FindMarkedStatementsAsync(Document document, string? markerId)
        {
            // F# marked statements would be tracked differently
            return Task.FromResult(Enumerable.Empty<MarkedStatement>());
        }

        public Task<bool> IsValidStatementLocationAsync(Document document, Location location)
        {
            // Simple validation for now
            return Task.FromResult(location.StartLine > 0);
        }

        public Task<StatementContext?> GetStatementContextAsync(Document document, Location location)
        {
            // F# statement context would be different
            return Task.FromResult<StatementContext?>(null);
        }

        private bool MatchesPattern(string name, string pattern)
        {
            if (pattern.Contains("*") || pattern.Contains("?"))
            {
                var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
                    .Replace("\\*", ".*")
                    .Replace("\\?", ".") + "$";
                return System.Text.RegularExpressions.Regex.IsMatch(name, regexPattern);
            }
            return name.Equals(pattern, StringComparison.OrdinalIgnoreCase);
        }

        private string MapNodeTypeToStatementType(FSharpNodeType nodeType)
        {
            return nodeType switch
            {
                FSharpNodeType.Function => "function",
                FSharpNodeType.Value => "value",
                FSharpNodeType.Type => "type",
                FSharpNodeType.Module => "module",
                FSharpNodeType.Expression => "expression",
                FSharpNodeType.Pattern => "pattern",
                FSharpNodeType.Binding => "binding",
                _ => "unknown"
            };
        }

        private string MapNodeTypeToSymbolKind(FSharpNodeType nodeType)
        {
            return nodeType switch
            {
                FSharpNodeType.Function => "Function",
                FSharpNodeType.Value => "Field",
                FSharpNodeType.Type => "Class",
                FSharpNodeType.Module => "Module",
                FSharpNodeType.Property => "Property",
                FSharpNodeType.Interface => "Interface",
                FSharpNodeType.Enum => "Enum",
                _ => "Unknown"
            };
        }

        private string DetermineStatementType(string line)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("let ")) return "let";
            if (trimmed.StartsWith("type ")) return "type";
            if (trimmed.StartsWith("module ")) return "module";
            if (trimmed.StartsWith("open ")) return "open";
            if (trimmed.StartsWith("match ")) return "match";
            return "expression";
        }

        private string GetParentContext(FSharpSymbolInfo symbol)
        {
            // Would need parent information from the AST
            return "";
        }
    }
}