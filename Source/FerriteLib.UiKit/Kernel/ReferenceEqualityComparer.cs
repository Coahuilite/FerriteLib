using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// Identity equality for reference types, for internal dictionaries keyed by an object whose own
/// <c>Equals</c> is the consumer's business.
/// </summary>
internal sealed class ReferenceEqualityComparer : IEqualityComparer<object>
{
    internal static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

    private ReferenceEqualityComparer()
    {
    }

    bool IEqualityComparer<object>.Equals(object? x, object? y) => ReferenceEquals(x, y);

    int IEqualityComparer<object>.GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
}
