namespace ClosedTypeHierarchyDiagnosticSuppressor.Tests;
static class TypeHierarchies
{
    public static class Closed
    {
        public const string Simple = @"
abstract class Root
{
    Root() {}
    public sealed class Leaf1 : Root {}
    public sealed class Leaf2 : Root {}
}
";

        public const string Nested = @"
abstract class Root
{
    Root() {}

    public sealed class Leaf1 : Root {}

    public abstract class Intermediate : Root
    {
        Intermediate() {}
        public sealed class Leaf2 : Intermediate {}
        public sealed class Leaf3 : Intermediate {}
    }
}
";

        public const string Generic = @"
abstract class Root<T>
{
    Root(T value) => Value = value;
    public sealed class Leaf1 : Root<T> { public Leaf1(T value) : base(value) {} }
    public sealed class Leaf2 : Root<T> { public Leaf2(T value) : base(value) {} }

    public T Value { get; }
}
";
        public const string Deconstruct = @"
abstract class Root
{
    Root() {}
    public sealed class Leaf1 : Root
    {
        public Leaf1(object value, string s, object otherValue)
        {
            Value = value;
            S = s;
            OtherValue = otherValue;
        }

        public object Value { get; }
        public string S { get; }
        public object OtherValue { get; }

        public void Deconstruct(out object value)
        {
            value = Value;
        }

        public void Deconstruct(out object value, out string s)
        {
            value = Value;
            s = S;
        }
    }

    public sealed class Leaf2 : Root {}
}

static class RootExtensions
{
    public static void Deconstruct(this Root.Leaf1 leaf1, out object value, out string s, out object otherValue) 
    {
        value = leaf1.Value;
        s = leaf1.S;
        otherValue = leaf1.OtherValue;
    }
}
";
    }

    public static class ProtectedCopyConstructorOnly
    {
        public const string ImplicitProtectedCopyCtor = @"
abstract record Root
{
    Root() {}
    public sealed record Leaf1 : Root {}
    public sealed record Leaf2 : Root {}
}
";
    }

    public static class NotClosed
    {
        public const string RootNotAbstract = @"
class Root
{
    Root() {}
    public sealed class Leaf1 : Root {}
    public sealed class Leaf2 : Root {}
}
";

        public const string CtorNotPrivate = @"
abstract class Root
{
    private protected Root() {}
    public sealed class Leaf1 : Root {}
    public sealed class Leaf2 : Root {}
}
";

        public const string ProtectedCtorOtherThanCopyConstructor = @"
abstract class Root
{
    protected Root() {}
    public sealed record Leaf1 : Root {}
    public sealed record Leaf2 : Root {}
}
";

        public const string LeafNotSealed = @"
abstract class Root
{
    Root() {}
    public class Leaf1 : Root {}
    public sealed class Leaf2 : Root {}
}
";

        public const string ExplicitPrivateProtectedCopyCtor = @"
    abstract class Root
    {
        private protected Root(Root root) {}
        public sealed record Leaf1 : Root {}
        public sealed record Leaf2 : Root {}
    }
    ";

        public const string ExplicitProtectedCopyCtor = @"
    abstract class Root
    {
        protected Root(Root root) {}
        public sealed record Leaf1 : Root {}
        public sealed record Leaf2 : Root {}
    }
    ";
    }
}
