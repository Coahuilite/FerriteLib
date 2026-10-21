using System;
using System.Collections.Generic;

namespace FerriteLib.UiKit.Kernel;

internal sealed class UiTrackedBindingRead
{
    internal readonly string Key;
    internal readonly Func<object?> Read;
    internal readonly Func<object?, object?, bool>? Equal;
    internal UiTrackedBindingRead(string key, Func<object?> read, Func<object?, object?, bool>? equal)
    { Key = key; Read = read; Equal = equal; }
}

/// <summary>A host-owned, atomically synchronized read view. Business reads outside Enter stay immediate.</summary>
public sealed class UiBindingPresentation : IDisposable
{
    private readonly UiBindings bindings;
    private readonly UiReactiveOwner owner;
    private readonly Dictionary<string, UiReactiveComputed<object?>> calculations = new(StringComparer.Ordinal);
    private Dictionary<string, object?> current = new(StringComparer.Ordinal);
    private bool disposed;
    public int Revision { get; private set; }
    private readonly Dictionary<string, int> revisions = new(StringComparer.Ordinal);
    public UiBindingPresentation(UiBindings bindings)
    {
        this.bindings = bindings;
        owner = bindings.Reactive.CreateOwner("presentation");
    }
    /// <summary>Stage all opted-in values before publishing any. A failure leaves the previous read view intact.</summary>
    public void Synchronize()
    {
        bindings.Reactive.VerifyAccess();
        if (disposed) throw new ObjectDisposedException(nameof(UiBindingPresentation));
        var added = new List<string>();
        try
        {
            Dictionary<string, UiTrackedBindingRead> reads = bindings.TrackedReads();
            foreach (var entry in reads)
            {
                if (calculations.ContainsKey(entry.Key)) continue;
                calculations.Add(entry.Key, owner.Compute(entry.Value.Key, entry.Value.Read)); added.Add(entry.Key);
            }
            var staged = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var entry in calculations) staged.Add(entry.Key, entry.Value.Value);
            // Refresh actual dependencies before comparing the values consumed by presentation/layout stages.
            var changed = new HashSet<string>(StringComparer.Ordinal);
            using (bindings.Reactive.ReadPhase())
            {
                foreach (var entry in staged)
                {
                    if (revisions.TryGetValue(entry.Key, out int old) && old == calculations[entry.Key].Revision) continue;
                    UiTrackedBindingRead read = reads[entry.Key];
                    if (!current.TryGetValue(entry.Key, out object? prior) || read.Equal == null || !read.Equal(prior, entry.Value))
                        changed.Add(read.Key);
                }
            }
            // Comparisons and every getter succeed before any public revision or read view changes.
            foreach (string key in changed) bindings.NotifyChanged(key);
            foreach (var entry in calculations) revisions[entry.Key] = entry.Value.Revision;
            current = staged;
            if (changed.Count != 0) Revision++;
        }
        catch
        {
            foreach (string key in added) { calculations[key].Dispose(); calculations.Remove(key); }
            throw;
        }
    }
    internal object? Read(string key)
    {
        if (!current.TryGetValue(key, out object? value))
            throw new InvalidOperationException("Tracked binding '" + key + "' has no synchronized presentation input.");
        return value;
    }
    public IDisposable Enter()
    {
        bindings.Reactive.VerifyAccess();
        if (disposed) throw new ObjectDisposedException(nameof(UiBindingPresentation));
        return bindings.UsePresentation(this);
    }
    public void Dispose()
    {
        bindings.Reactive.VerifyAccess(); if (disposed) return;
        disposed = true; owner.Dispose(); calculations.Clear(); current.Clear();
    }
}
