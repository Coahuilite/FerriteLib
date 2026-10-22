using System;
using System.Collections.Generic;
using System.Threading;

namespace FerriteLib.UiKit.Kernel;

/// <summary>Serially owned reactive graph. The adapter delivers work to the creating context;
/// this object neither owns a business scheduler nor marshals arbitrary external models.</summary>
public sealed class UiReactiveRuntime
{
    // WO foundation r2 exception: dynamic call context only; never an owner of graph/business state.
    [ThreadStatic] internal static UiReactiveRuntime? Current;
    private readonly int thread = Thread.CurrentThread.ManagedThreadId;
    private readonly List<UiReactiveReaction> pending = new();
    private readonly HashSet<UiReactiveReaction> queued = new();
    private int batchDepth;
    private bool draining;
    private int pureDepth;
    internal UiReactiveObserver? Observer;
    internal HashSet<UiReactiveSource>? Reads;
    internal Dictionary<UiReactiveSource, int>? ReadVersions;

    public void VerifyAccess()
    {
        if (Current != null && Current != this)
            throw new InvalidOperationException("Cross-runtime reactive access during a tracked/read phase. Project a safe result into the active runtime instead.");
        if (Thread.CurrentThread.ManagedThreadId != thread)
            throw new InvalidOperationException("Reactive access belongs to its creating serial context. Deliver a safe result through the host adapter first.");
    }

    public UiReactiveValue<int> Int(int value = 0) => Value(value, (a, b) => a == b);
    public UiReactiveValue<string> Text(string value = "") => Value(value, (a, b) => string.Equals(a, b, StringComparison.Ordinal));
    public UiReactiveValue<bool> Boolean(bool value = false) => Value(value, (a, b) => a == b);
    /// <summary>Connect any value with an explicitly chosen equality policy. No reference/deep default is inferred.</summary>
    public UiReactiveValue<T> Value<T>(T value, Func<T, T, bool> equal)
    {
        VerifyAccess();
        return new UiReactiveValue<T>(this, value, equal ?? throw new ArgumentNullException(nameof(equal)));
    }
    /// <summary>Controlled structure; item properties must be connected separately.</summary>
    public UiReactiveList<T> List<T>(IEnumerable<T> items, Func<T, T, bool> equal)
    {
        VerifyAccess();
        return new UiReactiveList<T>(this, items, equal);
    }
    public UiReactiveOwner CreateOwner(string name) { VerifyAccess(); return new UiReactiveOwner(this, name); }

    /// <summary>Nested batches defer automatic responses, not assignment, invalidation or explicit reads.</summary>
    public IDisposable Batch()
    {
        VerifyAccess(); AssertWritable(); batchDepth++;
        return new UiReactiveScope(() => { VerifyAccess(); if (--batchDepth == 0) Drain(); });
    }

    /// <summary>Opt-in pure measurement/drawing; legacy mixed Draw/input and ordinary getters are not enrolled.</summary>
    public IDisposable ReadPhase() => Pure();

    internal IDisposable Pure()
    {
        VerifyAccess();
        UiReactiveRuntime? previous = Current;
        Current = this; pureDepth++;
        return new UiReactiveScope(() => { try { pureDepth--; } finally { Current = previous; } });
    }

    internal void AssertWritable()
    {
        VerifyAccess();
        if (pureDepth != 0) throw new InvalidOperationException("Business writes/commands are forbidden in a tracked calculation or measurement.");
    }

