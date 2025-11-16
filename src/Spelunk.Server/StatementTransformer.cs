using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using CS = Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.VisualBasic;
using VB = Microsoft.CodeAnalysis.VisualBasic.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Text.Json;
using CSSyntaxKind = Microsoft.CodeAnalysis.CSharp.SyntaxKind;
using VBSyntaxKind = Microsoft.CodeAnalysis.VisualBasic.SyntaxKind;

namespace Spelunk.Server;

/// <summary>
/// Handles semantic-aware transformations of statements
/// DEPRECATED: Refactorings should be implemented as agent workflows using primitive tools.
/// See docs/REFACTORING_AS_AGENTS.md for the new approach.
/// This class is retained for backward compatibility only.
/// </summary>
[Obsolete("Use agent orchestration with primitive tools instead. See docs/REFACTORING_AS_AGENTS.md")]
public class StatementTransformer
{
    private readonly SemanticModel _semanticModel;
    private readonly SourceText _sourceText;
    private Document? _document;
    
    public StatementTransformer(SemanticModel semanticModel, SourceText sourceText)
    {
        _semanticModel = semanticModel;
        _sourceText = sourceText;
    }
    
    public void SetDocument(Document document)
    {
        _document = document;
    }
    
    private async Task<Document?> GetDocumentAsync()
    {
        // Try to get document from the semantic model's compilation
        if (_document == null && _semanticModel.SyntaxTree != null)
        {
            // This is a fallback - ideally the document should be set explicitly
            return null;
        }
        return _document;
    }
    
    /// <summary>
    /// Transform a statement based on the provided rule
    /// DEPRECATED: Use agent orchestration instead.
    /// </summary>
    [Obsolete("Use agent orchestration with primitive tools instead")]
    public async Task<string?> TransformStatementAsync(
        SyntaxNode statement,
        StatementContextResult context,
        TransformationRule rule)
    {
        return rule.Type switch
        {
            TransformationType.AddNullCheck => await AddNullCheckAsync(statement, context, rule),
            TransformationType.ConvertToAsync => await ConvertToAsyncAsync(statement, context, rule),
            TransformationType.ExtractVariable => await ExtractVariableAsync(statement, context, rule),
            TransformationType.SimplifyConditional => await SimplifyConditionalAsync(statement, context, rule),
            TransformationType.ParameterizeQuery => await ParameterizeQueryAsync(statement, context, rule),
            TransformationType.ConvertToInterpolation => await ConvertToInterpolationAsync(statement, context, rule),
            TransformationType.AddAwait => await AddAwaitAsync(statement, context, rule),
            TransformationType.Custom => await ApplyCustomTransformationAsync(statement, context, rule),
            _ => null
        };
    }
    
    private async Task<string?> AddNullCheckAsync(SyntaxNode statement, StatementContextResult context, TransformationRule rule)
    {
        // For method calls, add null check before
        if (statement.Language == LanguageNames.CSharp)
        {
            if (statement is CS.ExpressionStatementSyntax exprStmt &&
                exprStmt.Expression is CS.InvocationExpressionSyntax invocation)
            {
                // Extract the object being called
                string? objectName = null;
                if (invocation.Expression is CS.MemberAccessExpressionSyntax memberAccess)
                {
                    objectName = memberAccess.Expression.ToString();
                }
                
                if (!string.IsNullOrEmpty(objectName))
                {
                    // Check if null check already exists by examining previous statements
                    var alreadyChecked = CheckIfAlreadyNullChecked(statement, objectName);
                    
                    if (!alreadyChecked)
                    {
                        var indent = GetIndentation(statement);
                        return $"{indent}ArgumentNullException.ThrowIfNull({objectName});\n{statement}";
                    }
                }
            }
        }
        
        return null;
    }
    
