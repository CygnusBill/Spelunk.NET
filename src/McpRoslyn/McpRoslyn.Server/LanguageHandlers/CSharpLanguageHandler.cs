using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace McpRoslyn.Server.LanguageHandlers;

/// <summary>
/// C# specific implementation of language operations
/// </summary>
public class CSharpLanguageHandler : ILanguageHandler
{
    public string Language => LanguageNames.CSharp;
    
    public bool IsTypeDeclaration(SyntaxNode node)
    {
        return node is TypeDeclarationSyntax or EnumDeclarationSyntax or DelegateDeclarationSyntax;
    }
    
    public bool IsMethodDeclaration(SyntaxNode node)
    {
        return node is MethodDeclarationSyntax;
    }
    
    public bool IsPropertyDeclaration(SyntaxNode node)
    {
        return node is PropertyDeclarationSyntax;
    }
    
    public bool IsFieldDeclaration(SyntaxNode node)
    {
        return node is FieldDeclarationSyntax;
    }
    
    public bool IsStatement(SyntaxNode node)
    {
        return node is StatementSyntax;
    }
    
    public string? GetTypeDeclarationName(SyntaxNode node)
    {
        return node switch
        {
            TypeDeclarationSyntax typeDecl => typeDecl.Identifier.Text,
            EnumDeclarationSyntax enumDecl => enumDecl.Identifier.Text,
            DelegateDeclarationSyntax delegateDecl => delegateDecl.Identifier.Text,
            _ => null
        };
    }
    
    public string? GetMethodDeclarationName(SyntaxNode node)
    {
        return node is MethodDeclarationSyntax method ? method.Identifier.Text : null;
    }
    
    public string? GetPropertyDeclarationName(SyntaxNode node)
    {
        return node is PropertyDeclarationSyntax property ? property.Identifier.Text : null;
    }
    
    public string? GetFieldDeclarationName(SyntaxNode node)
    {
        if (node is FieldDeclarationSyntax field && field.Declaration.Variables.Count > 0)
        {
            return field.Declaration.Variables[0].Identifier.Text;
        }
        return null;
    }
    
    public async Task<Document> FormatDocumentAsync(Document document, CancellationToken cancellationToken = default)
    {
        return await Formatter.FormatAsync(document, cancellationToken: cancellationToken);
    }
    
    public SyntaxNode? ParseStatement(string statementText)
    {
        var text = statementText.Trim();
        if (!text.EndsWith(";"))
            text += ";";
            
        var syntaxTree = CSharpSyntaxTree.ParseText(text);
        var root = syntaxTree.GetRoot();
        
        // Try to get the first statement
        var firstStatement = root.DescendantNodes().OfType<StatementSyntax>().FirstOrDefault();
        if (firstStatement != null)
            return firstStatement;
            
        // If no statement found, it might be an expression
        var expression = root.DescendantNodes().OfType<ExpressionSyntax>().FirstOrDefault();
        if (expression != null)
        {
            // Wrap in an expression statement
            return SyntaxFactory.ExpressionStatement(expression);
        }
        
        return null;
    }
    
    public IEnumerable<SyntaxNode> GetStatements(SyntaxNode node)
    {
        return node.DescendantNodes().Where(n => n is StatementSyntax);
    }
    
    public bool IsAsyncMethod(SyntaxNode methodNode)
    {
        if (methodNode is MethodDeclarationSyntax method)
        {
            return method.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword));
        }
        return false;
    }
    
    public SyntaxNode? GetMethodBody(SyntaxNode methodNode)
    {
        if (methodNode is MethodDeclarationSyntax method)
        {
            return method.Body ?? (SyntaxNode?)method.ExpressionBody;
        }
        return null;
    }
    
    public SyntaxTree ParseText(string sourceText, string filePath = "")
    {
        return CSharpSyntaxTree.ParseText(sourceText, path: filePath);
    }
}