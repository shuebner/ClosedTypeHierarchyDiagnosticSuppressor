using Microsoft.CodeAnalysis;
using NUnit.Framework;
using SvSoft.Analyzers.ClosedTypeHerarchyDiagnosticSuppression;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClosedTypeHierarchyDiagnosticSuppressor.Tests;
class TypeHierarchyHelperTests
{
    static string WrapInNamespace(string code) => $@"
namespace MyCode
{{
{code}
}}
";    

    static INamedTypeSymbol GetRootTypeCandidate(string typeCode, string typeName)
    {
        var completeCode = WrapInNamespace(typeCode);
        Compilation compilation = CompilationHelper.CreateCompilation(completeCode);
        INamedTypeSymbol t = compilation.GetSymbolsWithName(typeName)
            .OfType<INamedTypeSymbol>()
            .Single();

        return t;
    }

    [Test]
    [TestCaseSource(nameof(ClosedSamples))]
    public void When_type_hierarchy_is_closed_Then_returns_leaf_types(string typeCode, string[] expectedSubTypes)
    {
        INamedTypeSymbol type = GetRootTypeCandidate(typeCode, "Root");

        var subtypes = TypeHierarchyHelper.InterpretAsClosedTypeHierarchy(type, false);

        Assert.That(subtypes, Is.Not.Null);
        Assert.That(subtypes!.Select(s => s.Name), Is.EquivalentTo(expectedSubTypes));
    }

    [Test]
    [TestCaseSource(nameof(NotClosedSamples))]
    public void When_type_hierarchy_is_not_closed_Then_returns_null(string typeCode, bool allowProtectedCopyCtors)
    {
        INamedTypeSymbol type = GetRootTypeCandidate(typeCode, "Root");

        var subtypes = TypeHierarchyHelper.InterpretAsClosedTypeHierarchy(type, allowRecords: allowProtectedCopyCtors);

        Assert.That(subtypes, Is.Null);
    }

    [Test]
    [TestCaseSource(nameof(ProtectedCopyConstructorOnlySamples))]
    public void When_type_hierarchy_is_closed_except_for_copy_ctor_And_copy_ctor_is_not_explicitly_allowed_Then_returns_null(string typeCode, string[] _)
    {
        INamedTypeSymbol type = GetRootTypeCandidate(typeCode, "Root");

        var subtypes = TypeHierarchyHelper.InterpretAsClosedTypeHierarchy(type, false);

        Assert.That(subtypes, Is.Null);
    }

    [Test]
    [TestCaseSource(nameof(ProtectedCopyConstructorOnlySamples))]
    public void When_type_hierarchy_is_closed_except_for_copy_ctor_And_copy_ctor_is_explicitly_allowed_Then_returns_leaf_types(string typeCode, string[] expectedSubTypes)
    {
        INamedTypeSymbol type = GetRootTypeCandidate(typeCode, "Root");

        var subtypes = TypeHierarchyHelper.InterpretAsClosedTypeHierarchy(type, true);

        Assert.That(subtypes, Is.Not.Null);
        Assert.That(subtypes!.Select(s => s.Name), Is.EquivalentTo(expectedSubTypes));
    }

    public static readonly IEnumerable<TestCaseData> ClosedSamples = new (string SampleName, string TypeCode, string[] ExpectedTypeNames)[]
    {
        ("Simple", TypeHierarchies.Closed.Simple, new[] { "Leaf1", "Leaf2" }),
        ("Nested", TypeHierarchies.Closed.Nested, new[] { "Leaf1", "Leaf2", "Leaf3" }),
        ("Generic", TypeHierarchies.Closed.Generic, new[] { "Leaf1", "Leaf2" })
    }.Select(t => new TestCaseData(t.TypeCode, t.ExpectedTypeNames).SetArgDisplayNames(t.SampleName));

    public static readonly IEnumerable<TestCaseData> ProtectedCopyConstructorOnlySamples = new (string SampleName, string TypeCode, string[] ExpectedTypeNames)[]
    {
        ("ImplicitProtectedCopyCtor", TypeHierarchies.ProtectedCopyConstructorOnly.ImplicitProtectedCopyCtor, new[] { "Leaf1", "Leaf2" }),
    }.Select(t => new TestCaseData(t.TypeCode, t.ExpectedTypeNames).SetArgDisplayNames(t.SampleName));

    public static readonly IEnumerable<TestCaseData> NotClosedSamples = typeof(TypeHierarchies.NotClosed)
        .GetFields()
        .Select(f => (SampleName: f.Name, TypeCode: f.GetValue(null)))
        .SelectMany(t => new[] { true, false }.Select(allowCopyCtor => (allowCopyCtor, t))
        .Select(t => new TestCaseData(t.t.TypeCode, t.allowCopyCtor).SetArgDisplayNames(t.t.SampleName, t.allowCopyCtor ? "allow copy ctor" : "disallow copy ctor")));

    public static readonly IEnumerable<string> NotClosedCodeOnlySamples = typeof(TypeHierarchies.NotClosed)
        .GetFields()
        .Select(f => (string)f.GetValue(null)!);
}