    private bool CheckIfAlreadyNullChecked(SyntaxNode statement, string objectName)
    {
        // Get the containing block
        var block = statement.Parent as CS.BlockSyntax;
        if (block == null) return false;
        
        // Find the index of the current statement
        var statements = block.Statements;
        var currentIndex = -1;
        
        // Cast statement to StatementSyntax to find its index
        if (statement is CS.StatementSyntax stmtSyntax)
        {
            currentIndex = statements.IndexOf(stmtSyntax);
        }
        
        if (currentIndex <= 0) return false;
        
        // Check previous statements for null checks
        for (int i = currentIndex - 1; i >= 0; i--)
        {
            var prevStatement = statements[i];
            var prevText = prevStatement.ToString();
            
            // Check for ArgumentNullException.ThrowIfNull pattern
            if (prevText.Contains($"ArgumentNullException.ThrowIfNull({objectName})") ||
                prevText.Contains($"ArgumentNullException.ThrowIfNull({objectName},"))
            {
                return true;
            }
            
            // Check for if-null-throw pattern
            if (prevStatement is CS.IfStatementSyntax ifStmt)
            {
                var condition = ifStmt.Condition.ToString();
                if ((condition.Contains($"{objectName} == null") || 
                     condition.Contains($"null == {objectName}") ||
                     condition.Contains($"{objectName} is null")) &&
                    ifStmt.Statement.ToString().Contains("throw"))
                {
                    return true;
                }
            }
            
            // Check for null-coalescing throw pattern (obj ?? throw ...)
            if (prevText.Contains($"{objectName} ??") && prevText.Contains("throw"))
            {
                return true;
            }
            
            // Stop checking if we hit a control flow statement that might skip our code
            if (prevStatement is CS.ReturnStatementSyntax ||
                prevStatement is CS.ThrowStatementSyntax ||
                prevStatement is CS.BreakStatementSyntax ||
                prevStatement is CS.ContinueStatementSyntax)
            {
                break;
            }
        }
        
        return false;
    }
    
    private async Task<string?> ConvertToAsyncAsync(SyntaxNode statement, StatementContextResult context, TransformationRule rule)
    {
        if (statement.Language == LanguageNames.CSharp)
        {
            if (statement is CS.ExpressionStatementSyntax exprStmt &&
                exprStmt.Expression is CS.InvocationExpressionSyntax invocation)
            {
                var symbolInfo = _semanticModel.GetSymbolInfo(invocation);
                if (symbolInfo.Symbol is IMethodSymbol method)
                {
                    // Check if there's an async version
                    var asyncMethodName = method.Name + "Async";
                    var containingType = method.ContainingType;
                    
                    var asyncMethod = containingType?.GetMembers(asyncMethodName)
                        .OfType<IMethodSymbol>()
                        .FirstOrDefault(m => m.ReturnType.Name.Contains("Task"));
                    
                    if (asyncMethod != null)
                    {
                        var newStatement = statement.ToString()
                            .Replace(method.Name + "(", asyncMethodName + "(");
                        
                        // Add await if in async context
                        if (context.Context?.EnclosingSymbol?.IsAsyncContext == true)
                        {
                            newStatement = $"await {newStatement}";
                        }
                        
                        return newStatement;
                    }
                }
            }
        }
        
        return null;
    }
    
    private async Task<string?> ExtractVariableAsync(SyntaxNode statement, StatementContextResult context, TransformationRule rule)
    {
        if (statement.Language == LanguageNames.CSharp)
        {
            if (statement is CS.ExpressionStatementSyntax exprStmt)
            {
                // Find complex expressions to extract
                var complexExpressions = exprStmt.DescendantNodes()
                    .Where(n => n is CS.BinaryExpressionSyntax || n is CS.ConditionalExpressionSyntax)
                    .Where(n => n.Span.Length > 50); // Arbitrary complexity threshold
                
                var expr = complexExpressions.FirstOrDefault();
                if (expr != null)
                {
                    var varName = GenerateVariableName(expr);
                    var indent = GetIndentation(statement);
                    
                    var extractedVar = $"{indent}var {varName} = {expr};\n";
                    var newStatement = statement.ToString().Replace(expr.ToString(), varName);
                    
                    return extractedVar + newStatement;
                }
            }
        }
        
        return null;
    }
    
