using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;
using System.Text.RegularExpressions;

namespace McpRoslyn.Server.Transformers;

/// <summary>
/// Provides robust SQL parameterization using AST-based transformations
/// </summary>
public class SqlParameterizer
{
    private readonly SemanticModel _semanticModel;
    private readonly Document _document;
    
    public SqlParameterizer(Document document, SemanticModel semanticModel)
    {
        _document = document;
        _semanticModel = semanticModel;
    }
    
    /// <summary>
    /// Analyzes a statement to detect SQL usage patterns
    /// </summary>
    public async Task<SqlQueryInfo?> AnalyzeSqlUsageAsync(SyntaxNode statement)
    {
        // Check for various SQL patterns
        var info = new SqlQueryInfo { Statement = statement };
        
        // Pattern 1: SqlCommand creation
        var sqlCommandCreation = statement.DescendantNodes()
            .OfType<ObjectCreationExpressionSyntax>()
            .FirstOrDefault(oc => oc.Type.ToString().Contains("SqlCommand") || 
                                  oc.Type.ToString().Contains("MySqlCommand") ||
                                  oc.Type.ToString().Contains("NpgsqlCommand"));
        
        if (sqlCommandCreation != null)
        {
            info.Library = SqlLibrary.AdoNet;
            info.CreationExpression = sqlCommandCreation;
            ExtractSqlFromExpression(sqlCommandCreation.ArgumentList?.Arguments.FirstOrDefault()?.Expression, info);
            return info;
        }
        
        // Pattern 2: Dapper Query/Execute methods
        var dapperCall = statement.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault(inv => 
            {
                var methodName = GetMethodName(inv);
                return methodName != null && 
                       (methodName.StartsWith("Query") || 
                        methodName.StartsWith("Execute") ||
                        methodName == "QueryFirstOrDefault" ||
                        methodName == "QuerySingle");
            });
        
        if (dapperCall != null)
        {
            info.Library = SqlLibrary.Dapper;
            info.InvocationExpression = dapperCall;
            
            // First argument is usually the SQL
            var sqlArg = dapperCall.ArgumentList.Arguments.FirstOrDefault();
            if (sqlArg != null)
            {
                ExtractSqlFromExpression(sqlArg.Expression, info);
            }
            return info;
        }
        
        // Pattern 3: EF Core FromSqlRaw/FromSqlInterpolated
        var efCoreCall = statement.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault(inv =>
            {
                var methodName = GetMethodName(inv);
                return methodName == "FromSqlRaw" || 
                       methodName == "FromSqlInterpolated" ||
                       methodName == "ExecuteSqlRaw" ||
                       methodName == "ExecuteSqlInterpolated";
            });
        
        if (efCoreCall != null)
        {
            info.Library = SqlLibrary.EntityFramework;
            info.InvocationExpression = efCoreCall;
            
            var sqlArg = efCoreCall.ArgumentList.Arguments.FirstOrDefault();
            if (sqlArg != null)
            {
                ExtractSqlFromExpression(sqlArg.Expression, info);
            }
            return info;
        }
        
        // Pattern 4: String variable assignment that looks like SQL
        var stringAssignment = statement.DescendantNodes()
            .OfType<VariableDeclaratorSyntax>()
            .FirstOrDefault(v => 
            {
                var initValue = v.Initializer?.Value.ToString() ?? "";
                return ContainsSqlKeywords(initValue) && 
                       (v.Identifier.Text.ToLower().Contains("sql") || 
                        v.Identifier.Text.ToLower().Contains("query"));
            });
        
        if (stringAssignment != null)
        {
            info.Library = SqlLibrary.Unknown;
            ExtractSqlFromExpression(stringAssignment.Initializer?.Value, info);
            return info;
        }
        
        return null;
    }
    
    /// <summary>
    /// Transforms the SQL query to use parameters based on the target library
    /// </summary>
    public async Task<SyntaxNode?> TransformToParameterizedAsync(SqlQueryInfo info)
    {
        if (info.SqlExpression == null) return null;
        
        // Extract the SQL and find injection points
        var extraction = ExtractSqlComponents(info.SqlExpression);
        if (extraction == null || !extraction.HasInjections) return null;
        
        return info.Library switch
        {
            SqlLibrary.AdoNet => GenerateAdoNetParameterized(info, extraction),
            SqlLibrary.Dapper => GenerateDapperParameterized(info, extraction),
            SqlLibrary.EntityFramework => GenerateEfCoreParameterized(info, extraction),
            _ => GenerateGenericParameterized(info, extraction)
        };
    }
    
