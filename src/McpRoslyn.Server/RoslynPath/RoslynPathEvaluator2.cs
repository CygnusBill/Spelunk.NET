using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using CS = Microsoft.CodeAnalysis.CSharp.Syntax;
using VB = Microsoft.CodeAnalysis.VisualBasic.Syntax;
using CSharpSyntaxKind = Microsoft.CodeAnalysis.CSharp.SyntaxKind;
using VBSyntaxKind = Microsoft.CodeAnalysis.VisualBasic.SyntaxKind;

namespace McpRoslyn.Server.RoslynPath2
{
    /// <summary>
    /// Evaluates RoslynPath expressions against syntax trees using proper AST evaluation
    /// </summary>
    public class RoslynPathEvaluator2
    {
        private readonly SyntaxTree _tree;
        private readonly SyntaxNode _root;

        public RoslynPathEvaluator2(SyntaxTree tree)
        {
            _tree = tree;
            _root = tree.GetRoot();
        }

        public IEnumerable<SyntaxNode> Evaluate(string pathString)
        {
            var parser = new RoslynPathParser2();
            var path = parser.Parse(pathString);
            return Evaluate(path);
        }

        public IEnumerable<SyntaxNode> Evaluate(PathExpression path)
        {
            var context = new EvaluationContext(_root);
            return EvaluatePath(context, path);
        }

        private IEnumerable<SyntaxNode> EvaluatePath(EvaluationContext context, PathExpression path)
        {
            IEnumerable<SyntaxNode> current = path.IsAbsolute 
                ? new[] { _root } 
                : new[] { context.CurrentNode };

            foreach (var step in path.Steps)
            {
                current = EvaluateStep(current, step);
            }

            return current;
        }

        private IEnumerable<SyntaxNode> EvaluateStep(IEnumerable<SyntaxNode> nodes, PathStep step)
        {
            // Apply axis
            var afterAxis = nodes.SelectMany(n => ApplyAxis(n, step.Axis));

            // Apply node test
            var afterNodeTest = afterAxis.Where(n => MatchesNodeTest(n, step.NodeTest));

            // Apply predicates
            var result = afterNodeTest;
            foreach (var predicate in step.Predicates)
            {
                result = ApplyPredicate(result, predicate);
            }

            return result;
        }

        private IEnumerable<SyntaxNode> ApplyAxis(SyntaxNode node, StepAxis axis)
        {
            return axis switch
            {
                StepAxis.Child => node.ChildNodes(),
                StepAxis.Descendant => node.DescendantNodes(),
                StepAxis.DescendantOrSelf => node.DescendantNodesAndSelf(),
                StepAxis.Parent => node.Parent != null ? new[] { node.Parent } : Enumerable.Empty<SyntaxNode>(),
                StepAxis.Ancestor => GetAncestors(node),
                StepAxis.AncestorOrSelf => GetAncestorsAndSelf(node),
                StepAxis.Following => GetFollowing(node),
                StepAxis.FollowingSibling => GetFollowingSiblings(node),
                StepAxis.Preceding => GetPreceding(node),
                StepAxis.PrecedingSibling => GetPrecedingSiblings(node),
                StepAxis.Self => new[] { node },
                _ => Enumerable.Empty<SyntaxNode>()
            };
        }

        private bool MatchesNodeTest(SyntaxNode node, string nodeTest)
        {
            if (string.IsNullOrEmpty(nodeTest) || nodeTest == "*")
                return true;

            // Check for wildcard pattern
            if (nodeTest.Contains('*') || nodeTest.Contains('?'))
            {
                var nodeName = GetNodeName(node) ?? "";
                return MatchesWildcard(nodeName, nodeTest);
            }

            // Check enhanced node types
            var enhancedType = GetEnhancedNodeType(node);
            if (enhancedType == nodeTest)
                return true;

            // Check standard node types
            var standardType = GetStandardNodeType(node);
            if (standardType == nodeTest)
                return true;

            // Check node name
            var name = GetNodeName(node);
            return name != null && name.Equals(nodeTest, StringComparison.OrdinalIgnoreCase);
        }

