﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;
using SvSoft.Analyzers.ClosedTypeHerarchyDiagnosticSuppression;
using System;
using System.Threading.Tasks;

namespace ClosedTypeHierarchyDiagnosticSuppressor.Tests;
class SwitchStatementSuppressorTests
{
    static readonly DiagnosticAnalyzer IDE0010Analyzer = (DiagnosticAnalyzer)(Activator.CreateInstance(
        "Microsoft.CodeAnalysis.CSharp.CodeStyle",
        "Microsoft.CodeAnalysis.CSharp.PopulateSwitch.CSharpPopulateSwitchStatementDiagnosticAnalyzer")?.Unwrap()
        ?? throw new InvalidOperationException("could not instantiate populate switch statement analyzer for IDE0072"));

    Task EnsureNotSuppressed(string code, NullableContextOptions nullableContextOptions) =>
        DiagnosticSuppressorAnalyer.EnsureNotSuppressed(
            new SwitchStatementSuppressor(),
            code,
            nullableContextOptions,
            ("IDE0010", IDE0010Analyzer));

    Task EnsureSuppressed(string code, NullableContextOptions nullableContextOptions) =>
        DiagnosticSuppressorAnalyer.EnsureSuppressed(
            new SwitchStatementSuppressor(),
            SwitchStatementSuppressor.SuppressionDescriptorByDiagnosticId.Values,
            code,
            nullableContextOptions,
            ("IDE0010", IDE0010Analyzer));

