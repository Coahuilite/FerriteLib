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

/// <summary>Current validity and retained display are separate facts. No first-failure default is a value.</summary>
public sealed class UiPresentationStatus
{
    public bool HasLastValue { get; }
    public UiReactiveFault? Fault { get; }
    public bool IsCurrent => HasLastValue && Fault == null;
    internal UiPresentationStatus(bool hasLast, UiReactiveFault? fault) { HasLastValue = hasLast; Fault = fault; }
}

/// <summary>One consumer's synchronized presentation. Host demand is actual reading, never focus.</summary>
public sealed class UiBindingPresentation : IDisposable
{
    private readonly UiBindings bindings;
    private readonly UiReactiveOwner owner;
    private readonly Dictionary<string, UiReactiveComputed<object?>> calculations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, object?> current = new(StringComparer.Ordinal);
    private readonly Dictionary<string, UiReactiveFault> faults = new(StringComparer.Ordinal);
    private readonly HashSet<string> synchronized = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> evaluations = new(StringComparer.Ordinal);
    private Dictionary<string, UiTrackedBindingRead> reads = new(StringComparer.Ordinal);
    private bool disposed;
    public int Revision { get; private set; }
    internal int DemandRevision { get; private set; }
    public UiBindingPresentation(UiBindings bindings) : this(bindings, "presentation") { }
    internal UiBindingPresentation(UiBindings bindings, string name)
    {
        this.bindings = bindings;
        owner = bindings.Reactive.CreateOwner(name);
        bindings.AttachPresentation(this);
    }
    /// <summary>Eager explicit consumer. Failures remain queryable; independent keys still synchronize.</summary>
    public void Synchronize() => Synchronize(true);
    internal void Synchronize(bool eager)
    {
        bindings.Reactive.VerifyAccess();
        if (disposed) throw new ObjectDisposedException(nameof(UiBindingPresentation));
        reads = bindings.TrackedReads(); synchronized.Clear();
        foreach (var entry in reads)
        {
            if (calculations.ContainsKey(entry.Key)) continue;
            UiReactiveComputed<object?> calculation = owner.Compute(entry.Value.Key, entry.Value.Read);
            calculation.Invalidated = () => DemandRevision++;
            calculations.Add(entry.Key, calculation);
        }
        if (eager) foreach (string key in calculations.Keys) SynchronizeKey(key);
    }
    private void SynchronizeKey(string key)
    {
        if (!synchronized.Add(key)) return;
        UiReactiveComputed<object?> calculation = calculations[key];
        bool hadFault = faults.ContainsKey(key);
        try
        {
            object? value = calculation.Value;
            if (evaluations.TryGetValue(key, out int evaluated) && evaluated == calculation.Revision) return;
            UiTrackedBindingRead descriptor = reads[key];
            bool changed;
            using (bindings.Reactive.ReadPhase())
                changed = !current.TryGetValue(key, out object? prior) || descriptor.Equal == null || !descriptor.Equal(prior, value);
            current[key] = value; faults.Remove(key);
            evaluations[key] = calculation.Revision;
            if (changed || hadFault) { bindings.NotifyChanged(descriptor.Key); Revision++; }
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            UiReactiveFault failure = error as UiReactiveFault ?? new UiReactiveFault("presentation/" + reads[key].Key, "presentation-comparison", "Related input change or Retry", error);
            evaluations[key] = calculation.Revision;
            if (!faults.TryGetValue(key, out UiReactiveFault? old) || !ReferenceEquals(old, failure))
            { faults[key] = failure; bindings.NotifyChanged(reads[key].Key); Revision++; }
        }
    }
    internal void Prepare(string key)
    {
        if (calculations.ContainsKey("value:" + key)) SynchronizeKey("value:" + key);
        if (calculations.ContainsKey("options:" + key)) SynchronizeKey("options:" + key);
    }
    internal void PrepareAll() { foreach (string key in calculations.Keys) SynchronizeKey(key); }
    internal object? Read(string key, bool displayOnly = false)
    {
        if (disposed) throw new ObjectDisposedException(nameof(UiBindingPresentation));
        if (!calculations.ContainsKey(key)) throw new InvalidOperationException("Tracked binding '" + key + "' has no presentation registration.");
        SynchronizeKey(key);
        if (faults.TryGetValue(key, out UiReactiveFault? failure))
        {
            bindings.NoteDisplayFault(failure);
            if (!displayOnly || !current.ContainsKey(key)) throw failure;
        }
        return current[key];
    }
    public UiPresentationStatus GetStatus(string key, bool options = false)
    {
        bindings.Reactive.VerifyAccess();
        string identity = (options ? "options:" : "value:") + key;
        faults.TryGetValue(identity, out UiReactiveFault? fault);
        return new UiPresentationStatus(current.ContainsKey(identity), fault);
    }
    /// <summary>Retry this pure presentation calculation; never replays commands or resumes callbacks.</summary>
    public void Retry(string key, bool options = false)
    {
        bindings.Reactive.VerifyAccess();
        if (disposed) throw new ObjectDisposedException(nameof(UiBindingPresentation));
        if (calculations.TryGetValue((options ? "options:" : "value:") + key, out var calculation)) calculation.Retry();
    }
    public IDisposable Enter()
    {
        bindings.Reactive.VerifyAccess();
        if (disposed) throw new ObjectDisposedException(nameof(UiBindingPresentation));
        return bindings.UsePresentation(this);
    }
    internal void Forget(string key)
    {
        foreach (string identity in new[] { "value:" + key, "options:" + key, "command:" + key })
        {
            if (calculations.TryGetValue(identity, out var calculation))
            { calculations.Remove(identity); owner.Release(calculation); }
            reads.Remove(identity); current.Remove(identity); faults.Remove(identity);
            synchronized.Remove(identity); evaluations.Remove(identity);
        }
        DemandRevision++;
    }
    public void Dispose()
    {
        bindings.Reactive.VerifyAccess(); if (disposed) return;
        disposed = true; bindings.DetachPresentation(this); owner.Dispose(); calculations.Clear(); current.Clear(); faults.Clear(); reads.Clear(); synchronized.Clear();
    }
}