    internal T Track<T>(UiReactiveObserver observer, Func<T> read, bool pure)
    {
        VerifyAccess();
        UiReactiveRuntime? previousRuntime = Current;
        Current = this;
        UiReactiveObserver? previous = Observer;
        HashSet<UiReactiveSource>? previousReads = Reads;
        Dictionary<UiReactiveSource, int>? previousVersions = ReadVersions;
        var found = new HashSet<UiReactiveSource>();
        Observer = observer; Reads = found;
        var versions = new Dictionary<UiReactiveSource, int>();
        ReadVersions = versions;
        IDisposable? scope = pure ? Pure() : null;
        try
        {
            T result = read();
            observer.ReplaceDependencies(found);
            foreach (var entry in versions) if (entry.Key.Revision != entry.Value) { observer.MarkDirty(); break; }
            return result;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Recovery follows this attempt's actual reads, not the old value's obsolete branch.
            observer.ReplaceDependencies(found);
            throw ex is UiReactiveFault failure ? failure.Through(observer.Name) : new UiReactiveFault(observer.Name, "evaluation", "Related input change or Retry", ex);
        }
        finally { scope?.Dispose(); Observer = previous; Reads = previousReads; ReadVersions = previousVersions; Current = previousRuntime; }
    }

    internal void Schedule(UiReactiveReaction reaction)
    {
        if (queued.Add(reaction)) pending.Add(reaction);
    }

    internal void Drain()
    {
        if (batchDepth != 0 || draining) return;
        draining = true;
        var executions = new Dictionary<UiReactiveReaction, int>();
        try
        {
            while (pending.Count != 0)
            {
                UiReactiveReaction next = pending[0]; pending.RemoveAt(0); queued.Remove(next);
                if (next.Disposed || next.Fault != null) continue;
                int steps = executions.TryGetValue(next, out int previous) ? previous + 1 : 1;
                executions[next] = steps;
                if (steps > 256)
                {
                    next.Pause(new UiReactiveFault(next.Name, "propagation-budget", "Owner Resume after correcting the response", new InvalidOperationException("256 repeated evaluations of this response in one drain; not proof of a complete cycle. Applied writes remain applied.")));
                    continue;
                }
                next.Run();
            }
        }
        finally { draining = false; }
    }
}

internal sealed class UiReactiveScope : IDisposable
{
    private Action? end;
    internal UiReactiveScope(Action end) { this.end = end; }
    public void Dispose() { Action? action = end; end = null; action?.Invoke(); }
}

internal abstract class UiReactiveSource
{
    protected readonly UiReactiveRuntime Runtime;
    internal int Revision;
    private readonly HashSet<UiReactiveObserver> observers = new();
    internal int SubscriberCount => observers.Count;
    protected UiReactiveSource(UiReactiveRuntime runtime) { Runtime = runtime; }
    internal void Read()
    {
        Runtime.VerifyAccess();
        Runtime.Reads?.Add(this);
        if (Runtime.ReadVersions != null && !Runtime.ReadVersions.ContainsKey(this)) Runtime.ReadVersions.Add(this, Revision);
    }
    internal void Subscribe(UiReactiveObserver observer) => observers.Add(observer);
    internal void Unsubscribe(UiReactiveObserver observer) => observers.Remove(observer);
    internal virtual void RetryFailure(HashSet<UiReactiveSource> visited) { }
    internal void Invalidate()
    {
        Revision++;
        // Finish the entire invalidation graph before running any scheduled response.
        foreach (UiReactiveObserver observer in new List<UiReactiveObserver>(observers)) observer.MarkDirty();
    }
}

/// <summary>Explicit scalar source; arbitrary reference/deep/collection comparison is not a public default.</summary>
public sealed class UiReactiveValue<T>
{
    private sealed class Source : UiReactiveSource
    {
        internal Source(UiReactiveRuntime runtime) : base(runtime) { }
    }
    private readonly UiReactiveRuntime runtime;
    private readonly Source source;
    private readonly Func<T, T, bool> equal;
    private T value;
    internal int SubscriberCount => source.SubscriberCount;
    internal UiReactiveValue(UiReactiveRuntime runtime, T value, Func<T, T, bool> equal)
    { this.runtime = runtime; this.value = value; this.equal = equal; source = new Source(runtime); }
    // Mutation implementations need the current storage, not an implicit input dependency.
    internal T ReadCurrent() { runtime.VerifyAccess(); return value; }
    public T Value
    {
        get { source.Read(); return value; }
        set
        {
            runtime.AssertWritable();
            if (equal(this.value, value)) return;
            this.value = value;
            source.Invalidate(); runtime.Drain();
        }
    }
}

