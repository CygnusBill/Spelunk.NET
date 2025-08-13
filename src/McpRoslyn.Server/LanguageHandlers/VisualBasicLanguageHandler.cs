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
        return node is StatementSyntax && 
            !(node is MethodStatementSyntax) && 
            !(node is PropertyStatementSyntax) &&
            !(node is FieldDeclarationSyntax) &&
            !(node is ClassStatementSyntax) &&
            !(node is StructureStatementSyntax) &&
            !(node is InterfaceStatementSyntax) &&
            !(node is ModuleStatementSyntax) &&
            !(node is EnumStatementSyntax) &&
            !(node is EndBlockStatementSyntax) &&
            !(node is NamespaceBlockSyntax) &&
            !(node is TypeBlockSyntax) &&
            !(node is MethodBlockBaseSyntax) &&
            !(node is PropertyBlockSyntax) &&
            !(node is NamespaceStatementSyntax) &&
            !(node is DelegateStatementSyntax) &&
            !(node is MultiLineIfBlockSyntax) &&
            !(node is SingleLineIfStatementSyntax) &&  // Exclude if we want the contained statements
            !(node is MultiLineLambdaExpressionSyntax) &&
            !(node is WithBlockSyntax) &&
            !(node is UsingBlockSyntax) &&
            !(node is SyncLockBlockSyntax) &&
            !(node is TryBlockSyntax) &&
            !(node is WhileBlockSyntax) &&
            !(node is DoLoopBlockSyntax) &&
            !(node is ForBlockSyntax) &&
            !(node is ForEachBlockSyntax) &&
            !(node is SelectBlockSyntax);
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
            !(n is EndBlockStatementSyntax) &&
            !(n is NamespaceBlockSyntax) &&
            !(n is TypeBlockSyntax) &&
            !(n is MethodBlockBaseSyntax) &&
            !(n is PropertyBlockSyntax));
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
    
    // ===== Block and Statement Manipulation =====
    
    public bool IsBlock(SyntaxNode node)
    {
        // In VB, blocks are implicit in method/property bodies
        return node is MethodBlockBaseSyntax || 
               node is PropertyBlockSyntax || 
               node is MultiLineIfBlockSyntax ||
               node is MultiLineLambdaExpressionSyntax;
    }
    
    public IEnumerable<SyntaxNode> GetBlockStatements(SyntaxNode block)
    {
        return block switch
        {
            MethodBlockBaseSyntax methodBlock => methodBlock.Statements,
            PropertyBlockSyntax propertyBlock => propertyBlock.Accessors.SelectMany(a => a.Statements),
            MultiLineIfBlockSyntax ifBlock => ifBlock.Statements,
            MultiLineLambdaExpressionSyntax lambda => lambda.Statements,
            _ => Enumerable.Empty<SyntaxNode>()
        };
    }
    
    public SyntaxNode CreateBlock(IEnumerable<SyntaxNode> statements)
    {
        // VB doesn't have explicit block syntax like C#
        // This would typically be used within a method or other container
        throw new NotSupportedException("VB.NET doesn't support standalone blocks. Use within a method or other container.");
    }
    
    public SyntaxNode InsertIntoBlock(SyntaxNode block, int index, SyntaxNode statement)
    {
        if (statement is not StatementSyntax statementSyntax)
            return block;
            
        return block switch
        {
            MethodBlockBaseSyntax methodBlock => 
                methodBlock.WithStatements(methodBlock.Statements.Insert(index, statementSyntax)),
            MultiLineIfBlockSyntax ifBlock => 
                ifBlock.WithStatements(ifBlock.Statements.Insert(index, statementSyntax)),
            _ => block
        };
    }
    
    public SyntaxNode RemoveFromBlock(SyntaxNode block, SyntaxNode statement)
    {
        if (statement is not StatementSyntax statementSyntax)
            return block;
            
        return block switch
        {
            MethodBlockBaseSyntax methodBlock => 
                methodBlock.WithStatements(methodBlock.Statements.Remove(statementSyntax)),
            MultiLineIfBlockSyntax ifBlock => 
                ifBlock.WithStatements(ifBlock.Statements.Remove(statementSyntax)),
            _ => block
        };
    }
    
    // ===== Trivia Handling =====
    
    public SyntaxTrivia CreateWhitespaceTrivia(string text)
    {
        return SyntaxFactory.Whitespace(text);
    }
    
    public SyntaxTrivia CreateEndOfLineTrivia()
    {
        return SyntaxFactory.EndOfLine(Environment.NewLine);
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
        return trivia.IsKind(SyntaxKind.CommentTrivia) ||
               trivia.IsKind(SyntaxKind.DocumentationCommentTrivia);
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
        return node is SubNewStatementSyntax || 
               (node is MethodBlockSyntax methodBlock && 
                methodBlock.SubOrFunctionStatement.DeclarationKeyword.IsKind(SyntaxKind.NewKeyword));
    }
    
    public bool IsEventDeclaration(SyntaxNode node)
    {
        return node is EventStatementSyntax || node is EventBlockSyntax;
    }
    
    public string? GetConstructorName(SyntaxNode node)
    {
        // In VB, constructors are always named "New"
        if (IsConstructor(node))
        {
            return "New";
        }
        return null;
    }
    
    // ===== Parsing Extensions =====
    
    public SyntaxNode? ParseMemberDeclaration(string memberText)
    {
        var wrappedCode = $@"Class TempClass
    {memberText}
End Class";
        var syntaxTree = VisualBasicSyntaxTree.ParseText(wrappedCode);
        var root = syntaxTree.GetRoot();
        var classBlock = root.DescendantNodes().OfType<ClassBlockSyntax>().FirstOrDefault();
        return classBlock?.Members.FirstOrDefault();
    }
    
    public SyntaxNode? ParseTypeDeclaration(string typeText)
    {
        var syntaxTree = VisualBasicSyntaxTree.ParseText(typeText);
        var root = syntaxTree.GetRoot();
        return root.DescendantNodes().FirstOrDefault(n => IsTypeDeclaration(n));
    }
    
    public SyntaxNode? ParseExpression(string expressionText)
    {
        var wrappedCode = $"Dim x = {expressionText}";
        var syntaxTree = VisualBasicSyntaxTree.ParseText(wrappedCode);
        var root = syntaxTree.GetRoot();
        var variableDecl = root.DescendantNodes().OfType<VariableDeclaratorSyntax>().FirstOrDefault();
        return variableDecl?.Initializer?.Value;
    }
}