    [Test]
    public Task When_type_is_not_considered_closed_Then_do_not_suppress()
    {
        var code = CodeHelper.WrapInNamespace(TypeHierarchies.NotClosed.LeafNotSealed + @"
static class SwitchTest
{
    public static void DoSwitch(Root root)
    {
        switch(root)
        {
            case Root.Leaf1:
                break;
            case Root.Leaf2:
                break;
        }
    }
}
");

        return EnsureNotSuppressed(code, NullableContextOptions.Enable);
    }

    [Test]
    public Task When_not_all_subtypes_are_matched_Then_do_not_suppress()
    {
        var code = CodeHelper.WrapInNamespace(TypeHierarchies.Closed.Simple + @"
static class SwitchTest
{
    public static void DoSwitch(Root root)
    {
        switch(root)
        {
            case Root.Leaf1:
                break;
            case null:
                break;
        }
    }
}
");

        return EnsureNotSuppressed(code, NullableContextOptions.Disable);
    }

    [Test]
    public Task When_nullable_is_disabled_And_null_is_not_matched_Then_do_not_suppress()
    {
        var code = CodeHelper.WrapInNamespace(TypeHierarchies.Closed.Simple + @"
static class SwitchTest
{
    public static void DoSwitch(Root root)
    {
        switch(root)
        {
            case Root.Leaf1:
                break;
            case Root.Leaf2:
                break;
        }
    }
}
");

        return EnsureNotSuppressed(code, NullableContextOptions.Disable);
    }

    [Test]
    public Task When_type_with_destructure_And_all_subtypes_match_Then_suppress()
    {
        var code = CodeHelper.WrapInNamespace(TypeHierarchies.Closed.Deconstruct + @"
static class SwitchTest
{
    public static void DoSwitch(Root root)
    {
        switch(root)
        {
            case Root.Leaf1(object value):
                break;
            case Root.Leaf2:
                break;
        }
    }
}
");

        return EnsureSuppressed(code, NullableContextOptions.Enable);
    }

    [Test]
    public Task When_type_with_destructure_And_other_Deconstruct_methods_has_all_subtypes_match_Then_suppress()
    {
        var code = CodeHelper.WrapInNamespace(TypeHierarchies.Closed.Deconstruct + @"
static class SwitchTest
{
    public static void DoSwitch(Root root)
    {
        switch(root)
        {
            case Root.Leaf1(object value, string s):
                break;
            case Root.Leaf2:
                break;
        }
    }
}
");

        return EnsureSuppressed(code, NullableContextOptions.Enable);
    }

    [Test]
    public Task When_type_with_destructure_extension_method_has_all_subtypes_match_Then_suppress()
    {
        var code = CodeHelper.WrapInNamespace(TypeHierarchies.Closed.Deconstruct + @"
static class SwitchTest
{
    public static void DoSwitch(Root root)
    {
        switch(root)
        {
            case Root.Leaf1(object value, string s, object otherValue):
                break;
            case Root.Leaf2:
                break;
        }
    }
}
");

        return EnsureSuppressed(code, NullableContextOptions.Enable);
    }

    [Test]
    public Task When_type_with_destructure_And_only_base_type_is_matched_Then_do_not_suppress()
    {
        var code = CodeHelper.WrapInNamespace(TypeHierarchies.Closed.Deconstruct + @"
static class SwitchTest
{
    public static void DoSwitch(Root root)
    {
        switch(root)
        {
            case Root.Leaf1(string value):
                break;
            case Root.Leaf2:
                break;
        };
    }
}
");

        return EnsureNotSuppressed(code, NullableContextOptions.Enable);
    }
    
    [Test]
    public Task When_nullable_is_disabled_And_null_is_matched_on_its_own_Then_suppress()
    {
        var code = CodeHelper.WrapInNamespace(TypeHierarchies.Closed.Simple + @"
static class SwitchTest
{
    public static void DoSwitch(Root root)
    {
        switch(root)
        {
            case Root.Leaf1:
                break;
            case Root.Leaf2:
                break;
            case null:
                break;
        }
    }
}
");

        return EnsureSuppressed(code, NullableContextOptions.Disable);
    }

    [Test]
    public Task When_nullable_is_enabled_And_subtype_matching_is_exhaustive_Then_suppress()
    {
        var code = CodeHelper.WrapInNamespace(TypeHierarchies.Closed.Simple + @"
static class SwitchTest
{
    public static void DoSwitch(Root root)
    {
        switch(root)
        {
            case Root.Leaf1:
                break;
            case Root.Leaf2:
                break;
        }
    }
}
");

        return EnsureSuppressed(code, NullableContextOptions.Enable);
    }

    [Test]
    public Task When_switching_on_explicitly_typed_non_null_local_variable_Then_suppress()
    {
        var code = CodeHelper.WrapInNamespace(TypeHierarchies.Closed.Simple + @"
static class SwitchTest
{
    public static void DoSwitch(Root root)
    {
        Root local = root;
        switch(local)
        {
            case Root.Leaf1:
                break;
            case Root.Leaf2:
                break;
        }
    }
}
");

        return EnsureSuppressed(code, NullableContextOptions.Enable);
    }

    [Test]
    public Task When_switching_on_implicitly_typed_non_null_local_variable_Then_suppress()
    {
        // "var" always implies a nullable type (see https://github.com/dotnet/csharplang/blob/main/proposals/csharp-8.0/nullable-reference-types-specification.md#nullable-implicitly-typed-local-variables)
        // only the null flow analysis can determine non-nullness
        var code = CodeHelper.WrapInNamespace(TypeHierarchies.Closed.Simple + @"
static class SwitchTest
{
    public static void DoSwitch(Root root)
    {
        var local = root;
        switch(local)
        {
            case Root.Leaf1:
                break;
            case Root.Leaf2:
                break;
        }
    }
}
");

        return EnsureSuppressed(code, NullableContextOptions.Enable);
    }

    [Test]
    public Task When_switching_on_null_And_not_matching_null_Then_do_not_suppress()
    {
        // "var" always implies a nullable type (see https://github.com/dotnet/csharplang/blob/main/proposals/csharp-8.0/nullable-reference-types-specification.md#nullable-implicitly-typed-local-variables)
        // only the null flow analysis can determine non-nullness
        var code = CodeHelper.WrapInNamespace(TypeHierarchies.Closed.Simple + @"
static class SwitchTest
{
    public static void DoSwitch(Root root)
    {
        Root? local = null;
        switch(local)
        {
            case Root.Leaf1:
                break;
            case Root.Leaf2:
                break;
        }
    }
}
");

        return EnsureNotSuppressed(code, NullableContextOptions.Enable);
    }

    [Test]
    public Task When_switching_with_declaration_Then_suppress()
    {
        var code = CodeHelper.WrapInNamespace(TypeHierarchies.Closed.Simple + @"
record Wrapper(int Value);

static class SwitchTest
{
    public static void DoSwitch(Root root)
    {
        switch(root)
        {
            case Root.Leaf1 leaf1:
                break;
            case Root.Leaf2 leaf2:
                break;
        }
    }
}
");

        return EnsureSuppressed(code, NullableContextOptions.Enable);
    }

    [Test]
    public Task When_switching_with_non_restrictive_pattern_Then_suppress()
    {
        var code = CodeHelper.WrapInNamespace(TypeHierarchies.Closed.Generic + @"
static class SwitchTest
{
    public static void DoSwitch(Root<int> root)
    {
        switch(root)
        {
            case Root<int>.Leaf1 { Value: int value }:
                break;
            case Root<int>.Leaf2 leaf2:
                break;
        }
    }
}
");

        return EnsureSuppressed(code, NullableContextOptions.Enable);
    }

    [Test]
    public Task When_switching_with_recursive_non_restrictive_pattern_Then_suppress()
    {
        var code = CodeHelper.WrapInNamespace(TypeHierarchies.Closed.Generic + @"
static class SwitchTest
{
    public static void DoSwitch(Root<Root<int>> root)
    {
        switch(root)
        {
            case Root<Root<int>>.Leaf1 { Value: { Value: var value } } :
                break;
            case Root<Root<int>>.Leaf2 leaf2:
                break;
        }
    }
}
");

        return EnsureSuppressed(code, NullableContextOptions.Enable);
    }

    [Test]
    public Task When_switching_with_restrictive_pattern_Then_do_not_suppress()
    {
        var code = CodeHelper.WrapInNamespace(TypeHierarchies.Closed.Generic + @"
static class SwitchTest
{
    public static void DoSwitch(Root<int> root)
    {
        switch(root)
        {
            case Root<int>.Leaf1 { Value: > 0 }:
                break;
            case Root<int>.Leaf2 leaf2:
                break;
        }
    }
}
");

        return EnsureNotSuppressed(code, NullableContextOptions.Enable);
    }

    [Test]
    public Task When_switching_with_when_pattern_Then_do_not_suppress()
    {
        var code = CodeHelper.WrapInNamespace(TypeHierarchies.Closed.Simple + @"
static class SwitchTest
{
    public static void DoSwitch(Root root)
    {
        switch(root)
        {
            case Root.Leaf1 when true:
                break;
            case Root.Leaf2 leaf2:
                break;
        }
    }
}
");

        return EnsureNotSuppressed(code, NullableContextOptions.Enable);
    }

    [Test]
    public Task When_switching_with_recursive_restrictive_pattern_Then_do_not_suppress()
    {
        var code = CodeHelper.WrapInNamespace(TypeHierarchies.Closed.Generic + @"
static class SwitchTest
{
    public static void DoSwitch(Root<Root<int>> root)
    {
        switch(root)
        {
            case Root<Root<int>>.Leaf1 { Value: { Value: > 0 } }:
                break;
            case Root<Root<int>>.Leaf2 leaf2:
                break;
        }
    }
}
");

        return EnsureNotSuppressed(code, NullableContextOptions.Enable);
    }

    [Test]
    public Task When_pattern_match_is_deconstructed_with_var_Then_suppress()
    {
        var code = CodeHelper.WrapInNamespace(TypeHierarchies.Closed.Generic + @"
static class SwitchTest
{
    public static void DoSwitch(Root<(int Value1, int Value2)> root)
    {
        switch(root)
        {
            case Root<(int Value1, int Value2)>.Leaf1 { Value: var (value1, value2) }:
                break;
            case Root<(int Value1, int Value2)>.Leaf2:
                break;
        }
    }
}
");

        return EnsureSuppressed(code, NullableContextOptions.Enable);
    }

    [Test]
    public Task When_pattern_match_is_deconstructed_with_explicit_types_Then_bail_out_and_err_on_the_safe_side_and_do_not_suppress()
    {
        var code = CodeHelper.WrapInNamespace(TypeHierarchies.Closed.Generic + @"
static class SwitchTest
{
    public static void DoSwitch(Root<(int Value1, int Value2)> root)
    {
        switch(root)
        {
            case Root<(int Value1, int Value2)>.Leaf1 { Value: (int value1, int value2) }:
                break;
            case Root<(int Value1, int Value2)>.Leaf2:
                break;
        }
    }
}
");

        return EnsureNotSuppressed(code, NullableContextOptions.Enable);
    }

    [Test]
    public Task When_matching_subtypes_via_assignable_types_Then_suppress()
    {

        var code = CodeHelper.WrapInNamespace(@"

interface ILeaf {}

abstract class Root
{
    Root() {}
    public sealed class Leaf1 : Root, ILeaf {}
    public sealed class Leaf2 : Root, ILeaf {}
    public sealed class Leaf3 : Root {}
}

static class SwitchTest
{
    public static void DoSwitch(Root root)
    {
        switch(root)
        {
            case ILeaf:
                break;
            case Root.Leaf3:
                break;
        }
    }
}
");

        return EnsureSuppressed(code, NullableContextOptions.Enable);
    }
}
