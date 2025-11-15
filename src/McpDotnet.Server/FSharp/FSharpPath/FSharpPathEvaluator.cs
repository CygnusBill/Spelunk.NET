using System;
using System.Collections.Generic;
using System.Linq;
using FSharp.Compiler.Syntax;
using Microsoft.FSharp.Collections;

namespace Spelunk.Server.FSharp.FSharpPath;

/// <summary>
/// Evaluates FSharpPath queries against F# AST nodes.
/// </summary>
public class FSharpPathEvaluator
{
    private readonly FSharpPathParser _parser;

    public FSharpPathEvaluator()
    {
        _parser = new FSharpPathParser();
    }

    /// <summary>
    /// Evaluates an FSharpPath query against a parse tree.
    /// </summary>
    public List<object> Evaluate(string query, ParsedInput parsedInput)
    {
        var parsedQuery = _parser.Parse(query);
        var results = new List<object> { parsedInput };

        foreach (var step in parsedQuery.Steps)
        {
            results = EvaluateStep(results, step);
        }

        return results;
    }

    private List<object> EvaluateStep(List<object> context, FSharpPathStep step)
    {
        var results = new List<object>();

        foreach (var node in context)
        {
            var stepResults = GetNodesForAxis(node, step.Axis);
            var filtered = FilterByNodeTest(stepResults, step.NodeTest);
            var predicateFiltered = ApplyPredicates(filtered, step.Predicates);
            results.AddRange(predicateFiltered);
        }

        return results.Distinct().ToList();
    }

    private List<object> GetNodesForAxis(object node, FSharpPathAxis axis)
    {
        return axis switch
        {
            FSharpPathAxis.Child => GetChildren(node),
            FSharpPathAxis.Descendant => GetDescendants(node),
            FSharpPathAxis.DescendantOrSelf => GetDescendantOrSelf(node),
            FSharpPathAxis.Parent => GetParent(node),
            FSharpPathAxis.Ancestor => GetAncestors(node),
            FSharpPathAxis.AncestorOrSelf => GetAncestorOrSelf(node),
            FSharpPathAxis.FollowingSibling => GetFollowingSiblings(node),
            FSharpPathAxis.PrecedingSibling => GetPrecedingSiblings(node),
            _ => new List<object>()
        };
    }

    private List<object> GetChildren(object node)
    {
        var children = new List<object>();

        switch (node)
        {
            case ParsedInput.ImplFile implFile:
                foreach (var moduleOrNamespace in implFile.Item.Contents)
                {
                    children.Add(moduleOrNamespace);
                }
                break;

            case SynModuleOrNamespace moduleOrNamespace:
                foreach (var decl in moduleOrNamespace.decls)
                {
                    children.Add(decl);
                }
                break;

            case SynModuleDecl moduleDecl:
                children.AddRange(GetModuleDeclChildren(moduleDecl));
                break;

            case SynExpr expr:
                children.AddRange(GetExprChildren(expr));
                break;

            case SynPat pat:
                children.AddRange(GetPatternChildren(pat));
                break;

            case SynType type:
                children.AddRange(GetTypeChildren(type));
                break;
        }

        return children;
    }

    private List<object> GetModuleDeclChildren(SynModuleDecl decl)
    {
        var children = new List<object>();

        switch (decl)
        {
            case SynModuleDecl.Let let:
                if (let.isRecursive) // isRecursive
                {
                    foreach (var binding in let.bindings)
                    {
                        children.Add(binding);
                    }
                }
                else
                {
                    foreach (var binding in let.bindings)
                    {
                        children.Add(binding);
                    }
                }
                break;

            case SynModuleDecl.Types types:
                foreach (var typeDef in types.typeDefns)
                {
                    children.Add(typeDef);
                }
                break;

            case SynModuleDecl.NestedModule nestedModule:
                children.Add(nestedModule.moduleInfo);
                children.AddRange(nestedModule.decls);
                break;

            case SynModuleDecl.Open open:
                children.Add(open.target);
                break;
        }

        return children;
    }

