using Microsoft.CodeAnalysis;

namespace McpRoslyn.Server;

public static class SymbolExtensions
{
    public static IEnumerable<INamedTypeSymbol> GetAllTypes(this IAssemblySymbol assembly)
    {
        var stack = new Stack<INamespaceOrTypeSymbol>();
        stack.Push(assembly.GlobalNamespace);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            
            if (current is INamedTypeSymbol type)
            {
                yield return type;
            }

            foreach (var member in current.GetMembers())
            {
                if (member is INamespaceOrTypeSymbol nsOrType)
                {
                    stack.Push(nsOrType);
                }
            }
        }
    }
}