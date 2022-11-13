using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SvSoft.Analyzers.ClosedTypeHierarchyDiagnosticSuppression;
static class OptionsHelper
{
    public static bool AreRecordHierarchiesAllowed(this SuppressionAnalysisContext context, SyntaxTree syntaxTree)
    {
        AnalyzerConfigOptions options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(syntaxTree);
        bool allowRecords = options.TryGetValue("dotnet_diagnostic.CTH001.suppress_on_record_hierarchies", out string? allowRecordHierarchiesStr) &&
            bool.TryParse(allowRecordHierarchiesStr, out bool allowRecordHierarchies)
            && allowRecordHierarchies;

        return allowRecords;
    }
}
