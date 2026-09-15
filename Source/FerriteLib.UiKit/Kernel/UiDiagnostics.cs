using System;
using System.Collections.Generic;
using System.Globalization;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// The channels one <see cref="UiDiagnosticSubscription"/> can receive. A kind is a stable vocabulary
/// rather than a free-form label: a consumer switches on it, and the string a human reads is
/// <see cref="UiDiagnosticEvent.Code"/>.
/// </summary>
public enum UiDiagnosticKind
{
    /// <summary>A document reload attempt: accepted, skipped or rejected, wrapping the service's own report.</summary>
    Reload = 0,

    /// <summary>A fit-audit finding: the text half (a label that does not fit) or the appearance half (a fallback).</summary>
    Fit = 1,

    /// <summary>A widget Measure/Draw threw and the session replaced it with its recovery band.</summary>
    Recovery = 2,

    /// <summary>A sampled aggregate of one timed phase, never a per-frame line.</summary>
    Timing = 3
}

/// <summary>
/// One sampling window's aggregate over one timed phase. Timing is the one channel that is a summary by
/// construction: a per-frame event would be a log, and the requirement is a number a consumer can read
/// without the diagnostic surface becoming the cost it was added to measure.
/// </summary>
public readonly struct UiDiagnosticTiming
{
    internal UiDiagnosticTiming(string name, int samples, double totalMilliseconds, double maxMilliseconds)
    {
        Name = name ?? "";
        Samples = samples;
        TotalMilliseconds = totalMilliseconds;
        MaxMilliseconds = maxMilliseconds;
    }

    /// <summary>The phase that was timed, e.g. <c>arrange</c> or <c>draw</c>.</summary>
    public string Name { get; }

    /// <summary>Frames folded into this aggregate. One event covers this many samples, not one.</summary>
    public int Samples { get; }

    /// <summary>Summed elapsed time across <see cref="Samples"/> frames.</summary>
    public double TotalMilliseconds { get; }

    /// <summary>Worst single sample in the window.</summary>
    public double MaxMilliseconds { get; }

    /// <summary>Mean of the window; zero when it holds no sample.</summary>
    public double AverageMilliseconds => Samples > 0 ? TotalMilliseconds / Samples : 0d;

    public override string ToString()
    {
        return Name + ": n=" + Samples.ToString(CultureInfo.InvariantCulture)
            + " avg=" + AverageMilliseconds.ToString("0.###", CultureInfo.InvariantCulture) + "ms"
            + " max=" + MaxMilliseconds.ToString("0.###", CultureInfo.InvariantCulture) + "ms";
    }
}

