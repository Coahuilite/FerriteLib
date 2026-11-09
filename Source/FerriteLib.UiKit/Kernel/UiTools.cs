using System;
using System.Collections.Generic;
using System.Threading;

namespace FerriteLib.UiKit.Kernel;

public enum UiToolAccess { ReadOnly, Operate }
public enum UiToolOperation { Activate, Expand, Collapse, Select, SetNumber, SetDraft, CommitEdit, CancelEdit }
public enum UiToolRequestStatus { Queued, Handled, Rejected, Closed, Failed }

/// <summary>A wrapper-owned semantic part, not a business command exposed by name.</summary>
public sealed class UiSemanticAction
{
    public UiSemanticAction(UiToolOperation operation, string part = "", string item = "", string name = "")
    { Operation = operation; Part = part; Item = item; Name = name; }
    public UiToolOperation Operation { get; }
    public string Part { get; }
    public string Item { get; }
    public string Name { get; }
    internal object? Choice;
    internal string IdentityKey => Operation + "/" + Part.Length + ":" + Part + Item.Length + ":" + Item;
    internal bool Same(UiSemanticAction other) => Operation == other.Operation && Part == other.Part && Item == other.Item && Equals(Choice, other.Choice);
}

/// <summary>Optional custom wrapper seam. Reads must be pure; execution uses the original typed owner.</summary>
public interface IUiSemanticWidget
{
    IReadOnlyList<UiSemanticAction> ReadActions(UiWidgetContext context, int maximum);
    bool ExecuteAction(UiWidgetContext context, UiSemanticAction action, UiToolValue value);
}

public sealed class UiToolValue
{
    public UiToolValue(float? number = null, string? text = null)
    {
        if (number.HasValue && (float.IsNaN(number.Value) || float.IsInfinity(number.Value))) throw new ArgumentOutOfRangeException(nameof(number));
        if (text?.Length > 4096) throw new ArgumentOutOfRangeException(nameof(text));
        Number = number; Text = text;
    }
    public float? Number { get; }
    public string? Text { get; }
}

/// <summary>Connection-local opaque address. Names and declared IDs are display data, never authority.</summary>
public sealed class UiToolTarget
{
    internal UiToolTarget(string id, int host, int definition, long mount, string node, string scope, UiSemanticAction action, long itemLifetime = 0)
    { Id = id; Host = host; Definition = definition; Mount = mount; Node = node; Scope = scope; Action = new UiSemanticAction(action.Operation, action.Part, action.Item, action.Name); ItemLifetime = itemLifetime; }
    public string Id { get; }
    public int Host { get; }
    public int Definition { get; }
    public long Mount { get; }
    public long ItemLifetime { get; }
    public string Node { get; }
    public string Scope { get; }
    public UiSemanticAction Action { get; }
}

// Live typed choice identities never escape in detached public snapshots.
internal sealed class UiToolCapability
{
    internal UiToolCapability(UiToolTarget target, UiSemanticAction action) { Target = target; Action = action; }
    internal UiToolTarget Target { get; }
    internal UiSemanticAction Action { get; }
}

public sealed class UiToolSnapshot
{
    internal UiToolSnapshot(long revision, UiHostObservation observation, IReadOnlyList<UiToolTarget> targets, bool targetsMayBeTruncated)
    { Revision = revision; Observation = observation; Targets = targets; TargetsMayBeTruncated = targetsMayBeTruncated; }
    public long Revision { get; }
    public UiHostObservation Observation { get; }
    public IReadOnlyList<UiToolTarget> Targets { get; }
    public bool TargetsMayBeTruncated { get; }
}

public sealed class UiToolRequestResult
{
    internal UiToolRequestResult(string request, UiToolRequestStatus status, string reason, bool attempted, long revision)
    { Request = request; Status = status; Reason = reason.Length <= 4096 ? reason : reason.Substring(0, 4096); Attempted = attempted; Revision = revision; }
    public string Request { get; }
    public UiToolRequestStatus Status { get; }
    public string Reason { get; }
    public bool Attempted { get; }
    public long Revision { get; }
}

public sealed class UiToolDiagnostics
{
    internal UiToolDiagnostics(IReadOnlyList<UiDiagnosticEvent> events, UiDevGeometrySnapshot? geometry, long dropped)
    { Events = events; Geometry = geometry; Dropped = dropped; }
    public IReadOnlyList<UiDiagnosticEvent> Events { get; }
    public UiDevGeometrySnapshot? Geometry { get; }
    public long Dropped { get; }
    public UiObservationStatus GeometryStatus => Geometry == null ? UiObservationStatus.NotSampled : UiObservationStatus.Sampled;
}

