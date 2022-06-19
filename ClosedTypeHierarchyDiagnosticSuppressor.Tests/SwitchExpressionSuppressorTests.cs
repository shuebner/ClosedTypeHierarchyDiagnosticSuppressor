using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;
using SvSoft.Analyzers.ClosedTypeHerarchyDiagnosticSuppression;
using System;
using System.Threading.Tasks;

namespace ClosedTypeHierarchyDiagnosticSuppressor.Tests;
class SwitchExpressionSuppressorTests
{
    static readonly DiagnosticAnalyzer IDE0072Analyzer = (DiagnosticAnalyzer)(Activator.CreateInstance(
        "Microsoft.CodeAnalysis.CSharp.CodeStyle",
        "Microsoft.CodeAnalysis.CSharp.PopulateSwitch.CSharpPopulateSwitchExpressionDiagnosticAnalyzer")?.Unwrap()
        ?? throw new InvalidOperationException("could not instantiate populate switch expression analyzer for IDE0072"));

    Task EnsureNotSuppressed(string code, NullableContextOptions nullableContextOptions) =>
        DiagnosticSuppressorAnalyer.EnsureNotSuppressed(
            new SwitchExpressionSuppressor(),
            code,
            nullableContextOptions,
            ("IDE0072", IDE0072Analyzer));

    Task EnsureSuppressed(string code, NullableContextOptions nullableContextOptions) =>
        DiagnosticSuppressorAnalyer.EnsureSuppressed(
            new SwitchExpressionSuppressor(),
            SwitchExpressionSuppressor.SuppressionDescriptorByDiagnosticId.Values,
            code,
            nullableContextOptions,
            ("IDE0072", IDE0072Analyzer));

