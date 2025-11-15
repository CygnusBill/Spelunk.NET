using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FSharp.Compiler.Syntax;
using McpDotnet.Server.FSharp.FSharpPath;
using Microsoft.Extensions.Logging;
using Microsoft.FSharp.Collections;

namespace McpDotnet.Server.FSharp.Tools;

/// <summary>
/// MCP tool for querying F# code using FSharpPath.
/// </summary>
public class FSharpQueryTool
{
    private readonly FSharpWorkspaceManager _workspaceManager;
    private readonly ILogger<FSharpQueryTool> _logger;
    private readonly FSharpPathEvaluator _evaluator;

    public FSharpQueryTool(FSharpWorkspaceManager workspaceManager, ILogger<FSharpQueryTool> logger)
    {
        _workspaceManager = workspaceManager;
        _logger = logger;
        _evaluator = new FSharpPathEvaluator();
    }

    public async Task<object> ExecuteAsync(string fsharpPath, string? filePath = null, bool includeContext = false, int contextLines = 2)
    {
        try
        {
            _logger.LogInformation("Querying F# code with FSharpPath: {Query}", fsharpPath);

            if (string.IsNullOrEmpty(filePath))
            {
                return new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = "File path is required for F# queries. Please specify the 'file' parameter."
                        }
                    },
                    isError = true
                };
            }

            if (!FSharpFileDetector.IsFSharpFile(filePath))
            {
                return new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = $"Not an F# file: {filePath}"
                        }
                    },
                    isError = true
                };
            }

            // Parse and check the file
            var (success, parseResults, checkResults, diagnostics) = await _workspaceManager.ParseAndCheckFileAsync(filePath);
            
            if (!success || parseResults == null)
            {
                return new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = $"Failed to parse F# file: {filePath}"
                        }
                    },
                    isError = true
                };
            }

            // Evaluate the FSharpPath query
            var results = _evaluator.Evaluate(fsharpPath, parseResults.ParseTree);

            if (results.Count == 0)
            {
                return new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = $"No matches found for FSharpPath query: {fsharpPath}"
                        }
                    },
                    nodes = new object[0]
                };
            }

            // Format results
            var formattedResults = FormatResults(results, filePath, includeContext, contextLines);

            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = $"# FSharpPath Query Results\n\nQuery: `{fsharpPath}`\nFile: {filePath}\nMatches: {results.Count}\n\n{formattedResults}"
                    }
                },
                nodes = results.Select(r => FormatNode(r)).ToArray()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing FSharpPath query");
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = $"Error executing FSharpPath query: {ex.Message}"
                    }
                },
                isError = true
            };
        }
    }

    private string FormatResults(List<object> results, string filePath, bool includeContext, int contextLines)
    {
        var formatted = new List<string>();

        foreach (var result in results)
        {
            var nodeInfo = FormatNodeInfo(result);
            formatted.Add(nodeInfo);

            if (includeContext)
            {
                var context = GetNodeContext(result, filePath, contextLines);
                if (!string.IsNullOrEmpty(context))
                {
                    formatted.Add(context);
                }
            }

            formatted.Add(""); // Empty line between results
        }

        return string.Join("\n", formatted);
    }

    private string FormatNodeInfo(object node)
    {
        return node switch
        {
            SynBinding binding => FormatBinding(binding),
            SynTypeDefn typeDef => FormatTypeDefn(typeDef),
            SynModuleOrNamespace module => FormatModule(module),
            SynExpr expr => FormatExpression(expr),
            SynPat pattern => FormatPattern(pattern),
            _ => $"## {node.GetType().Name}"
        };
    }

    private string FormatBinding(SynBinding binding)
    {
        var name = GetBindingName(binding) ?? "anonymous";
        var bindingType = IsFunctionBinding(binding) ? "Function" : "Value";
        var range = binding.range;
        
        return $"## {bindingType}: {name}\n" +
               $"- Location: {range.FileName}:{range.StartLine}:{range.StartColumn}";
    }

    private string FormatTypeDefn(SynTypeDefn typeDef)
    {
        var name = GetTypeDefnName(typeDef) ?? "anonymous";
        var typeKind = GetTypeKind(typeDef);
        var range = typeDef.range;
        
        return $"## Type: {name} ({typeKind})\n" +
               $"- Location: {range.FileName}:{range.StartLine}:{range.StartColumn}";
    }

    private string FormatModule(SynModuleOrNamespace module)
    {
        var name = string.Join(".", module.longId.Select(id => id.idText));
        var range = module.range;
        
        return $"## Module: {name}\n" +
               $"- Location: {range.FileName}:{range.StartLine}:{range.StartColumn}";
    }

    private string FormatExpression(SynExpr expr)
    {
        var exprType = expr.GetType().Name.Replace("SynExpr+", "");
        var range = expr.Range;
        
        return $"## Expression: {exprType}\n" +
               $"- Location: {range.FileName}:{range.StartLine}:{range.StartColumn}";
    }

    private string FormatPattern(SynPat pattern)
    {
        var patternType = pattern.GetType().Name.Replace("SynPat+", "");
        var range = pattern.Range;
        
        return $"## Pattern: {patternType}\n" +
               $"- Location: {range.FileName}:{range.StartLine}:{range.StartColumn}";
    }

    private object FormatNode(object node)
    {
        var range = GetNodeRange(node);
        
        return new
        {
            type = node.GetType().Name,
            name = GetNodeName(node),
            location = range != null ? new
            {
                file = range.Value.FileName,
                startLine = range.Value.StartLine,
                startColumn = range.Value.StartColumn,
                endLine = range.Value.EndLine,
                endColumn = range.Value.EndColumn
            } : null
        };
    }

    private string GetNodeContext(object node, string filePath, int contextLines)
    {
        var range = GetNodeRange(node);
        if (range == null) return "";

        try
        {
            var lines = System.IO.File.ReadAllLines(filePath);
            var startLine = Math.Max(1, range.Value.StartLine - contextLines);
            var endLine = Math.Min(lines.Length, range.Value.EndLine + contextLines);

            var contextBuilder = new System.Text.StringBuilder();
            contextBuilder.AppendLine("```fsharp");

            for (int i = startLine; i <= endLine; i++)
            {
                if (i - 1 < lines.Length)
                {
                    var prefix = (i >= range.Value.StartLine && i <= range.Value.EndLine) ? ">" : " ";
                    contextBuilder.AppendLine($"{prefix} {i,4}: {lines[i - 1]}");
                }
            }

            contextBuilder.AppendLine("```");
            return contextBuilder.ToString();
        }
        catch
        {
            return "";
        }
    }

    private global::FSharp.Compiler.Text.Range? GetNodeRange(object node)
    {
        return node switch
        {
            SynBinding binding => binding.range,
            SynTypeDefn typeDef => typeDef.range,
            SynModuleOrNamespace module => module.range,
            SynExpr expr => expr.Range,
            SynPat pattern => pattern.Range,
            _ => null
        };
    }

    private string? GetNodeName(object node)
    {
        return node switch
        {
            SynBinding binding => GetBindingName(binding),
            SynTypeDefn typeDef => GetTypeDefnName(typeDef),
            SynModuleOrNamespace module => string.Join(".", module.longId.Select(id => id.idText)),
            _ => null
        };
    }

    private string? GetBindingName(SynBinding binding)
    {
        return binding.headPat switch
        {
            SynPat.Named named => named.ident.ident.idText,
            SynPat.LongIdent longIdent => longIdent.longDotId.LongIdent.Last().idText,
            _ => null
        };
    }

    private string? GetTypeDefnName(SynTypeDefn typeDef)
    {
        var typeInfo = typeDef.typeInfo;
        return typeInfo.longId.Last().idText;
    }

    private bool IsFunctionBinding(SynBinding binding)
    {
        return binding.valData is SynValData valData &&
               !ListModule.IsEmpty(valData.SynValInfo.CurriedArgInfos);
    }

    private string GetTypeKind(SynTypeDefn typeDef)
    {
        return typeDef.typeRepr switch
        {
            SynTypeDefnRepr.Simple simple => simple.simpleRepr switch
            {
                SynTypeDefnSimpleRepr.Union => "Union",
                SynTypeDefnSimpleRepr.Record => "Record",
                SynTypeDefnSimpleRepr.Enum => "Enum",
                SynTypeDefnSimpleRepr.TypeAbbrev => "Type Abbreviation",
                _ => "Simple Type"
            },
            SynTypeDefnRepr.ObjectModel => "Class/Interface",
            _ => "Type"
        };
    }
}