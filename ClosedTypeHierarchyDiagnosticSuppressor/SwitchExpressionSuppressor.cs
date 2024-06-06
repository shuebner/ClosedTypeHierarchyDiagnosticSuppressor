using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SvSoft.Analyzers.ClosedTypeHierarchyDiagnosticSuppression;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace SvSoft.Analyzers.ClosedTypeHerarchyDiagnosticSuppression;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SwitchExpressionSuppressor : DiagnosticSuppressor
{
    readonly bool _forceAllowRecords;

    public SwitchExpressionSuppressor() : this(false) { }
    
    /// <summary>Constructor to facilitate unit testing</summary>
    internal SwitchExpressionSuppressor(bool forceAllowRecords)
    {
        _forceAllowRecords = forceAllowRecords;
    }
    
    static readonly string[] SuppressedDiagnosticIds = { "CS8509", "IDE0072" };

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
        }

        void HandleDiagnostic(Diagnostic diagnostic)
        {
            SyntaxNode? node = diagnostic.Location.SourceTree?
                .GetRoot(context.CancellationToken)
                .FindNode(diagnostic.Location.SourceSpan);

            if (node is null)
            {
                return;
            }

            bool allowRecords = _forceAllowRecords || context.AreRecordHierarchiesAllowed(node.SyntaxTree);

            var switchExpression = node.DescendantNodesAndSelf().OfType<SwitchExpressionSyntax>().FirstOrDefault();

            ExpressionSyntax switchee = switchExpression.GoverningExpression;
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

            bool mustHandleNullCase = switcheeTypeInfo.Nullability switch
            {
                // e. g. implicitly typed with 'var' but non-null according to flow analysis
                { Annotation: NullableAnnotation.Annotated, FlowState: NullableFlowState.NotNull } => false,

                // explicitly typed non-null
                { Annotation: NullableAnnotation.NotAnnotated } => false,

                // workaround for a bug in Roslyn: see https://github.com/dotnet/roslyn/issues/59875
                { Annotation: NullableAnnotation.None } => switcheeModel.GetSymbolInfo(switchee).Symbol switch
                {
                    IParameterSymbol { NullableAnnotation: NullableAnnotation.NotAnnotated } => false,
                    ILocalSymbol { NullableAnnotation: NullableAnnotation.NotAnnotated } => false,
                    _ => true
                },
                _ => true
            };

            var arms = switchExpression.Arms;

            if (mustHandleNullCase && !arms.Any(HandlesNullCase))
            {
                return;
            }

            var unhandledSubtypes = subtypes.Where(t => !arms.Any(a => ArmHandlesTypeWithoutRestrictions(a, t)));
            if (unhandledSubtypes.Any())
            {
                return;
            }

            context.ReportSuppression(Suppression.Create(SuppressionDescriptorByDiagnosticId[diagnostic.Id], diagnostic));

            static bool HandlesNullCase(SwitchExpressionArmSyntax a) =>
                // we bail at when clauses and do not try to understand them
                // both "_" and "null" match null
                a.WhenClause is null && a.Pattern is
                    DiscardPatternSyntax or
                    ConstantPatternSyntax { Expression: LiteralExpressionSyntax { Token: SyntaxToken { Value: null } } };

            bool ArmHandlesTypeWithoutRestrictions(SwitchExpressionArmSyntax a, INamedTypeSymbol t) =>
                // we bail at when clauses and do not try to understand them
                a.WhenClause is null && PatternHelper.HandlesTypeWithoutRestrictions(a.Pattern, t, switcheeModel, context.Compilation);
        }
    }
}
