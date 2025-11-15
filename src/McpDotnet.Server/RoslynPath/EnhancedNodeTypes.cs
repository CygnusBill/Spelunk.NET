using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using CS = Microsoft.CodeAnalysis.CSharp.Syntax;
using VB = Microsoft.CodeAnalysis.VisualBasic.Syntax;

namespace McpDotnet.Server.RoslynPath;

/// <summary>
/// Enhanced node type mappings for comprehensive AST navigation
/// </summary>
public static class EnhancedNodeTypes
{
    /// <summary>
    /// Get a detailed node type name for RoslynPath queries
    /// </summary>
    public static string GetDetailedNodeTypeName(SyntaxNode node)
    {
        var language = node.Language;
        
        if (language == LanguageNames.CSharp)
        {
            return GetCSharpNodeTypeName(node);
        }
        else if (language == LanguageNames.VisualBasic)
        {
            return GetVBNodeTypeName(node);
        }
        
        return node.GetType().Name.Replace("Syntax", "").ToLower();
    }
    
    private static string GetCSharpNodeTypeName(SyntaxNode node)
    {
        return node switch
        {
            // High-level constructs (existing)
            CS.ClassDeclarationSyntax _ => "class",
            CS.InterfaceDeclarationSyntax _ => "interface",
            CS.StructDeclarationSyntax _ => "struct",
            CS.EnumDeclarationSyntax _ => "enum",
            CS.MethodDeclarationSyntax _ => "method",
            CS.PropertyDeclarationSyntax _ => "property",
            CS.FieldDeclarationSyntax _ => "field",
            CS.ConstructorDeclarationSyntax _ => "constructor",
            CS.NamespaceDeclarationSyntax _ => "namespace",
            CS.FileScopedNamespaceDeclarationSyntax _ => "namespace",
            CS.BlockSyntax _ => "block",
            
            // Statement types
            CS.IfStatementSyntax _ => "if-statement",
            CS.ForStatementSyntax _ => "for-statement",
            CS.ForEachStatementSyntax _ => "foreach-statement",
            CS.WhileStatementSyntax _ => "while-statement",
            CS.DoStatementSyntax _ => "do-statement",
            CS.SwitchStatementSyntax _ => "switch-statement",
            CS.TryStatementSyntax _ => "try-statement",
            CS.ThrowStatementSyntax _ => "throw-statement",
            CS.ReturnStatementSyntax _ => "return-statement",
            CS.UsingStatementSyntax _ => "using-statement",
            CS.LocalDeclarationStatementSyntax _ => "local-declaration",
            CS.ExpressionStatementSyntax _ => "expression-statement",
            CS.EmptyStatementSyntax _ => "empty-statement",
            CS.BreakStatementSyntax _ => "break-statement",
            CS.ContinueStatementSyntax _ => "continue-statement",
            CS.YieldStatementSyntax _ => "yield-statement",
            CS.LockStatementSyntax _ => "lock-statement",
            CS.CheckedStatementSyntax _ => "checked-statement",
            CS.UnsafeStatementSyntax _ => "unsafe-statement",
            CS.FixedStatementSyntax _ => "fixed-statement",
            CS.StatementSyntax _ => "statement", // Fallback for other statements
            
            // Expression types
            CS.BinaryExpressionSyntax _ => "binary-expression",
            CS.PrefixUnaryExpressionSyntax _ => "unary-expression",
            CS.PostfixUnaryExpressionSyntax _ => "unary-expression",
            CS.LiteralExpressionSyntax _ => "literal",
            CS.IdentifierNameSyntax _ => "identifier",
            CS.InvocationExpressionSyntax _ => "invocation",
            CS.MemberAccessExpressionSyntax _ => "member-access",
            CS.AssignmentExpressionSyntax _ => "assignment",
            CS.ConditionalExpressionSyntax _ => "conditional",
            CS.LambdaExpressionSyntax _ => "lambda",
            CS.AwaitExpressionSyntax _ => "await-expression",
            CS.ObjectCreationExpressionSyntax _ => "object-creation",
            CS.ArrayCreationExpressionSyntax _ => "array-creation",
            CS.ElementAccessExpressionSyntax _ => "element-access",
            CS.CastExpressionSyntax _ => "cast-expression",
            CS.TypeOfExpressionSyntax _ => "typeof-expression",
            CS.IsPatternExpressionSyntax _ => "is-pattern",
            CS.InterpolatedStringExpressionSyntax _ => "interpolated-string",
            CS.ParenthesizedExpressionSyntax _ => "parenthesized",
            CS.QueryExpressionSyntax _ => "query-expression",
            CS.ExpressionSyntax _ => "expression", // Fallback for other expressions
            
            // Declaration types
            CS.ParameterSyntax _ => "parameter",
            CS.TypeParameterSyntax _ => "type-parameter",
            CS.VariableDeclaratorSyntax _ => "variable",
            CS.ArgumentSyntax _ => "argument",
            CS.AttributeSyntax _ => "attribute",
            CS.AttributeListSyntax _ => "attribute-list",
            
            // Other useful nodes
            CS.CatchClauseSyntax _ => "catch-clause",
            CS.FinallyClauseSyntax _ => "finally-clause",
            CS.ElseClauseSyntax _ => "else-clause",
            CS.SwitchSectionSyntax _ => "switch-section",
            CS.CaseSwitchLabelSyntax _ => "case-label",
            CS.DefaultSwitchLabelSyntax _ => "default-label",
            CS.WhenClauseSyntax _ => "when-clause",
            CS.UsingDirectiveSyntax _ => "using-directive",
            
            _ => node.GetType().Name.Replace("Syntax", "").ToLower()
        };
    }
    
