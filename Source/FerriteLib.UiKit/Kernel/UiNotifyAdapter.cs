using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// The optional bridge from <see cref="INotifyPropertyChanged"/> to this library's binding notification.
/// <para>
/// <b>Explicit, never inferred.</b> The library does not walk a model, does not read attributes and does not
/// guess which keys a property feeds. A consumer says so once, with <see cref="Map"/> (one property drives
/// one or more binding keys) and <see cref="MapAll"/> (the empty-property-name convention, which means "the
/// whole object moved"). A property nobody mapped announces nothing - even when <see cref="MapAll"/> was
/// declared, a named property is announced only by its own mapping; a consumer that wants a property to
/// announce everything says so with <c>Map(name, allTheKeys)</c>. An unmapped property is a consumer's
/// omission that stays visible, not a notification silently widened to every key in the page.
/// </para>
/// <para>
/// <b>A plain C# object is a VM.</b> There is no base class to inherit, no container to configure and no
/// reflection: the adapter is ordinary event plumbing over <see cref="IUiBindings.NotifyChanged"/>, which
/// already coalesces announcements into one commit at the next frame boundary. What the adapter adds is the
/// explicit mapping, a bounded pending set, a nesting batch scope, and one honest answer about threads.
/// </para>
/// <para>
/// <b>Bounded, so a burst cannot grow without limit.</b> Distinct keys pending a flush are capped by
/// <see cref="MaxPendingKeys"/>; reaching the cap flushes what is held, even inside a batch. A batch is a
/// coalescing optimisation with a ceiling, not a promise that exactly one announcement will ever be made.
/// </para>
/// <para>
/// <b>Threads.</b> This first version delivers on the main thread only. A notification raised from any other
/// thread is refused before it can touch a game object, counted in
/// <see cref="OffThreadNotificationCount"/> and surfaced through <see cref="NotificationRejected"/>; it is
/// never queued and never replayed later. A background producer must marshal to the main thread itself. That
/// is a deliberate first-version boundary stated rather than hidden - a "safe" queue that nobody has observed
/// in a real game would be a promise this round cannot keep.
/// </para>
/// <para>
/// <b>Who releases whom.</b> The adapter owns its subscriptions, not the source objects: <see cref="Dispose"/>
/// unsubscribes every source and never disposes one, because the consumer owns its own model. The handle
/// <see cref="Attach"/> returns does the same for one source, so a page that attaches on open and releases on
/// close accumulates nothing.
/// </para>
/// </summary>
public sealed class UiNotifyAdapter : IDisposable
{
    /// <summary>The default cap on distinct keys pending one flush.</summary>
    public const int DefaultMaxPendingKeys = 256;

    private readonly Dictionary<string, string[]> propertyKeys = new Dictionary<string, string[]>(StringComparer.Ordinal);
    private readonly Dictionary<INotifyPropertyChanged, PropertyChangedEventHandler> handlers =
        new Dictionary<INotifyPropertyChanged, PropertyChangedEventHandler>(ReferenceEqualityComparer.Instance);
    private readonly HashSet<string> pendingKeys = new HashSet<string>(StringComparer.Ordinal);
    private string[] allKeys = Array.Empty<string>();
    private int batchDepth;
    private bool disposed;

    public UiNotifyAdapter(IUiBindings bindings, IUiMainThread? mainThread = null, int maxPendingKeys = DefaultMaxPendingKeys)
    {
        Bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
        MainThread = mainThread ?? VerseFerriteMainThread.Instance;
        if (maxPendingKeys <= 0) throw new ArgumentOutOfRangeException(nameof(maxPendingKeys));
        MaxPendingKeys = maxPendingKeys;
    }

    /// <summary>The binding surface every announcement is written to.</summary>
    public IUiBindings Bindings { get; }

    /// <summary>The thread question this adapter asks before it delivers anything.</summary>
    public IUiMainThread MainThread { get; }

