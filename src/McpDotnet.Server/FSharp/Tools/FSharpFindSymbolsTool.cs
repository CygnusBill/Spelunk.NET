using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Spelunk.Server.FSharp.Tools;

/// <summary>
/// MCP tool for finding symbols in F# code.
/// </summary>
public class FSharpFindSymbolsTool
{
    private readonly FSharpWorkspaceManager _workspaceManager;
    private readonly ILogger<FSharpFindSymbolsTool> _logger;

    public FSharpFindSymbolsTool(FSharpWorkspaceManager workspaceManager, ILogger<FSharpFindSymbolsTool> logger)
    {
        _workspaceManager = workspaceManager;
        _logger = logger;
    }


    public async Task<object> ExecuteAsync(string pattern, string? filePath = null, string? kind = null)
    {
        try
        {
            _logger.LogInformation("Finding F# symbols with pattern: {Pattern}", pattern);

            if (string.IsNullOrEmpty(filePath))
            {
                return new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = "File path is required for F# symbol search. Please specify the 'file' parameter."
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

            var symbols = await _workspaceManager.FindSymbolsAsync(filePath, pattern);

            // Filter by kind if specified
            if (!string.IsNullOrEmpty(kind))
            {
                symbols = symbols.Where(s => s.Kind.Equals(kind, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (symbols.Count == 0)
            {
                return new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = $"No symbols found matching pattern '{pattern}'" + 
                                   (kind != null ? $" with kind '{kind}'" : "") +
                                   $" in file {filePath}"
                        }
                    }
                };
            }

            var result = $"# Found {symbols.Count} F# symbol{(symbols.Count != 1 ? "s" : "")} matching '{pattern}'";
            if (kind != null)
            {
                result += $" (kind: {kind})";
            }
            result += "\n\n";

            foreach (var symbol in symbols.OrderBy(s => s.Name))
            {
                result += $"## {symbol.Kind}: {symbol.Name}\n";
                result += $"- **Full Name:** {symbol.FullName}\n";
                result += $"- **Location:** {symbol.FilePath}:{symbol.StartLine}:{symbol.StartColumn}\n";
                if (!string.IsNullOrEmpty(symbol.Documentation))
                {
                    result += $"- **Documentation:** {symbol.Documentation}\n";
                }
                result += "\n";
            }

            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = result
                    }
                },
                symbols = symbols.Select(s => new
                {
                    name = s.Name,
                    fullName = s.FullName,
                    kind = s.Kind,
                    location = new
                    {
                        file = s.FilePath,
                        line = s.StartLine,
                        column = s.StartColumn
                    }
                })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding F# symbols");
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = $"Error finding F# symbols: {ex.Message}"
                    }
                },
                isError = true
            };
        }
    }
}