internal abstract class UiReactiveObserver : UiReactiveSource, IDisposable
{
    internal readonly string Name;
    private HashSet<UiReactiveSource> dependencies = new();
    internal bool Disposed;
    internal UiReactiveObserver(UiReactiveRuntime runtime, string name) : base(runtime) { Name = name; }
    internal void ReplaceDependencies(HashSet<UiReactiveSource> found)
    {
        if (Disposed) return;
        foreach (UiReactiveSource old in dependencies) if (!found.Contains(old)) old.Unsubscribe(this);
        foreach (UiReactiveSource item in found) if (!dependencies.Contains(item)) item.Subscribe(this);
        dependencies = found;
    }
    internal override void RetryFailure(HashSet<UiReactiveSource> visited)
    {
        if (!visited.Add(this)) return;
        foreach (UiReactiveSource dependency in dependencies) dependency.RetryFailure(visited);
    }
    internal abstract void MarkDirty();
    public virtual void Dispose()
    {
        Runtime.VerifyAccess();
        if (Disposed) return;
        Disposed = true;
        foreach (UiReactiveSource item in dependencies) item.Unsubscribe(this);
        dependencies.Clear();
    }
}

/// <summary>A located managed failure. Old presentation is separate from a current invalid result.</summary>
public sealed class UiReactiveFault : InvalidOperationException
{
    public string Identity { get; }
    public string Kind { get; }
    public string Recovery { get; }
    public string StoppedScope { get; }
    internal UiReactiveFault(string identity, string kind, string recovery, Exception cause)
        : base("Reactive " + kind + " at '" + identity + "'. Stopped: " + identity + ". Recovery: " + recovery + ".", cause)
    { Identity = identity; Kind = kind; Recovery = recovery; StoppedScope = identity; }
    private UiReactiveFault(UiReactiveFault cause, string dependent)
        : base(cause.Message + " Dependent unavailable: " + dependent + ".", cause)
    { Identity = cause.Identity; Kind = cause.Kind; Recovery = cause.Recovery; StoppedScope = cause.StoppedScope + " -> " + dependent; }
    internal UiReactiveFault Through(string dependent) => new(this, dependent);
}

/// <summary>A lazy pure read, attempted once per input change or explicit Retry.</summary>
public sealed class UiReactiveComputed<T> : IDisposable
{
    private sealed class Calculation : UiReactiveObserver
    {
        private Func<T>? read;
        internal T Last = default!;
        internal bool HasValue;
        internal UiReactiveFault? Fault;
        private bool dirty = true;
        private bool evaluating;
        internal int EvaluationRevision;
        internal Action? Invalidated;
        public override void Dispose() { base.Dispose(); read = null; Last = default!; HasValue = false; Fault = null; Invalidated = null; }
        internal Calculation(UiReactiveRuntime runtime, string name, Func<T> read) : base(runtime, name) { this.read = read; }
        internal T Value
        {
            get
            {
                Runtime.VerifyAccess();
                if (Disposed) throw new ObjectDisposedException(Name);
                // A failed child is still an actual dependency, including the edge closing a cycle.
                Read();
                if (evaluating) throw new UiReactiveFault(Name, "pure-cycle", "Related input change or Retry", new InvalidOperationException("Recursive pure calculation A -> ... -> A."));
                if (dirty)
                {
                    evaluating = true; dirty = false; EvaluationRevision++;
                    try { Last = Runtime.Track(this, read!, true); HasValue = true; Fault = null; }
                    catch (UiReactiveFault failure) { Fault = failure; }
                    finally { evaluating = false; }
                }
                if (Fault != null) throw Fault;
                return Last;
            }
        }
        internal override void MarkDirty() { if (dirty || Disposed) return; dirty = true; Invalidate(); Invalidated?.Invoke(); }
        internal override void RetryFailure(HashSet<UiReactiveSource> visited)
        {
            if (visited.Contains(this)) return;
            base.RetryFailure(visited);
            if (Fault != null) MarkDirty();
        }
        internal void Retry() { Runtime.VerifyAccess(); if (Disposed) throw new ObjectDisposedException(Name); RetryFailure(new HashSet<UiReactiveSource>()); MarkDirty(); }
    }
    private readonly Calculation calculation;
    internal UiReactiveComputed(UiReactiveRuntime runtime, string name, Func<T> read) { calculation = new Calculation(runtime, name, read); }
    public T Value => calculation.Value;
    public UiReactiveFault? Fault => calculation.Fault;
    public bool HasValue => calculation.HasValue;
    public bool TryGetLastValue(out T value) { value = calculation.Last; return calculation.HasValue; }
    public void Retry() => calculation.Retry();
    internal Action? Invalidated { set => calculation.Invalidated = value; }
    internal int Revision => calculation.EvaluationRevision;
    public void Dispose() => calculation.Dispose();
}

