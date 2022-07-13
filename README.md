# ClosedTypeHierarchyDiagnosticSuppressor

Enhances the exhaustiveness check of the C# compiler for switch statements and switch expressions on structurally closed typed hierarchies aka discriminated unions.

Get on [nuget.org](https://www.nuget.org/packages/SvSoft.Analyzers.ClosedTypeHierarchyDiagnosticSuppression) or just include with
```csproj
<PackageReference Include="SvSoft.Analyzers.ClosedTypeHierarchyDiagnosticSuppression" Version="1.0.0" PrivateAssets="All" />
```

There are no attributes and no configuration.

It is implemented as a `DiagnosticSuppressor`.
It suppresses the compiler's own exhaustiveness warnings only when it is really, really sure that the switch is exhaustive.
It suppresses IDE0010, IDE0072 and CS8509.

At the moment it is as paranoid as possible to prevent accidental misuse.
As such, it will only suppress exhaustiveness warnings on provably closed type hierarchies with an abstract base class with private constructor and sealed nested classes inheriting from the base class.

In particular, it will atm not suppress any warnings for record types because they could be inherited from outside via the protected copy constructor.
I may add a flag to opt-in suppressing for records later, because it is such a common use-case and arguably low-risk.

See the test samples for [switch statement](https://github.com/shuebner/ClosedTypeHierarchyDiagnosticSuppressor/blob/main/ClosedTypeHierarchyDiagnosticSuppressor.Tests/SwitchStatementSuppressorTests.cs) and [switch expression](https://github.com/shuebner/ClosedTypeHierarchyDiagnosticSuppressor/blob/main/ClosedTypeHierarchyDiagnosticSuppressor.Tests/SwitchExpressionSuppressorTests.cs) to see what is supported.
I may add more documentation and examples in this README soon.

## Features

### NRT-aware

### Base type matching support

It supports matching against base types (think of matching a `Result<TValue, TError>` against `IOk<TValue>`and `IFailure<TError>`.
That would be treated as exhaustive (provided the switch subject cannot be `null`).

### Pattern matching support

Pattern matching, including nested pattern matching is taken into account.
