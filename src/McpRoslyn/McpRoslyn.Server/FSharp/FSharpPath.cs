using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.FSharp.Compiler.Syntax;
using Microsoft.FSharp.Compiler.Text;

namespace McpRoslyn.Server.FSharp
{
    /// <summary>
    /// FSharpPath - XPath-style query language for F# AST navigation
    /// </summary>
    public class FSharpPath
    {
        private readonly ParsedInput _parsedInput;
        private readonly string? _sourceText;

        public FSharpPath(ParsedInput parsedInput, string? sourceText = null)
        {
            _parsedInput = parsedInput;
            _sourceText = sourceText;
        }

        /// <summary>
        /// Evaluate an FSharpPath expression and return matching nodes
        /// </summary>
        public IEnumerable<FSharpNode> Evaluate(string path)
        {
            var parser = new FSharpPathParser();
            var expression = parser.Parse(path);
            
            if (expression is PathSequence sequence)
            {
                return EvaluateSequence(sequence);
            }

            return Enumerable.Empty<FSharpNode>();
        }

        private IEnumerable<FSharpNode> EvaluateSequence(PathSequence sequence)
        {
            // Start with the root of the F# AST
            IEnumerable<FSharpNode> current = new[] { new FSharpNode(_parsedInput, null) };

            foreach (var step in sequence.Steps)
            {
                current = EvaluateStep(current, step);
            }

            return current;
        }