/// <summary>One selected Host connection. Queue/result are thread-safe; Capture belongs to the Host thread.</summary>
public sealed partial class UiToolConnection : IDisposable
{
    private readonly WeakReference<UiHost> host;
    private readonly object gate = new();
    private readonly Queue<Pending> queue = new();
    private readonly Dictionary<string, UiToolRequestResult> results = new(StringComparer.Ordinal);
    private readonly Dictionary<string, UiToolCapability> targets = new(StringComparer.Ordinal);
    private readonly int thread;
    private long revision;
    private bool closed;
    private UiToolSnapshot? snapshot;
    private UiCostSampleAttachment? costSampleProvider;
    private UiDiagnosticSubscription? instruments;
    private bool priorGeometry, priorOverlay, priorTiming;
    internal sealed class Pending
    {
        internal string Request = "";
        internal UiToolCapability? Capability;
        internal UiToolTarget Target => Capability!.Target;
        internal UiToolValue Value = null!;
        internal UiCostSampleAttachment? CostProvider;
        internal UiCostSampleRequest? CostRequest;
    }
    internal UiToolConnection(UiHost host, UiToolAccess access)
    { this.host = new WeakReference<UiHost>(host); thread = Thread.CurrentThread.ManagedThreadId; Access = access; }
    public UiToolAccess Access { get; }
    public bool IsClosed { get { lock (gate) return closed; } }
    public UiToolSnapshot? Latest { get { lock (gate) return snapshot; } }
    internal void RequireThread()
    { if (Thread.CurrentThread.ManagedThreadId != thread) throw new InvalidOperationException("Use the Host thread for live reads, advancement and instrumentation; background clients may only queue/read detached results."); }

    public UiToolSnapshot Capture(UiObservationBudget? budget = null, string? nodeKey = null)
    {
        RequireThread();
        if (!host.TryGetTarget(out UiHost? owner) || !owner.Session.IsActive || IsClosed) throw new ObjectDisposedException(nameof(UiToolConnection));
        UiObservationBudget limit = budget ?? new UiObservationBudget();
        HashSet<string>? previous = InterestedNodes();
        UiHostObservation observed = owner.Observe(limit, nodeKey);
        long next;
        lock (gate) next = ++revision;
        var capabilities = owner.ReadToolTargets(observed, limit, out bool limited);
        var addresses = new List<UiToolTarget>();
        foreach (UiToolCapability capability in capabilities) addresses.Add(capability.Target);
        var captured = new UiToolSnapshot(next, observed, addresses.AsReadOnly(), limited);
        lock (gate)
        {
            if (closed) throw new ObjectDisposedException(nameof(UiToolConnection));
            targets.Clear();
            foreach (UiToolCapability capability in capabilities) targets.Add(capability.Target.Id, capability);
            snapshot = captured;
        }
        if (previous != null)
            foreach (string key in previous)
                if (!HoldsNode(key)) owner.Session.FindObservationNode(key)?.ClearToolItems();
        return captured;
    }

