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
/// <b>A leaked inner scope cannot strand the set.</b> Batch scopes nest, and only the outermost open scope
/// commits. Ending a scope closes it and every scope opened after it, so a consumer that drops an inner
/// handle still delivers when the outer one ends: the pending set is written because the outermost scope is
/// gone, not because every handle was released. A leaked <em>outermost</em> scope is the one case the ceiling
/// resolves, which is exactly why the ceiling exists.
/// </para>
/// <para>
/// <b>What "the batch is never ended" does to a pending key, stated rather than implied.</b> A key queued
/// inside an open batch is delivered when the outermost scope ends or when the pending set reaches
/// <see cref="MaxPendingKeys"/>. It is not delivered by <see cref="Flush"/> while any scope is open, and an
/// outermost scope that is neither ended nor pushed to the ceiling leaves the key pending - bounded, visible
/// through <see cref="PendingKeyCount"/>, and released by the ceiling rather than declared impossible.
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

        // Validated before the assignment, so a refused mapping never replaces the one already in force.
        propertyKeys[propertyName] = Copy(bindingKeys, nameof(bindingKeys));
        return this;
    }

    /// <summary>
    /// Maps the empty-property-name notification ("the whole object moved") to these binding keys. It does not
    /// apply to named properties: see the type summary.
    /// </summary>
    public UiNotifyAdapter MapAll(params string[] bindingKeys)
    {
        EnsureAlive();
        allKeys = Copy(bindingKeys, nameof(bindingKeys));
        return this;
    }

    /// <summary>
    /// Subscribes to one source and returns the handle that unsubscribes it. Attaching the same source twice is
    /// idempotent: one subscription, two handles, and exactly one of them releases it. The handle is bound to
    /// the subscription it was handed out for, so a stale handle from an earlier attach cannot release a
    /// subscription that replaced it.
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

        return new SourceSubscription(this, source, handler);
    }

    /// <summary>
    /// Unsubscribes one source, whichever subscription is currently attached. False when it was not attached.
    /// Unlike the rest of the surface this does not throw after <see cref="Dispose"/>: a subscription handle
    /// that is released twice, or released after the page closed, must be harmless.
    /// </summary>
    public bool Detach(INotifyPropertyChanged source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (disposed) return false;

        if (!handlers.TryGetValue(source, out PropertyChangedEventHandler? handler))
        {
            return false;
        }

        Remove(source, handler);
        return true;
    }

    /// <summary>
    /// Releases one specific subscription, refusing when the source now carries a different one: a handle that
    /// outlived the subscription it was handed out for must not release the subscription that replaced it.
    /// </summary>
    private bool Release(INotifyPropertyChanged source, PropertyChangedEventHandler handler)
    {
        if (disposed) return false;
        if (!handlers.TryGetValue(source, out PropertyChangedEventHandler? current)) return false;
        if (!ReferenceEquals(current, handler)) return false;

        Remove(source, current);
        return true;
    }

    private void Remove(INotifyPropertyChanged source, PropertyChangedEventHandler handler)
    {
        handlers.Remove(source);
        source.PropertyChanged -= handler;
    }

    /// <summary>
    /// Opens a batch: announcements merge into one flush when the outermost batch closes, so a VM that raises
    /// twenty notifications while recomputing announces once. Nesting is allowed; only the outermost scope
    /// flushes, and ending a scope also closes the scopes nested inside it - so an inner handle that is never
    /// released cannot strand the pending set. The set is still bounded inside a batch: reaching
    /// <see cref="MaxPendingKeys"/> flushes early rather than growing.
    /// </summary>
    public IDisposable BeginBatch()
    {
        EnsureAlive();
        batchDepth++;
        return new BatchScope(this, batchDepth);
    }

    /// <summary>
    /// Writes every pending key to the bindings now. Inside a batch this is deliberately a no-op: the batch's
    /// outermost scope is the commit point, and a flush that punched through it would make the coalescing
    /// window's width depend on which notifications happened to call this. Outside a batch a notification
    /// commits as it arrives, so the set is already empty when this runs; the member stays public because the
    /// contract names it, and it is the explicit door a future queue-without-commit path would use.
    /// </summary>
    public void Flush()
    {
        EnsureAlive();
        if (batchDepth > 0)
        {
            return;
        }

        ForceFlush();
    }

    /// <summary>
    /// Unsubscribes every attached source and drops the pending set. It never disposes a source - the
    /// consumer owns its model - and it is idempotent. After this only <see cref="Detach"/> is callable:
    /// every other member throws, while a subscription handle or batch scope that outlives the adapter stays
    /// harmless rather than throwing out of a <c>using</c> block.
    /// </summary>
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

        // Outside every batch, one notification is one write however many keys its mapping carries: the
        // loop above only queues, and the commit happens here. Flushing per key inside the loop would turn
        // one property whose mapping names three keys into three separate announcements, which is the
        // opposite of what the mapping is for.
        if (batchDepth == 0)
        {
            ForceFlush();
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
        // it holds instead of growing without limit. Whether an under-cap set is written now is decided by the
        // caller, because a batch's outer scope - not each key - is the commit point.
        if (pendingKeys.Count >= MaxPendingKeys)
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

    private void EndBatch(int level)
    {
        // Tolerant on purpose: the scope may be disposed after the adapter was disposed, and a handle that
        // outlives its owner must not throw out of a using block. A scope whose level is above the current
        // depth was already closed by an enclosing scope, so releasing it again is a no-op rather than a
        // resurrection of the depth it used to hold.
        if (disposed || level > batchDepth)
        {
            return;
        }

        // Ending a scope closes it and every scope opened after it. That is the whole fix for a dropped
        // inner handle: the outermost scope is still the commit point, so its end writes the keys instead of
        // them staying pending because one nested handle was never released.
        batchDepth = level - 1;
        if (batchDepth == 0)
        {
            ForceFlush();
        }
    }

    /// <summary>
    /// Clones a mapping's key list, refusing anything the binding surface could never deliver. A null list
    /// (a literal <c>null</c> passed for the <c>params</c> array) and a null or empty entry are all caller
    /// errors: a key that can never be announced is a configuration mistake, not an empty mapping, and the
    /// binding surface's own <c>NotifyChanged</c> refuses the same shape. The clone means a caller's later
    /// mutation of its array cannot reach into a mapping already in force.
    /// </summary>
    private static string[] Copy(string[]? keys, string parameterName)
    {
        if (keys == null)
        {
            throw new ArgumentNullException(parameterName, "A mapped binding key list cannot be null.");
        }

        if (keys.Length == 0)
        {
            return Array.Empty<string>();
        }

        var copy = new string[keys.Length];
        for (int i = 0; i < keys.Length; i++)
        {
            string? key = keys[i];
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException(
                    "A mapped binding key must be non-null and non-empty; entry " + i + " is not.", parameterName);
            }

            copy[i] = key;
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

    /// <summary>
    /// The handle one <see cref="Attach"/> call handed out. Releasing it twice is harmless, and it carries the
    /// handler that was current when it was created so the release cannot land on a later subscription.
    /// </summary>
    private sealed class SourceSubscription : IDisposable
    {
        private UiNotifyAdapter? owner;
        private readonly INotifyPropertyChanged source;
        private readonly PropertyChangedEventHandler handler;

        internal SourceSubscription(UiNotifyAdapter owner, INotifyPropertyChanged source, PropertyChangedEventHandler handler)
        {
            this.owner = owner;
            this.source = source;
            this.handler = handler;
        }

        public void Dispose()
        {
            UiNotifyAdapter? current = owner;
            owner = null;
            current?.Release(source, handler);
        }
    }

    /// <summary>
    /// One nesting level of a batch scope. It carries the depth it was opened at, because ending it has to
    /// close every scope that was opened after it - see <see cref="EndBatch"/>.
    /// </summary>
    private sealed class BatchScope : IDisposable
    {
        private UiNotifyAdapter? owner;
        private readonly int level;

        internal BatchScope(UiNotifyAdapter owner, int level)
        {
            this.owner = owner;
            this.level = level;
        }

        public void Dispose()
        {
            UiNotifyAdapter? current = owner;
            owner = null;
            current?.EndBatch(level);
        }
    }
}
