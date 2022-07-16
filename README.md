# Exhaustiveness check for Discriminated Unions

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
<PackageReference Include="SvSoft.Analyzers.ClosedTypeHierarchyDiagnosticSuppression" Version="1.0.1" PrivateAssets="All" />
```

There are no attributes and no configuration.
It will just do The Right Thing™.

See the test samples for [switch statement](https://github.com/shuebner/ClosedTypeHierarchyDiagnosticSuppressor/blob/main/ClosedTypeHierarchyDiagnosticSuppressor.Tests/SwitchStatementSuppressorTests.cs) and [switch expression](https://github.com/shuebner/ClosedTypeHierarchyDiagnosticSuppressor/blob/main/ClosedTypeHierarchyDiagnosticSuppressor.Tests/SwitchExpressionSuppressorTests.cs) to see what is supported.
I may add more documentation and examples in this README soon.


## Features

### NRT-aware

### Base type matching support

It supports matching against base types (think of matching a `Result<TValue, TError>` against `IOk<TValue>`and `IFailure<TError>`.
That would be treated as exhaustive.

### Pattern matching support

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

In particular, it will atm not suppress any warnings for record types because they could be inherited from outside via the protected copy constructor.
I may add a flag to opt-in suppressing for records later, because it is such a common use-case and arguably low-risk.