    private List<object> GetExprChildren(SynExpr expr)
    {
        var children = new List<object>();

        switch (expr)
        {
            case SynExpr.App app:
                children.Add(app.funcExpr); // funcExpr
                children.Add(app.argExpr); // argExpr
                break;

            case SynExpr.Lambda lambda:
                children.Add(lambda.body); // body
                break;

            case SynExpr.LetOrUse letOrUse:
                foreach (var binding in letOrUse.bindings)
                {
                    children.Add(binding);
                }
                children.Add(letOrUse.body); // body
                break;

            case SynExpr.Match match:
                children.Add(match.expr); // expr
                foreach (var clause in match.clauses)
                {
                    children.Add(clause);
                }
                break;

            case SynExpr.Sequential seq:
                children.Add(seq.expr1); // expr1
                children.Add(seq.expr2); // expr2
                break;
        }

        return children;
    }

    private List<object> GetPatternChildren(SynPat pattern)
    {
        var children = new List<object>();

        switch (pattern)
        {
            case SynPat.Named named:
                children.Add(named.ident);
                break;

            case SynPat.Typed typed:
                children.Add(typed.pat); // pattern
                children.Add(typed.targetType); // type
                break;

            case SynPat.Or orPat:
                children.Add(orPat.lhsPat); // left
                children.Add(orPat.rhsPat); // right
                break;

            case SynPat.Tuple tuple:
                foreach (var pat in tuple.elementPats)
                {
                    children.Add(pat);
                }
                break;
        }

        return children;
    }

    private List<object> GetTypeChildren(SynType type)
    {
        var children = new List<object>();

        switch (type)
        {
            case SynType.Fun fun:
                children.Add(fun.argType); // argType
                children.Add(fun.returnType); // returnType
                break;

            case SynType.Tuple tuple:
                foreach (var segment in tuple.path)
                {
                    if (segment is SynTupleTypeSegment.Type typeSegment)
                    {
                        children.Add(typeSegment.typeName);
                    }
                }
                break;

            case SynType.App app:
                children.Add(app.typeName); // typeName
                foreach (var typeArg in app.typeArgs)
                {
                    children.Add(typeArg);
                }
                break;
        }

        return children;
    }

    private List<object> GetDescendants(object node)
    {
        var descendants = new List<object>();
        var children = GetChildren(node);
        
        foreach (var child in children)
        {
            descendants.Add(child);
            descendants.AddRange(GetDescendants(child));
        }

        return descendants;
    }

    private List<object> GetDescendantOrSelf(object node)
    {
        var result = new List<object> { node };
        result.AddRange(GetDescendants(node));
        return result;
    }

    private List<object> GetParent(object node)
    {
        // This would require maintaining parent references
        // For now, return empty list
        return new List<object>();
    }

    private List<object> GetAncestors(object node)
    {
        // This would require maintaining parent references
        // For now, return empty list
        return new List<object>();
    }

    private List<object> GetAncestorOrSelf(object node)
    {
        var result = new List<object> { node };
        result.AddRange(GetAncestors(node));
        return result;
    }

    private List<object> GetFollowingSiblings(object node)
    {
        // This would require maintaining sibling references
        // For now, return empty list
        return new List<object>();
    }

    private List<object> GetPrecedingSiblings(object node)
    {
        // This would require maintaining sibling references
        // For now, return empty list
        return new List<object>();
    }

    private List<object> FilterByNodeTest(List<object> nodes, FSharpNodeTest nodeTest)
    {
        if (nodeTest.Type == FSharpNodeType.Any)
        {
            return nodes;
        }

        return nodes.Where(node => MatchesNodeTest(node, nodeTest)).ToList();
    }

