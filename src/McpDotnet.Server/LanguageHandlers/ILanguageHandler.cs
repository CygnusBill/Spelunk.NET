using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace McpDotnet.Server.LanguageHandlers;

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
    
    // ===== Block and Statement Manipulation =====
    
    /// <summary>
    /// Determines if a syntax node is a block (containing statements)
    /// </summary>
    bool IsBlock(SyntaxNode node);
    
    /// <summary>
    /// Gets statements from a block node
    /// </summary>
    IEnumerable<SyntaxNode> GetBlockStatements(SyntaxNode block);
    
    /// <summary>
    /// Creates a new block with the given statements
    /// </summary>
    SyntaxNode CreateBlock(IEnumerable<SyntaxNode> statements);
    
    /// <summary>
    /// Inserts a statement into a block at the specified index
    /// </summary>
    SyntaxNode InsertIntoBlock(SyntaxNode block, int index, SyntaxNode statement);
    
    /// <summary>
    /// Removes a statement from a block
    /// </summary>
    SyntaxNode RemoveFromBlock(SyntaxNode block, SyntaxNode statement);
    
    // ===== Trivia Handling =====
    
    /// <summary>
    /// Creates whitespace trivia
    /// </summary>
    SyntaxTrivia CreateWhitespaceTrivia(string text);
    
    /// <summary>
    /// Creates end-of-line trivia
    /// </summary>
    SyntaxTrivia CreateEndOfLineTrivia();
    
    /// <summary>
    /// Checks if trivia is whitespace
    /// </summary>
    bool IsWhitespaceTrivia(SyntaxTrivia trivia);
    
    /// <summary>
    /// Checks if trivia is end-of-line
    /// </summary>
    bool IsEndOfLineTrivia(SyntaxTrivia trivia);
    
    /// <summary>
    /// Checks if trivia is a comment
    /// </summary>
    bool IsCommentTrivia(SyntaxTrivia trivia);
    
    /// <summary>
    /// Applies indentation to a statement based on context
    /// </summary>
    SyntaxNode ApplyIndentation(SyntaxNode statement, SyntaxNode context);
    
    // ===== Node Identification =====
    
    /// <summary>
    /// Determines if a node is a constructor
    /// </summary>
    bool IsConstructor(SyntaxNode node);
    
    /// <summary>
    /// Determines if a node is an event declaration
    /// </summary>
    bool IsEventDeclaration(SyntaxNode node);
    
    /// <summary>
    /// Gets the name of a constructor (usually the class name)
    /// </summary>
    string? GetConstructorName(SyntaxNode node);
    
    // ===== Parsing Extensions =====
    
    /// <summary>
    /// Parses a member declaration (method, property, field, etc.)
    /// </summary>
    SyntaxNode? ParseMemberDeclaration(string memberText);
    
    /// <summary>
    /// Parses a type declaration (class, interface, etc.)
    /// </summary>
    SyntaxNode? ParseTypeDeclaration(string typeText);
    
    /// <summary>
    /// Parses an expression
    /// </summary>
    SyntaxNode? ParseExpression(string expressionText);
}