    /// <summary>The cap on distinct keys held before the adapter flushes early.</summary>
    public int MaxPendingKeys { get; }

    /// <summary>How many sources are attached.</summary>
    public int AttachedSourceCount => handlers.Count;

    /// <summary>How many property names carry an explicit mapping.</summary>
    public int MappedPropertyCount => propertyKeys.Count;

    /// <summary>Distinct keys waiting for the next flush.</summary>
    public int PendingKeyCount => pendingKeys.Count;

    /// <summary>Notifications refused because they arrived off the main thread.</summary>
    public int OffThreadNotificationCount { get; private set; }

    /// <summary>Notifications that resolved to no key at all: a named property with no mapping.</summary>
    public int UnmappedNotificationCount { get; private set; }

    /// <summary>How many times a pending set was written to the bindings.</summary>
    public int FlushCount { get; private set; }

    /// <summary>
    /// Raised once per refused notification, with the property name the source announced (empty for the
    /// all-properties convention). It exists so "the update did not arrive" is diagnosable at the point it
    /// happened rather than inferred later from a stale control.
    /// </summary>
    public event Action<string>? NotificationRejected;

    /// <summary>
    /// Maps one property name to the binding keys it drives. A repeated call replaces the mapping. An empty
    /// property name is the all-properties convention and is equivalent to <see cref="MapAll"/>.
    /// </summary>
    public UiNotifyAdapter Map(string propertyName, params string[] bindingKeys)
    {
        EnsureAlive();
        if (propertyName == null) throw new ArgumentNullException(nameof(propertyName));
        if (propertyName.Length == 0)
        {
            return MapAll(bindingKeys);
        }

        propertyKeys[propertyName] = Copy(bindingKeys);
        return this;
    }

    /// <summary>
    /// Maps the empty-property-name notification ("the whole object moved") to these binding keys. It does not
    /// apply to named properties: see the type summary.
    /// </summary>
    public UiNotifyAdapter MapAll(params string[] bindingKeys)
    {
        EnsureAlive();
        allKeys = Copy(bindingKeys);
        return this;
    }

    /// <summary>
    /// Subscribes to one source and returns the handle that unsubscribes it. Attaching the same source twice is
    /// idempotent: one subscription, two handles that each release it once.
    /// </summary>
    public IDisposable Attach(INotifyPropertyChanged source)
    {
        EnsureAlive();
        if (source == null) throw new ArgumentNullException(nameof(source));

        if (!handlers.TryGetValue(source, out PropertyChangedEventHandler? handler))
        {
            handler = OnPropertyChanged;
            handlers.Add(source, handler);
            source.PropertyChanged += handler;
        }

        return new SourceSubscription(this, source);
    }

    /// <summary>
    /// Unsubscribes one source. False when it was not attached. Unlike the rest of the surface this does not
    /// throw after <see cref="Dispose"/>: a subscription handle that is released twice, or released after the
    /// page closed, must be harmless.
    /// </summary>
    public bool Detach(INotifyPropertyChanged source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (disposed) return false;

        if (!handlers.TryGetValue(source, out PropertyChangedEventHandler? handler))
        {
            return false;
        }

        handlers.Remove(source);
        source.PropertyChanged -= handler;
        return true;
    }

    /// <summary>
    /// Opens a batch: announcements merge into one flush when the outermost batch closes, so a VM that raises
    /// twenty notifications while recomputing announces once. Nesting is allowed; only the outermost scope
    /// flushes. The pending set is still bounded inside a batch - reaching <see cref="MaxPendingKeys"/>
    /// flushes early rather than growing.
    /// </summary>
    public IDisposable BeginBatch()
    {
        EnsureAlive();
        batchDepth++;
        return new BatchScope(this);
    }

