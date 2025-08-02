using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.FSharp.Compiler.Syntax;
using Microsoft.FSharp.Compiler.Text;

namespace McpRoslyn.Server.FSharp
{
    /// <summary>
    /// Represents a node in the F# AST for FSharpPath navigation
    /// </summary>
    public class FSharpNode
    {
        private readonly object _astNode;
        private readonly FSharpNode? _parent;
        private readonly List<FSharpNode> _children = new();

        public FSharpNode(object astNode, FSharpNode? parent)
        {
            _astNode = astNode;
            _parent = parent;
            InitializeChildren();
        }

        public FSharpNode? Parent => _parent;

        public FSharpNodeType NodeType => DetermineNodeType();

        public bool IsAsync => CheckIsAsync();
        public bool IsStatic => CheckIsStatic();
        public bool IsMutable => CheckIsMutable();
        public bool IsRecursive => CheckIsRecursive();
        public bool IsInline => CheckIsInline();
        public bool IsActivePattern => CheckIsActivePattern();

        public IEnumerable<FSharpNode> GetChildren() => _children;

        public IEnumerable<FSharpNode> GetDescendants()
        {
            foreach (var child in _children)
            {
                yield return child;
                foreach (var descendant in child.GetDescendants())
                {
                    yield return descendant;
                }
            }
        }

        public string? GetName()
        {
            return _astNode switch
            {
                SynModuleDecl.Let(bindings: var bindings) when bindings.Count > 0 => 
                    GetBindingName(bindings[0]),
                    
                SynModuleDecl.Types(typeDefns: var types) when types.Count > 0 => 
                    GetTypeName(types[0]),
                    
                SynBinding binding => GetBindingName(binding),
                
                SynTypeDefn typeDefn => GetTypeName(typeDefn),
                
                SynMemberDefn.Member(memberBinding: var binding) => GetBindingName(binding),
                
                SynMemberDefn.PropertyGetSet(memberBindingForGet: var getBinding) => 
                    getBinding != null ? GetBindingName(getBinding) : null,
                    
                SynExpr.Match(clauses: var clauses) => "match",
                
                SynMatchClause clause => "clause",
                
                _ => null
            };
        }

        public string? GetText(string? sourceText)
        {
            if (sourceText == null || !(_astNode is ISynNode synNode))
                return null;

            var range = GetRange();
            if (range == null)
                return null;

            // Extract text from source using range
            // This is a simplified implementation - actual implementation would use the range
            // to extract the exact text from the source
            return null;
        }

        public string? GetAccessibility()
        {
            return _astNode switch
            {
                SynBinding binding => GetAccessibilityFromBinding(binding),
                SynTypeDefn typeDefn => GetAccessibilityFromTypeDefn(typeDefn),
                SynMemberDefn.Member(memberBinding: var binding) => GetAccessibilityFromBinding(binding),
                _ => null
            };
        }

        private void InitializeChildren()
        {
            switch (_astNode)
            {
                case ParsedInput.ImplFile(contents: var implFile):
                    AddModules(implFile.Contents);
                    break;

                case ParsedInput.SigFile(contents: var sigFile):
                    AddSignatureModules(sigFile.Contents);
                    break;

                case SynModuleOrNamespace module:
                    AddModuleDecls(module.Decls);
                    break;

                case SynModuleDecl.Let(bindings: var bindings):
                    foreach (var binding in bindings)
                    {
                        _children.Add(new FSharpNode(binding, this));
                    }
                    break;

                case SynModuleDecl.Types(typeDefns: var types):
                    foreach (var type in types)
                    {
                        _children.Add(new FSharpNode(type, this));
                    }
                    break;

                case SynTypeDefn typeDefn:
                    AddTypeMembers(typeDefn.Members);
                    break;

                case SynBinding binding:
                    if (binding.Expr != null)
                    {
                        _children.Add(new FSharpNode(binding.Expr, this));
                    }
                    break;

                case SynExpr expr:
                    AddExpressionChildren(expr);
                    break;
            }
        }

        private void AddModules(IList<SynModuleOrNamespace> modules)
        {
            foreach (var module in modules)
            {
                _children.Add(new FSharpNode(module, this));
            }
        }

        private void AddSignatureModules(IList<SynModuleOrNamespaceSig> modules)
        {
            foreach (var module in modules)
            {
                _children.Add(new FSharpNode(module, this));
            }
        }

        private void AddModuleDecls(IList<SynModuleDecl> decls)
        {
            foreach (var decl in decls)
            {
                _children.Add(new FSharpNode(decl, this));
            }
        }

        private void AddTypeMembers(IList<SynMemberDefn> members)
        {
            foreach (var member in members)
            {
                _children.Add(new FSharpNode(member, this));
            }
        }

