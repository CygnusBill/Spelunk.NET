using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Spelunk.Server;

/// <summary>
/// Extracts semantic information from Roslyn syntax nodes
/// </summary>
public class SemanticInfoExtractor
{
    private readonly SemanticModel _semanticModel;
    private readonly Document _document;

    public SemanticInfoExtractor(SemanticModel semanticModel, Document document)
    {
        _semanticModel = semanticModel;
        _document = document;
    }

    public Dictionary<string, object> ExtractSemanticInfo(SyntaxNode node)
    {
        var info = new Dictionary<string, object>();

        // Get symbol info for the node
        var symbol = GetSymbolForNode(node);
        if (symbol != null)
        {
            AddSymbolInfo(info, symbol);
        }

        // Get type info for expressions
        if (node is ExpressionSyntax expression)
        {
            AddExpressionTypeInfo(info, expression);
        }

        // Get specific info based on node type
        switch (node)
        {
            case MethodDeclarationSyntax method:
                AddMethodInfo(info, method, symbol as IMethodSymbol);
                break;
            case PropertyDeclarationSyntax property:
                AddPropertyInfo(info, property, symbol as IPropertySymbol);
                break;
            case VariableDeclaratorSyntax variable:
                AddVariableInfo(info, variable);
                break;
            case ParameterSyntax parameter:
                AddParameterInfo(info, parameter);
                break;
            case ClassDeclarationSyntax classDecl:
                AddTypeInfo(info, classDecl, symbol as INamedTypeSymbol);
                break;
            case LocalDeclarationStatementSyntax localDecl:
                AddLocalDeclarationInfo(info, localDecl);
                break;
        }

        // Add diagnostics if any
        var diagnostics = _semanticModel.GetDiagnostics(node.Span);
        if (diagnostics.Any())
        {
            AddDiagnostics(info, diagnostics);
        }

        // Add project context
        if (_document.Project != null)
        {
            info["project"] = _document.Project.Name;
            info["assembly"] = _document.Project.AssemblyName;
        }

        return info;
    }

    private ISymbol? GetSymbolForNode(SyntaxNode node)
    {
        switch (node)
        {
            case MethodDeclarationSyntax method:
                return _semanticModel.GetDeclaredSymbol(method);
            case PropertyDeclarationSyntax property:
                return _semanticModel.GetDeclaredSymbol(property);
            case ClassDeclarationSyntax classDecl:
                return _semanticModel.GetDeclaredSymbol(classDecl);
            case VariableDeclaratorSyntax variable:
                return _semanticModel.GetDeclaredSymbol(variable);
            case ParameterSyntax parameter:
                return _semanticModel.GetDeclaredSymbol(parameter);
            default:
                return _semanticModel.GetSymbolInfo(node).Symbol;
        }
    }

    private void AddSymbolInfo(Dictionary<string, object> info, ISymbol symbol)
    {
        info["symbolKind"] = symbol.Kind.ToString();
        info["symbolName"] = symbol.ToDisplayString();
        info["accessibility"] = symbol.DeclaredAccessibility.ToString().ToLowerInvariant();

        // Add modifiers
        var modifiers = new List<string>();
        if (symbol.IsStatic) modifiers.Add("static");
        if (symbol.IsVirtual) modifiers.Add("virtual");
        if (symbol.IsOverride) modifiers.Add("override");
        if (symbol.IsAbstract) modifiers.Add("abstract");
        if (symbol.IsSealed) modifiers.Add("sealed");
        if (symbol.IsExtern) modifiers.Add("extern");
        
        if (modifiers.Any())
        {
            info["modifiers"] = modifiers.ToArray();
        }

        // Add containing type and namespace
        if (symbol.ContainingType != null)
        {
            info["containingType"] = symbol.ContainingType.ToDisplayString();
        }
        
        if (symbol.ContainingNamespace != null && !symbol.ContainingNamespace.IsGlobalNamespace)
        {
            info["containingNamespace"] = symbol.ContainingNamespace.ToDisplayString();
        }
    }

    private void AddExpressionTypeInfo(Dictionary<string, object> info, ExpressionSyntax expression)
    {
        var typeInfo = _semanticModel.GetTypeInfo(expression);
        if (typeInfo.Type != null)
        {
            info["type"] = typeInfo.Type.ToDisplayString();
            
            // Add element type for arrays/collections
            if (typeInfo.Type is IArrayTypeSymbol arrayType)
            {
                info["elementType"] = arrayType.ElementType.ToDisplayString();
            }
        }
    }