    private static string GetVBNodeTypeName(SyntaxNode node)
    {
        return node switch
        {
            // High-level constructs
            VB.ClassBlockSyntax _ => "class",
            VB.InterfaceBlockSyntax _ => "interface",
            VB.StructureBlockSyntax _ => "struct",
            VB.EnumBlockSyntax _ => "enum",
            VB.MethodBlockSyntax _ => "method",
            VB.PropertyBlockSyntax _ => "property",
            VB.FieldDeclarationSyntax _ => "field",
            VB.SubNewStatementSyntax _ => "constructor",
            VB.NamespaceBlockSyntax _ => "namespace",
            
            // Statement types
            VB.MultiLineIfBlockSyntax _ => "if-statement",
            VB.SingleLineIfStatementSyntax _ => "if-statement",
            VB.ForBlockSyntax _ => "for-statement",
            VB.ForEachBlockSyntax _ => "foreach-statement",
            VB.WhileBlockSyntax _ => "while-statement",
            VB.DoLoopBlockSyntax _ => "do-statement",
            VB.SelectBlockSyntax _ => "switch-statement",
            VB.TryBlockSyntax _ => "try-statement",
            VB.ThrowStatementSyntax _ => "throw-statement",
            VB.ReturnStatementSyntax _ => "return-statement",
            VB.UsingBlockSyntax _ => "using-statement",
            VB.LocalDeclarationStatementSyntax _ => "local-declaration",
            VB.ExpressionStatementSyntax _ => "expression-statement",
            VB.AssignmentStatementSyntax _ => "assignment",
            VB.StatementSyntax _ => "statement",
            
            // Expression types
            VB.BinaryExpressionSyntax _ => "binary-expression",
            VB.LiteralExpressionSyntax _ => "literal",
            VB.IdentifierNameSyntax _ => "identifier",
            VB.InvocationExpressionSyntax _ => "invocation",
            VB.MemberAccessExpressionSyntax _ => "member-access",
            VB.TernaryConditionalExpressionSyntax _ => "conditional",
            VB.LambdaExpressionSyntax _ => "lambda",
            VB.AwaitExpressionSyntax _ => "await-expression",
            VB.ObjectCreationExpressionSyntax _ => "object-creation",
            VB.ArrayCreationExpressionSyntax _ => "array-creation",
            VB.ExpressionSyntax _ => "expression",
            
            // Declaration types
            VB.ParameterSyntax _ => "parameter",
            VB.TypeParameterSyntax _ => "type-parameter",
            VB.ModifiedIdentifierSyntax _ => "variable",
            VB.SimpleArgumentSyntax _ => "argument",
            VB.AttributeSyntax _ => "attribute",
            
            _ => node.GetType().Name.Replace("Syntax", "").ToLower()
        };
    }
    