        private IEnumerable<FSharpNode> EvaluateStep(IEnumerable<FSharpNode> nodes, PathStep step)
        {
            var results = new List<FSharpNode>();

            foreach (var node in nodes)
            {
                IEnumerable<FSharpNode> stepResults = Enumerable.Empty<FSharpNode>();

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

        private IEnumerable<FSharpNode> GetChildren(FSharpNode node, string nodeTest)
        {
            var children = node.GetChildren();
            
            if (string.IsNullOrEmpty(nodeTest))
                return children;

            return children.Where(child => MatchesNodeTest(child, nodeTest));
        }

        private IEnumerable<FSharpNode> GetDescendants(FSharpNode node, string nodeTest)
        {
            var descendants = node.GetDescendants();
            
            if (string.IsNullOrEmpty(nodeTest))
                return descendants;

            return descendants.Where(child => MatchesNodeTest(child, nodeTest));
        }

        private bool MatchesNodeTest(FSharpNode node, string nodeTest)
        {
            var nodeTypeName = GetNodeTypeName(node);
            return nodeTypeName.Equals(nodeTest, StringComparison.OrdinalIgnoreCase);
        }

        private string GetNodeTypeName(FSharpNode node)
        {
            return node.NodeType switch
            {
                FSharpNodeType.Module => "module",
                FSharpNodeType.Type => "type",
                FSharpNodeType.Function => "function",
                FSharpNodeType.Value => "value",
                FSharpNodeType.Pattern => "pattern",
                FSharpNodeType.Expression => "expression",
                FSharpNodeType.MatchClause => "match",
                FSharpNodeType.Binding => "binding",
                FSharpNodeType.Member => "member",
                FSharpNodeType.Property => "property",
                FSharpNodeType.Record => "record",
                FSharpNodeType.Union => "union",
                FSharpNodeType.Enum => "enum",
                FSharpNodeType.Interface => "interface",
                FSharpNodeType.Class => "class",
                FSharpNodeType.Attribute => "attribute",
                FSharpNodeType.Open => "open",
                _ => "unknown"
            };
        }

        private IEnumerable<FSharpNode> EvaluateAxis(FSharpNode node, string axis, string nodeTest)
        {
            return axis switch
            {
                "ancestor::" => GetAncestors(node, nodeTest),
                "ancestor-or-self::" => GetAncestorsOrSelf(node, nodeTest),
                "following-sibling::" => GetFollowingSiblings(node, nodeTest),
                "preceding-sibling::" => GetPrecedingSiblings(node, nodeTest),
                _ => Enumerable.Empty<FSharpNode>()
            };
        }

        private IEnumerable<FSharpNode> GetAncestors(FSharpNode node, string nodeTest)
        {
            var parent = node.Parent;
            while (parent != null)
            {
                if (string.IsNullOrEmpty(nodeTest) || MatchesNodeTest(parent, nodeTest))
                    yield return parent;
                parent = parent.Parent;
            }
        }

        private IEnumerable<FSharpNode> GetAncestorsOrSelf(FSharpNode node, string nodeTest)
        {
            if (string.IsNullOrEmpty(nodeTest) || MatchesNodeTest(node, nodeTest))
                yield return node;

            foreach (var ancestor in GetAncestors(node, nodeTest))
                yield return ancestor;
        }

        private IEnumerable<FSharpNode> GetFollowingSiblings(FSharpNode node, string nodeTest)
        {
            if (node.Parent == null) yield break;

            var siblings = node.Parent.GetChildren().ToList();
            var index = siblings.IndexOf(node);

            for (int i = index + 1; i < siblings.Count; i++)
            {
                if (string.IsNullOrEmpty(nodeTest) || MatchesNodeTest(siblings[i], nodeTest))
                    yield return siblings[i];
            }
        }

        private IEnumerable<FSharpNode> GetPrecedingSiblings(FSharpNode node, string nodeTest)
        {
            if (node.Parent == null) yield break;

            var siblings = node.Parent.GetChildren().ToList();
            var index = siblings.IndexOf(node);

            for (int i = index - 1; i >= 0; i--)
            {
                if (string.IsNullOrEmpty(nodeTest) || MatchesNodeTest(siblings[i], nodeTest))
                    yield return siblings[i];
            }
        }

        private IEnumerable<FSharpNode> ApplyPredicate(IEnumerable<FSharpNode> nodes, Predicate predicate)
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

        private IEnumerable<FSharpNode> ApplyNamePredicate(IEnumerable<FSharpNode> nodes, NamePredicate predicate)
        {
            return nodes.Where(node =>
            {
                var name = node.GetName();
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

        private IEnumerable<FSharpNode> ApplyPositionPredicate(IEnumerable<FSharpNode> nodes, PositionPredicate predicate)
        {
            var nodeList = nodes.ToList();
            
            if (predicate.Expression == "last()")
            {
                return nodeList.Count > 0 ? new[] { nodeList.Last() } : Enumerable.Empty<FSharpNode>();
            }
            
            if (predicate.Expression.StartsWith("last()-"))
            {
                var offset = int.Parse(predicate.Expression.Substring(7));
                var index = nodeList.Count - 1 - offset;
                return index >= 0 && index < nodeList.Count 
                    ? new[] { nodeList[index] } 
                    : Enumerable.Empty<FSharpNode>();
            }
            
            if (int.TryParse(predicate.Expression, out var position))
            {
                // Convert to 0-based index
                var index = position - 1;
                return index >= 0 && index < nodeList.Count 
                    ? new[] { nodeList[index] } 
                    : Enumerable.Empty<FSharpNode>();
            }

            return Enumerable.Empty<FSharpNode>();
        }

        private IEnumerable<FSharpNode> ApplyAttributePredicate(IEnumerable<FSharpNode> nodes, AttributePredicate predicate)
        {
            return nodes.Where(node =>
            {
                switch (predicate.Name)
                {
                    case "type":
                        var typeName = node.NodeType.ToString();
                        return MatchesValue(typeName, predicate.Value, predicate.Operator);

                    case "contains":
                        var text = node.GetText(_sourceText);
                        return text?.Contains(predicate.Value) ?? false;

                    case "matches":
                        var nodeText = node.GetText(_sourceText);
                        return nodeText != null && Regex.IsMatch(nodeText, predicate.Value);
                        
                    case "accessibility":
                        return CheckAccessibility(node, predicate.Value, predicate.Operator);
                        
                    case "mutable":
                        return node.IsMutable.ToString().Equals(predicate.Value, StringComparison.OrdinalIgnoreCase);
                        
                    case "recursive":
                        return node.IsRecursive.ToString().Equals(predicate.Value, StringComparison.OrdinalIgnoreCase);

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

        private bool CheckAccessibility(FSharpNode node, string value, string op)
        {
            var accessibility = node.GetAccessibility();
            if (accessibility == null) return false;
            
            if (op == "=")
            {
                return accessibility.Equals(value, StringComparison.OrdinalIgnoreCase);
            }
            
            return false;
        }

        private IEnumerable<FSharpNode> ApplyBooleanPredicate(IEnumerable<FSharpNode> nodes, BooleanPredicate predicate)
        {
            return nodes.Where(node =>
            {
                switch (predicate.Name)
                {
                    case "async":
                        return node.IsAsync;

                    case "public":
                        return node.GetAccessibility()?.Equals("public", StringComparison.OrdinalIgnoreCase) ?? false;

                    case "private":
                        return node.GetAccessibility()?.Equals("private", StringComparison.OrdinalIgnoreCase) ?? false;

                    case "internal":
                        return node.GetAccessibility()?.Equals("internal", StringComparison.OrdinalIgnoreCase) ?? false;

                    case "static":
                        return node.IsStatic;

                    case "mutable":
                        return node.IsMutable;

                    case "recursive":
                        return node.IsRecursive;
                        
                    case "inline":
                        return node.IsInline;

                    case "active-pattern":
                        return node.IsActivePattern;

                    default:
                        return false;
                }
            });
        }

        private IEnumerable<FSharpNode> ApplyCompoundPredicate(IEnumerable<FSharpNode> nodes, CompoundPredicate predicate)
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

        private IEnumerable<FSharpNode> ApplyNotPredicate(IEnumerable<FSharpNode> nodes, NotPredicate predicate)
        {
            var matching = ApplyPredicate(nodes, predicate.Inner).ToList();
            return nodes.Where(n => !matching.Contains(n));
        }
    }

    /// <summary>
    /// Parser for FSharpPath expressions (reuses RoslynPath parser structure)
    /// </summary>
    public class FSharpPathParser : RoslynPath.RoslynPathParser
    {
        // Inherits parsing logic from RoslynPathParser
    }
}