        private IEnumerable<SyntaxNode> ApplyPredicate(IEnumerable<SyntaxNode> nodes, PredicateExpr predicate)
        {
            // Position predicates need special handling at collection level
            if (predicate is PositionExpr pos)
            {
                var nodeList = nodes.ToList();
                
                if (int.TryParse(pos.Position, out var position))
                {
                    // 1-based index
                    var index = position - 1;
                    if (index >= 0 && index < nodeList.Count)
                        return new[] { nodeList[index] };
                    return Enumerable.Empty<SyntaxNode>();
                }
                
                if (pos.Position == "last()")
                {
                    return nodeList.Count > 0 ? new[] { nodeList.Last() } : Enumerable.Empty<SyntaxNode>();
                }
                
                if (pos.Position.StartsWith("last()-"))
                {
                    var offset = int.Parse(pos.Position.Substring(7));
                    var index = nodeList.Count - 1 - offset;
                    if (index >= 0 && index < nodeList.Count)
                        return new[] { nodeList[index] };
                    return Enumerable.Empty<SyntaxNode>();
                }
                
                return Enumerable.Empty<SyntaxNode>();
            }
            
            return nodes.Where(node => EvaluatePredicate(node, predicate));
        }

        private bool EvaluatePredicate(SyntaxNode node, PredicateExpr predicate)
        {
            var context = new EvaluationContext(node);
            return predicate switch
            {
                AndExpr and => EvaluatePredicate(node, and.Left) && EvaluatePredicate(node, and.Right),
                OrExpr or => EvaluatePredicate(node, or.Left) || EvaluatePredicate(node, or.Right),
                NotExpr not => !EvaluatePredicate(node, not.Inner),
                AttributeExpr attr => EvaluateAttribute(node, attr),
                NameExpr name => EvaluateName(node, name),
                PositionExpr pos => false, // Position predicates need special handling at collection level
                PathPredicateExpr path => EvaluatePathPredicate(node, path),
                _ => false
            };
        }

        private bool EvaluateAttribute(SyntaxNode node, AttributeExpr attr)
        {
            var value = GetAttributeValue(node, attr.Name);
            if (value == null)
                return false;

            // Boolean attribute (no operator)
            if (attr.Operator == null)
                return value.Equals("true", StringComparison.OrdinalIgnoreCase);

            // Special handling for @contains - always do contains check
            if (attr.Name.Equals("contains", StringComparison.OrdinalIgnoreCase))
            {
                var expectedValue = attr.Value ?? "";
                return value.Contains(expectedValue, StringComparison.OrdinalIgnoreCase);
            }

            // Compare with operator
            var expectedVal = attr.Value ?? "";
            return attr.Operator switch
            {
                "=" => value.Equals(expectedVal, StringComparison.OrdinalIgnoreCase),
                "!=" => !value.Equals(expectedVal, StringComparison.OrdinalIgnoreCase),
                "~=" => value.Contains(expectedVal, StringComparison.OrdinalIgnoreCase),
                "<" => string.Compare(value, expectedVal, StringComparison.OrdinalIgnoreCase) < 0,
                ">" => string.Compare(value, expectedVal, StringComparison.OrdinalIgnoreCase) > 0,
                "<=" => string.Compare(value, expectedVal, StringComparison.OrdinalIgnoreCase) <= 0,
                ">=" => string.Compare(value, expectedVal, StringComparison.OrdinalIgnoreCase) >= 0,
                _ => false
            };
        }

        private bool EvaluateName(SyntaxNode node, NameExpr name)
        {
            var nodeName = GetNodeName(node);
            if (nodeName == null)
                return false;

            // Check for wildcard pattern
            if (name.Pattern.Contains('*') || name.Pattern.Contains('?'))
            {
                return MatchesWildcard(nodeName, name.Pattern);
            }

            return nodeName.Equals(name.Pattern, StringComparison.OrdinalIgnoreCase);
        }

