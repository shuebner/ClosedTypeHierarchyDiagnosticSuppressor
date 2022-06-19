using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;

namespace ClosedTypeHierarchyDiagnosticSuppressor.Tests;
static class CompilationHelper
{
    public static Compilation CreateCompilation(
        string? code = null,
        NullableContextOptions nullableContextOptions = NullableContextOptions.Disable)
    {
        var syntaxTrees = code is null
            ? null
            : new[] { CSharpSyntaxTree.ParseText(code) };

        return CSharpCompilation.Create(
            Guid.NewGuid().ToString("N"),
            syntaxTrees,
            references: new MetadataReference[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                reportSuppressedDiagnostics: true,
                nullableContextOptions: nullableContextOptions));
    }
}
