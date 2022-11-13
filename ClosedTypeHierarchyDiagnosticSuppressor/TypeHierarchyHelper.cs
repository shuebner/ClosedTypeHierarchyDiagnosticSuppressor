using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace SvSoft.Analyzers.ClosedTypeHerarchyDiagnosticSuppression;

public static class TypeHierarchyHelper
{
    public static IEnumerable<INamedTypeSymbol>? InterpretAsClosedTypeHierarchy(INamedTypeSymbol typeSymbol, bool allowRecords)
    {
        if (!IsPartOfClosedHierarchy(typeSymbol))
        {
            return null;
        }

        return GetConcreteSubtypes(typeSymbol);

        bool IsPartOfClosedHierarchy(INamedTypeSymbol typeSymbol)
        {
            if (CanBeClosedHierarchyRoot(typeSymbol))
            {
                var nestedTypes = typeSymbol.GetMembers().OfType<INamedTypeSymbol>();
                var subtypes = nestedTypes.Where(t => typeSymbol.Equals(t.BaseType, SymbolEqualityComparer.Default));

                return subtypes.All(IsPartOfClosedHierarchy);
            }

            return CanBeClosedHierarchyLeaf(typeSymbol);
        }

        IEnumerable<INamedTypeSymbol> GetConcreteSubtypes(INamedTypeSymbol typeSymbol)
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

        bool CanBeClosedHierarchyRoot(INamedTypeSymbol rootCandidate) =>
            rootCandidate.IsAbstract &&
            (allowRecords
                ? HasOnlyPrivateConstructorsAndProtectedCopyCtors(rootCandidate)
                : HasOnlyPrivateConstructors(rootCandidate));

        static bool HasOnlyPrivateConstructors(INamedTypeSymbol rootCandidate) =>
            rootCandidate.Constructors.All(c => c.DeclaredAccessibility == Accessibility.Private);

        static bool HasOnlyPrivateConstructorsAndProtectedCopyCtors(INamedTypeSymbol rootCandidate) =>
            HasOnlyPrivateConstructors(rootCandidate) ||
            (IsRecord(rootCandidate) &&
            rootCandidate.Constructors.All(c => c.DeclaredAccessibility == Accessibility.Private || MatchesImplicitlyCreatedRecordCopyCtor(rootCandidate, c)));

        const string CompilerCreatedCloneMethodNameOnRecordTypes = "<Clone>$";

        static bool IsRecord(INamedTypeSymbol recordCandidate) =>
            recordCandidate.MemberNames.Contains(CompilerCreatedCloneMethodNameOnRecordTypes);

        static bool MatchesImplicitlyCreatedRecordCopyCtor(INamedTypeSymbol constructedType, IMethodSymbol ctor) =>
            ctor.DeclaredAccessibility == Accessibility.Protected &&
            ctor.Parameters.Length == 1 &&
            ctor.Parameters[0].Type.Equals(constructedType, SymbolEqualityComparer.Default);

        static bool CanBeClosedHierarchyLeaf(INamedTypeSymbol typeSymbol) => typeSymbol.IsSealed;
    }
}
