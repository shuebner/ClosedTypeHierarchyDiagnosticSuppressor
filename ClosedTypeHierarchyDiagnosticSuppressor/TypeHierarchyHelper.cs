using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace SvSoft.Analyzers.ClosedTypeHerarchyDiagnosticSuppression;

public static class TypeHierarchyHelper
{
    public static IEnumerable<INamedTypeSymbol>? InterpretAsClosedTypeHierarchy(INamedTypeSymbol typeSymbol)
    {
        if (!IsPartOfClosedHierarchy(typeSymbol))
        {
            return null;
        }

        return GetConcreteSubtypes(typeSymbol);

        static bool IsPartOfClosedHierarchy(INamedTypeSymbol typeSymbol)
        {
            if (CanBeClosedHierarchyRoot(typeSymbol))
            {
                var nestedTypes = typeSymbol.GetMembers().OfType<INamedTypeSymbol>();
                var subtypes = nestedTypes.Where(t => typeSymbol.Equals(t.BaseType, SymbolEqualityComparer.Default));

                return subtypes.All(IsPartOfClosedHierarchy);
            }

            return CanBeClosedHierarchyLeaf(typeSymbol);
        }

        static IEnumerable<INamedTypeSymbol> GetConcreteSubtypes(INamedTypeSymbol typeSymbol)
        {
            if (CanBeClosedHierarchyRoot(typeSymbol))
            {
                var nestedTypes = typeSymbol.GetMembers().OfType<INamedTypeSymbol>();
                foreach (var nestedType in nestedTypes)
                {
                    if (typeSymbol.Equals(nestedType.BaseType, SymbolEqualityComparer.Default))
                    {
                        foreach (var concreteType in GetConcreteSubtypes(nestedType))
                        {
                            yield return concreteType;
                        }
                    }
                }
            }

            if (CanBeClosedHierarchyLeaf(typeSymbol))
            {
                yield return typeSymbol;
            }
        }

        static bool CanBeClosedHierarchyRoot(INamedTypeSymbol rootCandidate) =>
            rootCandidate.IsAbstract &&
            rootCandidate.Constructors.All(c => c.DeclaredAccessibility == Accessibility.Private);

        static bool CanBeClosedHierarchyLeaf(INamedTypeSymbol typeSymbol) => typeSymbol.IsSealed;
    }
}
