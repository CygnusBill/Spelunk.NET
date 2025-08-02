using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using CS = Microsoft.CodeAnalysis.CSharp.Syntax;
using VB = Microsoft.CodeAnalysis.VisualBasic.Syntax;
using CSSyntaxKind = Microsoft.CodeAnalysis.CSharp.SyntaxKind;
using VBSyntaxKind = Microsoft.CodeAnalysis.VisualBasic.SyntaxKind;

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
            var language = node.Language;
            
            if (language == LanguageNames.CSharp)
            {
                return node switch
                {
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
                    CS.StatementSyntax _ => "statement",
                    CS.ExpressionSyntax _ => "expression",
                    _ => node.GetType().Name.Replace("Syntax", "").ToLower()
                };
            }
            else if (language == LanguageNames.VisualBasic)
            {
                return node switch
                {
                    VB.ClassBlockSyntax _ => "class",
                    VB.InterfaceBlockSyntax _ => "interface",
                    VB.StructureBlockSyntax _ => "struct",
                    VB.EnumBlockSyntax _ => "enum",
                    VB.MethodBlockSyntax _ => "method",
                    VB.PropertyBlockSyntax _ => "property",
                    VB.FieldDeclarationSyntax _ => "field",
                    VB.SubNewStatementSyntax _ => "constructor",
                    VB.ConstructorBlockSyntax _ => "constructor",
                    VB.NamespaceBlockSyntax _ => "namespace",
                    VB.StatementSyntax _ => "statement",
                    VB.ExpressionSyntax _ => "expression",
                    _ => node.GetType().Name.Replace("Syntax", "").ToLower()
                };
            }
            else
            {
                // Fallback for unknown languages
                return node.GetType().Name.Replace("Syntax", "").ToLower();
            }
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
            var language = node.Language;
            
            if (language == LanguageNames.CSharp)
            {
                return node switch
                {
                    CS.ClassDeclarationSyntax classDecl => classDecl.Identifier.Text,
                    CS.InterfaceDeclarationSyntax interfaceDecl => interfaceDecl.Identifier.Text,
                    CS.StructDeclarationSyntax structDecl => structDecl.Identifier.Text,
                    CS.EnumDeclarationSyntax enumDecl => enumDecl.Identifier.Text,
                    CS.MethodDeclarationSyntax methodDecl => methodDecl.Identifier.Text,
                    CS.PropertyDeclarationSyntax propDecl => propDecl.Identifier.Text,
                    CS.FieldDeclarationSyntax fieldDecl => fieldDecl.Declaration.Variables.FirstOrDefault()?.Identifier.Text,
                    CS.ConstructorDeclarationSyntax ctorDecl => ctorDecl.Identifier.Text,
                    CS.NamespaceDeclarationSyntax nsDecl => GetNamespaceName(nsDecl.Name),
                    CS.FileScopedNamespaceDeclarationSyntax fsNsDecl => GetNamespaceName(fsNsDecl.Name),
                    _ => null
                };
            }
            else if (language == LanguageNames.VisualBasic)
            {
                return node switch
                {
                    VB.ClassBlockSyntax classBlock => classBlock.ClassStatement.Identifier.Text,
                    VB.InterfaceBlockSyntax interfaceBlock => interfaceBlock.InterfaceStatement.Identifier.Text,
                    VB.StructureBlockSyntax structBlock => structBlock.StructureStatement.Identifier.Text,
                    VB.EnumBlockSyntax enumBlock => enumBlock.EnumStatement.Identifier.Text,
                    VB.MethodBlockSyntax methodBlock => methodBlock.SubOrFunctionStatement.Identifier.Text,
                    VB.PropertyBlockSyntax propBlock => propBlock.PropertyStatement.Identifier.Text,
                    VB.FieldDeclarationSyntax fieldDecl => fieldDecl.Declarators.FirstOrDefault()?.Names.FirstOrDefault()?.Identifier.Text,
                    VB.SubNewStatementSyntax ctorStmt => "New",
                    VB.ConstructorBlockSyntax ctorBlock => "New",
                    VB.NamespaceBlockSyntax nsBlock => GetVBNamespaceName(nsBlock.NamespaceStatement.Name),
                    _ => null
                };
            }
            else
            {
                return null;
            }
        }

        private string? GetNamespaceName(CS.NameSyntax name)
        {
            return name?.ToString();
        }
        
        private string? GetVBNamespaceName(VB.NameSyntax name)
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
                        
                    case "language":
                        return MatchesValue(node.Language, predicate.Value, predicate.Operator);
                        
                    case "modifiers":
                        return CheckModifiers(node, predicate.Value, predicate.Operator);
                        
                    case "returns":
                        return CheckReturnType(node, predicate.Value, predicate.Operator);
                        
                    case "methodtype":
                        return CheckMethodType(node, predicate.Value, predicate.Operator);

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
        
        private bool CheckModifiers(SyntaxNode node, string value, string op)
        {
            var modifiers = GetModifiers(node);
            if (modifiers == null) return false;
            
            if (op == "~=") // contains
            {
                return modifiers.Any(m => m.Equals(value, StringComparison.OrdinalIgnoreCase));
            }
            
            return false;
        }
        
        private bool CheckReturnType(SyntaxNode node, string value, string op)
        {
            var returnType = GetReturnType(node);
            if (returnType == null) return false;
            
            if (op == "=")
            {
                return returnType.Equals(value, StringComparison.OrdinalIgnoreCase);
            }
            else if (op == "~=") // contains
            {
                return returnType.Contains(value, StringComparison.OrdinalIgnoreCase);
            }
            
            return false;
        }
        
        private bool CheckMethodType(SyntaxNode node, string value, string op)
        {
            // Only applicable to VB.NET methods
            if (node.Language != LanguageNames.VisualBasic) return false;
            
            if (node is VB.MethodBlockSyntax methodBlock)
            {
                var isSubMethod = methodBlock.SubOrFunctionStatement.SubOrFunctionKeyword.IsKind(VBSyntaxKind.SubKeyword);
                var methodType = isSubMethod ? "sub" : "function";
                
                if (op == "=")
                {
                    return methodType.Equals(value, StringComparison.OrdinalIgnoreCase);
                }
            }
            
            return false;
        }
        
        private List<string>? GetModifiers(SyntaxNode node)
        {
            if (node.Language == LanguageNames.CSharp)
            {
                return node switch
                {
                    CS.ClassDeclarationSyntax classDecl => classDecl.Modifiers.Select(m => m.Text).ToList(),
                    CS.MethodDeclarationSyntax methodDecl => methodDecl.Modifiers.Select(m => m.Text).ToList(),
                    CS.PropertyDeclarationSyntax propDecl => propDecl.Modifiers.Select(m => m.Text).ToList(),
                    CS.FieldDeclarationSyntax fieldDecl => fieldDecl.Modifiers.Select(m => m.Text).ToList(),
                    _ => null
                };
            }
            else if (node.Language == LanguageNames.VisualBasic)
            {
                return node switch
                {
                    VB.ClassBlockSyntax classBlock => classBlock.ClassStatement.Modifiers.Select(m => m.Text).ToList(),
                    VB.MethodBlockSyntax methodBlock => methodBlock.SubOrFunctionStatement.Modifiers.Select(m => m.Text).ToList(),
                    VB.PropertyBlockSyntax propBlock => propBlock.PropertyStatement.Modifiers.Select(m => m.Text).ToList(),
                    VB.FieldDeclarationSyntax fieldDecl => fieldDecl.Modifiers.Select(m => m.Text).ToList(),
                    _ => null
                };
            }
            
            return null;
        }
        
        private string? GetReturnType(SyntaxNode node)
        {
            if (node.Language == LanguageNames.CSharp)
            {
                if (node is CS.MethodDeclarationSyntax methodDecl)
                {
                    return methodDecl.ReturnType.ToString();
                }
            }
            else if (node.Language == LanguageNames.VisualBasic)
            {
                if (node is VB.MethodBlockSyntax methodBlock)
                {
                    // Check if it's a Sub (void) or Function
                    if (methodBlock.SubOrFunctionStatement.SubOrFunctionKeyword.IsKind(VBSyntaxKind.SubKeyword))
                    {
                        return "void"; // Map VB Sub to C# void concept
                    }
                    else if (methodBlock.SubOrFunctionStatement.AsClause is VB.SimpleAsClauseSyntax asClause)
                    {
                        return asClause.Type.ToString();
                    }
                }
            }
            
            return null;
        }

        private IEnumerable<SyntaxNode> ApplyBooleanPredicate(IEnumerable<SyntaxNode> nodes, BooleanPredicate predicate)
        {
            return nodes.Where(node =>
            {
                var modifiers = GetModifiers(node);
                if (modifiers == null) return false;
                
                switch (predicate.Name)
                {
                    case "async":
                        return modifiers.Any(m => m.Equals("async", StringComparison.OrdinalIgnoreCase));

                    case "public":
                        return modifiers.Any(m => m.Equals("public", StringComparison.OrdinalIgnoreCase));

                    case "private":
                        return modifiers.Any(m => m.Equals("private", StringComparison.OrdinalIgnoreCase));

                    case "static":
                        return modifiers.Any(m => m.Equals("static", StringComparison.OrdinalIgnoreCase) || 
                                               m.Equals("shared", StringComparison.OrdinalIgnoreCase)); // VB.NET uses "Shared"

                    case "abstract":
                        return modifiers.Any(m => m.Equals("abstract", StringComparison.OrdinalIgnoreCase) ||
                                               m.Equals("mustinherit", StringComparison.OrdinalIgnoreCase)); // VB.NET uses "MustInherit"

                    case "virtual":
                        return modifiers.Any(m => m.Equals("virtual", StringComparison.OrdinalIgnoreCase) ||
                                               m.Equals("overridable", StringComparison.OrdinalIgnoreCase)); // VB.NET uses "Overridable"

                    case "override":
                        return modifiers.Any(m => m.Equals("override", StringComparison.OrdinalIgnoreCase) ||
                                               m.Equals("overrides", StringComparison.OrdinalIgnoreCase)); // VB.NET uses "Overrides"
                        
                    case "has-getter":
                        return CheckHasGetter(node);
                        
                    case "has-setter":
                        return CheckHasSetter(node);

                    default:
                        return false;
                }
            });
        }
        
        private bool CheckHasGetter(SyntaxNode node)
        {
            if (node.Language == LanguageNames.CSharp && node is CS.PropertyDeclarationSyntax csProp)
            {
                return csProp.AccessorList?.Accessors.Any(a => a.IsKind(CSSyntaxKind.GetAccessorDeclaration)) ?? false;
            }
            else if (node.Language == LanguageNames.VisualBasic && node is VB.PropertyBlockSyntax vbProp)
            {
                return vbProp.Accessors.Any(a => a.IsKind(VBSyntaxKind.GetAccessorBlock));
            }
            
            return false;
        }
        
        private bool CheckHasSetter(SyntaxNode node)
        {
            if (node.Language == LanguageNames.CSharp && node is CS.PropertyDeclarationSyntax csProp)
            {
                return csProp.AccessorList?.Accessors.Any(a => a.IsKind(CSSyntaxKind.SetAccessorDeclaration)) ?? false;
            }
            else if (node.Language == LanguageNames.VisualBasic && node is VB.PropertyBlockSyntax vbProp)
            {
                return vbProp.Accessors.Any(a => a.IsKind(VBSyntaxKind.SetAccessorBlock));
            }
            
            return false;
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