    public UiToolRequestResult Queue(string requestId, string targetId, UiToolValue? value = null)
        => QueueCore(requestId, targetId, value, null);
    /// <summary>Uses the same queue, ledger and one-attempt owner budget as semantic operations. ReadCostSample selects the optional provider first.</summary>
    public UiToolRequestResult QueueCostSample(string requestId, UiCostSampleRequest request)
        => QueueCore(requestId, null, null, request ?? throw new ArgumentNullException(nameof(request)));
    private UiToolRequestResult QueueCore(string requestId, string? targetId, UiToolValue? value, UiCostSampleRequest? costRequest)
    {
        if (string.IsNullOrWhiteSpace(requestId) || requestId.Length > 128) throw new ArgumentException("A request ID of 1..128 characters is required.", nameof(requestId));
        lock (gate)
        {
            if (results.TryGetValue(requestId, out UiToolRequestResult? existing)) return existing;
            UiToolRequestStatus status = UiToolRequestStatus.Queued;
            string reason = "await-owner-advance";
            if (closed) { status = UiToolRequestStatus.Closed; reason = "connection-closed"; }
            else if (Access != UiToolAccess.Operate) { status = UiToolRequestStatus.Rejected; reason = "read-only-connection"; }
            else if (results.Count >= 256) { return new UiToolRequestResult(requestId, UiToolRequestStatus.Rejected, "connection-budget-exhausted; reconnect-for-new-requests", false, revision); }
            else if (queue.Count >= 64) { status = UiToolRequestStatus.Rejected; reason = "queue-budget-exhausted"; }
            else if (costRequest != null && costSampleProvider == null) { status = UiToolRequestStatus.Rejected; reason = "no-selected-sample-provider; read-cost-sample-first"; }
            else if (costRequest == null && (targetId == null || !targets.ContainsKey(targetId))) { status = UiToolRequestStatus.Rejected; reason = "target-not-in-latest-capture"; }
            var result = new UiToolRequestResult(requestId, status, reason, false, revision);
            // The bounded idempotency ledger is retained until disconnect; terminal results are never evicted.
            if (results.Count < 256) results.Add(requestId, result);
            if (status == UiToolRequestStatus.Queued) queue.Enqueue(new Pending { Request = requestId,
                Capability = costRequest == null ? targets[targetId!] : null, Value = value ?? new UiToolValue(),
                CostProvider = costRequest == null ? null : costSampleProvider, CostRequest = costRequest });
            return result;
        }
    }
    public UiToolRequestResult? Result(string requestId) { lock (gate) return results.TryGetValue(requestId, out UiToolRequestResult? result) ? result : null; }
    /// <summary>Reads only the actual window's retained sample. Unsupported/NotSampled/Failed never imply measured zero.</summary>
    public UiCostSampleSnapshot ReadCostSample()
    {
        RequireThread();
        if (IsClosed || !host.TryGetTarget(out UiHost? owner) || !owner.Session.IsActive) throw new ObjectDisposedException(nameof(UiToolConnection));
        UiCostSampleAttachment? source = owner.CostSampleProvider;
        lock (gate)
        {
            if (closed) throw new ObjectDisposedException(nameof(UiToolConnection));
            costSampleProvider = source;
        }
        if (source == null) return new UiCostSampleSnapshot(UiObservationStatus.Unsupported, "host/optional-cost-provider", endReason: "no-provider");
        try { return source.Provider.ReadSample() ?? new UiCostSampleSnapshot(UiObservationStatus.Failed, "host/optional-cost-provider", endReason: "provider-returned-null"); }
        catch (Exception error) when (UiObservationBoundary.IsOrdinary(error))
        { return new UiCostSampleSnapshot(UiObservationStatus.Failed, "host/optional-cost-provider", endReason: error.GetType().Name + ": " + error.Message); }
    }
    public UiToolDiagnostics ReadDiagnostics()
    {
        RequireThread();
        if (IsClosed || !host.TryGetTarget(out UiHost? owner) || !owner.Session.IsActive) throw new ObjectDisposedException(nameof(UiToolConnection));
        UiDiagnosticSubscription? source = owner.CurrentDiagnostics;
        UiDevGeometrySnapshot? geometry = null;
        source?.TryGetGeometrySnapshot(out geometry);
        return new UiToolDiagnostics(source?.Snapshot() ?? Array.Empty<UiDiagnosticEvent>(), geometry, source?.Dropped ?? 0);
    }
    internal Pending? Take() { lock (gate) return closed || queue.Count == 0 ? null : queue.Dequeue(); }
    internal HashSet<string>? InterestedNodes()
    {
        lock (gate)
        {
            if (targets.Count == 0 && queue.Count == 0) return null;
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (UiToolCapability target in targets.Values) keys.Add(target.Target.Node);
            foreach (Pending pending in queue) if (pending.Capability != null) keys.Add(pending.Target.Node);
            return keys.Count == 0 ? null : keys;
        }
    }
    internal bool HoldsNode(string key)
    {
        lock (gate)
        {
            foreach (UiToolCapability target in targets.Values) if (target.Target.Node == key) return true;
            foreach (Pending pending in queue) if (pending.Capability != null && pending.Target.Node == key) return true;
            return false;
        }
    }
    internal void Complete(Pending pending, UiToolRequestStatus status, string reason, bool attempted)
    { lock (gate) results[pending.Request] = new UiToolRequestResult(pending.Request, status, reason, attempted, revision); }

    /// <summary>Explicit per-target opt-in. Does not alter vanilla DevMode or other Hosts.</summary>
    public void SetInstruments(bool geometry = false, bool overlay = false, bool timing = false)
    {
        RequireThread();
        if (IsClosed || !host.TryGetTarget(out UiHost? owner) || !owner.Session.IsActive) throw new ObjectDisposedException(nameof(UiToolConnection));
        if (instruments == null)
        {
            instruments = owner.AcquireToolDiagnostics();
            priorGeometry = instruments.GeometryEnabled; priorOverlay = instruments.GeometryOverlay; priorTiming = instruments.TimingEnabled;
            instruments.ToolsAttached = true;
        }
        instruments.GeometryEnabled = geometry; instruments.GeometryOverlay = overlay; instruments.TimingEnabled = timing;
    }
    internal void ReleaseInstruments()
    {
        RequireThread();
        if (instruments == null) return;
        instruments.GeometryEnabled = priorGeometry; instruments.GeometryOverlay = priorOverlay; instruments.TimingEnabled = priorTiming;
        instruments.ToolsAttached = false;
        if (host.TryGetTarget(out UiHost? owner)) owner.ReleaseToolDiagnostics(instruments);
        instruments = null;
    }
    public void Dispose()
    {
        lock (gate)
        {
            if (closed) return;
            closed = true;
            while (queue.Count > 0) { Pending pending = queue.Dequeue(); results[pending.Request] = new UiToolRequestResult(pending.Request, UiToolRequestStatus.Closed, "connection-closed-before-attempt", false, revision); }
            targets.Clear(); snapshot = null; costSampleProvider = null;
        }
        if (Thread.CurrentThread.ManagedThreadId == thread)
        {
            ReleaseInstruments();
            if (host.TryGetTarget(out UiHost? owner)) owner.DetachTools(this);
        }
    }
}
