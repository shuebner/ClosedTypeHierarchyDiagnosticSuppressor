using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SvSoft.Analyzers.ClosedTypeHierarchyDiagnosticSuppression;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace SvSoft.Analyzers.ClosedTypeHerarchyDiagnosticSuppression;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SwitchStatementSuppressor : DiagnosticSuppressor
{
    static readonly string[] SuppressedDiagnosticIds = { "IDE0010" };

    public static readonly IReadOnlyDictionary<string, SuppressionDescriptor> SuppressionDescriptorByDiagnosticId = SuppressedDiagnosticIds.ToDictionary(
        id => id,
        id => new SuppressionDescriptor("CTH001", id, "every possible type of closed type hierarchy was matched without restrictions"));

    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions { get; } = ImmutableArray.CreateRange(SuppressionDescriptorByDiagnosticId.Values);
    
    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        foreach (Diagnostic diagnostic in context.ReportedDiagnostics)
        {
            if (SuppressedDiagnosticIds.Contains(diagnostic.Id))
            {
                HandleDiagnostic(diagnostic);
            }

            void HandleDiagnostic(Diagnostic diagnostic)
            {
                SyntaxNode? node = diagnostic.Location.SourceTree?
                    .GetRoot(context.CancellationToken)
                    .FindNode(diagnostic.Location.SourceSpan);

                if (node is not SwitchStatementSyntax switchStatement)
                {
                    return;
                }

                bool allowRecords = context.AreRecordHierarchiesAllowed(node.SyntaxTree);

                ExpressionSyntax switchee = switchStatement.Expression;
                var switcheeModel = context.GetSemanticModel(switchee.SyntaxTree);
                var switcheeTypeInfo = switcheeModel.GetTypeInfo(switchee);
                if (switcheeTypeInfo.Type is not INamedTypeSymbol switcheeType)
                {
                    return;
                }

                if (TypeHierarchyHelper.InterpretAsClosedTypeHierarchy(switcheeType, allowRecords) is not IEnumerable<INamedTypeSymbol> subtypes)
                {
                    return;
                }

                bool mustHandleNullCase = switcheeTypeInfo.Nullability is { Annotation: NullableAnnotation.Annotated, FlowState: NullableFlowState.MaybeNull }
                                                                       or { Annotation: NullableAnnotation.None };

                var sections = switchStatement.Sections;
                if (mustHandleNullCase && !sections.Any(SectionHandlesNullCase))
                {
                    return;
                }

                var unhandledSubtypes = subtypes.Where(t => !sections.Any(a => SectionHandlesTypeWithoutRestrictions(a, t)));
                if (unhandledSubtypes.Any())
                {
                    return;
                }

                context.ReportSuppression(Suppression.Create(SuppressionDescriptorByDiagnosticId[diagnostic.Id], diagnostic));

                static bool SectionHandlesNullCase(SwitchSectionSyntax s) =>
                    s.Labels.Any(LabelHandlesNullCase);

                static bool LabelHandlesNullCase(SwitchLabelSyntax s) =>
                    s switch
                    {
                        CaseSwitchLabelSyntax { Value: LiteralExpressionSyntax { Token: SyntaxToken { Value: null } } } => true,
                        CasePatternSwitchLabelSyntax { Pattern: var p } => PatternHelper.HandlesNull(p),
                        _ => false
                    };

                bool SectionHandlesTypeWithoutRestrictions(SwitchSectionSyntax s, INamedTypeSymbol t) =>
                    s.Labels.Any(s => LabelHandlesTypeWithoutRestriction(s, t));

                bool LabelHandlesTypeWithoutRestriction(SwitchLabelSyntax s, INamedTypeSymbol t) =>
                    s switch
                    {
                        CaseSwitchLabelSyntax @case => CaseSwitchLabelHandlesTypeWithoutRestriction(@case, t),
                        CasePatternSwitchLabelSyntax { WhenClause: null } casePattern => PatternHelper.HandlesTypeWithoutRestrictions(casePattern.Pattern, t, switcheeModel, context.Compilation),
                        _ => false
                    };

                bool CaseSwitchLabelHandlesTypeWithoutRestriction(CaseSwitchLabelSyntax s, INamedTypeSymbol t) =>
                    switcheeModel.GetSymbolInfo(s.Value).Symbol is INamedTypeSymbol type && context.Compilation.HasImplicitConversion(t, type);
            }
        }
    }
}