        private bool EvaluatePathPredicate(SyntaxNode node, PathPredicateExpr pathExpr)
        {
            // Parse and evaluate the nested path from current node context
            try
            {
                var parser = new RoslynPathParser2();
                var path = parser.Parse(pathExpr.PathString);
                var context = new EvaluationContext(node);
                var results = EvaluatePath(context, path);
                return results.Any();
            }
            catch
            {
                return false;
            }
        }

        private string? GetAttributeValue(SyntaxNode node, string attrName)
        {
            return attrName.ToLower() switch
            {
                "name" => GetNodeName(node),
                "type" => node.GetType().Name,
                "kind" => node.Language == LanguageNames.CSharp 
                    ? ((CSharpSyntaxKind)node.RawKind).ToString()
                    : ((VBSyntaxKind)node.RawKind).ToString(),
                "text" => node.ToString(),
                "contains" => node.ToString(),
                "async" => IsAsync(node) ? "true" : "false",
                "public" => HasModifier(node, "public") ? "true" : "false",
                "private" => HasModifier(node, "private") ? "true" : "false",
                "protected" => HasModifier(node, "protected") ? "true" : "false",
                "internal" => HasModifier(node, "internal") ? "true" : "false",
                "static" => HasModifier(node, "static") ? "true" : "false",
                "virtual" => HasModifier(node, "virtual") ? "true" : "false",
                "abstract" => HasModifier(node, "abstract") ? "true" : "false",
                "override" => HasModifier(node, "override") ? "true" : "false",
                "sealed" => HasModifier(node, "sealed") ? "true" : "false",
                "readonly" => HasModifier(node, "readonly") ? "true" : "false",
                "modifiers" => GetModifiers(node),
                "returns" => GetReturnType(node),
                "operator" => GetBinaryOperator(node),
                "literal-value" => GetLiteralValue(node),
                "right-text" => GetRightOperandText(node),
                "left-text" => GetLeftOperandText(node),
                _ => null
            };
        }

        private bool MatchesWildcard(string text, string pattern)
        {
            var regexPattern = "^" + Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";
            return Regex.IsMatch(text, regexPattern, RegexOptions.IgnoreCase);
        }

        #region Helper Methods

        private IEnumerable<SyntaxNode> GetAncestors(SyntaxNode node)
        {
            var current = node.Parent;
            while (current != null)
            {
                yield return current;
                current = current.Parent;
            }
        }

        private IEnumerable<SyntaxNode> GetAncestorsAndSelf(SyntaxNode node)
        {
            yield return node;
            foreach (var ancestor in GetAncestors(node))
                yield return ancestor;
        }

        private IEnumerable<SyntaxNode> GetFollowing(SyntaxNode node)
        {
            // Get all nodes that come after this one in document order
            var parent = node.Parent;
            if (parent == null)
                return Enumerable.Empty<SyntaxNode>();

            bool foundNode = false;
            return parent.DescendantNodes().Where(n =>
            {
                if (n == node)
                {
                    foundNode = true;
                    return false;
                }
                return foundNode;
            });
        }

        private IEnumerable<SyntaxNode> GetFollowingSiblings(SyntaxNode node)
        {
            var parent = node.Parent;
            if (parent == null)
                return Enumerable.Empty<SyntaxNode>();

            bool foundNode = false;
            return parent.ChildNodes().Where(n =>
            {
                if (n == node)
                {
                    foundNode = true;
                    return false;
                }
                return foundNode;
            });
        }

        private IEnumerable<SyntaxNode> GetPreceding(SyntaxNode node)
        {
            var parent = node.Parent;
            if (parent == null)
                return Enumerable.Empty<SyntaxNode>();

            return parent.DescendantNodes().TakeWhile(n => n != node);
        }

        private IEnumerable<SyntaxNode> GetPrecedingSiblings(SyntaxNode node)
        {
            var parent = node.Parent;
            if (parent == null)
                return Enumerable.Empty<SyntaxNode>();

            return parent.ChildNodes().TakeWhile(n => n != node);
        }

