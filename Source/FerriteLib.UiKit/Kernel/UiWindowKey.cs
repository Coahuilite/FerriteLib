using System;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// The identity of one window instance: <c>(consumer, window kind, context key)</c>.
/// <para>
/// <b>Why the C# type is not the identity.</b> The vanilla <c>WindowStack.Add</c> path removes
/// same-typed windows by exact type, so a shared, generic window class - which is what a page shell
/// is - would make every ordinary page a sibling of every other one. The library's identity is this
/// triple instead: at most one open instance per value, reopening the same value activates the
/// instance already there, and two different context keys coexist even though they share a class.
/// </para>
/// <para>
/// <b>What each part means.</b> <c>Consumer</c> and <c>WindowKind</c> together name the registration
/// (<see cref="UiWindowCatalog.Register"/>); both must be non-empty. <c>ContextKey</c> is whatever the
/// consumer is showing instances of - a target, a document id, a profile - and an empty context key is
/// a legal, single "no context" instance rather than an error, because that is the honest shape of a
/// page that only ever appears once per kind.
/// </para>
/// <para>
/// Equality is ordinal over all three strings, including the context key: two instances of one kind
/// whose contexts differ are different windows, and the harness proves it with a planted near-miss key.
/// </para>
/// </summary>
public readonly struct UiWindowKey : IEquatable<UiWindowKey>
{
    public UiWindowKey(string consumer, string windowKind, string contextKey)
    {
        if (string.IsNullOrEmpty(consumer))
        {
            throw new ArgumentException("A window key needs a non-empty consumer id.", nameof(consumer));
        }

        if (string.IsNullOrEmpty(windowKind))
        {
            throw new ArgumentException("A window key needs a non-empty window kind.", nameof(windowKind));
        }

        Consumer = consumer;
        WindowKind = windowKind;
        ContextKey = contextKey ?? "";
    }

    /// <summary>Who registered the kind; the first half of the registration identity.</summary>
    public string Consumer { get; }

    /// <summary>The kind within that consumer; the second half of the registration identity.</summary>
    public string WindowKind { get; }

    /// <summary>Which instance of the kind this is; empty means the kind's one context-free instance.</summary>
    public string ContextKey { get; }

    public bool Equals(UiWindowKey other)
    {
        return string.Equals(Consumer, other.Consumer, StringComparison.Ordinal)
            && string.Equals(WindowKind, other.WindowKind, StringComparison.Ordinal)
            && string.Equals(ContextKey, other.ContextKey, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj)
    {
        return obj is UiWindowKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        // Combined by hand because HashCode.Combine is not on net472. The three ordinal hashes are the
        // whole identity: dropping any one of them would make two different windows share a bucket and,
        // with the default dictionary comparer, one window.
        unchecked
        {
            int hash = Consumer.GetHashCode();
            hash = (hash * 397) ^ WindowKind.GetHashCode();
            hash = (hash * 397) ^ ContextKey.GetHashCode();
            return hash;
        }
    }

    public static bool operator ==(UiWindowKey left, UiWindowKey right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(UiWindowKey left, UiWindowKey right)
    {
        return !left.Equals(right);
    }

    /// <summary>The three parts joined for a log line or a diagnostic path; never parsed back.</summary>
    public override string ToString()
    {
        return Consumer + "/" + WindowKind + "/" + ContextKey;
    }
}
