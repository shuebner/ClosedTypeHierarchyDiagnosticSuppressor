# Exhaustiveness check for Discriminated Unions

If you are using [OneOf](https://github.com/mcintyre321/OneOf), you may want to check out my other repo [OneOfDiagnosticSuppressor](https://github.com/shuebner/OneOfDiagnosticSuppressor).

This project enhances the exhaustiveness check of the C# compiler for switch statements and switch expressions on structurally closed typed hierarchies aka discriminated unions:

```csharp
public abstract class Root
{
    // no other subtypes of Root can exist anywhere
    Root() { }
    public sealed class Leaf1 : Root { }
    public sealed class Leaf2 : Root { }
}

#nullable enable
// without this suppressor: warning CS8509
// with this suppressor: no warnings
public static int Get(Root root) => root switch
{
    Root.Leaf1 => 1,
    Root.Leaf2 => 2
};

#nullable disable
// without this suppressor: warning CS8509
// with this suppressor: no warnings
public static int Get(Root root) => root switch
{
    Root.Leaf1 => 1,
    Root.Leaf2 => 2,
    // without NRTs, null must be matched to be exhaustive
    null => 0 
};
```

Get on [nuget.org](https://www.nuget.org/packages/SvSoft.Analyzers.ClosedTypeHierarchyDiagnosticSuppression) or just include with
```csproj
<PackageReference Include="SvSoft.Analyzers.ClosedTypeHierarchyDiagnosticSuppression" Version="1.1.0" PrivateAssets="All" />
```

There are no attributes and no configuration.
It will just do The Right Thing™.

The only exception is the configuration for opting into treating type hierarchies based on `record` types as closed, even though [they are not because of their implicit protected copy constructor](https://svenhuebner-it.com/closed-type-hierarchies-with-records-not/).
To suppress on records anyway, add the following in your `.editorconfig`:

```
dotnet_diagnostic.CTH001.suppress_on_record_hierarchies = true
```

See the test samples for [switch statement](https://github.com/shuebner/ClosedTypeHierarchyDiagnosticSuppressor/blob/main/ClosedTypeHierarchyDiagnosticSuppressor.Tests/SwitchStatementSuppressorTests.cs) and [switch expression](https://github.com/shuebner/ClosedTypeHierarchyDiagnosticSuppressor/blob/main/ClosedTypeHierarchyDiagnosticSuppressor.Tests/SwitchExpressionSuppressorTests.cs) to see what is supported.
I may add more documentation and examples in this README soon.

## Treating non-exhaustive switches as errors

In your project, enable IDE0010 (for switch statements) and IDE0072 (for switch expressions, not strictly necessary because we have CS8509 anyway) like this:
```csproj
<PropertyGroup>
  <!-- enables (among others) IDE0010 and IDE0072 -->
  <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
</PropertyGroup>
```

In your `.editorconfig` configure the severity of the exhaustiveness diagnostics:
```
[*.cs]
dotnet_diagnostic.IDE0010.severity = error
dotnet_diagnostic.IDE0072.severity = error
dotnet_diagnostic.CS8509.severity = error
```

# Development Experience

If you set diagnostic severity to error for diagnostics that may be suppressed by a diagnostic suppressor like this one, you may have to take additional action for a good development experience.

## Visual Studio (for Windows)

By default, Visual Studio does not run diagnostic suppressors when the build is implicitly triggered like by running a test.
If you have errors that are suppressed by diagnostic suppressors, those builds will fail, e. g. preventing the test from running (unless you do an explicit build first).
You can configure Visual Studio to always run analyzers (towards whom diagnostic suppressors are counted):

![image](https://user-images.githubusercontent.com/1770684/182022215-23902b8a-2c01-4fe1-bb47-943fc7bda140.png)

See also [here](https://developercommunity2.visualstudio.com/t/Test-run-fails-build-because-Diagnostic/10023425).

## Rider

[Rider does not support Diagnostic Suppressors](https://youtrack.jetbrains.com/issue/RSRP-481121) as of 2022-07-31.
Your development experience may suffer.

## Visual Studio Code

[OmniSharp and thus Visual Studio Code does not support Diagnostic Suppressors](https://github.com/OmniSharp/omnisharp-roslyn/issues/1711) as of 2022-07-31.
Your development experience may suffer.

Thanks to [rutgersc](https://github.com/rutgersc) for [bringing this up](https://github.com/shuebner/OneOfDiagnosticSuppressor/issues/1).

## Visual Studio for Mac
[Visual Studio for Mac does not support Diagnostic Suppressors](https://developercommunity.visualstudio.com/t/Support-for-Diagnostic-Suppressors/10247137?q=Diagnostic+Suppressors) as of 2023-01-06.

# Features

## NRT-aware

## Base type matching support

It supports matching against base types (think of matching a `Result<TValue, TError>` against `IOk<TValue>`and `IFailure<TError>`.
That would be treated as exhaustive.

## Pattern matching support

Pattern matching, including nested pattern matching is taken into account.

# Background

## The problem with the default compiler behavior

Writing the above will get you a compiler warning (CS8509) about the switch expression not being exhaustive.
Hypothetically, the compiler could know that the expression in in fact exhaustive, but it does not.
Both suppressing the warning (via attribute or pragma) or adding a discard case are not ideal, because when later a `Leaf3` subtype is added to `Root`, you will not be warned about your existing `switch`es not being exhaustive anymore.

Hence this project, that will suppress the compiler warnings in cases like this.

## Implementation

It is implemented as a `DiagnosticSuppressor`.
It suppresses the compiler's own exhaustiveness warnings only when it is really, really sure that the switch is exhaustive.
It suppresses IDE0010, IDE0072 and CS8509.

At the moment it is as paranoid as possible to prevent accidental misuse.
As such, it will only suppress exhaustiveness warnings on provably closed type hierarchies with an abstract base class with private constructor and sealed nested classes inheriting from the base class, see above.

In particular, it will not by default suppress any warnings for record types because [they could be inherited from outside via the protected copy constructor](https://svenhuebner-it.com/closed-type-hierarchies-with-records-not/).
You can however opt into suppressing for records (see above), because it is such a common use-case and arguably low-risk.