/// <summary>
/// One attributed diagnostic record. Every event carries the host identity, the session identity and a
/// stable kind, plus the node identity and display path where one exists, so two windows cannot be
/// confused for each other the way a single shared diagnostic slot confused them.
/// <para>
/// The payload is one struct with per-kind members rather than a class hierarchy: the channels are known
/// and few, a struct is what keeps a bounded ring cheap, and a consumer that switches on
/// <see cref="Kind"/> reads exactly the members that channel defines. The original record types travel
/// with the event (<see cref="Report"/>, <see cref="Overflow"/>, <see cref="Fallback"/>) rather than
/// being flattened and re-derived: a reload event is the document service's own report, attributed.
/// </para>
/// <para>
/// <b>No host, session or business reference is held.</b> Host and session travel as identity, so an
/// event buffered for a host that has closed cannot keep it, a pawn-like object or a save-scoped model
/// alive. That property is asserted by a lane, not assumed.
/// </para>
/// </summary>
public readonly struct UiDiagnosticEvent
{
    internal UiDiagnosticEvent(
        UiDiagnosticKind kind,
        string code,
        string key,
        long sequence,
        string host,
        int sessionId,
        UiNodeId node,
        string elementPath,
        string file,
        string element,
        string reason,
        UiReloadReport? report,
        UiOverflowReport? overflow,
        UiStyleFallbackReport? fallback,
        UiDiagnosticTiming? timing)
    {
        Kind = kind;
        Code = code ?? "";
        Key = key ?? "";
        Sequence = sequence;
        Host = host ?? "";
        SessionId = sessionId;
        Node = node;
        ElementPath = elementPath ?? "";
        File = file ?? "";
        Element = element ?? "";
        Reason = reason ?? "";
        Report = report;
        Overflow = overflow;
        Fallback = fallback;
        Timing = timing;
    }

    /// <summary>The channel this event belongs to.</summary>
    public UiDiagnosticKind Kind { get; }

    /// <summary>
    /// The stable sub-kind a consumer switches on: <c>reload.accepted</c>, <c>reload.rejected</c>,
    /// <c>reload.skipped</c>, <c>fit.overflow</c>, <c>fit.style-fallback</c>, <c>recovery.trip</c>,
    /// <c>timing</c>.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// The event's identity inside one subscription, used to report a repeating defect once. An empty
    /// key means the event is never deduplicated (timing is a summary, not a defect).
    /// </summary>
    public string Key { get; }

    /// <summary>Per-subscription arrival order, monotonic and starting at 1.</summary>
    public long Sequence { get; }

    /// <summary>The manifest source of the host this event is attributed to.</summary>
    public string Host { get; }

    /// <summary>The session that produced the event, unique for the life of the process.</summary>
    public int SessionId { get; }

    /// <summary>The node that produced the event, or <see cref="UiNodeId.None"/> where no element applies.</summary>
    public UiNodeId Node { get; }

    /// <summary>The element's display path, or the audit's <c>(unscoped)</c> when text had no element.</summary>
    public string ElementPath { get; }

    /// <summary>The file a reload event concerns; empty for every other kind.</summary>
    public string File { get; }

    /// <summary>The element a reload failure names; empty when the failure is not element-specific.</summary>
    public string Element { get; }

    /// <summary>Why a refusal happened, or what a recovery/commit did; empty when there is nothing to explain.</summary>
    public string Reason { get; }

    /// <summary>The document service's own report for a <see cref="UiDiagnosticKind.Reload"/> event, else null.</summary>
    public UiReloadReport? Report { get; }

    /// <summary>The fit audit's text-half record for a <c>fit.overflow</c> event, else null.</summary>
    public UiOverflowReport? Overflow { get; }

    /// <summary>The fit audit's appearance-half record for a <c>fit.style-fallback</c> event, else null.</summary>
    public UiStyleFallbackReport? Fallback { get; }

    /// <summary>The sampled aggregate for a <see cref="UiDiagnosticKind.Timing"/> event, else null.</summary>
    public UiDiagnosticTiming? Timing { get; }

    public override string ToString()
    {
        string node = Node.IsNone ? "" : " [" + Node.Path + "]";
        return Code + " @ " + Host + "#" + SessionId.ToString(CultureInfo.InvariantCulture) + node;
    }
}

/// <summary>
/// One subscriber's bounded diagnostic buffer. Created through <see cref="UiDiagnosticHub.Subscribe(UiHost,int)"/>
/// (or <see cref="UiHost.Diagnostics"/>), read by the consumer, and released by
/// <see cref="Dispose"/> or by disposing the host/session that owns it.
/// <para>
/// <b>Isolation is the whole point.</b> The ring, the dedup table, the budget and the dropped count are
/// per subscription, so a host that reports a defect sixty times a second exhausts its own allowance and
/// nobody else's, and two hosts' records never overwrite each other the way one shared slot did. The
/// buffer is a fixed-size ring with a dropped-count marker, and the dedup table is bounded by the ring
/// itself: a key leaves the table exactly when the event it keys leaves the buffer, so neither grows
/// without limit.
/// </para>
/// <para>
/// <b>No reference to the host.</b> The subscription carries the host's identity string and the session
/// id, never the objects, so a buffered event cannot keep a closed window alive.
/// </para>
/// </summary>
public sealed class UiDiagnosticSubscription : IDisposable
{
    private readonly UiDiagnosticEvent[] ring;
    private readonly HashSet<string> seen;
    private int start;
    private int count;
    private long sequence;
    private bool active = true;

    internal UiDiagnosticSubscription(string host, int sessionId, int budget)
    {
        Host = host ?? "";
        SessionId = sessionId;
        Budget = budget < 1
            ? 1
            : (budget > UiDiagnosticHub.MaxBudget ? UiDiagnosticHub.MaxBudget : budget);
        ring = new UiDiagnosticEvent[Budget];
        seen = new HashSet<string>(StringComparer.Ordinal);
    }

    /// <summary>The manifest source of the host this subscription belongs to.</summary>
    public string Host { get; }

    /// <summary>The session this subscription belongs to.</summary>
    public int SessionId { get; }

    /// <summary>How many events the ring retains. Fixed at creation and clamped to <see cref="UiDiagnosticHub.MaxBudget"/>.</summary>
    public int Budget { get; }