    private void ExtractSqlFromExpression(ExpressionSyntax? expression, SqlQueryInfo info)
    {
        if (expression == null) return;
        
        info.SqlExpression = expression;
        
        // Handle different expression types
        switch (expression)
        {
            case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AddExpression):
                // String concatenation
                info.SqlPattern = SqlPattern.Concatenation;
                ExtractConcatenatedVariables(binary, info);
                break;
                
            case InterpolatedStringExpressionSyntax interpolated:
                // String interpolation
                info.SqlPattern = SqlPattern.Interpolation;
                ExtractInterpolatedVariables(interpolated, info);
                break;
                
            case LiteralExpressionSyntax literal:
                // Plain string literal
                info.SqlPattern = SqlPattern.Literal;
                info.SqlText = literal.Token.ValueText;
                break;
                
            case IdentifierNameSyntax identifier:
                // Variable reference
                info.SqlPattern = SqlPattern.Variable;
                info.Variables.Add(new SqlVariable { Name = identifier.Identifier.Text });
                break;
                
            case InvocationExpressionSyntax invocation when invocation.Expression.ToString().Contains("Format"):
                // String.Format
                info.SqlPattern = SqlPattern.Format;
                ExtractFormatVariables(invocation, info);
                break;
        }
    }
    
    private void ExtractConcatenatedVariables(BinaryExpressionSyntax binary, SqlQueryInfo info)
    {
        var parts = new List<SqlPart>();
        ExtractConcatenationParts(binary, parts);
        
        var sqlBuilder = new StringBuilder();
        int paramIndex = 0;
        
        foreach (var part in parts)
        {
            if (part.IsLiteral)
            {
                sqlBuilder.Append(part.Text);
            }
            else
            {
                var paramName = $"@p{paramIndex++}";
                sqlBuilder.Append(paramName);
                info.Variables.Add(new SqlVariable 
                { 
                    Name = part.Text,
                    ParameterName = paramName,
                    Expression = part.Expression
                });
            }
        }
        
        info.SqlText = sqlBuilder.ToString();
    }
    
    private void ExtractConcatenationParts(ExpressionSyntax expr, List<SqlPart> parts)
    {
        if (expr is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.AddExpression))
        {
            ExtractConcatenationParts(binary.Left, parts);
            ExtractConcatenationParts(binary.Right, parts);
        }
        else if (expr is LiteralExpressionSyntax literal)
        {
            parts.Add(new SqlPart 
            { 
                IsLiteral = true, 
                Text = literal.Token.ValueText,
                Expression = literal
            });
        }
        else
        {
            parts.Add(new SqlPart 
            { 
                IsLiteral = false, 
                Text = expr.ToString(),
                Expression = expr
            });
        }
    }
    
    private void ExtractInterpolatedVariables(InterpolatedStringExpressionSyntax interpolated, SqlQueryInfo info)
    {
        var sqlBuilder = new StringBuilder();
        int paramIndex = 0;
        
        foreach (var content in interpolated.Contents)
        {
            if (content is InterpolatedStringTextSyntax text)
            {
                sqlBuilder.Append(text.TextToken.ValueText);
            }
            else if (content is InterpolationSyntax interpolation)
            {
                var paramName = $"@p{paramIndex++}";
                sqlBuilder.Append(paramName);
                info.Variables.Add(new SqlVariable
                {
                    Name = interpolation.Expression.ToString(),
                    ParameterName = paramName,
                    Expression = interpolation.Expression
                });
            }
        }
        
        info.SqlText = sqlBuilder.ToString();
    }
    
    private void ExtractFormatVariables(InvocationExpressionSyntax format, SqlQueryInfo info)
    {
        var args = format.ArgumentList.Arguments;
        if (args.Count < 2) return;
        
        // First arg is the format string
        if (args[0].Expression is LiteralExpressionSyntax formatString)
        {
            var sql = formatString.Token.ValueText;
            
            // Replace {0}, {1}, etc. with @p0, @p1, etc.
            for (int i = 1; i < args.Count; i++)
            {
                var placeholder = $"{{{i - 1}}}";
                var paramName = $"@p{i - 1}";
                sql = sql.Replace(placeholder, paramName);
                
                info.Variables.Add(new SqlVariable
                {
                    Name = args[i].Expression.ToString(),
                    ParameterName = paramName,
                    Expression = args[i].Expression
                });
            }
            
            info.SqlText = sql;
        }
    }
    
    private SqlExtraction? ExtractSqlComponents(ExpressionSyntax expression)
    {
        var extraction = new SqlExtraction();
        
        // Get the SQL text and variables
        if (expression is BinaryExpressionSyntax || 
            expression is InterpolatedStringExpressionSyntax ||
            expression is InvocationExpressionSyntax)
        {
            extraction.HasInjections = true;
            // Already extracted in AnalyzeSqlUsageAsync
            return extraction;
        }
        
        return extraction;
    }
    
    private SyntaxNode GenerateAdoNetParameterized(SqlQueryInfo info, SqlExtraction extraction)
    {
        var statements = new List<StatementSyntax>();
        
        // Format the SQL nicely
        var formattedSql = FormatSql(info.SqlText ?? "");
        
        // Create the command with parameterized SQL
        if (info.CreationExpression != null)
        {
            var newCreation = SyntaxFactory.ObjectCreationExpression(
                info.CreationExpression.Type,
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Argument(
                            CreateFormattedSqlLiteral(formattedSql)))),
                null);
            
            statements.Add(SyntaxFactory.ExpressionStatement(
                SyntaxFactory.AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    SyntaxFactory.IdentifierName("cmd"),
                    newCreation)));
        }
        
        // Add parameter statements
        foreach (var variable in info.Variables)
        {
            var paramStatement = SyntaxFactory.ExpressionStatement(
                SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName("cmd"),
                            SyntaxFactory.IdentifierName("Parameters")),
                        SyntaxFactory.IdentifierName("AddWithValue")))
                    .WithArgumentList(
                        SyntaxFactory.ArgumentList(
                            SyntaxFactory.SeparatedList(new[]
                            {
                                SyntaxFactory.Argument(
                                    SyntaxFactory.LiteralExpression(
                                        SyntaxKind.StringLiteralExpression,
                                        SyntaxFactory.Literal(variable.ParameterName ?? "@p"))),
                                SyntaxFactory.Argument(
                                    variable.Expression ?? SyntaxFactory.IdentifierName(variable.Name))
                            }))));
            
            statements.Add(paramStatement);
        }
        
        return SyntaxFactory.Block(statements);
    }
    
    private SyntaxNode GenerateDapperParameterized(SqlQueryInfo info, SqlExtraction extraction)
    {
        // Format the SQL
        var formattedSql = FormatSql(info.SqlText ?? "");
        
        // Create anonymous object for parameters
        var properties = info.Variables.Select(v =>
            SyntaxFactory.AnonymousObjectMemberDeclarator(
                SyntaxFactory.IdentifierName(v.Name)));
        
        var parametersArg = SyntaxFactory.Argument(
            SyntaxFactory.AnonymousObjectCreationExpression(
                SyntaxFactory.SeparatedList(properties)));
        
        // Update the invocation
        if (info.InvocationExpression != null)
        {
            var newArgs = new List<ArgumentSyntax>
            {
                SyntaxFactory.Argument(CreateFormattedSqlLiteral(formattedSql)),
                parametersArg
            };
            
            // Keep any additional arguments (like commandType)
            if (info.InvocationExpression.ArgumentList.Arguments.Count > 1)
            {
                newArgs.AddRange(info.InvocationExpression.ArgumentList.Arguments.Skip(2));
            }
            
            return info.InvocationExpression
                .WithArgumentList(SyntaxFactory.ArgumentList(
                    SyntaxFactory.SeparatedList(newArgs)));
        }
        
        return info.Statement;
    }
    
    private SyntaxNode GenerateEfCoreParameterized(SqlQueryInfo info, SqlExtraction extraction)
    {
        // For EF Core, use FromSqlInterpolated with proper interpolation
        var formattedSql = FormatSql(info.SqlText ?? "");
        
        // Build interpolated string with proper parameters
        var contents = new List<InterpolatedStringContentSyntax>();
        var currentPos = 0;
        
        foreach (var variable in info.Variables)
        {
            var paramPos = formattedSql.IndexOf(variable.ParameterName ?? "", currentPos);
            if (paramPos > currentPos)
            {
                // Add text before parameter
                contents.Add(SyntaxFactory.InterpolatedStringText(
                    SyntaxFactory.Token(
                        SyntaxFactory.TriviaList(),
                        SyntaxKind.InterpolatedStringTextToken,
                        formattedSql.Substring(currentPos, paramPos - currentPos),
                        formattedSql.Substring(currentPos, paramPos - currentPos),
                        SyntaxFactory.TriviaList())));
            }
            
            // Add the interpolation
            contents.Add(SyntaxFactory.Interpolation(
                variable.Expression ?? SyntaxFactory.IdentifierName(variable.Name)));
            
            currentPos = paramPos + (variable.ParameterName?.Length ?? 0);
        }
        
        // Add remaining text
        if (currentPos < formattedSql.Length)
        {
            contents.Add(SyntaxFactory.InterpolatedStringText(
                SyntaxFactory.Token(
                    SyntaxFactory.TriviaList(),
                    SyntaxKind.InterpolatedStringTextToken,
                    formattedSql.Substring(currentPos),
                    formattedSql.Substring(currentPos),
                    SyntaxFactory.TriviaList())));
        }
        
        var interpolatedString = SyntaxFactory.InterpolatedStringExpression(
            SyntaxFactory.Token(SyntaxKind.InterpolatedStringStartToken),
            SyntaxFactory.List(contents));
        
        // Update method name to FromSqlInterpolated if it was FromSqlRaw
        if (info.InvocationExpression != null)
        {
            var methodName = GetMethodName(info.InvocationExpression);
            if (methodName == "FromSqlRaw")
            {
                // Change to FromSqlInterpolated
                var newInvocation = info.InvocationExpression.WithExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        ((MemberAccessExpressionSyntax)info.InvocationExpression.Expression).Expression,
                        SyntaxFactory.IdentifierName("FromSqlInterpolated")));
                
                return newInvocation.WithArgumentList(
                    SyntaxFactory.ArgumentList(
                        SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.Argument(interpolatedString))));
            }
        }
        
        return info.Statement;
    }
    
    private SyntaxNode GenerateGenericParameterized(SqlQueryInfo info, SqlExtraction extraction)
    {
        // For unknown libraries, generate a safe pattern with comments
        var formattedSql = FormatSql(info.SqlText ?? "");
        
        var comment = SyntaxFactory.Comment(
            "// TODO: Update to use your SQL library's parameterization method");
        
        var literal = CreateFormattedSqlLiteral(formattedSql)
            .WithLeadingTrivia(SyntaxFactory.TriviaList(comment, SyntaxFactory.EndOfLine("\n")));
        
        return SyntaxFactory.ExpressionStatement(
            SyntaxFactory.AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                SyntaxFactory.IdentifierName("sql"),
                literal));
    }
    
    private ExpressionSyntax CreateFormattedSqlLiteral(string sql)
    {
        // Use verbatim string for multi-line SQL
        if (sql.Contains('\n'))
        {
            return SyntaxFactory.LiteralExpression(
                SyntaxKind.StringLiteralExpression,
                SyntaxFactory.Literal($@"@""{sql}""", sql));
        }
        
        return SyntaxFactory.LiteralExpression(
            SyntaxKind.StringLiteralExpression,
            SyntaxFactory.Literal(sql));
    }
    
    private string FormatSql(string sql)
    {
        // Basic SQL formatting - this could be much more sophisticated
        var formatted = sql
            .Replace("SELECT", "\n    SELECT")
            .Replace("FROM", "\n    FROM")
            .Replace("WHERE", "\n    WHERE")
            .Replace("AND", "\n        AND")
            .Replace("OR", "\n        OR")
            .Replace("ORDER BY", "\n    ORDER BY")
            .Replace("GROUP BY", "\n    GROUP BY")
            .Replace("INSERT INTO", "\n    INSERT INTO")
            .Replace("VALUES", "\n    VALUES")
            .Replace("UPDATE", "\n    UPDATE")
            .Replace("SET", "\n    SET")
            .Replace("DELETE", "\n    DELETE");
        
        // Clean up extra newlines at the start
        while (formatted.StartsWith("\n"))
        {
            formatted = formatted.Substring(1);
        }
        
        return formatted;
    }
    
    private string? GetMethodName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            _ => null
        };
    }
    
    private bool ContainsSqlKeywords(string text)
    {
        var keywords = new[] { "SELECT", "INSERT", "UPDATE", "DELETE", "FROM", "WHERE", "JOIN" };
        var upperText = text.ToUpper();
        return keywords.Any(k => upperText.Contains(k));
    }
}