    /// <summary>
    /// Get the operator for binary expressions
    /// </summary>
    public static string? GetBinaryOperator(SyntaxNode node)
    {
        if (node is CS.BinaryExpressionSyntax csBinary)
        {
            return csBinary.Kind() switch
            {
                SyntaxKind.EqualsExpression => "==",
                SyntaxKind.NotEqualsExpression => "!=",
                SyntaxKind.LessThanExpression => "<",
                SyntaxKind.LessThanOrEqualExpression => "<=",
                SyntaxKind.GreaterThanExpression => ">",
                SyntaxKind.GreaterThanOrEqualExpression => ">=",
                SyntaxKind.AddExpression => "+",
                SyntaxKind.SubtractExpression => "-",
                SyntaxKind.MultiplyExpression => "*",
                SyntaxKind.DivideExpression => "/",
                SyntaxKind.ModuloExpression => "%",
                SyntaxKind.LogicalAndExpression => "&&",
                SyntaxKind.LogicalOrExpression => "||",
                SyntaxKind.BitwiseAndExpression => "&",
                SyntaxKind.BitwiseOrExpression => "|",
                SyntaxKind.ExclusiveOrExpression => "^",
                SyntaxKind.LeftShiftExpression => "<<",
                SyntaxKind.RightShiftExpression => ">>",
                SyntaxKind.CoalesceExpression => "??",
                _ => null
            };
        }
        
        if (node is VB.BinaryExpressionSyntax vbBinary)
        {
            return vbBinary.Kind() switch
            {
                Microsoft.CodeAnalysis.VisualBasic.SyntaxKind.EqualsExpression => "=",
                Microsoft.CodeAnalysis.VisualBasic.SyntaxKind.NotEqualsExpression => "<>",
                Microsoft.CodeAnalysis.VisualBasic.SyntaxKind.LessThanExpression => "<",
                Microsoft.CodeAnalysis.VisualBasic.SyntaxKind.LessThanOrEqualExpression => "<=",
                Microsoft.CodeAnalysis.VisualBasic.SyntaxKind.GreaterThanExpression => ">",
                Microsoft.CodeAnalysis.VisualBasic.SyntaxKind.GreaterThanOrEqualExpression => ">=",
                Microsoft.CodeAnalysis.VisualBasic.SyntaxKind.AddExpression => "+",
                Microsoft.CodeAnalysis.VisualBasic.SyntaxKind.SubtractExpression => "-",
                Microsoft.CodeAnalysis.VisualBasic.SyntaxKind.MultiplyExpression => "*",
                Microsoft.CodeAnalysis.VisualBasic.SyntaxKind.DivideExpression => "/",
                Microsoft.CodeAnalysis.VisualBasic.SyntaxKind.ModuloExpression => "Mod",
                Microsoft.CodeAnalysis.VisualBasic.SyntaxKind.AndAlsoExpression => "AndAlso",
                Microsoft.CodeAnalysis.VisualBasic.SyntaxKind.OrElseExpression => "OrElse",
                _ => null
            };
        }
        
        return null;
    }
    
    /// <summary>
    /// Get literal value as string
    /// </summary>
    public static string? GetLiteralValue(SyntaxNode node)
    {
        if (node is CS.LiteralExpressionSyntax csLiteral)
        {
            return csLiteral.Token.ValueText ?? csLiteral.Token.Text;
        }
        
        if (node is VB.LiteralExpressionSyntax vbLiteral)
        {
            return vbLiteral.Token.ValueText;
        }
        
        return null;
    }
}