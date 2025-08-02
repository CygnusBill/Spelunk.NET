namespace McpRoslyn.Server;

/// <summary>
/// Centralized tool descriptions and metadata for MCP Roslyn tools.
/// Shared between STDIO and SSE server implementations.
/// </summary>
public static class ToolDescriptions
{
    public const string LoadWorkspace = "Load a .NET solution or project into the workspace";
    
    public const string AnalyzeSyntax = "Analyzes the syntax tree of a C# or VB.NET file";
    
    public const string GetSymbols = "Get symbols at a specific position in a file";
    
    public const string WorkspaceStatus = "Get loading progress and workspace info";
    
    public const string FindClass = "Find classes, interfaces, structs, or enums by name pattern (supports * and ? wildcards)";
    
    public const string FindMethod = "Find methods by name pattern with optional class pattern filter (supports * and ? wildcards)";
    
    public const string FindProperty = "Find properties and fields by name pattern with optional class pattern filter (supports * and ? wildcards)";
    
    public const string FindMethodCalls = "Find all methods called by a specific method (call tree analysis)";
    
    public const string FindMethodCallers = "Find all methods that call a specific method (caller tree analysis)";
    
    public const string FindReferences = "Find all references to a type, method, property, or field";
    
    public const string FindImplementations = "Find all implementations of an interface or abstract class";
    
    public const string FindOverrides = "Find all overrides of a virtual or abstract method";
    
    public const string FindDerivedTypes = "Find all types that derive from a base class";
    
    public const string RenameSymbol = "Rename a symbol (type, method, property, field) and update all references";
    
    public const string EditCode = "Perform surgical code edits using Roslyn. Operations: add-method, add-property, make-async, add-parameter, wrap-try-catch";
    
    public const string FixPattern = "Find code matching a pattern and transform it to a new pattern";
    
    public const string FindStatements = "Find statements in code matching a pattern. Returns statement IDs for use with other operations. Uses Roslyn's syntax tree to enumerate all statements.";
    
    public const string ReplaceStatement = "Replace a statement with new code. The statement is identified by its location from find-statements. Preserves indentation and formatting context.";
    
    public const string InsertStatement = "Insert a new statement before or after an existing statement. The reference statement is identified by its location from find-statements. Preserves indentation and formatting context.";
    
    public const string RemoveStatement = "Remove a statement from the code. The statement is identified by its location from find-statements. Can preserve comments attached to the statement.";
    
    public const string MarkStatement = "Mark a statement with an ephemeral marker for later reference. Markers are session-scoped and not persisted.";
    
    public const string FindMarkedStatements = "Find all or specific marked statements. Returns current locations even if code has been edited.";
    
    public const string UnmarkStatement = "Remove a specific marker by its ID.";
    
    public const string ClearMarkers = "Clear all markers in the current session.";
    
    public const string GetStatementContext = "Get comprehensive semantic context for a statement including symbols, types, diagnostics, and basic data flow";
    
    public const string GetDiagnostics = "Get compilation diagnostics (errors, warnings, info) from the workspace";
    
    public const string FSharpProjects = "Get information about F# projects in the workspace (detected but not loaded by MSBuild)";
    
    public const string LoadFSharpProject = "Load an F# project using FSharp.Compiler.Service (separate from MSBuild workspaces)";
    
    public const string FSharpFindSymbols = "Find symbols in F# code using FSharpPath queries";
}