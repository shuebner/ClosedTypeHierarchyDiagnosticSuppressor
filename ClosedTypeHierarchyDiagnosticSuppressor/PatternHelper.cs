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

        bool IsRecursivePatternNonRestrictive(RecursivePatternSyntax recursivePatternSyntax) =>
            (recursivePatternSyntax.PropertyPatternClause?.Subpatterns ?? recursivePatternSyntax.PositionalPatternClause?.Subpatterns)
            ?.Select((p, i) => (Pattern: p, Index: i)).All(pair => IsSubpatternNonRestrictive(pair.Pattern, pair.Index)) ?? false;

        bool IsSubpatternNonRestrictive(SubpatternSyntax subpatternSyntax, int index) =>
            subpatternSyntax.Pattern switch
            {
                VarPatternSyntax => true,
                RecursivePatternSyntax sub => IsRecursivePatternNonRestrictive(sub),
                DeclarationPatternSyntax declaration => IsDeclarationPatternNonRestrictive(declaration, subpatternSyntax, index),
                _ => false
            };

        bool IsDeclarationPatternNonRestrictive(
            DeclarationPatternSyntax declarationPatternSyntax,
            SubpatternSyntax containingSubpatternSyntax,
            int index)
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

                if (containingSubpatternSyntax is { Parent: PositionalPatternClauseSyntax positionalPattern, Pattern: DeclarationPatternSyntax declaration })
                {
                    var symbol = model.GetSymbolInfo(positionalPattern).Symbol;
                    if (symbol is IMethodSymbol { Name: "Deconstruct" } deconstructMethod)
                    {
                        var correspondingParameterIndex = deconstructMethod.IsExtensionMethod
                            ? index + 1
                            : index;
                        var positionalType = deconstructMethod.Parameters[correspondingParameterIndex].Type;

                        if (compilation.HasImplicitConversion(positionalType, declaredType))
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