    /// <summary>False once <see cref="Dispose"/> ran, or once the owning session was disposed.</summary>
    public bool IsActive => active;

    /// <summary>Events currently retained.</summary>
    public int Count => count;

    /// <summary>Events pushed out of the ring by this subscription's own budget.</summary>
    public long Dropped { get; private set; }

    /// <summary>Events suppressed as a repeat of one already in this subscription's buffer.</summary>
    public long Suppressed { get; private set; }

    /// <summary>Events accepted into this subscription since it was created or last cleared.</summary>
    public long Published { get; private set; }

    /// <summary>
    /// Whether the owning host samples phase timings for this subscription. Off by default: timing is the
    /// one channel that costs a clock read per frame, so it is opt-in even for a subscribed host.
    /// </summary>
    public bool TimingEnabled { get; set; }

    /// <summary>Frames folded into one timing event. One aggregate per window, never a per-frame line.</summary>
    public int TimingSampleFrames { get; set; } = 60;

    /// <summary>A copy of the retained events, oldest first. A copy, so the caller cannot mutate the ring.</summary>
    public IReadOnlyList<UiDiagnosticEvent> Snapshot()
    {
        var events = new List<UiDiagnosticEvent>(count);
        for (int i = 0; i < count; i++)
        {
            events.Add(ring[(start + i) % Budget]);
        }

        return events;
    }

    /// <summary>The most recent retained event, whatever its kind.</summary>
    public bool TryGetLatest(out UiDiagnosticEvent value)
    {
        if (count == 0)
        {
            value = default;
            return false;
        }

        value = ring[(start + count - 1) % Budget];
        return true;
    }

    /// <summary>The most recent retained event of one kind.</summary>
    public bool TryGetLatest(UiDiagnosticKind kind, out UiDiagnosticEvent value)
    {
        for (int i = count - 1; i >= 0; i--)
        {
            UiDiagnosticEvent candidate = ring[(start + i) % Budget];
            if (candidate.Kind == kind)
            {
                value = candidate;
                return true;
            }
        }

        value = default;
        return false;
    }

    /// <summary>How many retained events belong to one kind.</summary>
    public int CountOf(UiDiagnosticKind kind)
    {
        int found = 0;
        for (int i = 0; i < count; i++)
        {
            if (ring[(start + i) % Budget].Kind == kind)
            {
                found++;
            }
        }

        return found;
    }

    /// <summary>Empties the buffer and its dedup table. The subscription stays active and usable.</summary>
    public void Clear()
    {
        Array.Clear(ring, 0, ring.Length);
        seen.Clear();
        start = 0;
        count = 0;
        Dropped = 0;
        Suppressed = 0;
        Published = 0;
    }

    /// <summary>
    /// Leaves the hub and releases this subscription's buffers. Called by the owning host's close path as
    /// well, so a torn-down window leaves nothing behind either way; calling it twice is safe.
    /// </summary>
    public void Dispose()
    {
        if (!active) return;
        UiDiagnosticHub.Release(this);
        Deactivate();
    }

    /// <summary>Drops the buffers and marks the subscription inactive without re-entering the hub.</summary>
    internal void Deactivate()
    {
        if (!active) return;
        active = false;
        Array.Clear(ring, 0, ring.Length);
        seen.Clear();
        start = 0;
        count = 0;
    }

    internal void PublishReload(UiReloadReport report)
    {
        if (!active || report == null) return;
        bool accepted = report.Accepted;
        string code = accepted ? "reload.accepted" : (report.Skipped ? "reload.skipped" : "reload.rejected");
        string key = "reload|" + report.DocumentId + "|" + report.Version + "|" + code;
        Append(new UiDiagnosticEvent(
            UiDiagnosticKind.Reload, code, key, sequence + 1, Host, SessionId, UiNodeId.None, "",
            report.Path, report.Element, report.Reason, report, null, null, null));
    }

    internal void PublishOverflow(UiOverflowReport report, string key)
    {
        if (!active) return;
        Append(new UiDiagnosticEvent(
            UiDiagnosticKind.Fit, "fit.overflow", key, sequence + 1, Host, SessionId, UiNodeId.None,
            report.ElementPath, "", "", "", null, report, null, null));
    }

    internal void PublishStyleFallback(UiStyleFallbackReport report, string key)
    {
        if (!active) return;
        Append(new UiDiagnosticEvent(
            UiDiagnosticKind.Fit, "fit.style-fallback", key, sequence + 1, Host, SessionId, UiNodeId.None,
            report.ElementPath, "", "", report.Diagnostic, null, null, report, null));
    }