        private void AddExpressionChildren(SynExpr expr)
        {
            switch (expr)
            {
                case SynExpr.Match(clauses: var clauses):
                    foreach (var clause in clauses)
                    {
                        _children.Add(new FSharpNode(clause, this));
                    }
                    break;

                case SynExpr.LetOrUse(bindings: var bindings, body: var body):
                    foreach (var binding in bindings)
                    {
                        _children.Add(new FSharpNode(binding, this));
                    }
                    _children.Add(new FSharpNode(body, this));
                    break;

                case SynExpr.Sequential(expr1: var expr1, expr2: var expr2):
                    _children.Add(new FSharpNode(expr1, this));
                    _children.Add(new FSharpNode(expr2, this));
                    break;

                case SynExpr.Lambda(body: var body):
                    _children.Add(new FSharpNode(body, this));
                    break;
            }
        }

        private FSharpNodeType DetermineNodeType()
        {
            return _astNode switch
            {
                SynModuleOrNamespace _ => FSharpNodeType.Module,
                SynModuleDecl.Let _ => FSharpNodeType.Binding,
                SynModuleDecl.Types _ => FSharpNodeType.Type,
                SynBinding _ => FSharpNodeType.Binding,
                SynTypeDefn _ => FSharpNodeType.Type,
                SynMemberDefn.Member _ => FSharpNodeType.Member,
                SynMemberDefn.PropertyGetSet _ => FSharpNodeType.Property,
                SynExpr _ => FSharpNodeType.Expression,
                SynMatchClause _ => FSharpNodeType.MatchClause,
                SynPat _ => FSharpNodeType.Pattern,
                _ => FSharpNodeType.Unknown
            };
        }

        private string? GetBindingName(SynBinding binding)
        {
            return binding.Pattern switch
            {
                SynPat.Named(ident: var ident) => ident.idText,
                SynPat.LongIdent(longDotId: var longId) => longId.LongIdent.Last().idText,
                _ => null
            };
        }

        private string? GetTypeName(SynTypeDefn typeDefn)
        {
            return typeDefn.Info switch
            {
                SynComponentInfo(longId: var longId) => longId.Last().idText,
                _ => null
            };
        }

        private string? GetAccessibilityFromBinding(SynBinding binding)
        {
            if (binding.Accessibility == null)
                return "public"; // Default in F#

            return binding.Accessibility.Value switch
            {
                SynAccess.Public => "public",
                SynAccess.Internal => "internal",
                SynAccess.Private => "private",
                _ => null
            };
        }

        private string? GetAccessibilityFromTypeDefn(SynTypeDefn typeDefn)
        {
            var componentInfo = typeDefn.Info as SynComponentInfo;
            if (componentInfo?.Accessibility == null)
                return "public"; // Default in F#

            return componentInfo.Accessibility.Value switch
            {
                SynAccess.Public => "public",
                SynAccess.Internal => "internal",
                SynAccess.Private => "private",
                _ => null
            };
        }

        private Range? GetRange()
        {
            return _astNode switch
            {
                ISynNode synNode => synNode.Range,
                _ => null
            };
        }

        private bool CheckIsAsync()
        {
            if (_astNode is SynBinding binding)
            {
                // Check if the binding's expression is an async computation expression
                return binding.Expr is SynExpr.App app && 
                       app.FunctionExpr is SynExpr.Ident ident &&
                       ident.Ident.idText == "async";
            }
            return false;
        }

        private bool CheckIsStatic()
        {
            if (_astNode is SynMemberDefn.Member(memberBinding: var binding))
            {
                return binding.MemberFlags?.IsInstance == false;
            }
            return false;
        }

        private bool CheckIsMutable()
        {
            if (_astNode is SynBinding binding)
            {
                return binding.IsMutable;
            }
            return false;
        }

        private bool CheckIsRecursive()
        {
            // Check if parent is a recursive let binding
            if (_parent?._astNode is SynModuleDecl.Let(isRecursive: var isRec))
            {
                return isRec;
            }
            return false;
        }

        private bool CheckIsInline()
        {
            if (_astNode is SynBinding binding)
            {
                return binding.IsInline;
            }
            return false;
        }

        private bool CheckIsActivePattern()
        {
            if (_astNode is SynBinding binding)
            {
                return binding.Pattern is SynPat.Named(ident: var ident) &&
                       (ident.idText.StartsWith("|") && ident.idText.EndsWith("|"));
            }
            return false;
        }

        public override bool Equals(object? obj)
        {
            return obj is FSharpNode other && ReferenceEquals(_astNode, other._astNode);
        }

        public override int GetHashCode()
        {
            return _astNode.GetHashCode();
        }
    }

    /// <summary>
    /// Types of nodes in the F# AST
    /// </summary>
    public enum FSharpNodeType
    {
        Unknown,
        Module,
        Type,
        Function,
        Value,
        Pattern,
        Expression,
        MatchClause,
        Binding,
        Member,
        Property,
        Record,
        Union,
        Enum,
        Interface,
        Class,
        Attribute,
        Open
    }
}