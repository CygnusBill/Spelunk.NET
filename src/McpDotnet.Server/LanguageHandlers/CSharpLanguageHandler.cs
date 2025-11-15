using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace McpDotnet.Server.LanguageHandlers;

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
        // Regular statements
        if (node is StatementSyntax)
            return true;
            
        // Top-level statements in C# 9+ are wrapped in GlobalStatementSyntax
        // We want to treat the actual statement inside, not the wrapper
        if (node is GlobalStatementSyntax)
            return false; // The GlobalStatement itself is not a statement, its child is
            
        return false;
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
    
    // ===== Block and Statement Manipulation =====
    
    public bool IsBlock(SyntaxNode node)
    {
        return node is BlockSyntax;
    }
    
    public IEnumerable<SyntaxNode> GetBlockStatements(SyntaxNode block)
    {
        return block is BlockSyntax blockSyntax ? blockSyntax.Statements : Enumerable.Empty<SyntaxNode>();
    }
    
    public SyntaxNode CreateBlock(IEnumerable<SyntaxNode> statements)
    {
        var statementList = statements.Cast<StatementSyntax>();
        return SyntaxFactory.Block(statementList);
    }
    
    public SyntaxNode InsertIntoBlock(SyntaxNode block, int index, SyntaxNode statement)
    {
        if (block is BlockSyntax blockSyntax && statement is StatementSyntax statementSyntax)
        {
            var statements = blockSyntax.Statements.Insert(index, statementSyntax);
            return blockSyntax.WithStatements(statements);
        }
        return block;
    }
    
    public SyntaxNode RemoveFromBlock(SyntaxNode block, SyntaxNode statement)
    {
        if (block is BlockSyntax blockSyntax && statement is StatementSyntax statementSyntax)
        {
            var statements = blockSyntax.Statements.Remove(statementSyntax);
            return blockSyntax.WithStatements(statements);
        }
        return block;
    }
    
    // ===== Trivia Handling =====
    
    public SyntaxTrivia CreateWhitespaceTrivia(string text)
    {
        return SyntaxFactory.Whitespace(text);
    }
    
    public SyntaxTrivia CreateEndOfLineTrivia()
    {
        return SyntaxFactory.EndOfLine("\n");
    }
    
    public bool IsWhitespaceTrivia(SyntaxTrivia trivia)
    {
        return trivia.IsKind(SyntaxKind.WhitespaceTrivia);
    }
    
    public bool IsEndOfLineTrivia(SyntaxTrivia trivia)
    {
        return trivia.IsKind(SyntaxKind.EndOfLineTrivia);
    }
    
    public bool IsCommentTrivia(SyntaxTrivia trivia)
    {
        return trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) || 
               trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) ||
               trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
               trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia);
    }
    
    public SyntaxNode ApplyIndentation(SyntaxNode statement, SyntaxNode context)
    {
        // Get indentation from context
        var leadingTrivia = context.GetLeadingTrivia();
        var indentationTrivia = leadingTrivia
            .Where(t => t.IsKind(SyntaxKind.WhitespaceTrivia))
            .LastOrDefault();
            
        if (indentationTrivia != default)
        {
            // Apply same indentation to statement
            var newLeadingTrivia = SyntaxTriviaList.Create(indentationTrivia)
                .AddRange(statement.GetLeadingTrivia().Where(t => !t.IsKind(SyntaxKind.WhitespaceTrivia)));
            return statement.WithLeadingTrivia(newLeadingTrivia);
        }
        
        return statement;
    }
    
    // ===== Node Identification =====
    
    public bool IsConstructor(SyntaxNode node)
    {
        return node is ConstructorDeclarationSyntax;
    }
    
    public bool IsEventDeclaration(SyntaxNode node)
    {
        return node is EventDeclarationSyntax || node is EventFieldDeclarationSyntax;
    }
    
    public string? GetConstructorName(SyntaxNode node)
    {
        if (node is ConstructorDeclarationSyntax constructor)
        {
            return constructor.Identifier.Text;
        }
        return null;
    }
    
    // ===== Parsing Extensions =====
    
    public SyntaxNode? ParseMemberDeclaration(string memberText)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText($"class Temp {{ {memberText} }}");
        var root = syntaxTree.GetRoot();
        var classDecl = root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        return classDecl?.Members.FirstOrDefault();
    }
    
    public SyntaxNode? ParseTypeDeclaration(string typeText)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(typeText);
        var root = syntaxTree.GetRoot();
        return root.DescendantNodes().FirstOrDefault(n => IsTypeDeclaration(n));
    }
    
    public SyntaxNode? ParseExpression(string expressionText)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText($"var x = {expressionText};");
        var root = syntaxTree.GetRoot();
        var variableDecl = root.DescendantNodes().OfType<VariableDeclarationSyntax>().FirstOrDefault();
        return variableDecl?.Variables.FirstOrDefault()?.Initializer?.Value;
    }
}