    private void AddMethodInfo(Dictionary<string, object> info, MethodDeclarationSyntax method, IMethodSymbol? methodSymbol)
    {
        if (methodSymbol == null) return;

        // Return type
        info["returnType"] = methodSymbol.ReturnType.ToDisplayString();

        // Parameters
        if (methodSymbol.Parameters.Any())
        {
            info["parameters"] = methodSymbol.Parameters.Select(p => new Dictionary<string, object?>
            {
                ["name"] = p.Name,
                ["type"] = p.Type.ToDisplayString(),
                ["isOptional"] = p.IsOptional,
                ["defaultValue"] = p.HasExplicitDefaultValue ? p.ExplicitDefaultValue?.ToString() ?? "null" : null
            }).ToArray();
        }

        // Type parameters
        if (methodSymbol.TypeParameters.Any())
        {
            info["typeParameters"] = methodSymbol.TypeParameters.Select(tp => tp.Name).ToArray();
        }

        // Method-specific flags
        info["isAsync"] = methodSymbol.IsAsync;
        info["isExtensionMethod"] = methodSymbol.IsExtensionMethod;
        info["isGeneric"] = methodSymbol.IsGenericMethod;

        // Add async modifier if applicable
        if (methodSymbol.IsAsync && info.ContainsKey("modifiers"))
        {
            var modifiers = ((string[])info["modifiers"]).ToList();
            modifiers.Add("async");
            info["modifiers"] = modifiers.ToArray();
        }
        else if (methodSymbol.IsAsync)
        {
            info["modifiers"] = new[] { "async" };
        }
    }

    private void AddPropertyInfo(Dictionary<string, object> info, PropertyDeclarationSyntax property, IPropertySymbol? propertySymbol)
    {
        if (propertySymbol == null) return;

        info["type"] = propertySymbol.Type.ToDisplayString();
        
        // Property-specific info
        var accessors = new List<string>();
        if (propertySymbol.GetMethod != null) accessors.Add("get");
        if (propertySymbol.SetMethod != null) accessors.Add("set");
        info["accessors"] = accessors.ToArray();
    }

    private void AddVariableInfo(Dictionary<string, object> info, VariableDeclaratorSyntax variable)
    {
        var symbol = _semanticModel.GetDeclaredSymbol(variable);
        if (symbol is ILocalSymbol localSymbol)
        {
            info["type"] = localSymbol.Type.ToDisplayString();
            info["symbolKind"] = "Variable";
        }
        else if (symbol is IFieldSymbol fieldSymbol)
        {
            info["type"] = fieldSymbol.Type.ToDisplayString();
            info["symbolKind"] = "Field";
        }
    }

    private void AddParameterInfo(Dictionary<string, object> info, ParameterSyntax parameter)
    {
        var symbol = _semanticModel.GetDeclaredSymbol(parameter);
        if (symbol != null)
        {
            info["type"] = symbol.Type.ToDisplayString();
            info["symbolKind"] = "Parameter";
            info["isOptional"] = symbol.IsOptional;
            
            if (symbol.HasExplicitDefaultValue)
            {
                info["defaultValue"] = symbol.ExplicitDefaultValue?.ToString() ?? "null";
            }
        }
    }

    private void AddTypeInfo(Dictionary<string, object> info, ClassDeclarationSyntax classDecl, INamedTypeSymbol? typeSymbol)
    {
        if (typeSymbol == null) return;

        // Base type
        if (typeSymbol.BaseType != null && typeSymbol.BaseType.SpecialType != SpecialType.System_Object)
        {
            info["baseType"] = typeSymbol.BaseType.ToDisplayString();
        }

        // Interfaces
        if (typeSymbol.Interfaces.Any())
        {
            info["interfaces"] = typeSymbol.Interfaces.Select(i => i.ToDisplayString()).ToArray();
        }

        // Type parameters
        if (typeSymbol.TypeParameters.Any())
        {
            info["typeParameters"] = typeSymbol.TypeParameters.Select(tp => tp.Name).ToArray();
            info["isGeneric"] = true;
        }
    }

    private void AddLocalDeclarationInfo(Dictionary<string, object> info, LocalDeclarationStatementSyntax localDecl)
    {
        var variables = new List<Dictionary<string, object>>();
        
        foreach (var variable in localDecl.Declaration.Variables)
        {
            var symbol = _semanticModel.GetDeclaredSymbol(variable);
            if (symbol is ILocalSymbol local)
            {
                var varInfo = new Dictionary<string, object>
                {
                    ["name"] = local.Name,
                    ["type"] = local.Type.ToDisplayString()
                };

                // Check definite assignment
                var dataFlow = _semanticModel.AnalyzeDataFlow(variable);
                if (dataFlow.Succeeded)
                {
                    varInfo["definiteAssignment"] = dataFlow.DefinitelyAssignedOnEntry.Contains(local);
                }

                variables.Add(varInfo);
            }
        }

        if (variables.Any())
        {
            info["variables"] = variables.ToArray();
        }
    }

    private void AddDiagnostics(Dictionary<string, object> info, IEnumerable<Diagnostic> diagnostics)
    {
        var diagnosticList = diagnostics.Select(d => new Dictionary<string, object>
        {
            ["severity"] = d.Severity.ToString(),
            ["code"] = d.Id,
            ["message"] = d.GetMessage()
        }).ToArray();

        info["diagnostics"] = diagnosticList;
        info["hasErrors"] = diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
    }
}