using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace McpRoslyn.Server.RoslynPath
{
    /// <summary>
    /// Simple facade for using RoslynPath
    /// </summary>
    public static class RoslynPath
    {
        /// <summary>
        /// Find nodes in a syntax tree using RoslynPath syntax
        /// </summary>
        /// <param name="tree">The syntax tree to search</param>
        /// <param name="path">The RoslynPath expression</param>
        /// <param name="semanticModel">Optional semantic model for semantic queries</param>
        /// <returns>Matching syntax nodes</returns>
        public static IEnumerable<SyntaxNode> Find(SyntaxTree tree, string path, SemanticModel semanticModel = null)
        {
            var evaluator = new RoslynPathEvaluator(tree, semanticModel);
            return evaluator.Evaluate(path);
        }

        /// <summary>
        /// Find nodes in source code using RoslynPath syntax
        /// </summary>
        /// <param name="sourceCode">C# source code</param>
        /// <param name="path">The RoslynPath expression</param>
        /// <returns>Matching syntax nodes with location info</returns>
        public static IEnumerable<NodeResult> Find(string sourceCode, string path)
        {
            var tree = CSharpSyntaxTree.ParseText(sourceCode);
            var nodes = Find(tree, path);
            
            return nodes.Select(node => new NodeResult
            {
                Node = node,
                Text = node.ToString(),
                Location = GetLocation(node),
                NodeType = node.GetType().Name.Replace("Syntax", ""),
                Path = GetNodePath(node)
            });
        }

        /// <summary>
        /// Get a stable path for a syntax node
        /// </summary>
        public static string GetNodePath(SyntaxNode node)
        {
            var parts = new List<string>();
            var current = node;

            while (current != null)
            {
                var part = GetNodePathPart(current);
                if (!string.IsNullOrEmpty(part))
                    parts.Insert(0, part);
                current = current.Parent;
            }

            return "/" + string.Join("/", parts);
        }

        private static string GetNodePathPart(SyntaxNode node)
        {
            var typeName = node switch
            {
                ClassDeclarationSyntax _ => "class",
                MethodDeclarationSyntax _ => "method",
                PropertyDeclarationSyntax _ => "property",
                FieldDeclarationSyntax _ => "field",
                NamespaceDeclarationSyntax _ => "namespace",
                BlockSyntax _ => "block",
                StatementSyntax _ => "statement",
                _ => null
            };

            if (typeName == null) return null;

            // Add name if available
            var name = GetNodeName(node);
            if (!string.IsNullOrEmpty(name))
                return $"{typeName}[{name}]";

            // Add position for statements
            if (node is StatementSyntax && node.Parent != null)
            {
                var siblings = node.Parent.ChildNodes()
                    .Where(n => n is StatementSyntax)
                    .ToList();
                var index = siblings.IndexOf(node) + 1;
                return $"{typeName}[{index}]";
            }

            return typeName;
        }

        private static string GetNodeName(SyntaxNode node)
        {
            return node switch
            {
                ClassDeclarationSyntax classDecl => classDecl.Identifier.Text,
                MethodDeclarationSyntax methodDecl => methodDecl.Identifier.Text,
                PropertyDeclarationSyntax propDecl => propDecl.Identifier.Text,
                FieldDeclarationSyntax fieldDecl => fieldDecl.Declaration.Variables.FirstOrDefault()?.Identifier.Text,
                NamespaceDeclarationSyntax nsDecl => nsDecl.Name.ToString(),
                _ => null
            };
        }

        private static NodeLocation GetLocation(SyntaxNode node)
        {
            var lineSpan = node.GetLocation().GetLineSpan();
            return new NodeLocation
            {
                StartLine = lineSpan.StartLinePosition.Line + 1,
                StartColumn = lineSpan.StartLinePosition.Character + 1,
                EndLine = lineSpan.EndLinePosition.Line + 1,
                EndColumn = lineSpan.EndLinePosition.Character + 1
            };
        }
    }

    public class NodeResult
    {
        public SyntaxNode Node { get; set; }
        public string Text { get; set; }
        public NodeLocation Location { get; set; }
        public string NodeType { get; set; }
        public string Path { get; set; }
    }

    public class NodeLocation
    {
        public int StartLine { get; set; }
        public int StartColumn { get; set; }
        public int EndLine { get; set; }
        public int EndColumn { get; set; }

        public override string ToString()
        {
            return $"{StartLine}:{StartColumn}";
        }
    }
}