/// <summary>
/// Information about SQL usage in code
/// </summary>
public class SqlQueryInfo
{
    public SyntaxNode Statement { get; set; } = null!;
    public SqlLibrary Library { get; set; }
    public SqlPattern SqlPattern { get; set; }
    public ExpressionSyntax? SqlExpression { get; set; }
    public ObjectCreationExpressionSyntax? CreationExpression { get; set; }
    public InvocationExpressionSyntax? InvocationExpression { get; set; }
    public string? SqlText { get; set; }
    public List<SqlVariable> Variables { get; set; } = new();
}

/// <summary>
/// Represents a variable being injected into SQL
/// </summary>
public class SqlVariable
{
    public string Name { get; set; } = "";
    public string? ParameterName { get; set; }
    public ExpressionSyntax? Expression { get; set; }
    public ITypeSymbol? Type { get; set; }
}

/// <summary>
/// SQL library being used
/// </summary>
public enum SqlLibrary
{
    Unknown,
    AdoNet,
    Dapper,
    EntityFramework
}

/// <summary>
/// Pattern of SQL construction
/// </summary>
public enum SqlPattern
{
    Literal,
    Concatenation,
    Interpolation,
    Format,
    Variable,
    StringBuilder
}

/// <summary>
/// Extracted SQL components
/// </summary>
public class SqlExtraction
{
    public bool HasInjections { get; set; }
    public List<SqlPart> Parts { get; set; } = new();
}

/// <summary>
/// Part of a SQL string
/// </summary>
public class SqlPart
{
    public bool IsLiteral { get; set; }
    public string Text { get; set; } = "";
    public ExpressionSyntax? Expression { get; set; }
}