        private string? GetNodeName(SyntaxNode node)
        {
            return node switch
            {
                CS.ClassDeclarationSyntax cs => cs.Identifier.Text,
                CS.MethodDeclarationSyntax method => method.Identifier.Text,
                CS.PropertyDeclarationSyntax prop => prop.Identifier.Text,
                CS.FieldDeclarationSyntax field => field.Declaration.Variables.FirstOrDefault()?.Identifier.Text,
                VB.ClassBlockSyntax vbClass => vbClass.ClassStatement.Identifier.Text,
                VB.MethodBlockSyntax vbMethod => vbMethod.SubOrFunctionStatement.Identifier.Text,
                VB.PropertyBlockSyntax vbProp => vbProp.PropertyStatement.Identifier.Text,
                _ => null
            };
        }

        private string? GetEnhancedNodeType(SyntaxNode node)
        {
            if (node.Language == LanguageNames.CSharp)
            {
                return node switch
                {
                    CS.IfStatementSyntax => "if-statement",
                    CS.WhileStatementSyntax => "while-statement",
                    CS.ForStatementSyntax => "for-statement",
                    CS.ForEachStatementSyntax => "foreach-statement",
                    CS.DoStatementSyntax => "do-statement",
                    CS.SwitchStatementSyntax => "switch-statement",
                    CS.TryStatementSyntax => "try-statement",
                    CS.ThrowStatementSyntax => "throw-statement",
                    CS.ReturnStatementSyntax => "return-statement",
                    CS.UsingStatementSyntax => "using-statement",
                    CS.LockStatementSyntax => "lock-statement",
                    CS.BinaryExpressionSyntax => "binary-expression",
                    CS.PrefixUnaryExpressionSyntax or CS.PostfixUnaryExpressionSyntax => "unary-expression",
                    CS.LiteralExpressionSyntax => "literal",
                    CS.InvocationExpressionSyntax => "invocation",
                    CS.MemberAccessExpressionSyntax => "member-access",
                    CS.AssignmentExpressionSyntax => "assignment",
                    CS.ConditionalExpressionSyntax => "conditional",
                    CS.LambdaExpressionSyntax => "lambda",
                    CS.AwaitExpressionSyntax => "await-expression",
                    CS.ObjectCreationExpressionSyntax => "object-creation",
                    CS.ArrayCreationExpressionSyntax => "array-creation",
                    CS.ElementAccessExpressionSyntax => "element-access",
                    CS.CastExpressionSyntax => "cast-expression",
                    CS.TypeOfExpressionSyntax => "typeof-expression",
                    CS.QueryExpressionSyntax => "query-expression",
                    CS.LocalDeclarationStatementSyntax => "local-declaration",
                    CS.ExpressionStatementSyntax => "expression-statement",
                    CS.BlockSyntax => "block",
                    _ => null
                };
            }
            // TODO: Add VB support
            return null;
        }

        private string GetStandardNodeType(SyntaxNode node)
        {
            return node switch
            {
                CS.ClassDeclarationSyntax or VB.ClassBlockSyntax => "class",
                CS.MethodDeclarationSyntax or VB.MethodBlockSyntax => "method",
                CS.PropertyDeclarationSyntax or VB.PropertyBlockSyntax => "property",
                CS.FieldDeclarationSyntax or VB.FieldDeclarationSyntax => "field",
                CS.NamespaceDeclarationSyntax or VB.NamespaceBlockSyntax => "namespace",
                CS.InterfaceDeclarationSyntax or VB.InterfaceBlockSyntax => "interface",
                CS.StructDeclarationSyntax or VB.StructureBlockSyntax => "struct",
                CS.EnumDeclarationSyntax or VB.EnumBlockSyntax => "enum",
                // Exclude BlockSyntax from being classified as statement
                CS.BlockSyntax => "block",
                CS.StatementSyntax or VB.StatementSyntax => "statement",
                CS.ExpressionSyntax or VB.ExpressionSyntax => "expression",
                CS.ParameterSyntax or VB.ParameterSyntax => "parameter",
                CS.AttributeSyntax or VB.AttributeSyntax => "attribute",
                _ => ""
            };
        }