    private async Task<string?> SimplifyConditionalAsync(SyntaxNode statement, StatementContextResult context, TransformationRule rule)
    {
        if (statement.Language == LanguageNames.CSharp)
        {
            // Convert if (x != null) x.Method() to x?.Method()
            if (statement is CS.IfStatementSyntax ifStmt &&
                ifStmt.Condition is CS.BinaryExpressionSyntax binary &&
                binary.IsKind(CSSyntaxKind.NotEqualsExpression) &&
                binary.Right.IsKind(CSSyntaxKind.NullLiteralExpression))
            {
                var identifier = binary.Left.ToString();
                
                if (ifStmt.Statement is CS.BlockSyntax block && block.Statements.Count == 1)
                {
                    var innerStatement = block.Statements[0].ToString().Trim();
                    if (innerStatement.StartsWith(identifier + "."))
                    {
                        var nullConditional = innerStatement.Replace(identifier + ".", identifier + "?.");
                        return nullConditional;
                    }
                }
            }
        }
        
        return null;
    }
    
    private async Task<string?> ParameterizeQueryAsync(SyntaxNode statement, StatementContextResult context, TransformationRule rule)
    {
        if (statement.Language == LanguageNames.CSharp)
        {
            // Use the new SqlParameterizer for robust AST-based transformation
            var document = _document ?? await GetDocumentAsync();
            if (document != null)
            {
                var parameterizer = new Transformers.SqlParameterizer(document, _semanticModel);
                
                // Analyze the SQL usage
                var sqlInfo = await parameterizer.AnalyzeSqlUsageAsync(statement);
                if (sqlInfo != null)
                {
                    // Transform to parameterized version
                    var transformedNode = await parameterizer.TransformToParameterizedAsync(sqlInfo);
                    if (transformedNode != null)
                    {
                        // Return the transformed syntax as string
                        return transformedNode.ToFullString();
                    }
                }
            }
            
            // Fallback to the old implementation if needed
            // This ensures backward compatibility while we transition
            return await ParameterizeQueryLegacyAsync(statement, context, rule);
        }
        
        return null;
    }
    
    private async Task<string?> ParameterizeQueryLegacyAsync(SyntaxNode statement, StatementContextResult context, TransformationRule rule)
    {
        // Keep the old implementation as fallback for now
        if (statement.ToString().Contains("SqlCommand") && 
            (statement.ToString().Contains(" + ") || statement.ToString().Contains("$\"")))
        {
            var modifiedStatement = statement.ToString();
            
            if (statement is CS.LocalDeclarationStatementSyntax localDecl)
            {
                foreach (var variable in localDecl.Declaration.Variables)
                {
                    if (variable.Initializer?.Value.ToString().Contains("\"SELECT") == true)
                    {
                        var sql = variable.Initializer.Value.ToString();
                        var parameterized = ConvertToParameterizedSql(sql);
                        
                        if (parameterized != null)
                        {
                            var indent = GetIndentation(statement);
                            var cmdVar = variable.Identifier.Text;
                            var result = modifiedStatement.Replace(sql, parameterized.Sql) + "\n";
                            
                            foreach (var param in parameterized.Parameters)
                            {
                                result += $"{indent}{cmdVar}.Parameters.AddWithValue(\"{param.Name}\", {param.Value});\n";
                            }
                            
                            return result.TrimEnd();
                        }
                    }
                }
            }
        }
        
        return null;
    }
    
    private async Task<string?> ConvertToInterpolationAsync(SyntaxNode statement, StatementContextResult context, TransformationRule rule)
    {
        if (statement.Language == LanguageNames.CSharp)
        {
            var text = statement.ToString();
            
            // Convert string.Format to interpolation
            if (text.Contains("string.Format(") || text.Contains("String.Format("))
            {
                // Parse the format call
                if (statement.DescendantNodes()
                    .OfType<CS.InvocationExpressionSyntax>()
                    .FirstOrDefault(i => i.Expression.ToString().EndsWith(".Format")) is { } formatCall)
                {
                    if (formatCall.ArgumentList.Arguments.Count >= 2)
                    {
                        var formatString = formatCall.ArgumentList.Arguments[0].ToString().Trim('"');
                        var args = formatCall.ArgumentList.Arguments.Skip(1).Select(a => a.ToString()).ToList();
                        
                        // Convert {0}, {1} to interpolation
                        var interpolated = "$\"" + formatString;
                        for (int i = 0; i < args.Count; i++)
                        {
                            interpolated = interpolated.Replace($"{{{i}}}", $"{{{args[i]}}}");
                        }
                        interpolated += "\"";
                        
                        return text.Replace(formatCall.ToString(), interpolated);
                    }
                }
            }
        }
        
        return null;
    }
    
