using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace ClosedTypeHierarchyDiagnosticSuppressor.Tests;

public static class DiagnosticSuppressorAnalyer
{
    public static Task EnsureNotSuppressed(
        DiagnosticSuppressor suppressor,
        string testCode,
        NullableContextOptions nullableContextOptions = NullableContextOptions.Disable,
        params (string DiagnosticId, DiagnosticAnalyzer Analyzer)[] additionalAnalyzerOptions) =>
        EnsureSuppressed(suppressor, Array.Empty<SuppressionDescriptor>(), testCode, nullableContextOptions, additionalAnalyzerOptions);

    public static async Task EnsureSuppressed(
        DiagnosticSuppressor suppressor,
        IEnumerable<SuppressionDescriptor> suppressionDescriptors,
        string testCode,
        NullableContextOptions nullableContextOptions = NullableContextOptions.Disable,
        params (string DiagnosticId, DiagnosticAnalyzer Analyzer)[] additionalAnalyzerOptions)
    {
        if (suppressionDescriptors.Any())
        {
            Assert.That(suppressor.SupportedSuppressions, Is.SupersetOf(suppressionDescriptors));
        }

        Compilation compilation = CompilationHelper.CreateCompilation(
            testCode,
            nullableContextOptions);

        var additionalAnalyzers = additionalAnalyzerOptions.Select(p => p.Analyzer).ToImmutableArray();
        Compilation compilationWithWarnings;
        ImmutableArray<Diagnostic> compilationErrors;
        if (additionalAnalyzers.Any())
        {
            compilationWithWarnings = compilation.WithOptions(compilation.Options
                .WithSpecificDiagnosticOptions(additionalAnalyzerOptions.Select(p => KeyValuePair.Create(p.DiagnosticId, ReportDiagnostic.Warn))));
            var compilationWithoutSuppressor = compilationWithWarnings.WithAnalyzers(additionalAnalyzers);
            compilationErrors = await compilationWithoutSuppressor.GetAllDiagnosticsAsync();
        }
        else
        {
            compilationWithWarnings = compilation;
            compilationErrors = compilationWithWarnings.GetDiagnostics();
        }

        ImmutableArray<Diagnostic> nonHiddenErrors = compilationErrors
            .Where(d => d.Severity != DiagnosticSeverity.Hidden)
            .ToImmutableArray();

        ImmutableArray<Diagnostic> suppressibleErrors = nonHiddenErrors
            .Where(d => suppressor.SupportedSuppressions.Any(s => s.SuppressedDiagnosticId == d.Id))
            .ToImmutableArray();

        Assert.That(nonHiddenErrors, Is.EquivalentTo(suppressibleErrors), "there were non-suppressible errors");
        Assert.That(suppressibleErrors, Is.Not.Empty, "expected errors to suppress, but there weren't any");

        var compilationWithSuppressor = compilationWithWarnings.WithAnalyzers(additionalAnalyzers.Add(suppressor));

        ImmutableArray<Diagnostic> analyzerErrors = await compilationWithSuppressor.GetAllDiagnosticsAsync().ConfigureAwait(false);

        nonHiddenErrors = analyzerErrors
            .Where(d => d.Severity != DiagnosticSeverity.Hidden)
            .ToImmutableArray();

        Assert.That(nonHiddenErrors, Is.Not.Empty);

        Assert.Multiple(() =>
        {
            foreach (var error in nonHiddenErrors)
            {
                if (!suppressionDescriptors.Any())
                {
                    Assert.That(error.IsSuppressed, Is.False, $"Is suppressed even though it should not be: {error}");
                }
                else
                {
                    Assert.That(error.IsSuppressed, Is.True, $"Is not suppressed even though it should be: {error}");
                    if (error.IsSuppressed)
                    {
                        AssertProgrammaticSuppression(error, suppressionDescriptors);
                    }
                }
            }
        });
    }

    static void AssertProgrammaticSuppression(Diagnostic error, IEnumerable<SuppressionDescriptor> suppressionDescriptors)
    {
        // we cannot check for the correct suppression nicely.
        // DiagnosticWithProgrammaticSuppression is a private class.
        // ProgrammaticSuppressionInfo is an internal class.
        // resorting to reflection...
        Type errorType = error.GetType();
        Assert.That(errorType.Name, Is.EqualTo("DiagnosticWithProgrammaticSuppression"));

        var programmaticSuppressionInfoProperty = errorType.GetProperty("ProgrammaticSuppressionInfo", BindingFlags.Instance | BindingFlags.NonPublic);

        if (programmaticSuppressionInfoProperty is null)
        {
            Assert.Fail("expected a property with the name 'ProgrammaticSuppressionInfo'");
            return;
        }

        var programmaticSuppressionInfo = programmaticSuppressionInfoProperty.GetValue(error);

        if (programmaticSuppressionInfo is not null)
        {
            Type programmaticSuppressionInfoType = programmaticSuppressionInfo.GetType();
            Assert.That(programmaticSuppressionInfoType.Name, Is.EqualTo("ProgrammaticSuppressionInfo"));

            var suppressionsProperty = programmaticSuppressionInfoType.GetProperty("Suppressions");
            if (suppressionsProperty is not null)
            {
                var suppressions = suppressionsProperty.GetValue(programmaticSuppressionInfo);

                if (suppressions is ImmutableHashSet<(string Id, LocalizableString Justification)> suppressionsHashSet)
                {
                    Assert.That(suppressionDescriptors.Select(s => (s.Id, s.Justification)), Is.SupersetOf(suppressionsHashSet));
                }
            }
        }
    }
}