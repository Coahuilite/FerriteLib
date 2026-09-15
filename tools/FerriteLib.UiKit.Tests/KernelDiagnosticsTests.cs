using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

using UnityEngine;

using FerriteLib.UiKit.Kernel;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// Diagnostics lane (0.5.x, package P5): the diagnostic surface stops being one shared slot and becomes
/// per-host / per-session subscriptions with attributed, bounded events.
/// <para>
/// What this lane asserts, in the order the requirement states it. Two hosts that are open together each
/// receive only their own attributed events (<see cref="UiDiagnosticKind.Fit"/> and
/// <see cref="UiDiagnosticKind.Recovery"/>), including host and session identity and the node path.
/// Disposing one host releases only its subscription and its bounded buffers, and the other host keeps
/// reporting. A subscriber that floods its own ring exhausts only its own budget. The unsubscribed path
/// allocates nothing measurable per frame. A live subscription owns a fit finding and the pre-existing
/// process-wide channel is bypassed rather than written alongside it, so the fit audit's old semantics are
/// preserved without being the routing authority any more. Reload events wrap the document service's own
/// <see cref="UiReloadReport"/> and reach exactly the hosts the batch affected. Timing is sampled into
/// aggregates and stays off until a subscriber asks. No diagnostic record holds a host, session or node
/// reference, the subscription registry is bounded and drains to zero, and the host ledger no longer grows
/// without a bound.
/// </para>
/// <para>
/// Everything here is harness evidence over the stub game surface. It says nothing about a real window:
/// that closing a window in a running game releases the subscription, and that a save change takes no old
/// object with it, is the in-game half (<c>docs/development/0.5/40-verification.md</c>, A9).
/// </para>
/// </summary>
internal static class KernelDiagnosticsTests
{
    private static int failures;