    /// <summary>Writes every pending key to the bindings now. A no-op while a batch is open, and outside a batch
    /// a notification is already announced as it arrives.</summary>
    public void Flush()
    {
        EnsureAlive();
        if (batchDepth > 0)
        {
            return;
        }

        ForceFlush();
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        foreach (KeyValuePair<INotifyPropertyChanged, PropertyChangedEventHandler> pair in handlers)
        {
            pair.Key.PropertyChanged -= pair.Value;
        }

        handlers.Clear();
        propertyKeys.Clear();
        pendingKeys.Clear();
        allKeys = Array.Empty<string>();
        batchDepth = 0;
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        OnPropertyChanged(args?.PropertyName);
    }

    private void OnPropertyChanged(string? propertyName)
    {
        if (disposed)
        {
            return;
        }

        string name = propertyName ?? string.Empty;

        // The thread question is asked before the mapping is even resolved, because resolving it is harmless
        // but delivering it is not: a game object touched from a worker thread fails in ways that surface far
        // from the callback that caused them.
        if (!MainThread.IsCurrent)
        {
            OffThreadNotificationCount++;
            NotificationRejected?.Invoke(name);
            return;
        }

        string[] keys = Resolve(name);
        if (keys.Length == 0)
        {
            UnmappedNotificationCount++;
            return;
        }

        for (int i = 0; i < keys.Length; i++)
        {
            Queue(keys[i]);
        }
    }

    private string[] Resolve(string propertyName)
    {
        if (propertyName.Length == 0)
        {
            return allKeys;
        }

        return propertyKeys.TryGetValue(propertyName, out string[]? keys) ? keys : Array.Empty<string>();
    }

    private void Queue(string key)
    {
        if (key.Length == 0)
        {
            return;
        }

        if (!pendingKeys.Add(key))
        {
            return;
        }

        // The ceiling is the contract, so it wins over the batch: a burst that outruns the cap announces what
        // it holds instead of growing without limit.
        if (pendingKeys.Count >= MaxPendingKeys)
        {
            ForceFlush();
            return;
        }

        if (batchDepth == 0)
        {
            ForceFlush();
        }
    }

    private void ForceFlush()
    {
        if (pendingKeys.Count == 0)
        {
            return;
        }

        string[] keys = new string[pendingKeys.Count];
        pendingKeys.CopyTo(keys);
        pendingKeys.Clear();
        FlushCount++;
        Bindings.NotifyChanged(keys);
    }

    private void EndBatch()
    {
        // Tolerant on purpose: the scope may be disposed after the adapter was disposed, and a handle that
        // outlives its owner must not throw out of a using block.
        if (disposed || batchDepth == 0)
        {
            return;
        }

        batchDepth--;
        if (batchDepth == 0)
        {
            ForceFlush();
        }
    }

    private static string[] Copy(string[]? keys)
    {
        if (keys == null || keys.Length == 0)
        {
            return Array.Empty<string>();
        }

        var copy = new string[keys.Length];
        for (int i = 0; i < keys.Length; i++)
        {
            copy[i] = keys[i] ?? throw new ArgumentException("A mapped binding key cannot be null.", nameof(keys));
        }

        return copy;
    }

    private void EnsureAlive()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(UiNotifyAdapter));
        }
    }

    /// <summary>The handle one <see cref="Attach"/> call handed out; releasing it twice is harmless.</summary>
    private sealed class SourceSubscription : IDisposable
    {
        private UiNotifyAdapter? owner;
        private readonly INotifyPropertyChanged source;

        internal SourceSubscription(UiNotifyAdapter owner, INotifyPropertyChanged source)
        {
            this.owner = owner;
            this.source = source;
        }

        public void Dispose()
        {
            UiNotifyAdapter? current = owner;
            owner = null;
            current?.Detach(source);
        }
    }

    /// <summary>One nesting level of a batch scope.</summary>
    private sealed class BatchScope : IDisposable
    {
        private UiNotifyAdapter? owner;

        internal BatchScope(UiNotifyAdapter owner)
        {
            this.owner = owner;
        }

        public void Dispose()
        {
            UiNotifyAdapter? current = owner;
            owner = null;
            current?.EndBatch();
        }
    }
}
