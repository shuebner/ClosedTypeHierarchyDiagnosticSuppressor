using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

namespace SvSoft.Analyzers.ClosedTypeHerarchyDiagnosticSuppression;
static class PatternHelper
{
    public static bool HandlesTypeWithoutRestrictions(PatternSyntax patternSyntax, INamedTypeSymbol type, SemanticModel model, Compilation compilation)
    {
        SyntaxNode? typeSource = patternSyntax switch
        {
            ConstantPatternSyntax constantPattern => constantPattern.Expression switch
            {
                MemberAccessExpressionSyntax memberAccess => memberAccess.Name,
                IdentifierNameSyntax identifier => identifier,
                _ => null
            },
            TypePatternSyntax typePattern => typePattern.Type,
            DeclarationPatternSyntax declaration => declaration.Type,
            RecursivePatternSyntax recursive => recursive.Type,
            _ => null
        };

        if (typeSource is null)
        {
            return false;
        }

        if (model.GetSymbolInfo(typeSource).Symbol is not INamedTypeSymbol namedType)
        {
            return false;
        }

        bool matchedTypeIsSubtype = compilation.HasImplicitConversion(type, namedType);

        return patternSyntax switch
        {
            RecursivePatternSyntax recursivePatternSyntax => matchedTypeIsSubtype && IsRecursivePatternNonRestrictive(recursivePatternSyntax),
            _ => matchedTypeIsSubtype
        };

        bool IsSubpatternNonRestrictive(SubpatternSyntax subpatternSyntax) =>
            subpatternSyntax.Pattern switch
            {
                VarPatternSyntax => true,
                RecursivePatternSyntax sub => IsRecursivePatternNonRestrictive(sub),
                DeclarationPatternSyntax declaration => IsDeclarationPatternNonRestrictive(declaration, subpatternSyntax),
                _ => false


            };

        bool IsRecursivePatternNonRestrictive(RecursivePatternSyntax recursivePatternSyntax) =>
            recursivePatternSyntax.PropertyPatternClause?.Subpatterns.All(IsSubpatternNonRestrictive) ?? false;

        bool IsDeclarationPatternNonRestrictive(DeclarationPatternSyntax declarationPatternSyntax, SubpatternSyntax containingSubpatternSyntax)
        {
            SymbolInfo declaredSymbolInfo = model.GetSymbolInfo(declarationPatternSyntax.Type);

            if (declaredSymbolInfo.Symbol is INamedTypeSymbol declaredType)
            {
                if (containingSubpatternSyntax.NameColon is { Name: IdentifierNameSyntax propertyName })
                {
                    ISymbol? maybePropertySymbol = model.GetSymbolInfo(propertyName).Symbol;
                    if (maybePropertySymbol is IPropertySymbol propertySymbol)
                    {
                        if (compilation.HasImplicitConversion(propertySymbol.Type, declaredType))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
