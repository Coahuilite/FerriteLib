using System;
using System.Collections.Generic;
using System.Threading;

namespace FerriteLib.UiKit.Kernel;

/// <summary>Serially owned reactive graph. The adapter delivers work to the creating context;
/// this object neither owns a business scheduler nor marshals arbitrary external models.</summary>
public sealed class UiReactiveRuntime
{
    private readonly int thread = Thread.CurrentThread.ManagedThreadId;
    private readonly List<UiReactiveReaction> pending = new();
    private readonly HashSet<UiReactiveReaction> queued = new();
    private int batchDepth;
    private bool draining;
    private int pureDepth;
    internal UiReactiveObserver? Observer;
    internal HashSet<UiReactiveSource>? Reads;

    public void VerifyAccess()
    {
        if (Thread.CurrentThread.ManagedThreadId != thread)
            throw new InvalidOperationException("Reactive access belongs to its creating serial context. Deliver a safe result through the host adapter first.");
    }

    public UiReactiveValue<int> Int(int value = 0) => new(this, value, (a, b) => a == b);
    public UiReactiveValue<string> Text(string value = "") => new(this, value, (a, b) => string.Equals(a, b, StringComparison.Ordinal));
    public UiReactiveValue<bool> Boolean(bool value = false) => new(this, value, (a, b) => a == b);
    public UiReactiveOwner CreateOwner(string name) { VerifyAccess(); return new UiReactiveOwner(this, name); }

    /// <summary>Nested batches defer automatic responses, not assignment, invalidation or explicit reads.</summary>
    public IDisposable Batch()
    {
        VerifyAccess(); AssertWritable(); batchDepth++;
        return new UiReactiveScope(() => { VerifyAccess(); if (--batchDepth == 0) Drain(); });
    }

    internal IDisposable Pure()
    {
        VerifyAccess(); pureDepth++;
        return new UiReactiveScope(() => pureDepth--);
    }

    internal void AssertWritable()
    {
        VerifyAccess();
        if (pureDepth != 0) throw new InvalidOperationException("Business writes/commands are forbidden in a tracked calculation or measurement.");
    }

    internal T Track<T>(UiReactiveObserver observer, Func<T> read, bool pure)
    {
        VerifyAccess();
        UiReactiveObserver? previous = Observer;
        HashSet<UiReactiveSource>? previousReads = Reads;
        var found = new HashSet<UiReactiveSource>();
        Observer = observer; Reads = found;
        IDisposable? scope = pure ? Pure() : null;
        try
        {
            T result = read();
            observer.ReplaceDependencies(found);
            return result;
        }
        catch (Exception ex)
        {
            // A failed evaluation does not retain provisional edges, publish a value or replay an operation.
            throw new InvalidOperationException("Reactive evaluation failed at '" + observer.Name + "'.", ex);
        }
        finally { scope?.Dispose(); Observer = previous; Reads = previousReads; }
    }

    internal void Schedule(UiReactiveReaction reaction)
    {
        if (queued.Add(reaction)) pending.Add(reaction);
    }

    internal void Drain()
    {
        if (batchDepth != 0 || draining) return;
        draining = true;
        int steps = 0;
        try
        {
            while (pending.Count != 0)
            {
                UiReactiveReaction next = pending[0]; pending.RemoveAt(0); queued.Remove(next);
                if (next.Disposed) continue;
                if (++steps > 100) throw new InvalidOperationException("Reactive response cycle near '" + next.Name + "' (100 scheduled evaluations). Queue discarded; business writes remain applied.");
                next.Run();
            }
        }
        catch { pending.Clear(); queued.Clear(); throw; }
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
    private readonly HashSet<UiReactiveObserver> observers = new();
    protected UiReactiveSource(UiReactiveRuntime runtime) { Runtime = runtime; }
    internal void Read()
    {
        Runtime.VerifyAccess();
        Runtime.Reads?.Add(this);
    }
    internal void Subscribe(UiReactiveObserver observer) => observers.Add(observer);
    internal void Unsubscribe(UiReactiveObserver observer) => observers.Remove(observer);
    internal void Invalidate()
    {
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
    internal UiReactiveValue(UiReactiveRuntime runtime, T value, Func<T, T, bool> equal)
    { this.runtime = runtime; this.value = value; this.equal = equal; source = new Source(runtime); }
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

/// <summary>A lazy explicitly readable derived value, owned by a subscription lifetime.</summary>
public sealed class UiReactiveComputed<T> : IDisposable
{
    private sealed class Calculation : UiReactiveObserver
    {
        private Func<T>? read;
        private T value = default!;
        private bool dirty = true;
        private bool evaluating;
        internal int Revision;
        public override void Dispose() { base.Dispose(); read = null; value = default!; }
        internal Calculation(UiReactiveRuntime runtime, string name, Func<T> read) : base(runtime, name) { this.read = read; }
        internal T Value
        {
            get
            {
                Runtime.VerifyAccess();
                if (Disposed) throw new ObjectDisposedException(Name);
                if (evaluating) throw new InvalidOperationException("Reactive calculation cycle at '" + Name + "'.");
                if (dirty)
                {
                    evaluating = true;
                    try { value = Runtime.Track(this, read!, true); dirty = false; Revision++; }
                    finally { evaluating = false; }
                }
                Read(); return value;
            }
        }
        internal override void MarkDirty() { if (dirty || Disposed) return; dirty = true; Invalidate(); }
    }
    private readonly Calculation calculation;
    internal UiReactiveComputed(UiReactiveRuntime runtime, string name, Func<T> read) { calculation = new Calculation(runtime, name, read); }
    public T Value => calculation.Value;
    internal int Revision => calculation.Revision;
    public void Dispose() => calculation.Dispose();
}

internal sealed class UiReactiveReaction : UiReactiveObserver
{
    private Action? respond;
    internal UiReactiveReaction(UiReactiveRuntime runtime, string name, Action respond) : base(runtime, name) { this.respond = respond; }
    internal override void MarkDirty() { if (!Disposed) Runtime.Schedule(this); }
    public override void Dispose() { base.Dispose(); respond = null; }
    internal void Run() { if (!Disposed) Runtime.Track(this, () => { respond!(); return true; }, false); }
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
    public IDisposable Respond(string key, Action respond)
    {
        Check(); if (respond == null) throw new ArgumentNullException(nameof(respond));
        var reaction = new UiReactiveReaction(runtime, name + "/" + key, respond);
        try { reaction.Run(); children.Add(reaction); return reaction; }
        catch { reaction.Dispose(); throw; }
    }
    public void Dispose()
    {
        runtime.VerifyAccess(); if (disposed) return; disposed = true;
        foreach (IDisposable child in children) child.Dispose(); children.Clear();
    }
}
