using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace McpRoslyn.Server.RoslynPath
{
    /// <summary>
    /// Evaluates RoslynPath expressions against Roslyn syntax trees
    /// </summary>
    public class RoslynPathEvaluator
    {
        private readonly SyntaxTree _tree;
        private readonly SemanticModel? _semanticModel;

        public RoslynPathEvaluator(SyntaxTree tree, SemanticModel? semanticModel = null)
        {
            _tree = tree;
            _semanticModel = semanticModel;
        }

        public IEnumerable<SyntaxNode> Evaluate(string path)
        {
            var parser = new RoslynPathParser();
            var expression = parser.Parse(path);
            
            if (expression is PathSequence sequence)
            {
                return EvaluateSequence(sequence);
            }

            return Enumerable.Empty<SyntaxNode>();
        }

        private IEnumerable<SyntaxNode> EvaluateSequence(PathSequence sequence)
        {
            IEnumerable<SyntaxNode> current = new[] { _tree.GetRoot() };

            foreach (var step in sequence.Steps)
            {
                current = EvaluateStep(current, step);
            }

            return current;
        }

        private IEnumerable<SyntaxNode> EvaluateStep(IEnumerable<SyntaxNode> nodes, PathStep step)
        {
            var results = new List<SyntaxNode>();

            foreach (var node in nodes)
            {
                IEnumerable<SyntaxNode> stepResults = Enumerable.Empty<SyntaxNode>();

                switch (step.Type)
                {
                    case StepType.Child:
                        stepResults = GetChildren(node, step.NodeTest);
                        break;

                    case StepType.Descendant:
                        stepResults = GetDescendants(node, step.NodeTest);
                        break;

                    case StepType.Parent:
                        if (node.Parent != null)
                            stepResults = new[] { node.Parent };
                        break;

                    case StepType.Axis:
                        stepResults = EvaluateAxis(node, step.Axis, step.NodeTest);
                        break;
                }

                // Apply predicates
                foreach (var predicate in step.Predicates)
                {
                    stepResults = ApplyPredicate(stepResults, predicate);
                }

                results.AddRange(stepResults);
            }

            return results.Distinct();
        }

        private IEnumerable<SyntaxNode> GetChildren(SyntaxNode node, string nodeTest)
        {
            if (string.IsNullOrEmpty(nodeTest))
                return node.ChildNodes();

            return node.ChildNodes().Where(child => MatchesNodeTest(child, nodeTest));
        }

        private IEnumerable<SyntaxNode> GetDescendants(SyntaxNode node, string nodeTest)
        {
            if (string.IsNullOrEmpty(nodeTest))
                return node.DescendantNodes();

            return node.DescendantNodes().Where(child => MatchesNodeTest(child, nodeTest));
        }

        private bool MatchesNodeTest(SyntaxNode node, string nodeTest)
        {
            var nodeTypeName = GetNodeTypeName(node);
            return nodeTypeName.Equals(nodeTest, StringComparison.OrdinalIgnoreCase);
        }

        private string GetNodeTypeName(SyntaxNode node)
        {
            return node switch
            {
                ClassDeclarationSyntax _ => "class",
                InterfaceDeclarationSyntax _ => "interface",
                StructDeclarationSyntax _ => "struct",
                EnumDeclarationSyntax _ => "enum",
                MethodDeclarationSyntax _ => "method",
                PropertyDeclarationSyntax _ => "property",
                FieldDeclarationSyntax _ => "field",
                ConstructorDeclarationSyntax _ => "constructor",
                NamespaceDeclarationSyntax _ => "namespace",
                FileScopedNamespaceDeclarationSyntax _ => "namespace",
                BlockSyntax _ => "block",
                StatementSyntax _ => "statement",
                ExpressionSyntax _ => "expression",
                _ => node.GetType().Name.Replace("Syntax", "").ToLower()
            };
        }

        private IEnumerable<SyntaxNode> EvaluateAxis(SyntaxNode node, string axis, string nodeTest)
        {
            return axis switch
            {
                "ancestor::" => GetAncestors(node, nodeTest),
                "ancestor-or-self::" => GetAncestorsOrSelf(node, nodeTest),
                "following-sibling::" => GetFollowingSiblings(node, nodeTest),
                "preceding-sibling::" => GetPrecedingSiblings(node, nodeTest),
                _ => Enumerable.Empty<SyntaxNode>()
            };
        }

        private IEnumerable<SyntaxNode> GetAncestors(SyntaxNode node, string nodeTest)
        {
            var parent = node.Parent;
            while (parent != null)
            {
                if (string.IsNullOrEmpty(nodeTest) || MatchesNodeTest(parent, nodeTest))
                    yield return parent;
                parent = parent.Parent;
            }
        }

        private IEnumerable<SyntaxNode> GetAncestorsOrSelf(SyntaxNode node, string nodeTest)
        {
            if (string.IsNullOrEmpty(nodeTest) || MatchesNodeTest(node, nodeTest))
                yield return node;

            foreach (var ancestor in GetAncestors(node, nodeTest))
                yield return ancestor;
        }

        private IEnumerable<SyntaxNode> GetFollowingSiblings(SyntaxNode node, string nodeTest)
        {
            if (node.Parent == null) yield break;

            var siblings = node.Parent.ChildNodes().ToList();
            var index = siblings.IndexOf(node);

            for (int i = index + 1; i < siblings.Count; i++)
            {
                if (string.IsNullOrEmpty(nodeTest) || MatchesNodeTest(siblings[i], nodeTest))
                    yield return siblings[i];
            }
        }

        private IEnumerable<SyntaxNode> GetPrecedingSiblings(SyntaxNode node, string nodeTest)
        {
            if (node.Parent == null) yield break;

            var siblings = node.Parent.ChildNodes().ToList();
            var index = siblings.IndexOf(node);

            for (int i = index - 1; i >= 0; i--)
            {
                if (string.IsNullOrEmpty(nodeTest) || MatchesNodeTest(siblings[i], nodeTest))
                    yield return siblings[i];
            }
        }

        private IEnumerable<SyntaxNode> ApplyPredicate(IEnumerable<SyntaxNode> nodes, Predicate predicate)
        {
            return predicate switch
            {
                NamePredicate namePred => ApplyNamePredicate(nodes, namePred),
                PositionPredicate posPred => ApplyPositionPredicate(nodes, posPred),
                AttributePredicate attrPred => ApplyAttributePredicate(nodes, attrPred),
                BooleanPredicate boolPred => ApplyBooleanPredicate(nodes, boolPred),
                CompoundPredicate compPred => ApplyCompoundPredicate(nodes, compPred),
                NotPredicate notPred => ApplyNotPredicate(nodes, notPred),
                _ => nodes
            };
        }

        private IEnumerable<SyntaxNode> ApplyNamePredicate(IEnumerable<SyntaxNode> nodes, NamePredicate predicate)
        {
            return nodes.Where(node =>
            {
                var name = GetNodeName(node);
                if (string.IsNullOrEmpty(name)) return false;

                if (predicate.HasWildcard)
                {
                    var pattern = "^" + Regex.Escape(predicate.Name)
                        .Replace("\\*", ".*")
                        .Replace("\\?", ".") + "$";
                    return Regex.IsMatch(name, pattern);
                }

                return name.Equals(predicate.Name, StringComparison.Ordinal);
            });
        }

        private string? GetNodeName(SyntaxNode node)
        {
            return node switch
            {
                ClassDeclarationSyntax classDecl => classDecl.Identifier.Text,
                InterfaceDeclarationSyntax interfaceDecl => interfaceDecl.Identifier.Text,
                StructDeclarationSyntax structDecl => structDecl.Identifier.Text,
                EnumDeclarationSyntax enumDecl => enumDecl.Identifier.Text,
                MethodDeclarationSyntax methodDecl => methodDecl.Identifier.Text,
                PropertyDeclarationSyntax propDecl => propDecl.Identifier.Text,
                FieldDeclarationSyntax fieldDecl => fieldDecl.Declaration.Variables.FirstOrDefault()?.Identifier.Text,
                ConstructorDeclarationSyntax ctorDecl => ctorDecl.Identifier.Text,
                NamespaceDeclarationSyntax nsDecl => GetNamespaceName(nsDecl.Name),
                FileScopedNamespaceDeclarationSyntax fsNsDecl => GetNamespaceName(fsNsDecl.Name),
                _ => null
            };
        }

        private string? GetNamespaceName(NameSyntax name)
        {
            return name?.ToString();
        }

        private IEnumerable<SyntaxNode> ApplyPositionPredicate(IEnumerable<SyntaxNode> nodes, PositionPredicate predicate)
        {
            var nodeList = nodes.ToList();
            
            if (predicate.Expression == "last()")
            {
                return nodeList.Count > 0 ? new[] { nodeList.Last() } : Enumerable.Empty<SyntaxNode>();
            }
            
            if (predicate.Expression.StartsWith("last()-"))
            {
                var offset = int.Parse(predicate.Expression.Substring(7));
                var index = nodeList.Count - 1 - offset;
                return index >= 0 && index < nodeList.Count 
                    ? new[] { nodeList[index] } 
                    : Enumerable.Empty<SyntaxNode>();
            }
            
            if (int.TryParse(predicate.Expression, out var position))
            {
                // Convert to 0-based index
                var index = position - 1;
                return index >= 0 && index < nodeList.Count 
                    ? new[] { nodeList[index] } 
                    : Enumerable.Empty<SyntaxNode>();
            }

            return Enumerable.Empty<SyntaxNode>();
        }

        private IEnumerable<SyntaxNode> ApplyAttributePredicate(IEnumerable<SyntaxNode> nodes, AttributePredicate predicate)
        {
            return nodes.Where(node =>
            {
                switch (predicate.Name)
                {
                    case "type":
                        var typeName = node.GetType().Name.Replace("Syntax", "");
                        return MatchesValue(typeName, predicate.Value, predicate.Operator);

                    case "contains":
                        var text = node.ToString();
                        return text.Contains(predicate.Value);

                    case "matches":
                        var nodeText = node.ToString();
                        return Regex.IsMatch(nodeText, predicate.Value);

                    default:
                        return false;
                }
            });
        }

        private bool MatchesValue(string actual, string expected, string op)
        {
            if (op != "=") return false;

            // Handle wildcards in type matching
            if (expected.Contains("*") || expected.Contains("?"))
            {
                var pattern = "^" + Regex.Escape(expected)
                    .Replace("\\*", ".*")
                    .Replace("\\?", ".") + "$";
                return Regex.IsMatch(actual, pattern, RegexOptions.IgnoreCase);
            }

            return actual.Equals(expected, StringComparison.OrdinalIgnoreCase);
        }

        private IEnumerable<SyntaxNode> ApplyBooleanPredicate(IEnumerable<SyntaxNode> nodes, BooleanPredicate predicate)
        {
            return nodes.Where(node =>
            {
                switch (predicate.Name)
                {
                    case "async":
                        return node is MethodDeclarationSyntax method &&
                               method.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword));

                    case "public":
                        return HasModifier(node, SyntaxKind.PublicKeyword);

                    case "private":
                        return HasModifier(node, SyntaxKind.PrivateKeyword);

                    case "static":
                        return HasModifier(node, SyntaxKind.StaticKeyword);

                    case "abstract":
                        return HasModifier(node, SyntaxKind.AbstractKeyword);

                    case "virtual":
                        return HasModifier(node, SyntaxKind.VirtualKeyword);

                    case "override":
                        return HasModifier(node, SyntaxKind.OverrideKeyword);

                    default:
                        return false;
                }
            });
        }

        private bool HasModifier(SyntaxNode node, SyntaxKind modifierKind)
        {
            var modifiers = node switch
            {
                BaseTypeDeclarationSyntax typeDecl => typeDecl.Modifiers,
                BaseMethodDeclarationSyntax methodDecl => methodDecl.Modifiers,
                BasePropertyDeclarationSyntax propDecl => propDecl.Modifiers,
                BaseFieldDeclarationSyntax fieldDecl => fieldDecl.Modifiers,
                _ => default(SyntaxTokenList)
            };

            return modifiers.Any(m => m.IsKind(modifierKind));
        }

        private IEnumerable<SyntaxNode> ApplyCompoundPredicate(IEnumerable<SyntaxNode> nodes, CompoundPredicate predicate)
        {
            if (predicate.Operator == "and")
            {
                var leftResults = ApplyPredicate(nodes, predicate.Left);
                return ApplyPredicate(leftResults, predicate.Right);
            }
            else if (predicate.Operator == "or")
            {
                var leftResults = ApplyPredicate(nodes, predicate.Left).ToList();
                var rightResults = ApplyPredicate(nodes, predicate.Right).ToList();
                return leftResults.Union(rightResults).Distinct();
            }

            return nodes;
        }

        private IEnumerable<SyntaxNode> ApplyNotPredicate(IEnumerable<SyntaxNode> nodes, NotPredicate predicate)
        {
            var matching = ApplyPredicate(nodes, predicate.Inner).ToList();
            return nodes.Where(n => !matching.Contains(n));
        }
    }
}