    internal void PublishRecovery(UiNodeId node, string elementPath, string kind, string reason, string key)
    {
        if (!active) return;
        Append(new UiDiagnosticEvent(
            UiDiagnosticKind.Recovery, "recovery.trip", key, sequence + 1, Host, SessionId, node,
            elementPath, "", kind, reason, null, null, null, null));
    }

    internal void PublishTiming(string name, int samples, double totalMilliseconds, double maxMilliseconds)
    {
        if (!active) return;
        var timing = new UiDiagnosticTiming(name, samples, totalMilliseconds, maxMilliseconds);
        Append(new UiDiagnosticEvent(
            UiDiagnosticKind.Timing, "timing", "", sequence + 1, Host, SessionId, UiNodeId.None, "",
            "", "", "", null, null, null, timing));
    }

    private void Append(UiDiagnosticEvent value)
    {
        sequence++;
        if (value.Key.Length > 0 && !seen.Add(value.Key))
        {
            Suppressed++;
            return;
        }

        if (count == Budget)
        {
            UiDiagnosticEvent evicted = ring[start];
            if (evicted.Key.Length > 0)
            {
                seen.Remove(evicted.Key);
            }

            start = (start + 1) % Budget;
            count--;
            Dropped++;
        }

        ring[(start + count) % Budget] = value;
        count++;
        Published++;
    }
}

/// <summary>
/// Per-host / per-session diagnostic routing: the replacement for the one shared diagnostic slot that let
/// two coexisting hosts overwrite each other's records.
/// <para>
/// <b>How an event finds its subscriber.</b> A reload report is delivered by the document service to the
/// host it affected, which pushes it onto that host's own subscription. Text-fit findings are drawn
/// without a session argument, so they route through the ambient host scope a host opens around its own
/// arrange/draw (<see cref="EnterHost"/>); recovery trips and style fallbacks that do carry a session
/// route by that session's subscription. No path consults a process-wide sink when a subscription is
/// live, which is what keeps two hosts' records apart.
/// </para>
/// <para>
/// <b>Bounded, and released.</b> The registry is capped (<see cref="MaxSubscriptions"/>) and holds
/// subscriptions, not hosts or sessions; an entry leaves when the session is disposed, when the host
/// closes, or when the consumer disposes the subscription. The only ambient static is the subscription
/// of the draw scope currently open, and every scope restores its predecessor.
/// </para>
/// <para>
/// <b>Cheap when nobody subscribes.</b> A host that never touches <see cref="UiHost.Diagnostics"/> has no
/// subscription: the scope it opens is a struct push/pop, the reload hook returns on a null field, and
/// the fit audit keeps its own pre-existing, already-optional path. Single-threaded by contract: publish
/// happens on the frame thread.
/// </para>
/// </summary>
public static class UiDiagnosticHub
{
    /// <summary>Live subscriptions one process accepts; a bound refused rather than silently ignored.</summary>
    public const int MaxSubscriptions = 64;

    /// <summary>Retained events a subscription gets when the caller names no budget.</summary>
    public const int DefaultBudget = 32;

    /// <summary>Largest per-subscription budget accepted; larger requests are clamped, not trusted.</summary>
    public const int MaxBudget = 256;

    private static readonly Dictionary<int, UiDiagnosticSubscription> BySession =
        new Dictionary<int, UiDiagnosticSubscription>();

    // The subscription of the draw/measure scope currently open, restored by every scope. It is the one
    // ambient static here and it never outlives the call that opened it.
    private static UiDiagnosticSubscription? active;

    /// <summary>Live subscriptions. It drops back to zero when every host/session is disposed.</summary>
    public static int SubscriptionCount => BySession.Count;