    private bool MatchesNodeTest(object node, FSharpNodeTest nodeTest)
    {
        return nodeTest.Type switch
        {
            FSharpNodeType.Any => true,
            FSharpNodeType.Let => node is SynBinding,
            FSharpNodeType.Function => node is SynBinding binding && IsFunctionBinding(binding),
            FSharpNodeType.Value => node is SynBinding binding && !IsFunctionBinding(binding),
            FSharpNodeType.Type => node is SynTypeDefn,
            FSharpNodeType.Module => node is SynModuleOrNamespace || node is SynModuleDecl.NestedModule,
            FSharpNodeType.Union => node is SynTypeDefn typeDef && IsUnionType(typeDef),
            FSharpNodeType.Record => node is SynTypeDefn typeDef && IsRecordType(typeDef),
            FSharpNodeType.Match => node is SynExpr.Match,
            FSharpNodeType.Pattern => node is SynPat,
            FSharpNodeType.Class => node is SynTypeDefn typeDef && IsClassType(typeDef),
            FSharpNodeType.Interface => node is SynTypeDefn typeDef && IsInterfaceType(typeDef),
            FSharpNodeType.Named => MatchesName(node, nodeTest.Name),
            _ => false
        };
    }

    private bool IsFunctionBinding(SynBinding binding)
    {
        // Check if the binding has parameters (making it a function)
        return binding.valData is SynValData valData &&
               !ListModule.IsEmpty(valData.SynValInfo.CurriedArgInfos);
    }

    private bool IsUnionType(SynTypeDefn typeDef)
    {
        return typeDef.typeRepr is SynTypeDefnRepr.Simple simple &&
               simple.simpleRepr is SynTypeDefnSimpleRepr.Union;
    }

    private bool IsRecordType(SynTypeDefn typeDef)
    {
        return typeDef.typeRepr is SynTypeDefnRepr.Simple simple &&
               simple.simpleRepr is SynTypeDefnSimpleRepr.Record;
    }

    private bool IsClassType(SynTypeDefn typeDef)
    {
        return typeDef.typeRepr is SynTypeDefnRepr.ObjectModel;
    }

    private bool IsInterfaceType(SynTypeDefn typeDef)
    {
        return typeDef.typeRepr is SynTypeDefnRepr.Simple simple &&
               simple.simpleRepr is SynTypeDefnSimpleRepr.TypeAbbrev;
    }

    private bool MatchesName(object node, string? name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        // Extract name from various node types
        var nodeName = node switch
        {
            SynBinding binding => GetBindingName(binding),
            SynTypeDefn typeDef => GetTypeDefnName(typeDef),
            SynModuleOrNamespace module => GetModuleName(module),
            _ => null
        };

        return nodeName?.Equals(name, StringComparison.OrdinalIgnoreCase) == true;
    }

    private string? GetBindingName(SynBinding binding)
    {
        // Extract name from binding pattern
        return binding.headPat switch
        {
            SynPat.Named named => named.ident.ident.idText,
            SynPat.LongIdent longIdent => longIdent.longDotId.LongIdent.Last().idText,
            _ => null
        };
    }

    private string? GetTypeDefnName(SynTypeDefn typeDef)
    {
        // Extract name from type definition
        var typeInfo = typeDef.typeInfo;
        return typeInfo.longId.Last().idText;
    }

    private string? GetModuleName(SynModuleOrNamespace module)
    {
        // Extract module name
        return module.longId.Last().idText;
    }

    private string? GetNestedModuleName(SynModuleDecl.NestedModule nestedModule)
    {
        // Extract nested module name
        var moduleInfo = nestedModule.moduleInfo;
        return moduleInfo.longId.Last().idText;
    }

    private List<object> ApplyPredicates(List<object> nodes, List<FSharpPredicate> predicates)
    {
        var result = nodes;

        foreach (var predicate in predicates)
        {
            result = ApplyPredicate(result, predicate);
        }

        return result;
    }

    private List<object> ApplyPredicate(List<object> nodes, FSharpPredicate predicate)
    {
        return predicate.Type switch
        {
            FSharpPredicateType.Position => ApplyPositionPredicate(nodes, predicate),
            FSharpPredicateType.Attribute => ApplyAttributePredicate(nodes, predicate),
            FSharpPredicateType.BooleanAttribute => ApplyBooleanAttributePredicate(nodes, predicate),
            FSharpPredicateType.TypeTest => ApplyTypeTestPredicate(nodes, predicate),
            _ => nodes
        };
    }