    private async Task<string?> AddAwaitAsync(SyntaxNode statement, StatementContextResult context, TransformationRule rule)
    {
        if (statement.Language == LanguageNames.CSharp)
        {
            // Add await to async method calls that are missing it
            if (statement is CS.ReturnStatementSyntax returnStmt &&
                returnStmt.Expression is CS.InvocationExpressionSyntax invocation)
            {
                var symbolInfo = _semanticModel.GetSymbolInfo(invocation);
                if (symbolInfo.Symbol is IMethodSymbol method &&
                    method.ReturnType.Name.Contains("Task") &&
                    !statement.ToString().Contains("await"))
                {
                    return statement.ToString().Replace("return ", "return await ");
                }
            }
            else if (statement is CS.ExpressionStatementSyntax exprStmt &&
                     exprStmt.Expression is CS.InvocationExpressionSyntax inv)
            {
                var symbolInfo = _semanticModel.GetSymbolInfo(inv);
                if (symbolInfo.Symbol is IMethodSymbol method &&
                    method.ReturnType.Name.Contains("Task") &&
                    !statement.ToString().Contains("await"))
                {
                    return $"await {statement}";
                }
            }
        }
        
        return null;
    }
    
    private async Task<string?> ApplyCustomTransformationAsync(SyntaxNode statement, StatementContextResult context, TransformationRule rule)
    {
        // Apply custom transformation from rule parameters
        if (rule.Parameters.TryGetValue("replacement", out var replacement))
        {
            return replacement.ToString();
        }
        
        return null;
    }
    
    private string GetIndentation(SyntaxNode node)
    {
        var lineSpan = _sourceText.Lines.GetLinePositionSpan(node.Span);
        var line = _sourceText.Lines[lineSpan.Start.Line];
        var leadingWhitespace = line.ToString().TakeWhile(char.IsWhiteSpace).Count();
        return new string(' ', leadingWhitespace);
    }
    
    private string GenerateVariableName(SyntaxNode expression)
    {
        // Simple variable name generation based on expression type
        return expression switch
        {
            CS.BinaryExpressionSyntax => "result",
            CS.ConditionalExpressionSyntax => "condition",
            _ => "temp"
        };
    }
    
    private ParameterizedSql? ConvertToParameterizedSql(string sql)
    {
        // Simplified SQL parameterization - real implementation would be more robust
        var result = new ParameterizedSql { Sql = sql };
        
        // Look for concatenations like " + userName + "
        var pattern = @"\s*\+\s*(\w+)\s*\+\s*";
        var matches = System.Text.RegularExpressions.Regex.Matches(sql, pattern);
        
        int paramIndex = 0;
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var varName = match.Groups[1].Value;
            var paramName = $"@p{paramIndex++}";
            
            result.Sql = result.Sql.Replace(match.Value, paramName);
            result.Parameters.Add(new SqlParameter { Name = paramName, Value = varName });
        }
        
        return result.Parameters.Any() ? result : null;
    }
    
    private class ParameterizedSql
    {
        public string Sql { get; set; } = "";
        public List<SqlParameter> Parameters { get; set; } = new();
    }
    
    private class SqlParameter
    {
        public string Name { get; set; } = "";
        public string Value { get; set; } = "";
    }
}

/// <summary>
/// Defines a transformation rule for statement-level refactoring
/// DEPRECATED: Use agent workflows instead of transformation rules.
/// </summary>
[Obsolete("Use agent orchestration with primitive tools instead")]
public class TransformationRule
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string? SpelunkPathPattern { get; set; }
    public TransformationType Type { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
}

/// <summary>
/// Types of transformations supported
/// DEPRECATED: Each transformation should be an agent workflow.
/// </summary>
[Obsolete("Use agent orchestration with primitive tools instead")]
public enum TransformationType
{
    AddNullCheck,
    ConvertToAsync,
    ExtractVariable,
    SimplifyConditional,
    ParameterizeQuery,
    ConvertToInterpolation,
    AddAwait,
    Custom
}