using System;
using System.Collections.Generic;
using System.IO;
using FerriteLib.UiKit.Kernel;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// Reload-scheduling lane (0.6.x, package T2). The document service gains a scheduler: a watcher signal is
/// debounced by a quiet period, a transient read failure is retried a bounded number of times, a ready commit
/// is deferred - but never indefinitely - for the user's own interaction, and every timing decision is made
/// against <see cref="IUiTimeSource"/> rather than against a frame, host or pump count.
/// <para>
/// What this lane asserts, in the order the requirement states it: one read and one commit for a save burst,
/// with a later signal restarting the window (trailing edge); a bounded retry whose exhausted attempt is the
/// only one reported, with the previous valid version in force throughout and no pump-driven spin; a commit
/// while a game-pause-shaped fixture stays frozen; nothing read or parsed at all while no host is attached,
/// with the next attach resolving the newest content; a deferral for a held interaction that is visible in
/// <see cref="UiReloadSchedulerState"/> and never outlives
/// <see cref="UiReloadPolicy.MaxDeferSeconds"/>; the <c>Signal</c> (debounced) versus
/// <c>Reload</c> (immediate) channel split; and per-document atomicity - a layout and a style save in quick
/// succession, and a rolled-back batch that still loses no draft.
/// </para>
/// <para>
/// Every lane moves time by moving <see cref="ManualClock"/> and nothing else. The clock is the only thing
/// that advances: no frame is drawn to make time pass, no pump count is consulted, and the lane that
/// deliberately omits a clock pins that with the harness's constant-zero realtime stub.
/// </para>
/// <para>
/// What this is not: harness evidence over the stub game surface. It says nothing about an editor's real save
/// events, about the real IMGUI hot-control lifetime, or about a paused game in the running game - that half
/// stays <c>尚待外部团队验证</c> (see <c>docs/development/0.6/verification/t2-reload-scheduling.md</c>).
/// </para>
/// </summary>
internal static class KernelReloadSchedulingTests
{
    private static int failures;