    [Test]
    public Task When_type_is_not_considered_closed_Then_do_not_suppress()
    {
        var code = CodeHelper.WrapInNamespace(TypeHierarchies.NotClosed.LeafNotSealed + @"
static class SwitchTest
{
    public static int DoSwitch(Root root)
    {
        return root switch
        {
            Root.Leaf1 => 0,
            Root.Leaf2 => 1
        };
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
    public static int DoSwitch(Root root)
    {
        return root switch
        {
            Root.Leaf1 => 0,
            null => 2
        };
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
    public static int DoSwitch(Root root)
    {
        return root switch
        {
            Root.Leaf1 => 0,
            Root.Leaf2 => 1
        };
    }
}
");

        return EnsureNotSuppressed(code, NullableContextOptions.Disable);
    }

    [Test]
    public Task When_nullable_is_disabled_And_null_is_matched_on_its_own_Then_suppress()
    {
        var code = CodeHelper.WrapInNamespace(TypeHierarchies.Closed.Simple + @"
static class SwitchTest
{
    public static int DoSwitch(Root root)
    {
        return root switch
        {
            Root.Leaf1 => 0,
            Root.Leaf2 => 1,
            null => 2
        };
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
    public static int DoSwitch(Root root)
    {
        return root switch
        {
            Root.Leaf1 => 0,
            Root.Leaf2 => 1
        };
    }
}
");

        return EnsureSuppressed(code, NullableContextOptions.Enable);
    }

    [Test]
    // reason for this test is a bug in Roslyn: see https://github.com/dotnet/roslyn/issues/59875
    public Task When_switching_on_argument_And_expression_value_is_deconstructed_Then_suppress()
    {
        var code = CodeHelper.WrapInNamespace(TypeHierarchies.Closed.Simple + @"
static class SwitchTest
{
    public static int DoSwitch(Root root)
    {
        (int one, int two) = root switch
        {
            Root.Leaf1 => (0, 0),
            Root.Leaf2 => (1, 1)
        };

        return one;
    }
}
");

        return EnsureSuppressed(code, NullableContextOptions.Enable);
    }

    [Test]
    // reason for this test is a bug in Roslyn: see https://github.com/dotnet/roslyn/issues/59875
    public Task When_switching_on_explicitly_typed_local_variable_And_expression_value_is_deconstructed_Then_suppress()
    {
        var code = CodeHelper.WrapInNamespace(TypeHierarchies.Closed.Simple + @"
static class SwitchTest
{
    public static int DoSwitch(Root root)
    {
        Root local = root;
        (int one, int two) = local switch
        {
            Root.Leaf1 => (0, 0),
            Root.Leaf2 => (1, 1)
        };

        return one;
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
    public static int DoSwitch(Root root)
    {
        Root local = root;
        return local switch
        {
            Root.Leaf1 => 0,
            Root.Leaf2 => 1
        };
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
    public static int DoSwitch(Root root)
    {
        var local = root;
        return local switch
        {
            Root.Leaf1 => 0,
            Root.Leaf2 => 1
        };
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
    public static int DoSwitch(Root root)
    {
        Root? local = null;
        return local switch
        {
            Root.Leaf1 => 0,
            Root.Leaf2 => 1
        };
    }
}
");

        return EnsureNotSuppressed(code, NullableContextOptions.Enable);
    }

    [Test]
    public Task When_switching_in_method_argument_Then_suppress()
    {
        var code = CodeHelper.WrapInNamespace(TypeHierarchies.Closed.Simple + @"
record Wrapper(int Value);

static class SwitchTest
{
    public static Wrapper DoSwitch(Root root)
    {
        return new Wrapper(root switch
        {
            Root.Leaf1 => 0,
            Root.Leaf2 => 1
        });
    }
}
");

        return EnsureSuppressed(code, NullableContextOptions.Enable);
    }

    [Test]
    public Task When_switching_with_declaration_Then_suppress()
    {
        var code = CodeHelper.WrapInNamespace(TypeHierarchies.Closed.Simple + @"
record Wrapper(int Value);

static class SwitchTest
{
    public static Wrapper DoSwitch(Root root)
    {
        return new Wrapper(root switch
        {
            Root.Leaf1 leaf1 => 0,
            Root.Leaf2 leaf2 => 1
        });
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
    public static int DoSwitch(Root<int> root)
    {
        return root switch
        {
            Root<int>.Leaf1 { Value: int value } => 0,
            Root<int>.Leaf2 => 1
        };
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
    public static int DoSwitch(Root<Root<int>> root)
    {
        return root switch
        {
            Root<Root<int>>.Leaf1 { Value: { Value: var value } } => 0,
            Root<Root<int>>.Leaf2 => 1
        };
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
    public static int DoSwitch(Root<int> root)
    {
        return root switch
        {
            Root<int>.Leaf1 { Value: > 0 } => 0,
            Root<int>.Leaf2 => 1
        };
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
    public static int DoSwitch(Root root)
    {
        return root switch
        {
            Root.Leaf1 when true => 0,
            Root.Leaf2 leaf2 => 1
        };
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
    public static int DoSwitch(Root<Root<int>> root)
    {
        return root switch
        {
            Root<Root<int>>.Leaf1 { Value: { Value: > 0 } } => 0,
            Root<Root<int>>.Leaf2 => 1
        };
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
    public static int DoSwitch(Root<(int Value1, int Value2)> root)
    {
        return root switch
        {
            Root<(int Value1, int Value2)>.Leaf1 { Value: var (value1, value2) } => 0,
            Root<(int Value1, int Value2)>.Leaf2 => 1
        };
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
    public static int DoSwitch(Root<(int Value1, int Value2)> root)
    {
        return root switch
        {
            Root<(int Value1, int Value2)>.Leaf1 { Value: (int value1, int value2) } => 0,
            Root<(int Value1, int Value2)>.Leaf2 => 1
        };
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
    public static int DoSwitch(Root root)
    {
        return root switch
        {
            ILeaf => 0,
            Root.Leaf3 => 2
        };
    }
}
");

        return EnsureSuppressed(code, NullableContextOptions.Enable);
    }
}