        private bool IsAsync(SyntaxNode node)
        {
            return node switch
            {
                CS.MethodDeclarationSyntax method => method.Modifiers.Any(m => m.IsKind(CSharpSyntaxKind.AsyncKeyword)),
                VB.MethodBlockSyntax vbMethod => vbMethod.SubOrFunctionStatement.Modifiers.Any(m => m.IsKind(VBSyntaxKind.AsyncKeyword)),
                _ => false
            };
        }

        private bool HasModifier(SyntaxNode node, string modifier)
        {
            var modifiers = GetModifierTokens(node);
            return modifiers.Any(m => m.ToString().Equals(modifier, StringComparison.OrdinalIgnoreCase));
        }

        private IEnumerable<SyntaxToken> GetModifierTokens(SyntaxNode node)
        {
            return node switch
            {
                CS.BaseMethodDeclarationSyntax method => method.Modifiers,
                CS.BaseTypeDeclarationSyntax type => type.Modifiers,
                CS.BasePropertyDeclarationSyntax prop => prop.Modifiers,
                CS.BaseFieldDeclarationSyntax field => field.Modifiers,
                VB.MethodBlockSyntax vbMethod => vbMethod.SubOrFunctionStatement.Modifiers,
                VB.TypeBlockSyntax vbType => vbType.BlockStatement.Modifiers,
                VB.PropertyBlockSyntax vbProp => vbProp.PropertyStatement.Modifiers,
                _ => Enumerable.Empty<SyntaxToken>()
            };
        }

        private string GetModifiers(SyntaxNode node)
        {
            var modifiers = GetModifierTokens(node);
            return string.Join(" ", modifiers.Select(m => m.ToString().ToLower()));
        }

        private string? GetReturnType(SyntaxNode node)
        {
            return node switch
            {
                CS.MethodDeclarationSyntax method => method.ReturnType.ToString(),
                VB.MethodBlockSyntax vbMethod => vbMethod.SubOrFunctionStatement.AsClause?.Type?.ToString() ?? "void",
                _ => null
            };
        }

        private string? GetBinaryOperator(SyntaxNode node)
        {
            if (node is CS.BinaryExpressionSyntax csBinary)
            {
                return csBinary.Kind() switch
                {
                    CSharpSyntaxKind.EqualsExpression => "==",
                    CSharpSyntaxKind.NotEqualsExpression => "!=",
                    CSharpSyntaxKind.LogicalAndExpression => "&&",
                    CSharpSyntaxKind.LogicalOrExpression => "||",
                    CSharpSyntaxKind.AddExpression => "+",
                    CSharpSyntaxKind.SubtractExpression => "-",
                    CSharpSyntaxKind.MultiplyExpression => "*",
                    CSharpSyntaxKind.DivideExpression => "/",
                    _ => csBinary.OperatorToken.Text
                };
            }
            // TODO: Add VB support
            return null;
        }

        private string? GetLiteralValue(SyntaxNode node)
        {
            if (node is CS.LiteralExpressionSyntax literal)
            {
                return literal.Token.Text;
            }
            // TODO: Add VB support
            return null;
        }

        private string? GetRightOperandText(SyntaxNode node)
        {
            if (node is CS.BinaryExpressionSyntax binary)
            {
                return binary.Right.ToString();
            }
            // TODO: Add VB support
            return null;
        }

        private string? GetLeftOperandText(SyntaxNode node)
        {
            if (node is CS.BinaryExpressionSyntax binary)
            {
                return binary.Left.ToString();
            }
            // TODO: Add VB support
            return null;
        }

        #endregion

        private class EvaluationContext
        {
            public SyntaxNode CurrentNode { get; }

            public EvaluationContext(SyntaxNode currentNode)
            {
                CurrentNode = currentNode;
            }
        }
    }
}