/// <summary>Owner-controlled automatic callback. Resume runs current state, never historical operations.</summary>
public sealed class UiReactiveResponse : IDisposable
{
    private readonly UiReactiveReaction reaction;
    internal UiReactiveResponse(UiReactiveReaction reaction) { this.reaction = reaction; }
    public UiReactiveFault? Fault => reaction.Fault;
    public bool IsPaused => reaction.Fault != null;
    public void Resume() => reaction.Resume();
    public void Dispose() => reaction.Dispose();
}

internal sealed class UiReactiveReaction : UiReactiveObserver
{
    private Action? respond;
    internal UiReactiveFault? Fault;
    internal UiReactiveReaction(UiReactiveRuntime runtime, string name, Action respond) : base(runtime, name) { this.respond = respond; }
    internal override void MarkDirty() { if (!Disposed && Fault == null) Runtime.Schedule(this); }
    public override void Dispose() { base.Dispose(); respond = null; Fault = null; }
    internal void Pause(UiReactiveFault fault) { Fault = fault; }
    internal void Resume()
    {
        Runtime.AssertWritable();
        if (Disposed) throw new ObjectDisposedException(Name);
        Fault = null; Runtime.Schedule(this); Runtime.Drain();
    }
    internal void Run()
    {
        if (Disposed || Fault != null) return;
        try { Runtime.Track(this, () => { respond!(); return true; }, false); }
        catch (UiReactiveFault failure) { Pause(new UiReactiveFault(Name, "consumer-callback", "Owner Resume after checking applied business effects", failure)); }
    }
}

/// <summary>Owns only graph subscriptions/calculations, never the external model or its service.</summary>
public sealed class UiReactiveOwner : IDisposable
{
    private readonly UiReactiveRuntime runtime;
    private readonly string name;
    private readonly List<IDisposable> children = new();
    private bool disposed;
    internal UiReactiveOwner(UiReactiveRuntime runtime, string name) { this.runtime = runtime; this.name = name; }
    private void Check() { runtime.VerifyAccess(); if (disposed) throw new ObjectDisposedException(name); }
    public UiReactiveOwner CreateChild(string key)
    {
        Check(); var child = runtime.CreateOwner(name + "/" + key); children.Add(child); return child;
    }
    public UiReactiveComputed<T> Compute<T>(string key, Func<T> read)
    {
        Check(); if (read == null) throw new ArgumentNullException(nameof(read));
        var value = new UiReactiveComputed<T>(runtime, name + "/" + key, read); children.Add(value); return value;
    }
    public IDisposable Respond(string key, Action respond) => RespondManaged(key, respond);
    public UiReactiveResponse RespondManaged(string key, Action respond)
    {
        Check(); if (respond == null) throw new ArgumentNullException(nameof(respond));
        var reaction = new UiReactiveReaction(runtime, name + "/" + key, respond);
        var handle = new UiReactiveResponse(reaction);
        children.Add(handle); runtime.Schedule(reaction); runtime.Drain(); return handle;
    }
    public void Dispose()
    {
        runtime.VerifyAccess(); if (disposed) return; disposed = true;
        foreach (IDisposable child in children) child.Dispose(); children.Clear();
    }
}
