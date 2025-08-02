using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace McpRoslyn.Server.LanguageHandlers;

/// <summary>
/// Interface for language-specific syntax and semantic operations
/// </summary>
public interface ILanguageHandler
{
    /// <summary>
    /// The language this handler supports (e.g., "C#", "Visual Basic", "F#")
    /// </summary>
    string Language { get; }
    
    /// <summary>
    /// Determines if a syntax node represents a type declaration
    /// </summary>
    bool IsTypeDeclaration(SyntaxNode node);
    
    /// <summary>
    /// Determines if a syntax node represents a method declaration
    /// </summary>
    bool IsMethodDeclaration(SyntaxNode node);
    
    /// <summary>
    /// Determines if a syntax node represents a property declaration
    /// </summary>
    bool IsPropertyDeclaration(SyntaxNode node);
    
    /// <summary>
    /// Determines if a syntax node represents a field declaration
    /// </summary>
    bool IsFieldDeclaration(SyntaxNode node);
    
    /// <summary>
    /// Determines if a syntax node represents a statement
    /// </summary>
    bool IsStatement(SyntaxNode node);
    
    /// <summary>
    /// Gets the name of a type declaration node
    /// </summary>
    string? GetTypeDeclarationName(SyntaxNode node);
    
    /// <summary>
    /// Gets the name of a method declaration node
    /// </summary>
    string? GetMethodDeclarationName(SyntaxNode node);
    
    /// <summary>
    /// Gets the name of a property declaration node
    /// </summary>
    string? GetPropertyDeclarationName(SyntaxNode node);
    
    /// <summary>
    /// Gets the name of a field declaration node
    /// </summary>
    string? GetFieldDeclarationName(SyntaxNode node);
    
    /// <summary>
    /// Formats source text according to language conventions
    /// </summary>
    Task<Document> FormatDocumentAsync(Document document, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Creates a statement from text
    /// </summary>
    SyntaxNode? ParseStatement(string statementText);
    
    /// <summary>
    /// Gets all statements in a syntax node
    /// </summary>
    IEnumerable<SyntaxNode> GetStatements(SyntaxNode node);
    
    /// <summary>
    /// Checks if a method has async modifier
    /// </summary>
    bool IsAsyncMethod(SyntaxNode methodNode);
    
    /// <summary>
    /// Gets the body of a method (if it has one)
    /// </summary>
    SyntaxNode? GetMethodBody(SyntaxNode methodNode);
    
    /// <summary>
    /// Creates a syntax tree from source text
    /// </summary>
    SyntaxTree ParseText(string sourceText, string filePath = "");
}