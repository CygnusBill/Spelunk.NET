using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace McpRoslyn.Server.LanguageHandlers;

/// <summary>
/// Visual Basic specific implementation of language operations
/// </summary>
public class VisualBasicLanguageHandler : ILanguageHandler
{
    public string Language => LanguageNames.VisualBasic;
    
    public bool IsTypeDeclaration(SyntaxNode node)
    {
        return node is TypeBlockSyntax or EnumBlockSyntax or DelegateStatementSyntax;
    }
    
    public bool IsMethodDeclaration(SyntaxNode node)
    {
        return node is MethodBlockSyntax or MethodStatementSyntax;
    }
    
    public bool IsPropertyDeclaration(SyntaxNode node)
    {
        return node is PropertyBlockSyntax or PropertyStatementSyntax;
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
            ClassBlockSyntax classBlock => classBlock.ClassStatement.Identifier.Text,
            StructureBlockSyntax structBlock => structBlock.StructureStatement.Identifier.Text,
            InterfaceBlockSyntax interfaceBlock => interfaceBlock.InterfaceStatement.Identifier.Text,
            ModuleBlockSyntax moduleBlock => moduleBlock.ModuleStatement.Identifier.Text,
            EnumBlockSyntax enumBlock => enumBlock.EnumStatement.Identifier.Text,
            DelegateStatementSyntax delegateStmt => delegateStmt.Identifier.Text,
            _ => null
        };
    }
    
    public string? GetMethodDeclarationName(SyntaxNode node)
    {
        return node switch
        {
            MethodBlockSyntax methodBlock => methodBlock.SubOrFunctionStatement.Identifier.Text,
            MethodStatementSyntax methodStmt => methodStmt.Identifier.Text,
            _ => null
        };
    }
    
    public string? GetPropertyDeclarationName(SyntaxNode node)
    {
        return node switch
        {
            PropertyBlockSyntax propertyBlock => propertyBlock.PropertyStatement.Identifier.Text,
            PropertyStatementSyntax propertyStmt => propertyStmt.Identifier.Text,
            _ => null
        };
    }
    
    public string? GetFieldDeclarationName(SyntaxNode node)
    {
        if (node is FieldDeclarationSyntax field && field.Declarators.Count > 0)
        {
            var firstDeclarator = field.Declarators[0];
            if (firstDeclarator.Names.Count > 0)
            {
                return firstDeclarator.Names[0].Identifier.Text;
            }
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
        
        // VB doesn't require semicolons, but we need to wrap in a method to parse a statement
        var wrappedCode = $@"Module TempModule
    Sub TempMethod()
        {text}
    End Sub
End Module";
        
        var syntaxTree = VisualBasicSyntaxTree.ParseText(wrappedCode);
        var root = syntaxTree.GetRoot();
        
        // Find the statement inside the temporary method
        var tempMethod = root.DescendantNodes().OfType<MethodBlockSyntax>().FirstOrDefault();
        if (tempMethod != null)
        {
            var statements = tempMethod.Statements;
            if (statements.Count > 0)
            {
                return statements[0];
            }
        }
        
        return null;
    }
    
    public IEnumerable<SyntaxNode> GetStatements(SyntaxNode node)
    {
        return node.DescendantNodes().Where(n => n is StatementSyntax && 
            !(n is MethodStatementSyntax) && 
            !(n is PropertyStatementSyntax) &&
            !(n is FieldDeclarationSyntax) &&
            !(n is ClassStatementSyntax) &&
            !(n is StructureStatementSyntax) &&
            !(n is InterfaceStatementSyntax) &&
            !(n is ModuleStatementSyntax) &&
            !(n is EnumStatementSyntax) &&
            !(n is EndBlockStatementSyntax));
    }
    
    public bool IsAsyncMethod(SyntaxNode methodNode)
    {
        if (methodNode is MethodBlockSyntax methodBlock)
        {
            return methodBlock.SubOrFunctionStatement.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword));
        }
        else if (methodNode is MethodStatementSyntax methodStmt)
        {
            return methodStmt.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword));
        }
        return false;
    }
    
    public SyntaxNode? GetMethodBody(SyntaxNode methodNode)
    {
        if (methodNode is MethodBlockSyntax methodBlock)
        {
            return methodBlock;
        }
        return null;
    }
    
    public SyntaxTree ParseText(string sourceText, string filePath = "")
    {
        return VisualBasicSyntaxTree.ParseText(sourceText, path: filePath);
    }
}