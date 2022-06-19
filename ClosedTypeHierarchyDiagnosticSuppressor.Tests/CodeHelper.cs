namespace ClosedTypeHierarchyDiagnosticSuppressor.Tests;
static class CodeHelper
{
    public static string WrapInNamespace(string code) => $@"
namespace MyCode
{{
{code}
}}
";
}