    /// <summary>
    /// Opts one host in to diagnostics and returns its subscription; the same call through
    /// <see cref="UiHost.Diagnostics"/> reads the same object back. Throws when the process bound is
    /// reached or the host's session is already disposed, because a silently dropped subscription is the
    /// "reports nothing and looks fine" failure this surface exists to end.
    /// </summary>
    public static UiDiagnosticSubscription Subscribe(UiHost host, int budget = DefaultBudget)
    {
        if (host == null) throw new ArgumentNullException(nameof(host));

        UiDiagnosticSubscription? existing = host.CurrentDiagnostics;
        if (existing != null && existing.IsActive) return existing;

        UiSession session = host.Session;
        if (!session.IsActive)
        {
            throw new InvalidOperationException(
                "A disposed host cannot subscribe to diagnostics; create a new host/session.");
        }

        if (BySession.TryGetValue(session.Identity, out UiDiagnosticSubscription? registered))
        {
            host.AttachDiagnostics(registered);
            return registered;
        }

        if (BySession.Count >= MaxSubscriptions)
        {
            throw new InvalidOperationException(
                "At most " + MaxSubscriptions.ToString(CultureInfo.InvariantCulture)
                + " diagnostic subscriptions may be live at once; release one first.");
        }

        var subscription = new UiDiagnosticSubscription(host.Source, session.Identity, budget);
        BySession.Add(session.Identity, subscription);
        host.AttachDiagnostics(subscription);

        // Opting in is what arms the measuring half: the audit needs a ruler, and the host already holds
        // the one model its shell and its page measure with. The legacy Attach remains the path for a
        // consumer that wants the process-wide channel instead of a subscription.
        UiFitAudit.AttachMetrics(host.TextMetrics);
        return subscription;
    }

    /// <summary>The subscription of a session, or null when that session never opted in.</summary>
    public static UiDiagnosticSubscription? ForSession(UiSession session)
    {
        if (session == null) return null;
        return BySession.TryGetValue(session.Identity, out UiDiagnosticSubscription? subscription) ? subscription : null;
    }

    /// <summary>
    /// Releases a subscription's registry entry. Called by <see cref="UiDiagnosticSubscription.Dispose"/>;
    /// it never touches a sibling subscription, so closing one host cannot silence another.
    /// </summary>
    public static void Release(UiDiagnosticSubscription? subscription)
    {
        if (subscription == null) return;

        if (BySession.TryGetValue(subscription.SessionId, out UiDiagnosticSubscription? registered)
            && ReferenceEquals(registered, subscription))
        {
            BySession.Remove(subscription.SessionId);
        }

        if (ReferenceEquals(active, subscription))
        {
            active = null;
        }
    }

    /// <summary>The subscription of the arrange/draw scope currently open, or null.</summary>
    internal static UiDiagnosticSubscription? ActiveSubscription => active;

    /// <summary>
    /// Releases the subscription of a session being disposed. This is the lifetime half of the contract:
    /// a torn-down window takes its subscription and buffers with it, and only its own.
    /// </summary>
    internal static void ReleaseSession(UiSession session)
    {
        if (session == null) return;
        if (!BySession.TryGetValue(session.Identity, out UiDiagnosticSubscription? subscription))
        {
            return;
        }

        BySession.Remove(session.Identity);
        if (ReferenceEquals(active, subscription))
        {
            active = null;
        }

        subscription.Deactivate();
    }

    /// <summary>
    /// Opens the ambient host scope for one arrange or draw. The scope is a struct so the unsubscribed
    /// path allocates nothing; <paramref name="subscription"/> null still opens the scope, so a host with
    /// no subscription cannot inherit the routing of whichever host is drawing around it.
    /// </summary>
    internal static UiDiagnosticScope EnterHost(UiDiagnosticSubscription? subscription)
    {
        var scope = new UiDiagnosticScope(active);
        active = subscription != null && subscription.IsActive ? subscription : null;
        return scope;
    }

    /// <summary>Records one recovery trip against the session that owns it. No-ops for an unsubscribed session.</summary>
    internal static void PublishRecovery(UiSession session, UiNode node, string kind, string elementPath, string reason)
    {
        if (session == null || node == null) return;
        UiDiagnosticSubscription? subscription = ForSession(session);
        if (subscription == null || !subscription.IsActive) return;

        string key = "recovery|" + subscription.SessionId.ToString(CultureInfo.InvariantCulture)
            + "|" + node.Id.Key + "|" + (kind ?? "");
        subscription.PublishRecovery(node.Id, elementPath ?? "", kind ?? "", reason ?? "", key);
    }

    /// <summary>
    /// Restores the previous ambient subscription. A struct implementing <see cref="IDisposable"/>
    /// implicitly, so a <c>using</c> over it neither boxes nor allocates.
    /// </summary>
    internal readonly struct UiDiagnosticScope : IDisposable
    {
        private readonly UiDiagnosticSubscription? previous;

        internal UiDiagnosticScope(UiDiagnosticSubscription? previous)
        {
            this.previous = previous;
        }

        /// <summary>Puts the ambient subscription back to what it was before the scope opened.</summary>
        public void Dispose()
        {
            active = previous;
        }
    }
}
