using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FSharp.Compiler.Syntax;
using McpRoslyn.Server.FSharp.FSharpPath;
using Microsoft.Extensions.Logging;
using Microsoft.FSharp.Collections;

namespace McpRoslyn.Server.FSharp.Tools;

/// <summary>
/// MCP tool for getting F# AST structure.
/// </summary>
public class FSharpGetAstTool
{
    private readonly FSharpWorkspaceManager _workspaceManager;
    private readonly ILogger<FSharpGetAstTool> _logger;
    private readonly FSharpPathEvaluator _evaluator;

    public FSharpGetAstTool(FSharpWorkspaceManager workspaceManager, ILogger<FSharpGetAstTool> logger)
    {
        _workspaceManager = workspaceManager;
        _logger = logger;
        _evaluator = new FSharpPathEvaluator();
    }

    public async Task<object> ExecuteAsync(string filePath, string? root = null, int depth = 3, bool includeRange = true)
    {
        try
        {
            _logger.LogInformation("Getting F# AST for file: {FilePath}", filePath);

            if (!FSharpFileDetector.IsFSharpFile(filePath))
            {
                return new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = $"Not an F# file: {filePath}"
                        }
                    },
                    isError = true
                };
            }

            // Parse and check the file
            var (success, parseResults, checkResults, diagnostics) = await _workspaceManager.ParseAndCheckFileAsync(filePath);
            
            if (!success || parseResults == null)
            {
                return new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = $"Failed to parse F# file: {filePath}"
                        }
                    },
                    isError = true
                };
            }

            // Get the root node(s)
            object rootNode = parseResults.ParseTree;
            if (!string.IsNullOrEmpty(root))
            {
                var results = _evaluator.Evaluate(root, parseResults.ParseTree);
                if (results.Count == 0)
                {
                    return new
                    {
                        content = new[]
                        {
                            new
                            {
                                type = "text",
                                text = $"No nodes found matching FSharpPath: {root}"
                            }
                        },
                        isError = true
                    };
                }
                rootNode = results.First();
            }

            // Build AST representation
            var astNode = BuildAstNode(rootNode, depth, includeRange);

            // Format as tree
            var treeText = FormatAstTree(astNode, 0);

            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = $"# F# AST Structure\n\nFile: {filePath}\n{(!string.IsNullOrEmpty(root) ? $"Root: {root}\n" : "")}Depth: {depth}\n\n```\n{treeText}```"
                    }
                },
                ast = astNode
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting F# AST");
            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = $"Error getting F# AST: {ex.Message}"
                    }
                },
                isError = true
            };
        }
    }

    private AstNode BuildAstNode(object node, int remainingDepth, bool includeRange)
    {
        var astNode = new AstNode
        {
            Type = GetNodeType(node),
            Name = GetNodeName(node),
            Kind = GetNodeKind(node)
        };

        if (includeRange)
        {
            var range = GetNodeRange(node);
            if (range != null)
            {
                astNode.Location = new NodeLocation
                {
                    StartLine = range.Value.StartLine,
                    StartColumn = range.Value.StartColumn,
                    EndLine = range.Value.EndLine,
                    EndColumn = range.Value.EndColumn
                };
            }
        }

        // Add attributes
        astNode.Attributes = GetNodeAttributes(node);

        // Add children if depth allows
        if (remainingDepth > 0)
        {
            astNode.Children = GetNodeChildren(node)
                .Select(child => BuildAstNode(child, remainingDepth - 1, includeRange))
                .ToList();
        }

        return astNode;
    }

    private string GetNodeType(object node)
    {
        return node switch
        {
            ParsedInput => "ParsedInput",
            SynModuleOrNamespace => "Module",
            SynModuleDecl => "ModuleDeclaration",
            SynBinding => "Binding",
            SynTypeDefn => "TypeDefinition",
            SynExpr => "Expression",
            SynPat => "Pattern",
            SynType => "Type",
            SynValData => "ValueData",
            _ => node.GetType().Name
        };
    }

    private string GetNodeKind(object node)
    {
        return node switch
        {
            ParsedInput.ImplFile => "ImplementationFile",
            ParsedInput.SigFile => "SignatureFile",
            SynModuleDecl.Let => "Let",
            SynModuleDecl.Types => "Types",
            SynModuleDecl.Open => "Open",
            SynModuleDecl.NestedModule => "NestedModule",
            SynExpr.App => "Application",
            SynExpr.Lambda => "Lambda",
            SynExpr.Match => "Match",
            SynExpr.LetOrUse => "LetOrUse",
            SynExpr.Sequential => "Sequential",
            SynExpr.Ident => "Identifier",
            SynExpr.Const => "Constant",
            SynPat.Named => "Named",
            SynPat.Typed => "Typed",
            SynPat.Wild => "Wildcard",
            SynPat.Const => "Constant",
            _ => ""
        };
    }

    private string? GetNodeName(object node)
    {
        return node switch
        {
            SynBinding binding => GetBindingName(binding),
            SynTypeDefn typeDef => GetTypeDefnName(typeDef),
            SynModuleOrNamespace module => string.Join(".", module.longId.Select(id => id.idText)),
            SynModuleDecl.NestedModule nestedModule => GetNestedModuleName(nestedModule),
            SynExpr.Ident ident => ident.ident.idText,
            SynPat.Named named => named.ident.ident.idText,
            _ => null
        };
    }

    private Dictionary<string, object> GetNodeAttributes(object node)
    {
        var attributes = new Dictionary<string, object>();

        switch (node)
        {
            case SynBinding binding:
                if (IsFunctionBinding(binding))
                {
                    attributes["isFunction"] = true;
                }
                if (IsRecursiveBinding(binding))
                {
                    attributes["isRecursive"] = true;
                }
                if (IsMutableBinding(binding))
                {
                    attributes["isMutable"] = true;
                }
                break;

            case SynTypeDefn typeDef:
                attributes["typeKind"] = GetTypeKind(typeDef);
                break;

            case SynExpr.Const constExpr:
                attributes["value"] = GetConstantValue(constExpr.constant);
                break;

            case SynModuleOrNamespace module:
                attributes["isModule"] = module.kind.IsModule;
                break;
        }

        return attributes;
    }

    private List<object> GetNodeChildren(object node)
    {
        var children = new List<object>();

        switch (node)
        {
            case ParsedInput.ImplFile implFile:
                children.AddRange(implFile.Item.Contents);
                break;

            case SynModuleOrNamespace module:
                children.AddRange(module.decls);
                break;

            case SynModuleDecl.Let let:
                children.AddRange(let.bindings);
                break;

            case SynModuleDecl.Types types:
                children.AddRange(types.typeDefns);
                break;

            case SynModuleDecl.NestedModule nestedModule:
                children.Add(nestedModule.moduleInfo);
                children.AddRange(nestedModule.decls);
                break;

            case SynBinding binding:
                children.Add(binding.headPat); // pattern
                children.Add(binding.expr); // expression
                break;

            case SynTypeDefn typeDef:
                children.Add(typeDef.typeRepr); // representation
                if (!ListModule.IsEmpty(typeDef.members)) // members
                {
                    children.AddRange(typeDef.members);
                }
                break;

            case SynExpr.App app:
                children.Add(app.funcExpr); // function
                children.Add(app.argExpr); // argument
                break;

            case SynExpr.Lambda lambda:
                children.Add(lambda.args); // parameters
                children.Add(lambda.body); // body
                break;

            case SynExpr.Match match:
                children.Add(match.expr); // expression
                children.AddRange(match.clauses); // clauses
                break;

            case SynExpr.LetOrUse letOrUse:
                children.AddRange(letOrUse.bindings); // bindings
                children.Add(letOrUse.body); // body
                break;

            case SynExpr.Sequential seq:
                children.Add(seq.expr1); // expr1
                children.Add(seq.expr2); // expr2
                break;
        }

        return children;
    }

    private string FormatAstTree(AstNode node, int indent)
    {
        var indentStr = new string(' ', indent * 2);
        var lines = new List<string>();

        // Format node
        var nodeLine = $"{indentStr}{node.Type}";
        if (!string.IsNullOrEmpty(node.Kind))
        {
            nodeLine += $" [{node.Kind}]";
        }
        if (!string.IsNullOrEmpty(node.Name))
        {
            nodeLine += $" '{node.Name}'";
        }
        if (node.Location != null)
        {
            nodeLine += $" @ {node.Location.StartLine}:{node.Location.StartColumn}";
        }
        if (node.Attributes.Count > 0)
        {
            var attrs = string.Join(", ", node.Attributes.Select(a => $"{a.Key}={a.Value}"));
            nodeLine += $" ({attrs})";
        }
        lines.Add(nodeLine);

        // Format children
        foreach (var child in node.Children)
        {
            lines.Add(FormatAstTree(child, indent + 1));
        }

        return string.Join("\n", lines);
    }

    private global::FSharp.Compiler.Text.Range? GetNodeRange(object node)
    {
        return node switch
        {
            SynBinding binding => binding.range,
            SynTypeDefn typeDef => typeDef.range,
            SynModuleOrNamespace module => module.range,
            SynExpr expr => expr.Range,
            SynPat pattern => pattern.Range,
            SynType type => type.Range,
            _ => null
        };
    }

    private string? GetBindingName(SynBinding binding)
    {
        return binding.headPat switch
        {
            SynPat.Named named => named.ident.ident.idText,
            SynPat.LongIdent longIdent => longIdent.longDotId.LongIdent.Last().idText,
            _ => null
        };
    }

    private string? GetTypeDefnName(SynTypeDefn typeDef)
    {
        var typeInfo = typeDef.typeInfo;
        return typeInfo.longId.Last().idText;
    }

    private string? GetNestedModuleName(SynModuleDecl.NestedModule nestedModule)
    {
        var moduleInfo = nestedModule.moduleInfo;
        return moduleInfo.longId.Last().idText;
    }

    private bool IsFunctionBinding(SynBinding binding)
    {
        return binding.valData is SynValData valData &&
               !ListModule.IsEmpty(valData.SynValInfo.CurriedArgInfos);
    }

    private bool IsRecursiveBinding(SynBinding binding)
    {
        // This would require context from parent
        return false;
    }

    private bool IsMutableBinding(SynBinding binding)
    {
        // Check if binding is mutable
        // This would require checking the SynValData flags
        return false;
    }

    private string GetTypeKind(SynTypeDefn typeDef)
    {
        return typeDef.typeRepr switch
        {
            SynTypeDefnRepr.Simple simple => simple.simpleRepr switch
            {
                SynTypeDefnSimpleRepr.Union => "Union",
                SynTypeDefnSimpleRepr.Record => "Record",
                SynTypeDefnSimpleRepr.Enum => "Enum",
                SynTypeDefnSimpleRepr.TypeAbbrev => "TypeAbbreviation",
                _ => "Simple"
            },
            SynTypeDefnRepr.ObjectModel => "ObjectModel",
            _ => "Unknown"
        };
    }

    private object GetConstantValue(SynConst constant)
    {
        if (constant is SynConst.Bool b)
            return b.Item;
        if (constant is SynConst.Int32 i)
            return i.Item;
        if (constant is SynConst.String s)
            return s.text;
        if (constant is SynConst.Char c)
            return c.Item;
        if (constant is SynConst.Double d)
            return d.Item;
        if (constant is SynConst.Single f)
            return f.Item;
        if (constant.IsUnit)
            return "()";
        
        return constant.ToString();
    }

    private class AstNode
    {
        public string Type { get; set; } = "";
        public string? Kind { get; set; }
        public string? Name { get; set; }
        public NodeLocation? Location { get; set; }
        public Dictionary<string, object> Attributes { get; set; } = new();
        public List<AstNode> Children { get; set; } = new();
    }

    private class NodeLocation
    {
        public int StartLine { get; set; }
        public int StartColumn { get; set; }
        public int EndLine { get; set; }
        public int EndColumn { get; set; }
    }
}