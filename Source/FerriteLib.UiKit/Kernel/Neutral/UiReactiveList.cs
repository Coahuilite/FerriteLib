using System;
using System.Collections;
using System.Collections.Generic;

namespace FerriteLib.UiKit.Kernel;

/// <summary>Serial, controlled list structure. Each read observes structure, not arbitrary item mutation.
/// Snapshot retains a read-only structural version; project mutable item properties for presentation.</summary>
public sealed class UiReactiveList<T> : IReadOnlyList<T>
{
    private readonly UiReactiveRuntime runtime;
    private readonly Func<T, T, bool> equal;
    private readonly UiReactiveValue<IReadOnlyList<T>> structure;
    internal UiReactiveList(UiReactiveRuntime runtime, IEnumerable<T> items, Func<T, T, bool> equal)
    {
        this.runtime = runtime;
        this.equal = equal ?? throw new ArgumentNullException(nameof(equal));
        structure = runtime.Value(Copy(items), Same);
    }
    private static IReadOnlyList<T> Copy(IEnumerable<T> items)
        => new List<T>(items ?? throw new ArgumentNullException(nameof(items))).AsReadOnly();
    private bool Same(IReadOnlyList<T> a, IReadOnlyList<T> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++) if (!equal(a[i], b[i])) return false;
        return true;
    }
    public IReadOnlyList<T> Snapshot => structure.Value;
    internal int SubscriberCount => structure.SubscriberCount;
    public int Count => Snapshot.Count;
    public T this[int index] => Snapshot[index];
    public IEnumerator<T> GetEnumerator() => Snapshot.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public void Add(T item)
    {
        runtime.AssertWritable();
        var next = new List<T>(structure.ReadCurrent()) { item };
        structure.Value = next.AsReadOnly();
    }
    public void RemoveAt(int index)
    {
        runtime.AssertWritable();
        var next = new List<T>(structure.ReadCurrent()); next.RemoveAt(index);
        structure.Value = next.AsReadOnly();
    }
    public bool Remove(T item)
    {
        runtime.AssertWritable();
        IReadOnlyList<T> current = structure.ReadCurrent();
        for (int i = 0; i < current.Count; i++)
            if (equal(current[i], item)) { RemoveAt(i); return true; }
        return false;
    }
    /// <summary>Moves an item to its final zero-based index; equal indices are a structural no-op.</summary>
    public void Move(int from, int to)
    {
        runtime.AssertWritable();
        var next = new List<T>(structure.ReadCurrent());
        if (from < 0 || from >= next.Count) throw new ArgumentOutOfRangeException(nameof(from));
        if (to < 0 || to >= next.Count) throw new ArgumentOutOfRangeException(nameof(to));
        if (from == to) return;
        T item = next[from]; next.RemoveAt(from); next.Insert(to, item);
        structure.Value = next.AsReadOnly();
    }
    public void Clear() => ReplaceAll(Array.Empty<T>());
    /// <summary>One structural change if the explicit ordered element comparison differs.</summary>
    public void ReplaceAll(IEnumerable<T> items)
    {
        runtime.AssertWritable();
        structure.Value = Copy(items);
    }
}