    public static int RunAll()
    {
        failures = 0;
        string sandbox = Path.Combine(Path.GetTempPath(), "ferritelib-diagnostics-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sandbox);

        // The two probe kinds live in the core scope so every host source in this lane resolves them: the
        // host's source is its identity here, so no lane may be forced to share one to find a widget.
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        UiWidgetRegistry.Register(UiWidgetRegistry.CoreScope, "diag/overflow", () => new OverflowWidget());
        UiWidgetRegistry.Register(UiWidgetRegistry.CoreScope, "diag/stable", () => new StableOverflowWidget());
        UiWidgetRegistry.Register(UiWidgetRegistry.CoreScope, "diag/ruler", () => new RulerProbeWidget());
        UiWidgetRegistry.Register(UiWidgetRegistry.CoreScope, "diag/throwing", () => new ThrowingWidget());

        try
        {
            Run("Two coexisting hosts receive only their own attributed fit findings", VerifyTwoHostsAreIsolated);
            Run("Two hosts with distinct rulers each measure with their own ruler", VerifyRulersArePerHost);
            Run("Subscribing a host does not replace the legacy channel's ruler", VerifySubscriptionDoesNotOwnTheLegacyRuler);
            Run("A recovery trip is attributed to its own session and to no other", VerifyRecoveryAttribution);
            Run("Disposing one host releases only its own subscription", VerifyDisposalReleasesOnlyItsOwn);
            Run("A noisy subscriber exhausts only its own budget", VerifyNoisySubscriberKeepsItsOwnBudget);
            Run("Dedup is per subscriber: a repeat is suppressed, another host's copy is not", VerifyDedupIsPerSubscriber);
            Run("The unsubscribed diagnostic path allocates nothing per frame", VerifyUnsubscribedPathAllocatesNothing);
            Run("A live subscription owns the fit finding and bypasses the shared slot", VerifySubscriptionReplacesTheSharedSlot);
            Run("Reload events wrap the service's report and reach only affected hosts", () => VerifyReloadEvents(sandbox));
            Run("Timing is sampled into aggregates and stays off until asked", VerifyTimingIsSampled);
            Run("No diagnostic record holds a host, session or node reference", VerifyNoHostReferenceIsHeld);
            Run("The subscription registry is bounded and drains to zero", VerifyRegistryBoundAndDrain);
            Run("The host ledger is bounded and counts what it refuses", VerifyHostLedgerIsBounded);
        }
        finally
        {
            UiFitAudit.Detach();
            UiWidgetRegistry.Clear();
            try
            {
                Directory.Delete(sandbox, true);
            }
            catch (IOException)
            {
            }
        }

        return failures;
    }

    // --- (a) two hosts, two records ---------------------------------------------------------------

    private static void VerifyTwoHostsAreIsolated()
    {
        UiFitAudit.Enabled = true;
        using var hostA = NewHost("diag-a", Page("diag-a", "a-boom", "diag/overflow"));
        using var hostB = NewHost("diag-b", Page("diag-b", "b-boom", "diag/overflow"));
        UiDiagnosticSubscription subA = hostA.Diagnostics;
        UiDiagnosticSubscription subB = hostB.Diagnostics;

        Draw(hostA);

        Check(subA.CountOf(UiDiagnosticKind.Fit) == 1,
            "host A recorded exactly its own finding (got " + subA.CountOf(UiDiagnosticKind.Fit) + ")");
        Check(subB.Count == 0, "host B recorded nothing while host A drew (got " + subB.Count + ")");

        Draw(hostB);

        Check(subA.CountOf(UiDiagnosticKind.Fit) == 1 && subB.CountOf(UiDiagnosticKind.Fit) == 1,
            "the two hosts hold one finding each, not two records in one slot");
        Check(!ReferenceEquals(subA, subB), "each host owns its own subscription object");

        subA.TryGetLatest(UiDiagnosticKind.Fit, out UiDiagnosticEvent eventA);
        subB.TryGetLatest(UiDiagnosticKind.Fit, out UiDiagnosticEvent eventB);

        Check(eventA.Host == "diag-a" && eventA.SessionId == hostA.Session.Identity,
            "A's event carries A's host and session identity (got '" + eventA.Host + "' #" + eventA.SessionId + ")");
        Check(eventB.Host == "diag-b" && eventB.SessionId == hostB.Session.Identity,
            "B's event carries B's host and session identity (got '" + eventB.Host + "' #" + eventB.SessionId + ")");
        Check(eventA.SessionId != eventB.SessionId, "the two sessions are distinct identities");
        Check(eventA.ElementPath == "root/a-boom" && eventB.ElementPath == "root/b-boom",
            "each event names the element path it came from (" + eventA.ElementPath + " / " + eventB.ElementPath + ")");
        Check(eventA.Code == "fit.overflow" && eventA.Overflow.HasValue && eventA.Overflow.Value.Axis == UiOverflowAxis.Width,
            "the event carries the audit's own overflow record and its axis");
        Check(eventA.Sequence == 1 && eventB.Sequence == 1, "each subscription numbers its own arrivals from one");
    }

    // --- the ruler travels with the host, not with the process --------------------------------------

    /// <summary>
    /// R4 (external review): the subscription used to write the subscribing host's <see cref="ITextMetrics"/>
    /// into the one static slot <c>UiFitAudit.Check</c> reads, so merely subscribing host B changed what host
    /// A's unchanged text was measured against - the review probe's "A before=0, A after B's subscription=1"
    /// on the real <c>DrawFrame</c> path. This lane is the harness half of that probe, and it deliberately
    /// uses two DISTINCT ruler instances: the common case (two hosts sharing one Verse metrics instance)
    /// hides the defect entirely, so a lane that reused one ruler would pass for the wrong reason.
    /// </summary>
    private static void VerifyRulersArePerHost()
    {
        UiFitAudit.Enabled = true;
        using var hostA = NewHost("diag-ruler-a", Page("diag-ruler-a", "text", "diag/ruler"), new SmallRuler());
        using var hostB = NewHost("diag-ruler-b", Page("diag-ruler-b", "text", "diag/ruler"), new LargeRuler());
        UiDiagnosticSubscription subA = hostA.Diagnostics;

        Draw(hostA);
        Check(subA.CountOf(UiDiagnosticKind.Fit) == 0,
            "A's own ruler says its text fits (got " + subA.CountOf(UiDiagnosticKind.Fit) + ")");

        UiDiagnosticSubscription subB = hostB.Diagnostics;

        Draw(hostA);
        Check(subA.CountOf(UiDiagnosticKind.Fit) == 0,
            "subscribing B with a different ruler did not change A's verdict (got "
            + subA.CountOf(UiDiagnosticKind.Fit) + ")");

        Draw(hostB);
        Check(subB.CountOf(UiDiagnosticKind.Fit) == 1,
            "B reports the overflow under its own ruler (got " + subB.CountOf(UiDiagnosticKind.Fit)
            + ", published " + subB.Published + ", suppressed " + subB.Suppressed + ")");

        for (int i = 0; i < 3; i++)
        {
            Draw(hostA);
            Draw(hostB);
        }

        Check(subA.CountOf(UiDiagnosticKind.Fit) == 0,
            "interleaved A/B draws leave A at zero (got " + subA.CountOf(UiDiagnosticKind.Fit) + ")");
        Check(subB.Suppressed == 3,
            "B kept measuring every one of its four draws under its own ruler (got " + subB.Suppressed + " repeats)");

        hostB.Dispose();
        Draw(hostA);
        Check(subA.CountOf(UiDiagnosticKind.Fit) == 0,
            "closing B left A's measurement unchanged (got " + subA.CountOf(UiDiagnosticKind.Fit) + ")");
    }

    /// <summary>
    /// The half the review probe hit directly: an unsubscribed host measures through the legacy channel's
    /// static ruler, so subscribing a DIFFERENT host must not replace that ruler. Before the fix,
    /// <c>Subscribe</c> wrote the new host's ruler into the static slot and the unsubscribed host's next draw
    /// reported an overflow its own ruler never saw.
    /// </summary>
    private static void VerifySubscriptionDoesNotOwnTheLegacyRuler()
    {
        var legacy = new List<UiOverflowReport>();
        UiFitAudit.Attach(new SmallRuler(), legacy.Add);
        UiFitAudit.Reset();
        UiFitAudit.Enabled = true;

        using var hostA = NewHost("diag-legacy-ruler", Page("diag-legacy-ruler", "text", "diag/ruler"), new SmallRuler());
        Draw(hostA);
        Check(legacy.Count == 0, "the legacy channel measures with the ruler it was handed (got " + legacy.Count + ")");

        using var hostB = NewHost("diag-sub-ruler", Page("diag-sub-ruler", "text", "diag/ruler"), new LargeRuler());
        UiDiagnosticSubscription subB = hostB.Diagnostics;
        Check(legacy.Count == 0,
            "subscribing a host with a different ruler changed nothing on the legacy channel (got " + legacy.Count + ")");

        Draw(hostA);
        Check(legacy.Count == 0,
            "the unsubscribed host still measures with the legacy ruler (got " + legacy.Count + ")");

        Draw(hostB);
        Check(subB.CountOf(UiDiagnosticKind.Fit) == 1,
            "the subscribed host measures with its own ruler (got " + subB.CountOf(UiDiagnosticKind.Fit)
            + ", published " + subB.Published + ")");
        Check(legacy.Count == 0, "and its finding is not written to the legacy channel");
    }

    // --- recovery attribution ---------------------------------------------------------------------

    private static void VerifyRecoveryAttribution()
    {
        UiFitAudit.Enabled = false;
        using var hostA = NewHost("diag-rec-a", Page("diag-rec-a", "rec-boom", "diag/throwing"));
        using var hostB = NewHost("diag-rec-b", Page("diag-rec-b", "rec-fine", "diag/overflow"));
        UiDiagnosticSubscription subA = hostA.Diagnostics;
        UiDiagnosticSubscription subB = hostB.Diagnostics;

        Draw(hostA);

        Check(subA.CountOf(UiDiagnosticKind.Recovery) == 1,
            "the tripped session recorded one recovery event (got " + subA.CountOf(UiDiagnosticKind.Recovery) + ")");
        Check(subB.CountOf(UiDiagnosticKind.Recovery) == 0, "the healthy host recorded no recovery");
        Check(KernelTripGuard.AnyTripped(hostA.Session) || hostA.Session.TrippedNodes.Count > 0,
            "the session itself still owns the trip record (the pre-existing recovery surface)");

        subA.TryGetLatest(UiDiagnosticKind.Recovery, out UiDiagnosticEvent trip);
        Check(trip.Host == "diag-rec-a" && trip.SessionId == hostA.Session.Identity,
            "the recovery event is attributed to the tripped host/session");
        Check(trip.Node.Path == "root/rec-boom",
            "the event carries the node identity, not only a display string (got '" + trip.Node.Path + "')");
        Check(trip.Element == "diag/throwing", "and the kind that threw (got '" + trip.Element + "')");
        Check(trip.Reason.IndexOf("planted widget failure", StringComparison.Ordinal) >= 0,
            "and the diagnostic text the guard would have logged");

        Draw(hostA);
        Check(subA.CountOf(UiDiagnosticKind.Recovery) == 1,
            "a second trip of the same node is not a second event (dedup is per subscriber)");
    }

    // --- (b) release is per subscription ----------------------------------------------------------

    private static void VerifyDisposalReleasesOnlyItsOwn()
    {
        UiFitAudit.Enabled = true;
        var hostA = NewHost("diag-close-a", Page("diag-close-a", "ca-boom", "diag/overflow"));
        var hostB = NewHost("diag-close-b", Page("diag-close-b", "cb-boom", "diag/overflow"));
        UiDiagnosticSubscription subA = hostA.Diagnostics;
        UiDiagnosticSubscription subB = hostB.Diagnostics;

        Draw(hostA);
        Draw(hostB);
        int countB = subB.Count;
        Check(countB == 1 && subA.Count == 1, "both hosts reported before the close");

        hostA.Dispose();

        Check(!subA.IsActive, "the closed host's subscription is released");
        Check(subA.Count == 0, "and its bounded buffer is released with it (got " + subA.Count + ")");
        Check(UiDiagnosticHub.ForSession(hostA.Session) == null, "the hub no longer resolves the disposed session");
        Check(UiDiagnosticHub.SubscriptionCount == 1, "exactly one subscription remains (got " + UiDiagnosticHub.SubscriptionCount + ")");
        Check(subB.IsActive, "the surviving host's subscription was not touched");

        Draw(hostB);
        Check(subB.Count == countB + 1, "the surviving host keeps reporting after its sibling closed (got " + subB.Count + ")");

        bool refused = false;
        try
        {
            UiDiagnosticHub.Subscribe(hostA);
        }
        catch (InvalidOperationException)
        {
            refused = true;
        }

        Check(refused, "a disposed host cannot silently re-subscribe");
        Check(UiDiagnosticHub.SubscriptionCount == 1 && !subA.IsActive,
            "the refusal changed neither the dead subscription nor the live one");

        hostB.Dispose();
    }

    // --- (c) per-subscriber budget ---------------------------------------------------------------

    private static void VerifyNoisySubscriberKeepsItsOwnBudget()
    {
        UiFitAudit.Enabled = true;
        using var noisy = NewHost("diag-noisy", Page("diag-noisy", "n-boom", "diag/overflow"));
        using var quiet = NewHost("diag-quiet", Page("diag-quiet", "q-boom", "diag/overflow"));
        UiDiagnosticSubscription noisySub = UiDiagnosticHub.Subscribe(noisy, 4);
        UiDiagnosticSubscription quietSub = UiDiagnosticHub.Subscribe(quiet, 4);

        Check(noisySub.Budget == 4 && quietSub.Budget == 4, "each subscriber's budget is its own");

        for (int i = 0; i < 12; i++)
        {
            Draw(noisy);
        }

        Check(noisySub.Count == 4, "the noisy ring holds its budget and no more (got " + noisySub.Count + ")");
        Check(noisySub.Dropped == 8, "and its dropped-count marker says what its own budget pushed out (got " + noisySub.Dropped + ")");

        Draw(quiet);

        Check(quietSub.Count == 1, "the quiet subscriber still recorded its finding (got " + quietSub.Count + ")");
        Check(quietSub.Dropped == 0, "and was not charged for the noisy subscriber's overflow");
        Check(quietSub.Published == 1, "one accepted event, not one per suppressed or dropped sibling event");
        Check(noisySub.Count == 4, "drawing the quiet host did not disturb the noisy ring");
    }

    // --- dedup is per subscriber -------------------------------------------------------------------

    /// <summary>
    /// The budget lane above uses a widget whose text changes every draw, so it can only prove the ring is
    /// bounded. This lane uses one whose finding is identical every frame, which is the shape dedup exists
    /// for: five draws are one record plus four suppressed repeats - and the second host drawing the same
    /// element into the same rect is its own record rather than a duplicate of the first host's, which is
    /// exactly what a process-wide dedup set (the pre-P5 shape) would get wrong.
    /// </summary>
    private static void VerifyDedupIsPerSubscriber()
    {
        UiFitAudit.Enabled = true;
        using var hostA = NewHost("diag-dedup-a", Page("diag-dedup-a", "d-keep", "diag/stable"));
        using var hostB = NewHost("diag-dedup-b", Page("diag-dedup-b", "d-keep", "diag/stable"));
        UiDiagnosticSubscription subA = hostA.Diagnostics;
        UiDiagnosticSubscription subB = hostB.Diagnostics;

        for (int i = 0; i < 5; i++)
        {
            Draw(hostA);
        }

        Check(subA.CountOf(UiDiagnosticKind.Fit) == 1,
            "five identical findings produce one record (got " + subA.CountOf(UiDiagnosticKind.Fit) + ")");
        Check(subA.Suppressed == 4, "and the four repeats are counted as suppressed (got " + subA.Suppressed + ")");
        Check(subA.Count == 1, "the ring holds the record once, not once per frame");

        Draw(hostB);

        Check(subB.CountOf(UiDiagnosticKind.Fit) == 1,
            "the other host's identical finding is its own record, not a duplicate of A's (got "
            + subB.CountOf(UiDiagnosticKind.Fit) + ")");
        Check(subB.Suppressed == 0, "and it was not suppressed by a sibling's earlier copy");
        Check(subA.CountOf(UiDiagnosticKind.Fit) == 1, "B's arrival did not disturb A's record");
    }

    // --- (d) the unsubscribed path is allocation-free ----------------------------------------------

    /// <summary>
    /// The claim is "no allocation-heavy per-frame work when nobody subscribes", so the lane measures it
    /// instead of asserting it. A control loop that deliberately allocates proves the meter moves at all
    /// (a lane that cannot go red is not wired); the subject loop then drives exactly what an unsubscribed
    /// host drives per pass - the ambient scope's enter/exit and the audit's disabled short-circuit - and
    /// must allocate a small fraction of the control.
    /// </summary>
    private static void VerifyUnsubscribedPathAllocatesNothing()
    {
        UiFitAudit.Enabled = false;
        AppDomain.MonitoringIsEnabled = true;
        AppDomain domain = AppDomain.CurrentDomain;
        var probe = new Rect(0f, 0f, 8f, 8f);

        for (int i = 0; i < 500; i++)
        {
            UiDiagnosticHub.EnterHost(null, null).Dispose();
            UiFitAudit.Check(probe, "warmup", UiFont.Tiny, true);
        }

        long before = domain.MonitoringTotalAllocatedMemorySize;
        for (int i = 0; i < 20000; i++)
        {
            GC.KeepAlive(new object());
        }

        long control = domain.MonitoringTotalAllocatedMemorySize - before;

        before = domain.MonitoringTotalAllocatedMemorySize;
        for (int i = 0; i < 20000; i++)
        {
            UiDiagnosticHub.EnterHost(null, null).Dispose();
            UiFitAudit.Check(probe, "unsubscribed", UiFont.Tiny, true);
        }

        long subject = domain.MonitoringTotalAllocatedMemorySize - before;

        Check(control > 100000,
            "control: the allocation meter moves for a deliberately allocating loop (got " + control + " bytes)");
        Check(subject * 8 < control,
            "the unsubscribed diagnostic path allocates almost nothing (subject " + subject
            + " bytes vs control " + control + " bytes over 20000 passes)");
    }

    // --- the shared slot is replaced, not written alongside ---------------------------------------

    private static void VerifySubscriptionReplacesTheSharedSlot()
    {
        var legacy = new List<UiOverflowReport>();
        UiFitAudit.Attach(new FixedMetrics(), legacy.Add);
        UiFitAudit.Reset();
        UiFitAudit.Enabled = true;

        using (var plain = NewHost("diag-legacy", Page("diag-legacy", "legacy-boom", "diag/overflow")))
        {
            Draw(plain);
        }

        Check(legacy.Count == 1,
            "the pre-existing shared channel still reports for a host that never subscribed (got " + legacy.Count + ")");

        legacy.Clear();
        using var subscribed = NewHost("diag-sub", Page("diag-sub", "sub-boom", "diag/overflow"));
        UiDiagnosticSubscription sub = subscribed.Diagnostics;
        Draw(subscribed);

        Check(sub.CountOf(UiDiagnosticKind.Fit) == 1, "a live subscription receives the finding");
        Check(legacy.Count == 0,
            "and the process-wide slot is bypassed rather than written alongside it (got " + legacy.Count + ")");
    }

    // --- reload events wrap the service's report --------------------------------------------------

    private static void VerifyReloadEvents(string sandbox)
    {
        string dir = Path.Combine(sandbox, "reload");
        Directory.CreateDirectory(dir);
        string pathA = Path.Combine(dir, "a.xml");
        string pathB = Path.Combine(dir, "b.xml");
        string pageA = Page("diag-rel-a", "a-keep", "diag/overflow");
        string pageB = Page("diag-rel-b", "b-keep", "diag/overflow");
        File.WriteAllText(pathA, pageA);
        File.WriteAllText(pathB, pageB);

        using var service = new UiDocumentService(autoWatch: false);
        Check(service.Add(new UiDocumentSource("a", UiDocumentKind.Layout, pathA), pageA), "document a registers");
        Check(service.Add(new UiDocumentSource("b", UiDocumentKind.Layout, pathB), pageB), "document b registers");

        using var hostA = NewHost("diag-rel-a", UiLayoutManifest.Parse(pageA));
        using var hostB = NewHost("diag-rel-b", UiLayoutManifest.Parse(pageB));
        UiDiagnosticSubscription subA = hostA.Diagnostics;
        UiDiagnosticSubscription subB = hostB.Diagnostics;

        Check(service.Attach(hostA, "a"), "host A attaches to document a");
        Check(service.Attach(hostB, "b"), "host B attaches to document b");

        File.WriteAllText(pathA, Page("diag-rel-a", "a-added", "diag/overflow"));
        service.Signal("a");
        service.Pump();

        Check(subA.CountOf(UiDiagnosticKind.Reload) == 1,
            "the affected host received the accepted report (got " + subA.CountOf(UiDiagnosticKind.Reload) + ")");
        subA.TryGetLatest(UiDiagnosticKind.Reload, out UiDiagnosticEvent accepted);
        Check(accepted.Code == "reload.accepted" && accepted.Report != null && accepted.Report.Accepted,
            "the event wraps the service's own accepted report");
        Check(accepted.File == pathA, "and carries the file the report names (got '" + accepted.File + "')");
        Check(accepted.Host == "diag-rel-a" && accepted.SessionId == hostA.Session.Identity,
            "attributed to the host and session the batch touched");
        Check(subB.CountOf(UiDiagnosticKind.Reload) == 0,
            "the host that reads another document was not told about it");

        File.WriteAllText(pathA, "<UiPage Schema=\"2\" Source=\"diag-rel-a\"><Column Id=\"root\"></UiPage>");
        service.Signal("a");
        service.Pump();

        subA.TryGetLatest(UiDiagnosticKind.Reload, out UiDiagnosticEvent parseRefusal);
        Check(parseRefusal.Code == "reload.rejected" && parseRefusal.Report != null && parseRefusal.Report.Rejected,
            "a malformed candidate is reported as a rejection");
        Check(parseRefusal.File == pathA && parseRefusal.Reason.Length > 0,
            "with the file and the parser's reason (file='" + parseRefusal.File + "')");

        File.WriteAllText(pathA,
            "<UiPage Schema=\"2\" Source=\"diag-rel-a\"><Column Id=\"root\" Bogus=\"1\"></Column></UiPage>");
        service.Signal("a");
        service.Pump();

        subA.TryGetLatest(UiDiagnosticKind.Reload, out UiDiagnosticEvent elementRefusal);
        Check(elementRefusal.Code == "reload.rejected" && elementRefusal.Element.Length > 0,
            "a creation-contract refusal names the element it refused (got '" + elementRefusal.Element + "')");
        Check(subA.CountOf(UiDiagnosticKind.Reload) == 3,
            "the host holds one record per attempt, accepted and refused alike");
        Check(subB.CountOf(UiDiagnosticKind.Reload) == 0,
            "the unrelated host still saw no reload event of any kind");
    }

    // --- timing is sampled ------------------------------------------------------------------------

    private static void VerifyTimingIsSampled()
    {
        UiFitAudit.Enabled = false;
        using var host = NewHost("diag-timing", Page("diag-timing", "t-keep", "diag/overflow"));
        UiDiagnosticSubscription sub = host.Diagnostics;

        for (int i = 0; i < 10; i++)
        {
            Draw(host);
        }

        Check(sub.CountOf(UiDiagnosticKind.Timing) == 0, "timing stays off until the subscriber asks for it");

        sub.TimingEnabled = true;
        sub.TimingSampleFrames = 3;
        for (int i = 0; i < 6; i++)
        {
            Draw(host);
        }

        int arrange = 0;
        int draw = 0;
        bool windowHeld = true;
        foreach (UiDiagnosticEvent entry in sub.Snapshot())
        {
            if (entry.Kind != UiDiagnosticKind.Timing || entry.Timing == null)
            {
                continue;
            }

            UiDiagnosticTiming timing = entry.Timing.Value;
            if (timing.Samples != 3)
            {
                windowHeld = false;
            }

            if (timing.Name == "arrange")
            {
                arrange++;
            }
            else if (timing.Name == "draw")
            {
                draw++;
            }
        }

        Check(sub.CountOf(UiDiagnosticKind.Timing) == 4,
            "six frames fold into two windows per phase, four events in all (got "
            + sub.CountOf(UiDiagnosticKind.Timing) + ")");
        Check(arrange == 2 && draw == 2, "both phases were sampled (" + arrange + " arrange, " + draw + " draw)");
        Check(windowHeld, "one event covers exactly one sampling window, never one frame");
        Check(sub.CountOf(UiDiagnosticKind.Fit) == 0, "no fit finding was produced while timing was the thing under test");
    }

    // --- no host / session / node reference is retained -------------------------------------------

    private static void VerifyNoHostReferenceIsHeld()
    {
        Check(!MentionsHostObject(typeof(UiDiagnosticSubscription)),
            "a subscription holds no host, session or node reference");
        Check(!MentionsHostObject(typeof(UiDiagnosticEvent)),
            "an event holds no host, session or node reference");
        Check(!MentionsHostObject(typeof(UiDiagnosticTiming)),
            "a timing aggregate holds no host, session or node reference");
        Check(!MentionsHostObject(typeof(UiDiagnosticHub)),
            "the hub's own fields hold no host, session or node reference");
    }

    /// <summary>
    /// True when the type or any of its fields names a host, session or node type, generic arguments
    /// included. This is the lane half of "no event handler may hold a reference to a disposed host": the
    /// records carry identity, and a future field that carries the object turns this red.
    /// </summary>
    private static bool MentionsHostObject(Type type)
    {
        const BindingFlags All = BindingFlags.Instance | BindingFlags.Static
            | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        foreach (FieldInfo field in type.GetFields(All))
        {
            if (Mentions(type, field.FieldType, 0))
            {
                Console.Error.WriteLine("  info: " + type.Name + "." + field.Name + " names " + field.FieldType.Name);
                return true;
            }
        }

        return false;
    }

    private static bool Mentions(Type owner, Type candidate, int depth)
    {
        if (depth > 4)
        {
            return false;
        }

        if (typeof(UiHost).IsAssignableFrom(candidate)
            || typeof(UiSession).IsAssignableFrom(candidate)
            || typeof(UiNode).IsAssignableFrom(candidate))
        {
            return true;
        }

        if (candidate.IsGenericType)
        {
            foreach (Type argument in candidate.GetGenericArguments())
            {
                if (Mentions(owner, argument, depth + 1))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // --- the registry is bounded and releases -----------------------------------------------------

    private static void VerifyRegistryBoundAndDrain()
    {
        Check(UiDiagnosticHub.SubscriptionCount == 0,
            "every earlier lane released its subscriptions (got " + UiDiagnosticHub.SubscriptionCount + ")");
        Check(UiDiagnosticHub.MaxSubscriptions > 0 && UiDiagnosticHub.MaxBudget >= UiDiagnosticHub.DefaultBudget,
            "the registry and the per-subscriber budget are bounded constants");

        var hosts = new List<UiHost>();
        try
        {
            for (int i = 0; i < UiDiagnosticHub.MaxSubscriptions; i++)
            {
                string source = "diag-bound-" + i.ToString();
                UiHost host = NewHost(source, Page(source, "b" + i.ToString(), "diag/overflow"));
                hosts.Add(host);
                UiDiagnosticHub.Subscribe(host);
            }

            Check(UiDiagnosticHub.SubscriptionCount == UiDiagnosticHub.MaxSubscriptions,
                "the registry holds exactly its bound (got " + UiDiagnosticHub.SubscriptionCount + ")");

            using var extra = NewHost("diag-bound-extra", Page("diag-bound-extra", "bx", "diag/overflow"));
            bool refused = false;
            try
            {
                UiDiagnosticHub.Subscribe(extra);
            }
            catch (InvalidOperationException)
            {
                refused = true;
            }

            Check(refused, "a further subscription is refused rather than letting the registry grow");
            Check(UiDiagnosticHub.SubscriptionCount == UiDiagnosticHub.MaxSubscriptions,
                "the refusal left the registry exactly at its bound");
        }
        finally
        {
            foreach (UiHost host in hosts)
            {
                host.Dispose();
            }
        }

        Check(UiDiagnosticHub.SubscriptionCount == 0,
            "closing the hosts drains the registry back to zero (got " + UiDiagnosticHub.SubscriptionCount + ")");
    }

    // --- the host ledger is bounded ---------------------------------------------------------------

    /// <summary>
    /// The ledger is the other process-wide record this package owns. It is append-only by design, so it
    /// has to be bounded like the diagnostic buffers; the lane drives it past its bound directly (the
    /// record is a string, and building five hundred page trees to prove a list cap would measure the
    /// engine instead) and checks both the cap and the dropped-count marker that keeps a truncated record
    /// from looking complete.
    /// </summary>
    private static void VerifyHostLedgerIsBounded()
    {
        int start = UiHostLedger.SourceCount;
        Check(start <= UiHostLedger.MaxSources,
            "the ledger never grew past its bound before this lane (" + start + " of " + UiHostLedger.MaxSources + ")");

        int needed = UiHostLedger.MaxSources - start + 3;
        for (int i = 0; i < needed; i++)
        {
            UiHostLedger.Record("diag-ledger-source-" + i.ToString());
        }

        Check(UiHostLedger.SourceCount == UiHostLedger.MaxSources,
            "the ledger stops at its bound (got " + UiHostLedger.SourceCount + " of " + UiHostLedger.MaxSources + ")");
        Check(UiHostLedger.DroppedCount >= 3, "and counts every source it refused (got " + UiHostLedger.DroppedCount + ")");
        Check(UiHostLedger.Describe().IndexOf("refused by the", StringComparison.Ordinal) >= 0,
            "the description says the record is truncated instead of looking complete");
    }

    // --- fixture ----------------------------------------------------------------------------------

    private sealed class OverflowWidget : IUiWidget
    {
        private static int draws;

        public string Kind => "diag/overflow";

        public void Configure(UiElementSpec spec)
        {
        }

        public void Validate(IUiBindings bindings, string elementPath)
        {
        }

        public float Measure(UiWidgetContext ctx) => 20f;

        public void Draw(Rect rect, UiWidgetContext ctx)
        {
            // A distinct string per draw is what gives the budget lane distinct findings to overflow with.
            draws++;
            UiThemeDraw.Label(
                new Rect(rect.x, rect.y, 4f, 4f),
                "diag overflow text " + draws.ToString(),
                ctx.Theme,
                null,
                UiFont.Medium,
                TextAnchor.MiddleLeft,
                singleLine: true);
        }
    }

    /// <summary>
    /// Draws one label into a FIXED 100x16 band, so the only thing that decides whether it fits is the
    /// ruler: the small ruler measures "hello" at 40 units (fits) and the large one at 1000 (overflows).
    /// A kind that sized its own band from its own ruler (text/wrapped) would have nothing left to report,
    /// which is why the ruler lanes use this probe instead.
    /// </summary>
    private sealed class RulerProbeWidget : IUiWidget
    {
        public string Kind => "diag/ruler";

        public void Configure(UiElementSpec spec)
        {
        }

        public void Validate(IUiBindings bindings, string elementPath)
        {
        }

        public float Measure(UiWidgetContext ctx) => 16f;

        public void Draw(Rect rect, UiWidgetContext ctx)
        {
            UiThemeDraw.Label(
                new Rect(rect.x, rect.y, 100f, 16f),
                "hello",
                ctx.Theme,
                null,
                UiFont.Medium,
                TextAnchor.MiddleLeft,
                singleLine: true);
        }
    }

    /// <summary>The probe's normal ruler: a short label measures 40 units wide and 20 tall.</summary>
    private sealed class SmallRuler : ITextMetrics
    {
        public float MeasureText(string text, UiFont font, float width) => 20f;

        public float MeasureWidth(string text, UiFont font) => text.Length * 8f;
    }

    /// <summary>A deliberately different ruler: everything measures far past any arranged band.</summary>
    private sealed class LargeRuler : ITextMetrics
    {
        public float MeasureText(string text, UiFont font, float width) => 1000f;

        public float MeasureWidth(string text, UiFont font) => 1000f;
    }

    /// <summary>Draws the same overflowing label every pass, so a repeat is a genuine duplicate.</summary>
    private sealed class StableOverflowWidget : IUiWidget
    {
        public string Kind => "diag/stable";

        public void Configure(UiElementSpec spec)
        {
        }

        public void Validate(IUiBindings bindings, string elementPath)
        {
        }

        public float Measure(UiWidgetContext ctx) => 20f;

        public void Draw(Rect rect, UiWidgetContext ctx)
        {
            UiThemeDraw.Label(
                new Rect(rect.x, rect.y, 4f, 4f),
                "stable overflowing text",
                ctx.Theme,
                null,
                UiFont.Medium,
                TextAnchor.MiddleLeft,
                singleLine: true);
        }
    }

    private sealed class ThrowingWidget : IUiWidget
    {
        public string Kind => "diag/throwing";

        public void Configure(UiElementSpec spec)
        {
        }

        public void Validate(IUiBindings bindings, string elementPath)
        {
        }

        public float Measure(UiWidgetContext ctx) => throw new InvalidOperationException("planted widget failure");

        public void Draw(Rect rect, UiWidgetContext ctx) => throw new InvalidOperationException("planted widget failure");
    }

    private sealed class FixedMetrics : ITextMetrics
    {
        public float MeasureText(string text, UiFont font, float width) => 16f;

        public float MeasureWidth(string text, UiFont font) => StubTextWidth.Of(text, font);
    }

    private sealed class FixedTranslation : IUiTranslation
    {
        public string Translate(string key) => key;

        public int TranslationRevision => 0;
    }

    private static UiHost NewHost(string source, string xml)
    {
        return NewHost(source, UiLayoutManifest.Parse(xml));
    }

    private static UiHost NewHost(string source, UiLayoutManifest manifest)
    {
        return new UiHost(
            source, manifest, new UiBindings(), UiTheme.DarkGold.Clone(), new FixedMetrics(), new FixedTranslation());
    }

    private static void Draw(UiHost host)
    {
        host.DrawFrame(new Rect(0f, 0f, 400f, 300f));
    }

    private static UiHost NewHost(string source, string xml, ITextMetrics metrics)
    {
        return new UiHost(
            source, UiLayoutManifest.Parse(xml), new UiBindings(), UiTheme.DarkGold.Clone(), metrics, new FixedTranslation());
    }

    private static string Page(string source, string widgetId, string kind)
    {
        return "<UiPage Schema=\"2\" Source=\"" + source + "\">"
            + "<Column Id=\"root\">"
            + "<Widget Id=\"" + widgetId + "\" Kind=\"" + kind + "\" />"
            + "</Column></UiPage>";
    }

    private static void Run(string name, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            failures++;
            Console.Error.WriteLine("  FAIL: " + name + " threw " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static void Check(bool condition, string name)
    {
        if (condition)
        {
            Console.WriteLine("  ok: " + name);
            return;
        }

        failures++;
        Console.Error.WriteLine("  FAIL: " + name);
    }
}
