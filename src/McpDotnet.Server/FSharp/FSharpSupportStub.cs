using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace McpDotnet.Server.FSharp;

/// <summary>
/// Stub implementation for F# support that provides consistent "not implemented" responses
/// while maintaining the interface structure needed for future implementation.
/// </summary>
public class FSharpSupportStub
{
    private readonly ILogger<FSharpSupportStub> _logger;

    public FSharpSupportStub(ILogger<FSharpSupportStub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Stub for finding methods in F# files.
    /// </summary>
    public Task<object> FindMethodStubAsync(string filePath, string pattern)
    {
        _logger.LogInformation("F# find-method requested for {File} with pattern {Pattern}", filePath, pattern);
        
        return Task.FromResult<object>(new
        {
            success = false,
            message = FSharpFileDetector.GetFSharpNotSupportedMessage("find-method", filePath),
            info = new
            {
                requestedFile = filePath,
                requestedPattern = pattern,
                documentationLink = "docs/design/FSHARP_IMPLEMENTATION_GUIDE.md#finding-symbols"
            }
        });
    }

    /// <summary>
    /// Stub for F# RoslynPath queries (will be FSharpPath).
    /// </summary>
    public Task<object> QuerySyntaxStubAsync(string filePath, string query, bool includeSemanticInfo)
    {
        _logger.LogInformation("F# query-syntax requested for {File} with query {Query}", filePath, query);
        
        return Task.FromResult<object>(new
        {
            success = false,
            message = FSharpFileDetector.GetFSharpNotSupportedMessage("query-syntax", filePath),
            info = new
            {
                requestedFile = filePath,
                requestedQuery = query,
                includeSemanticInfo,
                note = "F# will use FSharpPath syntax instead of RoslynPath",
                documentationLink = "docs/design/FSHARP_IMPLEMENTATION_GUIDE.md#fsharppath-query-language"
            }
        });
    }

    /// <summary>
    /// Stub for finding references in F# code.
    /// </summary>
    public Task<object> FindReferencesStubAsync(string symbolName, string? filePath)
    {
        _logger.LogInformation("F# find-references requested for symbol {Symbol}", symbolName);
        
        return Task.FromResult<object>(new
        {
            success = false,
            message = FSharpFileDetector.GetFSharpNotSupportedMessage("find-references"),
            info = new
            {
                requestedSymbol = symbolName,
                requestedFile = filePath,
                note = "Cross-language references between F# and C#/VB.NET will be supported in the future",
                documentationLink = "docs/design/FSHARP_IMPLEMENTATION_GUIDE.md#cross-language-references"
            }
        });
    }

    /// <summary>
    /// Stub for statement operations in F# (will map to expressions).
    /// </summary>
    public Task<object> StatementOperationStubAsync(string operation, string filePath, object location)
    {
        _logger.LogInformation("F# {Operation} requested for {File}", operation, filePath);
        
        return Task.FromResult<object>(new
        {
            success = false,
            message = FSharpFileDetector.GetFSharpNotSupportedMessage(operation, filePath),
            info = new
            {
                requestedOperation = operation,
                requestedFile = filePath,
                requestedLocation = location,
                note = "F# uses expressions instead of statements - operations will be mapped appropriately",
                documentationLink = "docs/design/FSHARP_IMPLEMENTATION_GUIDE.md#statement-level-operations"
            }
        });
    }

    /// <summary>
    /// Get information about F# support status.
    /// </summary>
    public object GetFSharpSupportInfo()
    {
        return new
        {
            status = "planned",
            message = "F# support is documented and designed but not yet implemented",
            documentation = new
            {
                architecture = "docs/design/FSHARP_ARCHITECTURE.md",
                implementation = "docs/design/FSHARP_IMPLEMENTATION_GUIDE.md",
                roadmap = "docs/design/FSHARP_ROADMAP.md",
                currentStatus = "docs/FSHARP_IMPLEMENTATION_STATUS.md"
            },
            supportedFileTypes = new[] { ".fs", ".fsi", ".fsx", ".fsscript" },
            plannedFeatures = new[]
            {
                "Full syntax and semantic analysis via FSharp.Compiler.Service",
                "FSharpPath query language (similar to RoslynPath)",
                "Cross-language symbol resolution",
                "Expression-based refactoring",
                "Type provider support"
            },
            currentWorkarounds = new[]
            {
                "Use text-based search with find-in-project",
                "Analyze F# files separately with F# tooling",
                "Focus on C#/VB.NET portions of mixed solutions"
            }
        };
    }
}