    private List<object> ApplyPositionPredicate(List<object> nodes, FSharpPredicate predicate)
    {
        if (int.TryParse(predicate.Value, out var position) && position > 0 && position <= nodes.Count)
        {
            return new List<object> { nodes[position - 1] };
        }
        return new List<object>();
    }

    private List<object> ApplyAttributePredicate(List<object> nodes, FSharpPredicate predicate)
    {
        return nodes.Where(node => CheckAttribute(node, predicate)).ToList();
    }

    private List<object> ApplyBooleanAttributePredicate(List<object> nodes, FSharpPredicate predicate)
    {
        return nodes.Where(node => CheckBooleanAttribute(node, predicate)).ToList();
    }

    private List<object> ApplyTypeTestPredicate(List<object> nodes, FSharpPredicate predicate)
    {
        return predicate.Value?.ToLower() switch
        {
            "union" => nodes.Where(n => n is SynTypeDefn td && IsUnionType(td)).ToList(),
            "record" => nodes.Where(n => n is SynTypeDefn td && IsRecordType(td)).ToList(),
            "class" => nodes.Where(n => n is SynTypeDefn td && IsClassType(td)).ToList(),
            "interface" => nodes.Where(n => n is SynTypeDefn td && IsInterfaceType(td)).ToList(),
            _ => nodes
        };
    }

    private bool CheckAttribute(object node, FSharpPredicate predicate)
    {
        var attrValue = GetAttributeValue(node, predicate.AttributeName);
        if (attrValue == null) return false;

        return predicate.Operator switch
        {
            "=" => attrValue.Equals(predicate.Value, StringComparison.OrdinalIgnoreCase),
            "!=" => !attrValue.Equals(predicate.Value, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private bool CheckBooleanAttribute(object node, FSharpPredicate predicate)
    {
        return predicate.AttributeName?.ToLower() switch
        {
            "recursive" => node is SynBinding binding && IsRecursiveBinding(binding),
            "async" => node is SynExpr expr && IsAsyncExpression(expr),
            "mutable" => node is SynBinding binding && IsMutableBinding(binding),
            "inline" => node is SynBinding binding && IsInlineBinding(binding),
            _ => false
        };
    }

    private string? GetAttributeValue(object node, string? attributeName)
    {
        if (attributeName == null) return null;

        return attributeName.ToLower() switch
        {
            "name" => GetNodeName(node),
            "type" => GetNodeType(node),
            _ => null
        };
    }

    private string? GetNodeName(object node)
    {
        return node switch
        {
            SynBinding binding => GetBindingName(binding),
            SynTypeDefn typeDef => GetTypeDefnName(typeDef),
            SynModuleOrNamespace module => GetModuleName(module),
            SynModuleDecl.NestedModule nestedModule => GetNestedModuleName(nestedModule),
            _ => null
        };
    }

    private string GetNodeType(object node)
    {
        return node switch
        {
            SynBinding => "binding",
            SynTypeDefn => "type",
            SynModuleOrNamespace => "module",
            SynModuleDecl.NestedModule => "module",
            SynExpr => "expression",
            SynPat => "pattern",
            _ => node.GetType().Name
        };
    }

    private bool IsRecursiveBinding(SynBinding binding)
    {
        // Check if binding is part of recursive let
        // This would require context from parent
        return false;
    }

    private bool IsAsyncExpression(SynExpr expr)
    {
        return expr is SynExpr.App app && 
               app.funcExpr is SynExpr.Ident ident && 
               ident.ident.idText == "async";
    }

    private bool IsMutableBinding(SynBinding binding)
    {
        // Check if binding is mutable
        // This would require checking the SynValData flags
        return false;
    }

    private bool IsInlineBinding(SynBinding binding)
    {
        // Check if binding is inline
        // This would require checking the SynValData flags
        return false;
    }
}