    public static int RunAll()
    {
        failures = 0;
        string sandbox = Path.Combine(Path.GetTempPath(), "ferritelib-reload-scheduling-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sandbox);

        try
        {
            Run("A burst inside QuietSeconds is one read and one commit; a later signal restarts the window",
                () => VerifyQuietPeriodAndTrailingEdge(sandbox));
            Run("A transient read failure is retried at most MaxRetryAttempts and then reported",
                () => VerifyBoundedRetry(sandbox));
            Run("MaxRetryAttempts = 0 reports the first transient failure at once",
                () => VerifyZeroRetriesReportsImmediately(sandbox));
            Run("A commit happens while the simulation clock stays frozen",
                () => VerifyPauseIndependence(sandbox));
            Run("With zero attached hosts nothing is read; the next attach resolves the newest content",
                () => VerifyNothingOpenIsNotRebuilt(sandbox));
            Run("A ready commit is deferred for interaction, never past MaxDeferSeconds, and says so",
                () => VerifyBoundedDeferral(sandbox));
            Run("SchedulerState separates the quiet window from the deferral ceiling and the retry wait",
                () => VerifyStateSeparatesQuietFromDeferral(sandbox));
            Run("Signal is the debounced watcher channel; Reload is the immediate manual channel",
                () => VerifySignalIsDebouncedAndReloadIsImmediate(sandbox));
            Run("With a constant-zero clock, pumping alone never commits",
                () => VerifyZeroClockNeverCommitsByPumping(sandbox));
            Run("A layout save and a style save in quick succession both commit",
                () => VerifyLayoutAndStyleInQuickSuccession(sandbox));
            Run("A scheduler-driven rolled-back batch keeps the old tree and R1's draft",
                () => VerifySchedulerRollbackKeepsDraft(sandbox));
            Run("R06-2 reopen: a signal no pump observed is consumed by Attach, not dropped",
                () => VerifyReopenAfterSignalNoPump(sandbox));
            Run("R06-2 reopen: a content change with no signal at all resolves by content version",
                () => VerifyReopenAfterUnsignalledContentChange(sandbox));
            Run("R06-2 reopen: a file whose watcher never armed still resolves at attach",
                () => VerifyReopenWhenWatcherCouldNotArm(sandbox));
            Run("R06-2 reopen: a missing file keeps the last valid version and writes no report",
                () => VerifyReopenWithMissingFileKeepsLastValid(sandbox));
            Run("R06-2 reopen: a broken newer file keeps the last valid version and is reported once",
                () => VerifyReopenWithBrokenFileReportsOnce(sandbox));
            Run("R06-2 reopen: an unchanged file is not turned into a report",
                () => VerifyReopenWithUnchangedFileWritesNoReport(sandbox));
            Run("R06 risk 1: Reload from inside the host's own draw pass",
                () => VerifyReloadInsideDrawPass(sandbox));
            Run("R06 risk 1: ReloadAll from inside the host's own draw pass",
                () => VerifyReloadAllInsideDrawPass(sandbox));
        }
        finally
        {
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

    // --- (2) quiet period: one read and one commit for a burst -------------------------------

    private static void VerifyQuietPeriodAndTrailingEdge(string sandbox)
    {
        string dir = NewDir(sandbox, "quiet");
        string path = Path.Combine(dir, "page.xml");
        string v1 = KeepPage("sched-quiet");
        File.WriteAllText(path, v1);

        var clock = new ManualClock();
        var policy = new UiReloadPolicy(quietSeconds: 0.25, retrySeconds: 0.5, maxRetryAttempts: 3, maxDeferSeconds: 2.0);
        using var service = new UiDocumentService(false, policy, clock);
        service.Add(new UiDocumentSource("q", UiDocumentKind.Layout, path), v1);
        using var host = NewHost("sched-quiet", UiLayoutManifest.Parse(v1), new UiBindings());
        service.Attach(host, "q");
        Draw(host);

        string v2 = Page("sched-quiet",
            "<Column Id=\"root\">"
            + "<Widget Id=\"keep\" Kind=\"chrome/banner\" Text=\"keep\"/>"
            + "<Widget Id=\"added\" Kind=\"chrome/banner\" Text=\"added\"/>"
            + "</Column>");
        File.WriteAllText(path, v2);

        for (int i = 0; i < 5; i++)
        {
            service.Signal("q");
        }

        // Five signals inside the window are one pending entry, and the main-thread pump has not observed it
        // yet, so the quiet window has not even opened: nothing may be read.
        service.Pump();
        Check(service.SchedulerState.Pending == 1,
            "five signals inside the window coalesce into one pending document (pending=" + service.SchedulerState.Pending + ")");
        Check(service.Reports.Count == 0,
            "and nothing is read before the window opens (reports=" + service.Reports.Count + ")");
        Check(!HasElement(host.Manifest, "added"), "the host still draws the old tree");

        // Trailing edge. t=0.10: a later signal restarts the window. t=0.30: past the ORIGINAL deadline
        // (0.00 + 0.25), so a leading-edge or stamp-window-at-signal implementation would have committed.
        clock.Advance(0.1);
        service.Signal("q");
        service.Pump();
        clock.Advance(0.2);
        service.Pump();
        Check(service.Reports.Count == 0,
            "a later signal restarts the window: t=0.30 is past the original 0.25 deadline and still commits nothing (reports="
            + service.Reports.Count + ")");

        // t=0.36 is past the restarted deadline (0.10 + 0.25 = 0.35).
        clock.Advance(0.06);
        bool committed = service.Pump();
        Check(committed, "the pump after the restarted window commits");
        Check(service.Reports.Count == 1,
            "ONE read and ONE commit for the whole burst - a second read of the unchanged bytes would have published a skip (reports="
            + service.Reports.Count + ")");
        Check(service.LastReport != null && service.LastReport.Accepted, "and the one report is the accepted commit");
        Check(HasElement(host.Manifest, "added"), "the host follows the new tree");
        Check(service.SchedulerState.Pending == 0 && service.SchedulerState.Deferred == 0
            && service.SchedulerState.Retrying == 0,
            "the scheduler drains to zero after the commit");
    }

    // --- (3) bounded retry ------------------------------------------------------------------

    private static void VerifyBoundedRetry(string sandbox)
    {
        string dir = NewDir(sandbox, "retry");
        string path = Path.Combine(dir, "page.xml");
        string v1 = KeepPage("sched-retry");
        File.WriteAllText(path, v1);

        var clock = new ManualClock();
        var policy = new UiReloadPolicy(quietSeconds: 0.25, retrySeconds: 0.5, maxRetryAttempts: 2, maxDeferSeconds: 2.0);
        using var service = new UiDocumentService(false, policy, clock);
        service.Add(new UiDocumentSource("r", UiDocumentKind.Layout, path), v1);
        using var host = NewHost("sched-retry", UiLayoutManifest.Parse(v1), new UiBindings());
        service.Attach(host, "r");
        Draw(host);

        string v2 = Page("sched-retry",
            "<Column Id=\"root\">"
            + "<Widget Id=\"keep\" Kind=\"chrome/banner\" Text=\"keep\"/>"
            + "<Widget Id=\"added\" Kind=\"chrome/banner\" Text=\"added\"/>"
            + "</Column>");
        File.WriteAllText(path, v2);

        // A real sharing violation: the file is held open for the whole failure sequence, exactly the "locked
        // or being replaced" shape a save produces.
        using (var hold = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            service.Signal("r");
            service.Pump();
            clock.Advance(0.25);
            service.Pump();

            Check(service.SchedulerState.Retrying == 1,
                "attempt 1 (a locked file) is held as one retrying document, not reported (retrying="
                + service.SchedulerState.Retrying + ")");
            Check(service.Reports.Count == 0, "the transient read failure is not reported while retries remain");
            Check(HasElement(host.Manifest, "keep") && !HasElement(host.Manifest, "added"),
                "the previous valid version stays in force throughout");

            // No spin: two pumps at the same instant attempt nothing, because no time passed.
            service.Pump();
            service.Pump();
            Check(service.SchedulerState.Retrying == 1 && service.Reports.Count == 0,
                "pumping inside the retry interval attempts nothing");

            clock.Advance(0.5);
            service.Pump();
            Check(service.SchedulerState.Retrying == 1 && service.Reports.Count == 0,
                "attempt 2 is still held: MaxRetryAttempts=2 is not used up yet");

            clock.Advance(0.5);
            service.Pump();
            Check(service.Reports.Count == 1 && service.LastReport != null && service.LastReport.Rejected,
                "the first attempt past the retry budget (attempt 3 = 1 + MaxRetryAttempts) is reported as a failure");
            Check(service.SchedulerState.Retrying == 0, "and the scheduler stops holding it");

            // There is no path back into the read without a new signal.
            clock.Advance(100);
            service.Pump();
            service.Pump();
            Check(service.Reports.Count == 1, "no path keeps retrying on its own after the failure is reported");
            Check(HasElement(host.Manifest, "keep") && !HasElement(host.Manifest, "added"),
                "and the previous valid version is still the one in force");
        }

        // The lock is gone and the file is readable: a new signal commits the newest version through the same
        // bounded path.
        service.Signal("r");
        service.Pump();
        clock.Advance(0.25);
        service.Pump();
        Check(service.LastReport != null && service.LastReport.Accepted && HasElement(host.Manifest, "added"),
            "once the file is readable the newest version commits");
    }

    private static void VerifyZeroRetriesReportsImmediately(string sandbox)
    {
        string dir = NewDir(sandbox, "retry-zero");
        string path = Path.Combine(dir, "page.xml");
        string v1 = KeepPage("sched-retry-zero");
        File.WriteAllText(path, v1);

        var clock = new ManualClock();
        var policy = new UiReloadPolicy(quietSeconds: 0.25, retrySeconds: 0.5, maxRetryAttempts: 0, maxDeferSeconds: 2.0);
        using var service = new UiDocumentService(false, policy, clock);
        service.Add(new UiDocumentSource("r0", UiDocumentKind.Layout, path), v1);
        using var host = NewHost("sched-retry-zero", UiLayoutManifest.Parse(v1), new UiBindings());
        service.Attach(host, "r0");
        Draw(host);
        File.WriteAllText(path, KeepPage("sched-retry-zero").Replace("Text=\"keep\"", "Text=\"moved\""));

        using (var hold = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            service.Signal("r0");
            service.Pump();
            clock.Advance(0.25);
            service.Pump();

            Check(service.Reports.Count == 1 && service.LastReport != null && service.LastReport.Rejected,
                "MaxRetryAttempts=0 reports the first transient failure instead of holding it");
            Check(service.SchedulerState.Retrying == 0, "and nothing is left retrying");
            Check(HasElement(host.Manifest, "keep"), "the previous valid version is still in force");
        }
    }

    // --- (4) pause independence -------------------------------------------------------------

    /// <summary>
    /// A game-pause shape: the simulation clock is frozen and no tick is ever taken, while the wall clock the
    /// scheduler reads does move. This is the shape, not the game - it proves the scheduler makes no decision
    /// from simulation state, and leaves "a paused game really keeps pumping frames" to the in-game checklist.
    /// </summary>
    private sealed class PausedGameFixture
    {
        public bool Paused => true;

        public double SimulationSeconds => 0;

        public int Ticks { get; private set; }

        public void Tick()
        {
            Ticks++;
        }
    }

    private static void VerifyPauseIndependence(string sandbox)
    {
        string dir = NewDir(sandbox, "pause");
        string path = Path.Combine(dir, "page.xml");
        string v1 = KeepPage("sched-pause");
        File.WriteAllText(path, v1);

        var clock = new ManualClock();
        var policy = new UiReloadPolicy(quietSeconds: 0.25, retrySeconds: 0.5, maxRetryAttempts: 3, maxDeferSeconds: 2.0);
        using var service = new UiDocumentService(false, policy, clock);
        service.Add(new UiDocumentSource("p", UiDocumentKind.Layout, path), v1);
        using var host = NewHost("sched-pause", UiLayoutManifest.Parse(v1), new UiBindings());
        service.Attach(host, "p");
        Draw(host);

        var game = new PausedGameFixture();

        string v2 = Page("sched-pause",
            "<Column Id=\"root\">"
            + "<Widget Id=\"keep\" Kind=\"chrome/banner\" Text=\"keep\"/>"
            + "<Widget Id=\"added\" Kind=\"chrome/banner\" Text=\"added\"/>"
            + "</Column>");
        File.WriteAllText(path, v2);
        service.Signal("p");

        service.Pump();
        clock.Advance(0.25);
        bool committed = service.Pump();

        Check(game.Paused && game.SimulationSeconds == 0 && game.Ticks == 0,
            "the fixture's simulation clock is frozen (paused=" + game.Paused + ", ticks=" + game.Ticks + ")");
        Check(committed && service.LastReport != null && service.LastReport.Accepted,
            "the commit happens anyway: the scheduler's clock is real time, not the simulation clock");
        Check(HasElement(host.Manifest, "added"), "and the paused game's window follows the saved file");
    }

    // --- (5) nothing open, nothing rebuilt ---------------------------------------------------

    private static void VerifyNothingOpenIsNotRebuilt(string sandbox)
    {
        string dir = NewDir(sandbox, "closed");
        string path = Path.Combine(dir, "page.xml");
        string v1 = KeepPage("sched-closed");
        File.WriteAllText(path, v1);

        var clock = new ManualClock();
        var policy = new UiReloadPolicy(quietSeconds: 0.25, retrySeconds: 0.5, maxRetryAttempts: 3, maxDeferSeconds: 2.0);
        using var service = new UiDocumentService(false, policy, clock);
        service.Add(new UiDocumentSource("z", UiDocumentKind.Layout, path), v1);

        string v2 = Page("sched-closed",
            "<Column Id=\"root\"><Widget Id=\"added\" Kind=\"chrome/banner\" Text=\"added\"/></Column>");
        File.WriteAllText(path, v2);
        service.Signal("z");

        for (int i = 0; i < 5; i++)
        {
            clock.Advance(1.0);
            service.Pump();
        }

        // A read with no affected host still records an accepted report ("no live host depends on this
        // document yet"), so an empty ring is the observable proof that nothing was read or parsed at all.
        Check(service.Reports.Count == 0,
            "with zero attached hosts the service reads and parses nothing, however much time passes (reports="
            + service.Reports.Count + ")");
        Check(service.LastReport == null, "and no candidate was attempted");
        Check(service.SchedulerState.Pending == 1, "the signal is held, not dropped");

        using var host = NewHost("sched-closed", UiLayoutManifest.Parse(v1), new UiBindings());
        service.Attach(host, "z");

        Check(HasElement(host.Manifest, "added"),
            "the next attach resolves the newest valid content, not the version read when the source was registered");
        Check(service.SchedulerState.Pending == 0, "and the held signal is consumed");
        Check(service.Reports.Count == 1 && service.LastReport != null && service.LastReport.Accepted,
            "the resolution is one accepted report with no live host at read time");
    }

    // --- (6) bounded deferral ---------------------------------------------------------------

    private static void VerifyBoundedDeferral(string sandbox)
    {
        string dir = NewDir(sandbox, "defer");
        string path = Path.Combine(dir, "page.xml");
        string v1 = KeepPage("sched-defer");
        File.WriteAllText(path, v1);

        var clock = new ManualClock();
        var policy = new UiReloadPolicy(quietSeconds: 0.25, retrySeconds: 0.5, maxRetryAttempts: 3, maxDeferSeconds: 2.0);
        using var service = new UiDocumentService(false, policy, clock);
        service.Add(new UiDocumentSource("d", UiDocumentKind.Layout, path), v1);
        using var host = NewHost("sched-defer", UiLayoutManifest.Parse(v1), new UiBindings());
        service.Attach(host, "d");
        Draw(host);

        string v2 = Page("sched-defer",
            "<Column Id=\"root\">"
            + "<Widget Id=\"keep\" Kind=\"chrome/banner\" Text=\"keep\"/>"
            + "<Widget Id=\"added\" Kind=\"chrome/banner\" Text=\"added\"/>"
            + "</Column>");
        File.WriteAllText(path, v2);
        service.Signal("d");
        service.Pump();
        clock.Advance(0.25);

        // The real signal: the library's own IMGUI hot-control capture, which is what a drag or a text edit
        // holds. The lane drives the real session state rather than a test-only boolean.
        host.Session.CaptureHotControl(4242);
        bool committed = service.Pump();

        Check(!committed && service.Reports.Count == 0, "a ready commit is not applied while the user holds an interaction");
        Check(service.SchedulerState.Deferred == 1,
            "and SchedulerState names the reason - deferred, not pending (deferred=" + service.SchedulerState.Deferred + ")");
        Check(service.SchedulerState.Pending == 0, "the deferred document is not also counted as pending");
        Check(!HasElement(host.Manifest, "added"), "the old tree is still the one in force");

        clock.Advance(1.9);
        service.Pump();
        Check(service.SchedulerState.Deferred == 1 && service.Reports.Count == 0,
            "just below MaxDeferSeconds=2.0 the commit is still deferred");

        clock.Advance(0.2);
        committed = service.Pump();
        Check(committed && HasElement(host.Manifest, "added"), "past the ceiling the commit is forced through");
        Check(service.SchedulerState.Deferred == 0, "and the deferral clears");
        Check(host.Session.OwnedHotControl == null, "the forced commit released the held capture instead of leaving it behind");
    }

    private static void VerifyStateSeparatesQuietFromDeferral(string sandbox)
    {
        string dir = NewDir(sandbox, "state");
        string path = Path.Combine(dir, "page.xml");
        string v1 = KeepPage("sched-state");
        File.WriteAllText(path, v1);

        var clock = new ManualClock();
        var policy = new UiReloadPolicy(quietSeconds: 0.25, retrySeconds: 0.5, maxRetryAttempts: 3, maxDeferSeconds: 2.0);
        using var service = new UiDocumentService(false, policy, clock);
        service.Add(new UiDocumentSource("s", UiDocumentKind.Layout, path), v1);
        using var host = NewHost("sched-state", UiLayoutManifest.Parse(v1), new UiBindings());
        service.Attach(host, "s");
        Draw(host);

        File.WriteAllText(path, KeepPage("sched-state").Replace("Text=\"keep\"", "Text=\"moved\""));
        service.Signal("s");
        service.Pump();

        Check(service.SchedulerState.Pending == 1 && service.SchedulerState.Deferred == 0
            && service.SchedulerState.Retrying == 0,
            "a signal inside its quiet window is pending and nothing else");

        double oldestBefore = service.SchedulerState.OldestPendingSeconds;
        clock.Advance(0.5);
        Check(service.SchedulerState.OldestPendingSeconds > oldestBefore,
            "OldestPendingSeconds advances with the clock, not with anything else");

        host.Session.CaptureHotControl(7);
        service.Pump();
        Check(service.SchedulerState.Deferred == 1 && service.SchedulerState.Pending == 0
            && service.SchedulerState.Retrying == 0,
            "a ready commit held for interaction is deferred and not conflated with pending or retrying");
    }

    // --- the two channels -------------------------------------------------------------------

    private static void VerifySignalIsDebouncedAndReloadIsImmediate(string sandbox)
    {
        string dir = NewDir(sandbox, "channels");
        string path = Path.Combine(dir, "page.xml");
        string v1 = KeepPage("sched-channels");
        File.WriteAllText(path, v1);

        var clock = new ManualClock();
        var policy = new UiReloadPolicy(quietSeconds: 0.25, retrySeconds: 0.5, maxRetryAttempts: 3, maxDeferSeconds: 2.0);
        using var service = new UiDocumentService(false, policy, clock);
        service.Add(new UiDocumentSource("ch", UiDocumentKind.Layout, path), v1);
        using var host = NewHost("sched-channels", UiLayoutManifest.Parse(v1), new UiBindings());
        service.Attach(host, "ch");
        Draw(host);

        string v2 = Page("sched-channels",
            "<Column Id=\"root\">"
            + "<Widget Id=\"keep\" Kind=\"chrome/banner\" Text=\"keep\"/>"
            + "<Widget Id=\"added\" Kind=\"chrome/banner\" Text=\"added\"/>"
            + "</Column>");
        File.WriteAllText(path, v2);

        Check(service.Signal("ch"), "the watcher channel accepts the signal");
        service.Pump();
        Check(service.Reports.Count == 0 && !HasElement(host.Manifest, "added"),
            "a watcher signal alone never commits: it is debounced until the quiet window elapses");

        // The same bytes, through the manual channel: immediate, and the pending schedule is consumed.
        UiReloadReport? report = service.Reload("ch");
        Check(report != null && report.Accepted && HasElement(host.Manifest, "added"),
            "Reload commits the same bytes at once, on the identical validation/commit path");
        Check(service.SchedulerState.Pending == 0, "and the manual call consumed the scheduled signal");
    }

    // --- the fail-loud missing clock ---------------------------------------------------------

    private static void VerifyZeroClockNeverCommitsByPumping(string sandbox)
    {
        string dir = NewDir(sandbox, "zero-clock");
        string path = Path.Combine(dir, "page.xml");
        string v1 = KeepPage("sched-zero-clock");
        File.WriteAllText(path, v1);

        // No time source is injected: the service falls back to VerseFerriteTimeSource, whose
        // Time.realtimeSinceStartup is the harness's constant-zero stub. No time passes, so pumping can never
        // commit - the fail-loud shape that catches a scheduler built on a frame, host or pump count instead
        // of the clock.
        using var service = new UiDocumentService(false);
        service.Add(new UiDocumentSource("zc", UiDocumentKind.Layout, path), v1);
        using var host = NewHost("sched-zero-clock", UiLayoutManifest.Parse(v1), new UiBindings());
        service.Attach(host, "zc");
        Draw(host);

        string v2 = Page("sched-zero-clock",
            "<Column Id=\"root\"><Widget Id=\"added\" Kind=\"chrome/banner\" Text=\"added\"/></Column>");
        File.WriteAllText(path, v2);
        service.Signal("zc");

        for (int i = 0; i < 25; i++)
        {
            service.Pump();
        }

        Check(service.Reports.Count == 0 && !HasElement(host.Manifest, "added"),
            "with the constant-zero stub clock, pumping 25 times commits nothing (reports=" + service.Reports.Count + ")");
        Check(service.SchedulerState.Pending == 1, "the signal stays held for as long as no time passes");
    }

    // --- (7) atomicity through the scheduler ------------------------------------------------

    private static void VerifyLayoutAndStyleInQuickSuccession(string sandbox)
    {
        string dir = NewDir(sandbox, "pair");
        string layoutPath = Path.Combine(dir, "page.xml");
        string stylePath = Path.Combine(dir, "theme.xml");
        string layout1 = KeepPage("sched-pair");
        string style1 = PageSchemeStyle("ice", "#0000ff");
        File.WriteAllText(layoutPath, layout1);
        File.WriteAllText(stylePath, style1);

        var clock = new ManualClock();
        var policy = new UiReloadPolicy(quietSeconds: 0.25, retrySeconds: 0.5, maxRetryAttempts: 3, maxDeferSeconds: 2.0);
        using var service = new UiDocumentService(false, policy, clock);
        service.Add(new UiDocumentSource("lay", UiDocumentKind.Layout, layoutPath), layout1);
        service.Add(new UiDocumentSource("sty", UiDocumentKind.Style, stylePath), style1);

        UiTheme theme = UiTheme.DarkGold.Clone();
        using var host = NewHost("sched-pair", UiLayoutManifest.Parse(layout1), new UiBindings(), theme);
        service.Attach(host, "lay", "sty");
        Check(SameColor(theme.Panel, new Color(0f, 0f, 1f, 1f)), "the first style version applied on attach");

        // Two files saved in quick succession inside one quiet window: each document is its own batch and its
        // own report, and the one settle commits both.
        string layout2 = Page("sched-pair",
            "<Column Id=\"root\"><Widget Id=\"added\" Kind=\"chrome/banner\" Text=\"added\"/></Column>");
        string style2 = PageSchemeStyle("ice", "#00ff00");
        File.WriteAllText(layoutPath, layout2);
        File.WriteAllText(stylePath, style2);
        service.Signal("lay");
        service.Signal("sty");
        service.Pump();
        clock.Advance(0.25);
        Check(service.Pump(), "both documents commit in the settle after their shared quiet window");

        Check(HasElement(host.Manifest, "added"), "the layout version reached the host");
        Check(SameColor(theme.Panel, new Color(0f, 1f, 0f, 1f)), "and the style version reached the same host");
        Check(service.Reports.Count >= 2,
            "each document is its own batch and its own report (reports=" + service.Reports.Count + ")");
    }

    private static void VerifySchedulerRollbackKeepsDraft(string sandbox)
    {
        string dir = NewDir(sandbox, "rollback");
        string path = Path.Combine(dir, "shared.xml");
        string v1 = Page("sched-rollback",
            "<Column Id=\"root\"><Scroll Id=\"body\" Height=\"40\">"
            + "<Widget Id=\"keep\" Kind=\"chrome/banner\" Text=\"keep\"/>"
            + "</Scroll></Column>");
        File.WriteAllText(path, v1);

        var clock = new ManualClock();
        var policy = new UiReloadPolicy(quietSeconds: 0.25, retrySeconds: 0.5, maxRetryAttempts: 3, maxDeferSeconds: 2.0);
        using var service = new UiDocumentService(false, policy, clock);
        service.Add(new UiDocumentSource("rb", UiDocumentKind.Layout, path), v1);
        using var hostA = NewHost("sched-rb-a", UiLayoutManifest.Parse(v1), new UiBindings());
        using var hostB = NewHost("sched-rb-b", UiLayoutManifest.Parse(v1), new UiBindings());
        service.Attach(hostA, "rb");
        service.Attach(hostB, "rb");
        Draw(hostA);

        UiNode? draft = hostA.Session.GetNodeByElementId("keep");
        Check(draft != null, "the arranged element the draft hangs on exists");
        draft!.State.EditText = "uncommitted-draft";
        UiLayoutManifest before = hostA.Manifest;

        string v2 = Page("sched-rollback",
            "<Column Id=\"root\"><Widget Id=\"replacement\" Kind=\"chrome/banner\" Text=\"replacement\"/></Column>");
        File.WriteAllText(path, v2);
        service.Signal("rb");
        service.Pump();
        service.DocumentCommitFaultOverride = host =>
            ReferenceEquals(host, hostB) ? "planted host B commit failure" : "";
        clock.Advance(0.25);
        service.Pump();
        UiReloadReport? report = service.LastReport;
        service.DocumentCommitFaultOverride = null;

        Check(report != null && report.Rejected, "the scheduled batch is refused when a later host fails its commit");
        Check(ReferenceEquals(hostA.Manifest, before) && HasElement(hostA.Manifest, "keep"),
            "the rolled-back host is back on the old document");
        Check(draft.State.EditText == "uncommitted-draft",
            "and R1's draft survives the scheduler-driven rollback");
    }

    // --- R06-2: the reopen-refresh contract --------------------------------------------------

    /// <summary>
    /// The reviewer's exact shape: valid old content, the only host closed, the file changed, the watcher's
    /// signal delivered, then a new host attached with NO pump in between. The signal must be consumed, not
    /// dropped, so the reopen shows the new content.
    /// </summary>
    private static void VerifyReopenAfterSignalNoPump(string sandbox)
    {
        string dir = NewDir(sandbox, "reopen-signal");
        string path = Path.Combine(dir, "page.xml");
        string v1 = KeepPage("sched-reopen-signal");
        File.WriteAllText(path, v1);

        var clock = new ManualClock();
        var policy = new UiReloadPolicy(quietSeconds: 0.25, retrySeconds: 0.5, maxRetryAttempts: 3, maxDeferSeconds: 2.0);
        using var service = new UiDocumentService(false, policy, clock);
        service.Add(new UiDocumentSource("rs", UiDocumentKind.Layout, path), v1);

        var first = NewHost("sched-reopen-signal-1", UiLayoutManifest.Parse(v1), new UiBindings());
        service.Attach(first, "rs");
        first.Dispose();
        Check(service.DependencyCount == 0, "the only host is closed, so nothing can pump");

        string v2 = Page("sched-reopen-signal",
            "<Column Id=\"root\">"
            + "<Widget Id=\"keep\" Kind=\"chrome/banner\" Text=\"keep\"/>"
            + "<Widget Id=\"added\" Kind=\"chrome/banner\" Text=\"added\"/>"
            + "</Column>");
        File.WriteAllText(path, v2);
        Check(service.Signal("rs"), "the watcher posts a change signal while nothing is attached");
        Check(service.HasPending, "and it sits in the pending set: there is no host to pump it");

        using var reopened = NewHost("sched-reopen-signal-2", UiLayoutManifest.Parse(v1), new UiBindings());
        service.Attach(reopened, "rs");

        Check(HasElement(reopened.Manifest, "added"),
            "reopen shows the new content even though no Pump ever observed the signal");
        Check(!service.HasPending, "and the signal was consumed, not thrown away");
        Check(service.Reports.Count == 1 && service.LastReport != null && service.LastReport.Accepted,
            "the refresh is one accepted report (reports=" + service.Reports.Count + ")");
    }

    /// <summary>(b) No watcher fired at all: the file's own content version is what makes the reopen fresh.</summary>
    private static void VerifyReopenAfterUnsignalledContentChange(string sandbox)
    {
        string dir = NewDir(sandbox, "reopen-quiet");
        string path = Path.Combine(dir, "page.xml");
        string v1 = KeepPage("sched-reopen-quiet");
        File.WriteAllText(path, v1);

        var clock = new ManualClock();
        var policy = new UiReloadPolicy(quietSeconds: 0.25, retrySeconds: 0.5, maxRetryAttempts: 3, maxDeferSeconds: 2.0);
        using var service = new UiDocumentService(false, policy, clock);
        service.Add(new UiDocumentSource("rq", UiDocumentKind.Layout, path), v1);

        var first = NewHost("sched-reopen-quiet-1", UiLayoutManifest.Parse(v1), new UiBindings());
        service.Attach(first, "rq");
        first.Dispose();

        File.WriteAllText(path, Page("sched-reopen-quiet",
            "<Column Id=\"root\"><Widget Id=\"added\" Kind=\"chrome/banner\" Text=\"added\"/></Column>"));
        // Deliberately no Signal: the notification was never delivered.

        using var reopened = NewHost("sched-reopen-quiet-2", UiLayoutManifest.Parse(v1), new UiBindings());
        service.Attach(reopened, "rq");
        Check(HasElement(reopened.Manifest, "added"),
            "an unsignalled content change still resolves at attach (content-version freshness)");
        Check(service.Reports.Count == 1 && service.LastReport != null && service.LastReport.Accepted,
            "and it is one accepted report");
    }

    /// <summary>(b) The directory did not exist when the source was registered, so no watcher ever armed.</summary>
    private static void VerifyReopenWhenWatcherCouldNotArm(string sandbox)
    {
        string dir = Path.Combine(sandbox, "late-dir");
        string path = Path.Combine(dir, "page.xml");
        string fallback = KeepPage("sched-late");

        var clock = new ManualClock();
        var policy = new UiReloadPolicy(quietSeconds: 0.25, retrySeconds: 0.5, maxRetryAttempts: 3, maxDeferSeconds: 2.0);
        using var service = new UiDocumentService(true, policy, clock);
        Check(service.Add(new UiDocumentSource("late", UiDocumentKind.Layout, path), fallback),
            "a source under a directory that does not exist yet still registers");
        Check(!service.IsWatching("late"), "and holds no watcher: the directory is missing");

        Directory.CreateDirectory(dir);
        File.WriteAllText(path, Page("sched-late",
            "<Column Id=\"root\"><Widget Id=\"from-file\" Kind=\"chrome/banner\" Text=\"file\"/></Column>"));

        using var host = NewHost("sched-late", UiLayoutManifest.Parse(fallback), new UiBindings());
        service.Attach(host, "late");
        Check(HasElement(host.Manifest, "from-file"),
            "the late directory's file resolves at attach even though no watcher ever armed");
        Check(service.LastReport != null && service.LastReport.Accepted, "and the refresh is the accepted report");
    }

    /// <summary>(c) A missing file is not newer content: the last valid version stays in force, unreported.</summary>
    private static void VerifyReopenWithMissingFileKeepsLastValid(string sandbox)
    {
        string dir = NewDir(sandbox, "reopen-missing");
        string path = Path.Combine(dir, "page.xml");
        string external = Page("sched-missing",
            "<Column Id=\"root\"><Widget Id=\"external\" Kind=\"chrome/banner\" Text=\"external\"/></Column>");
        string embedded = Page("sched-missing",
            "<Column Id=\"root\"><Widget Id=\"embedded\" Kind=\"chrome/banner\" Text=\"embedded\"/></Column>");
        File.WriteAllText(path, external);

        var clock = new ManualClock();
        var policy = new UiReloadPolicy(quietSeconds: 0.25, retrySeconds: 0.5, maxRetryAttempts: 3, maxDeferSeconds: 2.0);
        using var service = new UiDocumentService(false, policy, clock);
        service.Add(new UiDocumentSource("rm", UiDocumentKind.Layout, path), embedded);

        var first = NewHost("sched-missing-1", UiLayoutManifest.Parse(external), new UiBindings());
        service.Attach(first, "rm");
        Check(HasElement(first.Manifest, "external"), "the first host started on the external version");
        first.Dispose();

        File.Delete(path);
        UiReloadReport? before = service.LastReport;
        int baseline = service.Reports.Count;

        using var reopened = NewHost("sched-missing-2", UiLayoutManifest.Parse(embedded), new UiBindings());
        service.Attach(reopened, "rm");
        Check(HasElement(reopened.Manifest, "external") && !HasElement(reopened.Manifest, "embedded"),
            "a missing file leaves the last valid external version in force, not the embedded fallback");
        Check(service.Reports.Count == baseline && ReferenceEquals(service.LastReport, before),
            "and attach does not turn an absence into a report");
    }

    /// <summary>(d) The content moved and is invalid: refused, reported once, last valid version kept.</summary>
    private static void VerifyReopenWithBrokenFileReportsOnce(string sandbox)
    {
        string dir = NewDir(sandbox, "reopen-broken");
        string path = Path.Combine(dir, "page.xml");
        string v1 = KeepPage("sched-broken");
        File.WriteAllText(path, v1);

        var clock = new ManualClock();
        var policy = new UiReloadPolicy(quietSeconds: 0.25, retrySeconds: 0.5, maxRetryAttempts: 3, maxDeferSeconds: 2.0);
        using var service = new UiDocumentService(false, policy, clock);
        service.Add(new UiDocumentSource("rb", UiDocumentKind.Layout, path), v1);

        var first = NewHost("sched-broken-1", UiLayoutManifest.Parse(v1), new UiBindings());
        service.Attach(first, "rb");
        first.Dispose();

        File.WriteAllText(path, "<UiPage Schema=\"2\" Source=\"sched-broken\"><Column Id=\"root\">");
        int baseline = service.Reports.Count;

        using var reopened = NewHost("sched-broken-2", UiLayoutManifest.Parse(v1), new UiBindings());
        service.Attach(reopened, "rb");
        Check(HasElement(reopened.Manifest, "keep"),
            "a broken newer file leaves the last valid version in force");
        Check(service.Reports.Count == baseline + 1 && service.LastReport != null && service.LastReport.Rejected,
            "and the break is reported once at attach (reports=" + service.Reports.Count + ")");

        using var third = NewHost("sched-broken-3", UiLayoutManifest.Parse(v1), new UiBindings());
        service.Attach(third, "rb");
        Check(service.Reports.Count == baseline + 1, "the same refusing version is not reported again");
        Check(HasElement(third.Manifest, "keep"), "and the next reopen is still bound to the last valid version");
    }

    /// <summary>The freshness probe must not turn an unchanged file into a report or a re-parse event.</summary>
    private static void VerifyReopenWithUnchangedFileWritesNoReport(string sandbox)
    {
        string dir = NewDir(sandbox, "reopen-same");
        string path = Path.Combine(dir, "page.xml");
        string v1 = KeepPage("sched-same");
        File.WriteAllText(path, v1);

        var clock = new ManualClock();
        var policy = new UiReloadPolicy(quietSeconds: 0.25, retrySeconds: 0.5, maxRetryAttempts: 3, maxDeferSeconds: 2.0);
        using var service = new UiDocumentService(false, policy, clock);
        service.Add(new UiDocumentSource("ru", UiDocumentKind.Layout, path), v1);

        int baseline = service.Reports.Count;
        using var host = NewHost("sched-same", UiLayoutManifest.Parse(v1), new UiBindings());
        service.Attach(host, "ru");
        Check(service.Reports.Count == baseline,
            "an unchanged file is not turned into a report by the attach freshness probe");
        Check(HasElement(host.Manifest, "keep"), "and the host is bound to the version already in force");
    }

    // --- R06 risk 1: the manual channel from inside a draw pass ------------------------------

    private static bool probeKindRegistered;

    private static void EnsureProbeKind()
    {
        if (probeKindRegistered) return;
        UiWidgetRegistry.InitializeCore();
        UiWidgetRegistry.Register(UiWidgetRegistry.CoreScope, "sched/probe", () => new PassProbeWidget());
        probeKindRegistered = true;
    }

    /// <summary>The probe draws in tree order and records the session's capture state at that moment.</summary>
    private static readonly List<string> PassTrace = new();

    /// <summary>One-shot trigger a probe fires from inside its own Draw, i.e. from inside the host's pass.</summary>
    private static Action? PendingTrigger;

    private sealed class PassProbeWidget : IUiWidget
    {
        private string label = "";

        public string Kind => "sched/probe";

        public void Configure(UiElementSpec spec)
        {
            label = spec.TryGetAttribute("Text", out string text) ? text : "";
        }

        public void Validate(IUiBindings bindings, string elementPath)
        {
        }

        public float Measure(UiWidgetContext ctx) => 16f;

        public void Draw(Rect rect, UiWidgetContext ctx)
        {
            PassTrace.Add(label + (ctx.Session.OwnedHotControl.HasValue ? ":hot" : ":free"));
            Action? trigger = PendingTrigger;
            PendingTrigger = null;
            trigger?.Invoke();
        }
    }

    /// <summary>
    /// Drives <see cref="UiDocumentService.Reload"/> from inside the host's own draw pass - exactly what the
    /// demo's manual reload button does - and records what the running pass, the session and the next pass
    /// actually do. The lane asserts the observation, not an assumption.
    /// </summary>
    private static void VerifyReloadInsideDrawPass(string sandbox)
    {
        EnsureProbeKind();

        string dir = NewDir(sandbox, "inpass");
        string path = Path.Combine(dir, "page.xml");
        string v1 = Page("sched-inpass",
            "<Column Id=\"root\">"
            + "<Widget Id=\"trigger\" Kind=\"sched/probe\" Text=\"trigger\"/>"
            + "<Widget Id=\"tail\" Kind=\"sched/probe\" Text=\"tail\"/>"
            + "</Column>");
        File.WriteAllText(path, v1);

        var clock = new ManualClock();
        var policy = new UiReloadPolicy(quietSeconds: 0.25, retrySeconds: 0.5, maxRetryAttempts: 3, maxDeferSeconds: 2.0);
        using var service = new UiDocumentService(false, policy, clock);
        service.Add(new UiDocumentSource("ip", UiDocumentKind.Layout, path), v1);
        using var host = NewHost("sched-inpass", UiLayoutManifest.Parse(v1), new UiBindings());
        service.Attach(host, "ip");
        PendingTrigger = null;
        Draw(host);

        UiNode? triggerNode = host.Session.GetNodeByElementId("trigger");
        Check(triggerNode != null, "the probe element has a node a draft can hang on");
        triggerNode!.State.EditText = "mid-pass-draft";

        string v2 = Page("sched-inpass",
            "<Column Id=\"root\">"
            + "<Widget Id=\"trigger\" Kind=\"sched/probe\" Text=\"trigger\"/>"
            + "<Widget Id=\"head\" Kind=\"sched/probe\" Text=\"head\"/>"
            + "<Widget Id=\"added\" Kind=\"chrome/banner\" Text=\"added\"/>"
            + "</Column>");
        File.WriteAllText(path, v2);

        host.Session.CaptureHotControl(4242);
        PassTrace.Clear();
        string? thrown = null;
        PendingTrigger = () => service.Reload("ip");
        try
        {
            Draw(host);
        }
        catch (Exception ex)
        {
            thrown = ex.GetType().Name + ": " + ex.Message;
        }

        Check(thrown == null, "a Reload from inside the host's own draw pass does not throw (got " + (thrown ?? "none") + ")");
        string trace = string.Join(",", PassTrace);
        Check(PassTrace.Count == 2 && PassTrace[0] == "trigger:hot" && PassTrace[1] == "tail:free",
            "the running pass finishes on the pre-reload snapshot and the seal lands mid-pass: the element the new tree removes still drew, already capture-free (["
            + trace + "])");
        Check(!PassTrace.Contains("head:hot") && !PassTrace.Contains("head:free"),
            "and the running pass never swaps in the new snapshot mid-pass");
        Check(service.LastReport != null && service.LastReport.Accepted,
            "Reload returned its synchronous accepted report from inside the pass");
        Check(HasElement(host.Manifest, "added") && !HasElement(host.Manifest, "tail"),
            "the commit is synchronous: the manifest is already the new tree when the pass returns");
        Check(host.Session.IsActive, "the session survives the mid-pass commit");
        Check(ReferenceEquals(triggerNode, host.Session.GetNodeByElementId("trigger"))
            && triggerNode!.State.EditText == "mid-pass-draft",
            "a surviving identity keeps its node and its draft across the mid-pass commit");
        Check(host.Session.OwnedHotControl == null, "and the held capture was released by the mid-pass seal");

        PendingTrigger = null;
        PassTrace.Clear();
        Draw(host);
        trace = string.Join(",", PassTrace);
        Check(PassTrace.Count == 2 && PassTrace[0] == "trigger:free" && PassTrace[1] == "head:free",
            "the next pass draws the new tree only ([" + trace + "])");
        Check(triggerNode!.State.EditText == "mid-pass-draft", "and the draft is still there on the pass after");
    }

    /// <summary>
    /// The same observation for <see cref="UiDocumentService.ReloadAll"/>, with a layout and a style document
    /// so the lane can see whether both halves commit inside the one pass.
    /// </summary>
    private static void VerifyReloadAllInsideDrawPass(string sandbox)
    {
        EnsureProbeKind();

        string dir = NewDir(sandbox, "inpass-all");
        string layoutPath = Path.Combine(dir, "page.xml");
        string stylePath = Path.Combine(dir, "theme.xml");
        string layout1 = Page("sched-inpass-all",
            "<Column Id=\"root\">"
            + "<Widget Id=\"trigger\" Kind=\"sched/probe\" Text=\"trigger\"/>"
            + "<Widget Id=\"tail\" Kind=\"sched/probe\" Text=\"tail\"/>"
            + "</Column>");
        string style1 = PageSchemeStyle("ice", "#0000ff");
        File.WriteAllText(layoutPath, layout1);
        File.WriteAllText(stylePath, style1);

        var clock = new ManualClock();
        var policy = new UiReloadPolicy(quietSeconds: 0.25, retrySeconds: 0.5, maxRetryAttempts: 3, maxDeferSeconds: 2.0);
        using var service = new UiDocumentService(false, policy, clock);
        service.Add(new UiDocumentSource("lay", UiDocumentKind.Layout, layoutPath), layout1);
        service.Add(new UiDocumentSource("sty", UiDocumentKind.Style, stylePath), style1);

        UiTheme theme = UiTheme.DarkGold.Clone();
        using var host = NewHost("sched-inpass-all", UiLayoutManifest.Parse(layout1), new UiBindings(), theme);
        service.Attach(host, "lay", "sty");
        PendingTrigger = null;
        Draw(host);
        Check(SameColor(theme.Panel, new Color(0f, 0f, 1f, 1f)), "the first style version applied on attach");

        string layout2 = Page("sched-inpass-all",
            "<Column Id=\"root\">"
            + "<Widget Id=\"trigger\" Kind=\"sched/probe\" Text=\"trigger\"/>"
            + "<Widget Id=\"head\" Kind=\"sched/probe\" Text=\"head\"/>"
            + "<Widget Id=\"added\" Kind=\"chrome/banner\" Text=\"added\"/>"
            + "</Column>");
        string style2 = PageSchemeStyle("ice", "#00ff00");
        File.WriteAllText(layoutPath, layout2);
        File.WriteAllText(stylePath, style2);

        PassTrace.Clear();
        string? thrown = null;
        PendingTrigger = () => { service.ReloadAll(); };
        try
        {
            Draw(host);
        }
        catch (Exception ex)
        {
            thrown = ex.GetType().Name + ": " + ex.Message;
        }

        Check(thrown == null, "ReloadAll from inside the draw pass does not throw (got " + (thrown ?? "none") + ")");
        string trace = string.Join(",", PassTrace);
        Check(PassTrace.Count == 2 && PassTrace[0] == "trigger:free" && PassTrace[1] == "tail:free",
            "the running pass still finishes on the pre-reload snapshot ([" + trace + "])");
        Check(HasElement(host.Manifest, "added"), "the layout half committed synchronously");
        Check(SameColor(theme.Panel, new Color(0f, 1f, 0f, 1f)),
            "and the style half was applied to the theme inside the same pass");

        PendingTrigger = null;
        PassTrace.Clear();
        Draw(host);
        trace = string.Join(",", PassTrace);
        Check(PassTrace.Count == 2 && PassTrace[0] == "trigger:free" && PassTrace[1] == "head:free",
            "the next pass draws the new tree only ([" + trace + "])");
    }

    // --- fixture ----------------------------------------------------------------------------

    private sealed class ManualClock : IUiTimeSource
    {
        public double Now;

        public double NowSeconds => Now;

        public void Advance(double seconds)
        {
            Now += seconds;
        }
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

    private static int Run(string name, Action action)
    {
        try
        {
            action();
            Console.WriteLine("  ok: " + name);
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("  FAIL: " + name + " - " + ex.Message);
            return 1;
        }
    }

    private static void Check(bool condition, string message)
    {
        if (condition)
        {
            Console.WriteLine("  ok: " + message);
        }
        else
        {
            failures++;
            Console.WriteLine("  FAIL: " + message);
        }
    }

    private static string NewDir(string sandbox, string name)
    {
        string dir = Path.Combine(sandbox, name);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static UiHost NewHost(string source, UiLayoutManifest manifest, IUiBindings bindings)
    {
        return new UiHost(source, manifest, bindings, UiTheme.DarkGold, new FixedMetrics(), new FixedTranslation());
    }

    private static UiHost NewHost(string source, UiLayoutManifest manifest, IUiBindings bindings, UiTheme theme)
    {
        return new UiHost(source, manifest, bindings, theme, new FixedMetrics(), new FixedTranslation());
    }

    private static void Draw(UiHost host)
    {
        host.DrawFrame(new Rect(0f, 0f, 400f, 300f));
    }

    private static string Page(string source, string body)
    {
        return "<UiPage Schema=\"2\" Source=\"" + source + "\">" + body + "</UiPage>";
    }

    private static string KeepPage(string source)
    {
        return Page(source, "<Column Id=\"root\"><Widget Id=\"keep\" Kind=\"chrome/banner\" Text=\"keep\"/></Column>");
    }

    private static string PageSchemeStyle(string schemeName, string panelValue)
    {
        return "<Styles Schema=\"1\" Scheme=\"" + schemeName + "\">"
            + "<Scheme Name=\"" + schemeName + "\">"
            + "<Color Token=\"Panel\" Value=\"" + panelValue + "\"/>"
            + "</Scheme>"
            + "</Styles>";
    }

    private static bool SameColor(Color a, Color b)
    {
        return Math.Abs(a.r - b.r) < 0.001f && Math.Abs(a.g - b.g) < 0.001f && Math.Abs(a.b - b.b) < 0.001f;
    }

    private static bool HasElement(UiLayoutManifest manifest, string id)
    {
        for (int i = 0; i < manifest.Roots.Count; i++)
        {
            if (HasElement(manifest.Roots[i], id)) return true;
        }

        return false;
    }

    private static bool HasElement(UiElementSpec spec, string id)
    {
        if (string.Equals(spec.Id, id, StringComparison.Ordinal)) return true;
        for (int i = 0; i < spec.Children.Count; i++)
        {
            if (HasElement(spec.Children[i], id)) return true;
        }

        return false;
    }
}
