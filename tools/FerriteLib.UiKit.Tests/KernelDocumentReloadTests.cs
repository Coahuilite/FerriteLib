using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FerriteLib.UiKit.Kernel;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// Document lane (0.5.x, package P4): a consumer-owned file is the page's second text origin, and editing
/// it has to reach the open window that reads it without a restart and without a recompile.
/// <para>
/// What the lane asserts, in the order the contract states it: a document is dependency-tracked so an edit
/// moves only the hosts that actually read it; a candidate is fully parsed and validated before anything is
/// committed, and the current GUI pass never swaps a tree under itself; one change batch is all-or-nothing
/// across every host it affects while a different document's failure stays its own batch; the first load
/// falls back to the embedded text when the file does not exist and a later bad candidate keeps the last
/// valid version; state survives a compatible reload and is cleaned up when identity or kind goes; a reload
/// commits no draft, replays no command and resets no model; and a held hot control is released rather than
/// left behind.
/// </para>
/// <para>
/// Everything here is harness evidence over the stub game surface. It says nothing about a real window: no
/// lane can show that an editor's save event arrives in a running game, or that the real IMGUI hot-control
/// release path behaves like the stub's. That half is <c>docs/development/0.5/40-verification.md</c>'s.
/// </para>
/// </summary>
internal static class KernelDocumentReloadTests
{
    private const string Scope = "documents-lane";

    private static int failures;

    public static int RunAll()
    {
        failures = 0;
        string sandbox = Path.Combine(Path.GetTempPath(), "ferritelib-documents-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sandbox);

        try
        {
            Run("An edited file updates its own host and leaves an unrelated host untouched", () => VerifyScopedUpdate(sandbox));
            Run("A malformed candidate is refused and the previous tree still draws (last-known-good)", () => VerifyLastKnownGood(sandbox));
            Run("A structurally broken style file keeps the previous valid document", () => VerifyStyleLastKnownGood(sandbox));
            Run("The first load falls back to the embedded text only when no file exists", () => VerifyEmbeddedFallback(sandbox));
            Run("A batch whose affected host refuses keeps every host on the old version", () => VerifyBatchAtomicity(sandbox));
            Run("Manual reload recovers after the file is fixed", () => VerifyManualReloadRecovery(sandbox));
            Run("A compatible reload keeps node state; a removed or re-kindded element is cleaned up", () => VerifyStateCarryOver(sandbox));
            Run("A reload writes no draft, replays no command and resets no model", () => VerifyNoSideEffects(sandbox));
            Run("A reload releases a held hot control and keeps the draft", () => VerifyHotControlReleased(sandbox));
            Run("Repeated notifications coalesce and an unchanged version is skipped", () => VerifyCoalescingAndDedup(sandbox));
            Run("Failures are reported once per refusing version", () => VerifyFailureDedup(sandbox));
            Run("Automatic watching is configurable and a real file write reaches the host", () => VerifyWatching(sandbox));
            Run("A watcher whose directory appeared later is re-armed on every entry point", () => VerifyWatchReArm(sandbox));
            Run("A shared style batch is pre-checked across hosts and rolled back, never half-updated", () => VerifyStyleBatchAtomicity(sandbox));
            Run("A consumer re-tint survives a reload; a page level the document drops is undone", () => VerifyConsumerRetintAcrossReload(sandbox));
            Run("A rolled-back batch restores the old document AND the interaction state it held", () => VerifyRolledBackBatchKeepsInteractionState(sandbox));
            Run("A transiently missing file keeps the last known good instead of the embedded text", () => VerifyTransientlyMissingFileKeepsLastKnownGood(sandbox));
            Run("First style attach runs the same validate-and-apply rule as a reload", () => VerifyFirstStyleAttachValidatesLikeReload(sandbox));
            Run("Rebinding a host to another service releases the old dependency", () => VerifyRebindingReleasesTheOldService(sandbox));
            Run("An oversized document is refused and keeps the last known good", () => VerifyOversizedDocumentKeepsLastKnownGood(sandbox));
            Run("A BOM-prefixed document parses exactly like its BOM-less equivalent", () => VerifyBomInputParsesLikePlain(sandbox));
            Run("A host disposed between stage and seal leaves no leak, no crash and no half-commit", () => VerifyDisposedHostBetweenStageAndSeal(sandbox));
            Run("The document path reads one snapshot and stays backend-free", VerifySourceWiring);
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

    // --- acceptance (a): one edit, one affected host --------------------------------------------

    private static void VerifyScopedUpdate(string sandbox)
    {
        string dir = NewDir(sandbox, "scoped");
        string pathA = Path.Combine(dir, "a.xml");
        string pathB = Path.Combine(dir, "b.xml");
        string pageA = KeepPage("scope-a");
        string pageB = KeepPage("scope-b");
        File.WriteAllText(pathA, pageA);
        File.WriteAllText(pathB, pageB);

        using var service = NewService(false);
        Check(service.Add(new UiDocumentSource("a", UiDocumentKind.Layout, pathA), pageA), "the first layout source registers");
        Check(service.Add(new UiDocumentSource("b", UiDocumentKind.Layout, pathB), pageB), "the second layout source registers");

        using var hostA = NewHost("scope-a", UiLayoutManifest.Parse(pageA), new UiBindings());
        using var hostB = NewHost("scope-b", UiLayoutManifest.Parse(pageB), new UiBindings());
        Check(service.Attach(hostA, "a"), "host A attaches to document a");
        Check(service.Attach(hostB, "b"), "host B attaches to document b");
        Check(service.DependencyCount == 2, "the service tracks one dependency per host");

        Draw(hostA);
        Draw(hostB);
        UiLayoutManifest aBefore = hostA.Manifest;
        UiLayoutManifest bBefore = hostB.Manifest;

        string pageA2 = Page("scope-a",
            "<Column Id=\"root\">"
            + "<Widget Id=\"keep\" Kind=\"chrome/banner\" Text=\"keep\"/>"
            + "<Widget Id=\"added\" Kind=\"chrome/banner\" Text=\"added\"/>"
            + "</Column>");
        File.WriteAllText(pathA, pageA2);

        Check(service.Signal("a"), "a watcher-shaped signal is accepted");
        Check(Settle(service), "the pump commits the changed document");
        Check(!ReferenceEquals(hostA.Manifest, aBefore) && HasElement(hostA.Manifest, "added"),
            "the affected host now carries the new tree");
        Check(ReferenceEquals(hostB.Manifest, bBefore), "the unrelated host's manifest object was never touched");

        UiLayoutSnapshot snapshotA = Arrange(hostA);
        UiLayoutSnapshot snapshotB = Arrange(hostB);
        Check(Visible(snapshotA, "root/added"), "the new element reaches the arranged snapshot");
        Check(!Visible(snapshotB, "root/added"), "the unrelated host's snapshot never grows the element");
    }

    // --- acceptance (b): last-known-good ---------------------------------------------------------

    private static void VerifyLastKnownGood(string sandbox)
    {
        string dir = NewDir(sandbox, "lkg");
        string path = Path.Combine(dir, "page.xml");
        string good = KeepPage("scope-lkg");
        File.WriteAllText(path, good);

        using var service = NewService(false);
        service.Add(new UiDocumentSource("l", UiDocumentKind.Layout, path), good);
        using var host = NewHost("scope-lkg", UiLayoutManifest.Parse(good), new UiBindings());
        service.Attach(host, "l");
        Draw(host);
        UiLayoutManifest before = host.Manifest;

        File.WriteAllText(path, "<UiPage Schema=\"2\" Source=\"scope-lkg\"><Column Id=\"root\">");
        service.Signal("l");
        Settle(service);

        UiReloadReport? refused = FindReport(service, "l");
        Check(refused != null && refused.Rejected, "a malformed candidate is refused");
        Check(refused != null && refused.Path.Length > 0 && refused.Reason.Length > 0,
            "the refusal names the file and the reason");
        Check(ReferenceEquals(host.Manifest, before), "the host keeps the last valid manifest object");

        UiLayoutSnapshot snapshot = Arrange(host);
        Check(Visible(snapshot, "root/keep"), "the previous tree still draws after the refusal");
    }

    private static void VerifyStyleLastKnownGood(string sandbox)
    {
        string dir = NewDir(sandbox, "lkg-style");
        string path = Path.Combine(dir, "theme.xml");
        const string good = "<Styles Schema=\"1\"><Scheme Name=\"ice\"><Color Token=\"Panel\" Value=\"#0000ff\"/></Scheme></Styles>";
        File.WriteAllText(path, good);

        using var service = NewService(false);
        service.Add(new UiDocumentSource("s", UiDocumentKind.Style, path), good);
        using var host = NewHost("scope-style", UiLayoutManifest.Parse(KeepPage("scope-style")), new UiBindings());
        service.Attach(host, null, "s");
        Check(host.StyleResolver.Document.SchemeNames.Count == 1, "the valid style document reached the host");

        File.WriteAllText(path, "<NotAStyleDocument/>");
        service.Signal("s");
        Settle(service);

        UiReloadReport? refused = FindReport(service, "s");
        Check(refused != null && refused.Rejected, "a structurally broken style file is refused as a version");
        Check(host.StyleResolver.Document.SchemeNames.Count == 1
            && ContainsName(host.StyleResolver.Document.SchemeNames, "ice"),
            "the previous valid style document is still the one the host resolves against");
    }

    // --- last-known-good, first load -------------------------------------------------------------

    private static void VerifyEmbeddedFallback(string sandbox)
    {
        string dir = NewDir(sandbox, "embedded");
        string missing = Path.Combine(dir, "not-written-yet.xml");
        string fallback = KeepPage("scope-embed");

        using var service = NewService(false);
        Check(service.Add(new UiDocumentSource("e", UiDocumentKind.Layout, missing), fallback),
            "a source with no file on disk still registers");
        using var host = NewHost("scope-embed", UiLayoutManifest.Parse(fallback), new UiBindings());
        service.Attach(host, "e");
        Check(HasElement(host.Manifest, "keep"), "the embedded fallback is what the page starts on");
        Check(service.LastReport == null, "a missing file with a valid fallback is not a failure");

        // The external file is the preferred origin the moment it exists.
        string external = Page("scope-embed",
            "<Column Id=\"root\">"
            + "<Widget Id=\"keep\" Kind=\"chrome/banner\" Text=\"keep\"/>"
            + "<Widget Id=\"from-file\" Kind=\"chrome/banner\" Text=\"file\"/>"
            + "</Column>");
        File.WriteAllText(missing, external);
        UiReloadReport? report = service.Reload("e");
        Check(report != null && report.Accepted, "the external file is read once it exists");
        Check(HasElement(host.Manifest, "from-file"), "and its tree wins over the embedded fallback");
    }

    // --- acceptance (c): batch atomicity ---------------------------------------------------------

    private static void VerifyBatchAtomicity(string sandbox)
    {
        string dir = NewDir(sandbox, "batch");
        string sharedPath = Path.Combine(dir, "shared.xml");
        string otherPath = Path.Combine(dir, "other.xml");
        string shared = KeepPage("scope-shared");
        string other = KeepPage("scope-other");
        File.WriteAllText(sharedPath, shared);
        File.WriteAllText(otherPath, other);

        using var service = NewService(false);
        service.Add(new UiDocumentSource("shared", UiDocumentKind.Layout, sharedPath), shared);
        service.Add(new UiDocumentSource("other", UiDocumentKind.Layout, otherPath), other);

        var bindingsA = new UiBindings();
        bindingsA.BindCommand("apply", () => { });
        var bindingsB = new UiBindings();

        using var hostA = NewHost("host-a", UiLayoutManifest.Parse(shared), bindingsA);
        using var hostB = NewHost("host-b", UiLayoutManifest.Parse(shared), bindingsB);
        using var hostC = NewHost("host-c", UiLayoutManifest.Parse(other), new UiBindings());
        service.Attach(hostA, "shared");
        service.Attach(hostB, "shared");
        service.Attach(hostC, "other");
        Draw(hostA);
        Draw(hostB);
        Draw(hostC);

        UiLayoutManifest aBefore = hostA.Manifest;
        UiLayoutManifest bBefore = hostB.Manifest;
        UiLayoutManifest cBefore = hostC.Manifest;

        string sharedV2 = Page("scope-shared",
            "<Column Id=\"root\">"
            + "<Widget Id=\"keep\" Kind=\"chrome/banner\" Text=\"keep\"/>"
            + "<Widget Id=\"go\" Kind=\"input/button\" Text=\"go\" ActionBind=\"apply\"/>"
            + "</Column>");
        string otherV2 = Page("scope-other",
            "<Column Id=\"root\">"
            + "<Widget Id=\"keep\" Kind=\"chrome/banner\" Text=\"keep\"/>"
            + "<Widget Id=\"other-added\" Kind=\"chrome/banner\" Text=\"other\"/>"
            + "</Column>");
        File.WriteAllText(sharedPath, sharedV2);
        File.WriteAllText(otherPath, otherV2);

        // Pin the real per-host pre-check directly: the batch's refusal must come from this call, not from
        // anything wrapped around it. Hollowing TryPrepareLayoutCandidate reddens here as well as below.
        UiLayoutManifest sharedCandidate = UiLayoutManifest.Parse(sharedV2);
        Check(!hostB.TryPrepareLayoutCandidate(sharedCandidate, out string directElement, out string directReason),
            "host B's real pre-check refuses the candidate that names an unbound command");
        Check(directElement.IndexOf("go", StringComparison.Ordinal) >= 0
            && directReason.IndexOf("apply", StringComparison.Ordinal) >= 0,
            "and it names the element and the binding that stopped it");
        Check(hostA.TryPrepareLayoutCandidate(sharedCandidate, out _, out _),
            "host A's real pre-check accepts the same candidate");

        service.Signal("shared");
        service.Signal("other");
        Settle(service);

        UiReloadReport? sharedReport = FindReport(service, "shared");
        Check(sharedReport != null && sharedReport.Rejected,
            "the shared batch is refused because one of its hosts cannot draw the candidate");
        Check(sharedReport != null && sharedReport.Element.Length > 0,
            "the refusal names the element the contract stopped at");
        Check(sharedReport != null && sharedReport.Reason.IndexOf("host-b", StringComparison.Ordinal) >= 0,
            "and names the host that refused it");
        Check(ReferenceEquals(hostA.Manifest, aBefore), "host A - which could have drawn it - keeps the old version");
        Check(ReferenceEquals(hostB.Manifest, bBefore), "host B keeps the old version");
        Check(!ReferenceEquals(hostC.Manifest, cBefore) && HasElement(hostC.Manifest, "other-added"),
            "the unrelated document's batch commits in the same pump");

        // The failing half was host B's missing command binding; binding it makes the candidate valid, and
        // the manual path - the recovery entry point - is what commits it.
        bindingsB.BindCommand("apply", () => { });
        UiReloadReport? recovered = service.Reload("shared");
        Check(recovered != null && recovered.Accepted && recovered.HostsCommitted == 2,
            "manual reload commits the same batch to both hosts once every host accepts it");
        Check(HasElement(hostA.Manifest, "go") && HasElement(hostB.Manifest, "go"),
            "and both hosts are on the new tree together");
    }

    // --- acceptance (d): manual recovery ---------------------------------------------------------

    private static void VerifyManualReloadRecovery(string sandbox)
    {
        string dir = NewDir(sandbox, "manual");
        string path = Path.Combine(dir, "page.xml");
        string good = KeepPage("scope-manual");
        File.WriteAllText(path, good);

        using var service = NewService(false);
        service.Add(new UiDocumentSource("m", UiDocumentKind.Layout, path), good);
        using var host = NewHost("scope-manual", UiLayoutManifest.Parse(good), new UiBindings());
        service.Attach(host, "m");
        Draw(host);

        File.WriteAllText(path, "<UiPage Schema=\"2\" Source=\"scope-manual\"><Column>");
        service.Signal("m");
        Settle(service);
        Check(FindReport(service, "m")?.Rejected == true, "the broken file is refused");

        string fixedPage = Page("scope-manual",
            "<Column Id=\"root\">"
            + "<Widget Id=\"keep\" Kind=\"chrome/banner\" Text=\"keep\"/>"
            + "<Widget Id=\"recovered\" Kind=\"chrome/banner\" Text=\"recovered\"/>"
            + "</Column>");
        File.WriteAllText(path, fixedPage);

        UiReloadReport? report = service.Reload("m");
        Check(report != null && report.Accepted && report.HostsCommitted == 1,
            "manual reload recovers after the file is fixed");
        Check(HasElement(host.Manifest, "recovered"), "and the host follows the fixed file");
        Check(Visible(Arrange(host), "root/recovered"), "the recovered tree draws");
    }

    // --- state across a reload -------------------------------------------------------------------

    private static void VerifyStateCarryOver(string sandbox)
    {
        string dir = NewDir(sandbox, "state");
        string path = Path.Combine(dir, "page.xml");
        string v1 = ScrollPage("scope-state", "");
        File.WriteAllText(path, v1);

        using var service = NewService(false);
        service.Add(new UiDocumentSource("st", UiDocumentKind.Layout, path), v1);
        using var host = NewHost("scope-state", UiLayoutManifest.Parse(v1), new UiBindings());
        service.Attach(host, "st");
        Draw(host);

        UiNode? keep = host.Session.GetNodeByElementId("keep");
        UiNode? scroll = host.Session.GetNodeByElementId("body");
        Check(keep != null && scroll != null, "the arrange published the nodes state hangs on");

        keep!.State.EditText = "draft";
        keep.State.Cursor = 4;
        keep.State.Focused = true;
        keep.State.FloatValue = 3f;
        keep.GetOrCreateState("extra").EditText = "slot";
        host.Session.SetScrollPosition(scroll!, new Vector2(0f, 12f));

        string v2 = ScrollPage("scope-state", "<Widget Id=\"added\" Kind=\"chrome/banner\" Text=\"added\"/>");
        File.WriteAllText(path, v2);
        service.Signal("st");
        Settle(service);
        Check(service.LastReport?.Accepted == true, "the compatible reload commits");
        Draw(host);

        UiNode? keepAfter = host.Session.GetNodeByElementId("keep");
        Check(ReferenceEquals(keep, keepAfter), "a compatible reload keeps the same node object for a stable identity");
        Check(keepAfter!.State.EditText == "draft" && keepAfter.State.Cursor == 4
            && keepAfter.State.Focused && Math.Abs(keepAfter.State.FloatValue - 3f) < 0.001f,
            "the draft, cursor, focus and value state all survive");
        Check(keepAfter.GetOrCreateState("extra").EditText == "slot", "a named state slot survives too");
        Vector2 survived = host.Session.GetScrollPosition(scroll!);
        Check(Math.Abs(survived.x) < 0.001f && Math.Abs(survived.y - 12f) < 0.001f, "the scroll position survives");

        // Kind changed under the same identity: the node is the same one, the control is not, so the state
        // belongs to nobody and must be dropped rather than handed to the new kind.
        keepAfter!.State.EditText = "draft-2";
        keepAfter.State.Cursor = 2;
        string v3 = ScrollPage("scope-state", "", keepElement: "<Widget Id=\"keep\" Kind=\"chrome/rule\"/>");
        File.WriteAllText(path, v3);
        service.Signal("st");
        Settle(service);
        Draw(host);
        UiNode? rekinded = host.Session.GetNodeByElementId("keep");
        Check(rekinded != null && ReferenceEquals(rekinded, keep),
            "the kind change keeps the node the stable identity names");
        Check(rekinded != null && rekinded.State.EditText.Length == 0 && rekinded.State.Cursor == 0,
            "a kind change under a stable identity cleans the old element's state");
        Check(rekinded != null && rekinded.GetOrCreateState("extra").EditText.Length == 0,
            "and the slots the old kind used");

        // Removed: identity is gone, so the old state must not linger for a future element to inherit.
        rekinded!.State.EditText = "draft-3";
        rekinded.State.Cursor = 3;
        string v4 = ScrollPage("scope-state", "", keepElement: "");
        File.WriteAllText(path, v4);
        service.Signal("st");
        Settle(service);
        Draw(host);
        Check(!HasElement(host.Manifest, "keep"), "the element is gone from the new tree");
        Check(rekinded.State.EditText.Length == 0 && rekinded.State.Cursor == 0
            && !rekinded.State.Focused && !rekinded.State.Dragging
            && Math.Abs(rekinded.State.FloatValue) < 0.001f,
            "a removed element's state is cleaned up");
        Check(rekinded.GetOrCreateState("extra").EditText.Length == 0, "and its named slots with it");

        // A removed scroll container's position is state as well, and it is cleaned up with the node.
        string v5 = Page("scope-state",
            "<Column Id=\"root\"><Widget Id=\"added\" Kind=\"chrome/banner\" Text=\"added\"/></Column>");
        File.WriteAllText(path, v5);
        service.Signal("st");
        Settle(service);
        Draw(host);
        Vector2 droppedScroll = host.Session.GetScrollPosition(scroll!);
        Check(Math.Abs(droppedScroll.x) < 0.001f && Math.Abs(droppedScroll.y) < 0.001f,
            "a removed scroll container's position is cleaned up too");
    }

    private static void VerifyNoSideEffects(string sandbox)
    {
        string dir = NewDir(sandbox, "side-effects");
        string path = Path.Combine(dir, "page.xml");
        string v1 = ModelPage();
        File.WriteAllText(path, v1);

        var model = new Model();
        var bindings = new UiBindings();
        bindings.BindValue<string>("draft", () => model.Draft, value =>
        {
            model.Writes++;
            throw new InvalidOperationException("a reload must not write the model");
        });
        bindings.BindCommand("apply", () =>
        {
            model.Commands++;
            throw new InvalidOperationException("a reload must not replay a command");
        });

        using var service = NewService(false);
        service.Add(new UiDocumentSource("se", UiDocumentKind.Layout, path), v1);
        using var host = NewHost("scope-side", UiLayoutManifest.Parse(v1), bindings);
        service.Attach(host, "se");
        Draw(host);

        string v2 = ModelPage().Replace("Text=\"field\"", "Text=\"field-2\"");
        File.WriteAllText(path, v2);
        service.Signal("se");
        Settle(service);
        Check(service.LastReport?.Accepted == true, "the reload committed");
        Check(model.Draft == "original", "a reload leaves the business model exactly as it was");
        Check(model.Writes == 0, "a reload never calls a value setter");
        Check(model.Commands == 0, "a reload never replays a command");
        Draw(host);
        Check(model.Commands == 0, "and neither does the frame that follows it");
    }

    private static void VerifyHotControlReleased(string sandbox)
    {
        string dir = NewDir(sandbox, "hot-control");
        string path = Path.Combine(dir, "page.xml");
        string v1 = KeepPage("scope-hot");
        File.WriteAllText(path, v1);

        using var service = NewService(false);
        service.Add(new UiDocumentSource("h", UiDocumentKind.Layout, path), v1);
        using var host = NewHost("scope-hot", UiLayoutManifest.Parse(v1), new UiBindings());
        service.Attach(host, "h");
        Draw(host);

        UiNode? keep = host.Session.GetNodeByElementId("keep");
        host.Session.CaptureHotControl(4242);
        keep!.State.EditText = "mid-edit";
        Check(host.Session.OwnedHotControl == 4242, "the session holds the capture an active edit would hold");

        string v2 = Page("scope-hot",
            "<Column Id=\"root\">"
            + "<Widget Id=\"keep\" Kind=\"chrome/banner\" Text=\"keep\"/>"
            + "<Widget Id=\"added\" Kind=\"chrome/banner\" Text=\"added\"/>"
            + "</Column>");
        File.WriteAllText(path, v2);
        service.Signal("h");
        Settle(service);

        Check(host.Session.OwnedHotControl == null, "a reload releases the hot control instead of leaving it held");
        Check(keep.State.EditText == "mid-edit", "and keeps the compatible draft");
    }

    private static void VerifyCoalescingAndDedup(string sandbox)
    {
        string dir = NewDir(sandbox, "coalesce");
        string path = Path.Combine(dir, "page.xml");
        string v1 = KeepPage("scope-coalesce");
        File.WriteAllText(path, v1);

        using var service = NewService(false);
        service.Add(new UiDocumentSource("c", UiDocumentKind.Layout, path), v1);
        using var host = NewHost("scope-coalesce", UiLayoutManifest.Parse(v1), new UiBindings());
        service.Attach(host, "c");
        Draw(host);

        int reportsBefore = service.Reports.Count;
        string v2 = Page("scope-coalesce",
            "<Column Id=\"root\">"
            + "<Widget Id=\"keep\" Kind=\"chrome/banner\" Text=\"keep\"/>"
            + "<Widget Id=\"added\" Kind=\"chrome/banner\" Text=\"added\"/>"
            + "</Column>");
        File.WriteAllText(path, v2);
        for (int i = 0; i < 5; i++)
        {
            service.Signal("c");
        }

        Check(service.HasPending, "pending work is visible before the pump");
        Check(Settle(service), "five notifications for one save commit once");
        Check(service.Reports.Count == reportsBefore + 1, "and produce exactly one report");
        Check(HasElement(host.Manifest, "added"), "the committed tree is the new one");

        UiReloadReport? skipped = service.Reload("c");
        Check(skipped != null && skipped.Skipped && !skipped.Accepted,
            "an unchanged version is skipped rather than committed again");
        Check(service.Reports.Count == reportsBefore + 1, "a skip is not a new report");
        Check(!service.Signal("missing"), "a signal for an unknown document is refused");
    }

    private static void VerifyFailureDedup(string sandbox)
    {
        string dir = NewDir(sandbox, "dedup");
        string path = Path.Combine(dir, "page.xml");
        File.WriteAllText(path, KeepPage("scope-dedup"));

        using var service = NewService(false);
        service.Add(new UiDocumentSource("d", UiDocumentKind.Layout, path), KeepPage("scope-dedup"));
        using var host = NewHost("scope-dedup", UiLayoutManifest.Parse(KeepPage("scope-dedup")), new UiBindings());
        service.Attach(host, "d");
        Draw(host);

        int before = service.Reports.Count;
        File.WriteAllText(path, "<UiPage Schema=\"2\" Source=\"scope-dedup\">");
        service.Signal("d");
        Settle(service);
        UiReloadReport? first = service.LastReport;
        Check(first != null && first.Rejected && !first.Duplicate, "the first failure of a version is reported");
        Check(service.Reports.Count == before + 1, "and recorded once");

        UiReloadReport? again = service.Reload("d");
        Check(again != null && again.Rejected && again.Duplicate, "the same refusing version is reported as a duplicate");
        Check(service.Reports.Count == before + 1, "a duplicate adds no report");

        File.WriteAllText(path, "<UiPage Schema=\"2\" Source=\"scope-dedup\"><Row>");
        service.Signal("d");
        Settle(service);
        Check(service.Reports.Count == before + 2, "a different broken version is a new failure");

        string fixedPage = Page("scope-dedup",
            "<Column Id=\"root\">"
            + "<Widget Id=\"keep\" Kind=\"chrome/banner\" Text=\"keep\"/>"
            + "<Widget Id=\"fixed\" Kind=\"chrome/banner\" Text=\"fixed\"/>"
            + "</Column>");
        File.WriteAllText(path, fixedPage);
        Check(service.Reload("d")?.Accepted == true, "and a fixed file is accepted again");
    }

    // --- watching --------------------------------------------------------------------------------

    private static void VerifyWatching(string sandbox)
    {
#if FER_DEV
        Check(UiDocumentService.DefaultAutoWatch, "a dev build turns automatic watching on by default");
#else
        Check(!UiDocumentService.DefaultAutoWatch, "a release build leaves automatic watching off by default");
#endif

        string dir = NewDir(sandbox, "watch");
        string path = Path.Combine(dir, "page.xml");
        string v1 = KeepPage("scope-watch");
        File.WriteAllText(path, v1);

        using (var toggled = NewService(false))
        {
            Check(!toggled.AutoWatch, "the constructor override turns watching off");
            toggled.Add(new UiDocumentSource("w", UiDocumentKind.Layout, path), v1);
            Check(!toggled.IsWatching("w"), "watching off arms no watcher");
            toggled.AutoWatch = true;
            Check(toggled.IsWatching("w"), "turning AutoWatch on arms a watcher for an already-registered document");
            toggled.AutoWatch = false;
            Check(!toggled.IsWatching("w"), "turning AutoWatch off disarms it again");
        }

        using var byDefault = NewService();
        Check(byDefault.AutoWatch == UiDocumentService.DefaultAutoWatch, "an unconfigured service follows the build default");

        string dir2 = NewDir(sandbox, "watch-live");
        string live = Path.Combine(dir2, "live.xml");
        File.WriteAllText(live, v1);

        using var service = NewService(true);
        service.Add(new UiDocumentSource("live", UiDocumentKind.Layout, live), v1);
        using var host = NewHost("scope-watch", UiLayoutManifest.Parse(v1), new UiBindings());
        service.Attach(host, "live");
        Draw(host);
        Check(service.IsWatching("live"), "watching on arms the real watcher");

        string v2 = Page("scope-watch",
            "<Column Id=\"root\">"
            + "<Widget Id=\"keep\" Kind=\"chrome/banner\" Text=\"keep\"/>"
            + "<Widget Id=\"added\" Kind=\"chrome/banner\" Text=\"added\"/>"
            + "</Column>");
        File.WriteAllText(live, v2);

        // The watcher is asynchronous by nature; the lane waits for the signal it produces instead of
        // assuming a latency. A dropped signal is the case Reload covers, not this one.
        bool committed = false;
        for (int attempt = 0; attempt < 200 && !committed; attempt++)
        {
            committed = Settle(service);
            if (!committed)
            {
                System.Threading.Thread.Sleep(25);
            }
        }

        Check(committed, "a real file write reaches the service through the armed watcher");
        Check(HasElement(host.Manifest, "added"), "and the host draws the edited tree");
    }

    // --- re-arm after a directory that did not exist yet -----------------------------------------

    /// <summary>
    /// The adversarial probe this lane answers: a registered file whose parent directory does not exist yet
    /// used to be a permanent silent no-op, because the setter early-returned on an unchanged value and the
    /// watch attempt returned when the directory was missing. A first run whose <c>Layouts/</c> folder is
    /// created later must pick the watcher up on every entry point.
    /// </summary>
    private static void VerifyWatchReArm(string sandbox)
    {
        string v1 = KeepPage("scope-rearm");

        string pathA = Path.Combine(sandbox, "rearm-a", "page.xml");
        using (var viaSetter = NewService(true))
        {
            Check(viaSetter.Add(new UiDocumentSource("a", UiDocumentKind.Layout, pathA), v1),
                "a source under a directory that does not exist yet still registers");
            Check(!viaSetter.IsWatching("a"), "and stays unwatched while the directory is missing");
            Directory.CreateDirectory(Path.Combine(sandbox, "rearm-a"));
            viaSetter.AutoWatch = true;
            Check(viaSetter.IsWatching("a"), "setting AutoWatch to its current value re-arms once the directory exists");
        }

        string pathB = Path.Combine(sandbox, "rearm-b", "page.xml");
        using (var viaPump = NewService(true))
        {
            viaPump.Add(new UiDocumentSource("b", UiDocumentKind.Layout, pathB), v1);
            Check(!viaPump.IsWatching("b"), "the second source starts unwatched for the same reason");
            Directory.CreateDirectory(Path.Combine(sandbox, "rearm-b"));
            viaPump.Pump();
            Check(viaPump.IsWatching("b"), "the frame-boundary pump re-attempts the arming without the caller touching AutoWatch");
        }

        string pathC = Path.Combine(sandbox, "rearm-c", "page.xml");
        using (var viaReload = NewService(true))
        {
            viaReload.Add(new UiDocumentSource("c", UiDocumentKind.Layout, pathC), v1);
            Check(!viaReload.IsWatching("c"), "the third source starts unwatched too");
            Directory.CreateDirectory(Path.Combine(sandbox, "rearm-c"));
            viaReload.Reload("c");
            Check(viaReload.IsWatching("c"), "the manual reload path re-attempts the arming as well");
        }
    }

    // --- style batches ---------------------------------------------------------------------------

    /// <summary>
    /// A shared style document with two hosts is a batch like any other, and both halves are pinned against
    /// the real code rather than a planted fault: the pre-check has no seam in front of it and refuses a
    /// candidate whose own page level names a scheme or density the document does not declare. The commit
    /// half still uses the instance seam, because a production commit cannot fail after every host
    /// pre-checked the candidate.
    /// </summary>
    private static void VerifyStyleBatchAtomicity(string sandbox)
    {
        string dir = NewDir(sandbox, "style-batch");
        string path = Path.Combine(dir, "theme.xml");
        string v1 = PageSchemeStyle("ice", "#0000ff");
        File.WriteAllText(path, v1);

        using var service = NewService(false);
        service.Add(new UiDocumentSource("shared-style", UiDocumentKind.Style, path), v1);
        UiTheme themeA = UiTheme.DarkGold.Clone();
        UiTheme themeB = UiTheme.DarkGold.Clone();
        using var hostA = NewHost("style-a", UiLayoutManifest.Parse(KeepPage("style-a")), new UiBindings(), themeA);
        using var hostB = NewHost("style-b", UiLayoutManifest.Parse(KeepPage("style-b")), new UiBindings(), themeB);
        service.Attach(hostA, null, "shared-style");
        service.Attach(hostB, null, "shared-style");

        UiStyleDocument beforeA = hostA.StyleResolver.Document;
        UiStyleDocument beforeB = hostB.StyleResolver.Document;
        Check(SameColor(themeA.Panel, new Color(0f, 0f, 1f, 1f)),
            "the first style document's page level reached host A");
        Check(SameColor(themeB.Panel, new Color(0f, 0f, 1f, 1f)),
            "and reached host B");

        // The real pre-check, driven directly with candidates that really fail: a page level naming a name
        // the document never declares can never take effect, so the version is refused instead of being
        // applied as a silent no-op. No seam stands in front of this call.
        const string unknownScheme =
            "<Styles Schema=\"1\" Scheme=\"ghost\"><Scheme Name=\"ice\"><Color Token=\"Panel\" Value=\"#00ff00\"/></Scheme></Styles>";
        const string unknownDensity =
            "<Styles Schema=\"1\" Density=\"ghost\"><Density Name=\"compact\"><Metric Token=\"Gap\" Value=\"2\"/></Density></Styles>";
        Check(!hostA.TryPrepareStyleCandidate(UiStyleDocument.Parse(unknownScheme), out _, out string schemeReason)
            && schemeReason.IndexOf("ghost", StringComparison.Ordinal) >= 0,
            "the real style pre-check refuses a page level naming a scheme the document does not declare");
        Check(!hostA.TryPrepareStyleCandidate(UiStyleDocument.Parse(unknownDensity), out _, out string densityReason)
            && densityReason.IndexOf("ghost", StringComparison.Ordinal) >= 0,
            "and the same for a page-level density");
        Check(hostA.TryPrepareStyleCandidate(UiStyleDocument.Parse(PageSchemeStyle("ice", "#00ff00")), out _, out _),
            "while a resolvable page level passes the same pre-check");

        File.WriteAllText(path, unknownScheme);
        UiReloadReport? preCheckRefusal = service.Reload("shared-style");
        Check(preCheckRefusal != null && preCheckRefusal.Rejected && preCheckRefusal.HostsCommitted == 0,
            "a style batch whose candidate fails the real pre-check is refused before anything commits");
        Check(preCheckRefusal != null
            && preCheckRefusal.Reason.IndexOf("style-a", StringComparison.Ordinal) >= 0
            && preCheckRefusal.Reason.IndexOf("ghost", StringComparison.Ordinal) >= 0,
            "and the refusal names the host and the unresolvable page level");
        Check(ReferenceEquals(hostA.StyleResolver.Document, beforeA)
            && ReferenceEquals(hostB.StyleResolver.Document, beforeB),
            "a real pre-check failure leaves both hosts of the style batch on the old document");

        // The commit half: a good candidate, with the second host's commit forced to fail.
        File.WriteAllText(path, PageSchemeStyle("ice", "#00ff00"));
        service.DocumentCommitFaultOverride = host =>
            string.Equals(host.Source, "style-b", StringComparison.Ordinal) ? "planted style commit failure" : "";
        UiReloadReport? commitRefusal = service.Reload("shared-style");
        service.DocumentCommitFaultOverride = null;
        Check(commitRefusal != null && commitRefusal.Rejected && commitRefusal.HostsCommitted == 0,
            "a style batch whose later commit fails is refused rather than half-applied");
        Check(commitRefusal != null && commitRefusal.Reason.IndexOf("rolled back", StringComparison.Ordinal) >= 0,
            "and the refusal says the batch was rolled back");
        Check(ReferenceEquals(hostA.StyleResolver.Document, beforeA)
            && ReferenceEquals(hostB.StyleResolver.Document, beforeB),
            "the already-committed host is rolled back to the old document");

        UiReloadReport? accepted = service.Reload("shared-style");
        Check(accepted != null && accepted.Accepted && accepted.HostsCommitted == 2,
            "once no host fails, the same style batch commits to both");
        Check(!ReferenceEquals(hostA.StyleResolver.Document, beforeA)
            && !ReferenceEquals(hostB.StyleResolver.Document, beforeB)
            && SameColor(themeA.Panel, new Color(0f, 1f, 0f, 1f))
            && SameColor(themeB.Panel, new Color(0f, 1f, 0f, 1f)),
            "and both hosts resolve the new page level together");
    }

    // --- the theme baseline ----------------------------------------------------------------------

    /// <summary>
    /// The adversarial probe that found <c>RestoreStyleBaseline</c> dropping a post-construction re-tint.
    /// The fix is a gate, not a promise: a document that applies no page level is not undone, so a re-tint
    /// on the theme the consumer handed in survives; a document that does apply one is still undone when
    /// the next version stops declaring it.
    /// <para>
    /// The last check pins the documented residual so the comment cannot drift from the code: the restore is
    /// not per-token, so a re-tint of a token the outgoing page level never declared is discarded by that
    /// reload too. Narrowing that would be a behaviour change - and would have to update this check.
    /// </para>
    /// </summary>
    private static void VerifyConsumerRetintAcrossReload(string sandbox)
    {
        string dir = NewDir(sandbox, "retint");
        string path = Path.Combine(dir, "page.xml");
        string v1 = KeepPage("scope-retint");
        File.WriteAllText(path, v1);

        UiTheme theme = UiTheme.DarkGold.Clone();
        using var service = NewService(false);
        service.Add(new UiDocumentSource("tint", UiDocumentKind.Layout, path), v1);
        using var host = NewHost("scope-retint", UiLayoutManifest.Parse(v1), new UiBindings(), theme);
        service.Attach(host, "tint");

        Color tint = new Color(0.5f, 0.1f, 0.1f, 1f);
        theme.Base = tint;

        File.WriteAllText(path, Page("scope-retint",
            "<Column Id=\"root\">"
            + "<Widget Id=\"keep\" Kind=\"chrome/banner\" Text=\"keep\"/>"
            + "<Widget Id=\"added\" Kind=\"chrome/banner\" Text=\"added\"/>"
            + "</Column>"));
        service.Signal("tint");
        Settle(service);

        Check(service.LastReport?.Accepted == true, "the layout reload committed");
        Check(SameColor(theme.Base, tint),
            "a consumer re-tint survives a reload whose document applies no page level");

        string stylePath = Path.Combine(dir, "theme.xml");
        string styleV1 = PageSchemeStyle("ice", "#0000ff");
        File.WriteAllText(stylePath, styleV1);

        using var styleService = NewService(false);
        styleService.Add(new UiDocumentSource("tint-style", UiDocumentKind.Style, stylePath), styleV1);
        UiTheme themed = UiTheme.DarkGold.Clone();
        Color panelBaseline = themed.Panel;
        Color baseBaseline = themed.Base;
        using var styled = NewHost("scope-retint-style", UiLayoutManifest.Parse(KeepPage("scope-retint-style")), new UiBindings(), themed);
        styleService.Attach(styled, null, "tint-style");
        Check(SameColor(themed.Panel, new Color(0f, 0f, 1f, 1f)),
            "a page-level document is applied on the first commit");

        File.WriteAllText(stylePath,
            "<Styles Schema=\"1\"><Scheme Name=\"ice\"><Color Token=\"Panel\" Value=\"#00ff00\"/></Scheme></Styles>");
        styleService.Signal("tint-style");
        Settle(styleService);
        Check(styleService.LastReport?.Accepted == true, "the style reload committed");
        Check(SameColor(themed.Panel, panelBaseline),
            "and a page level the new document no longer declares is undone");

        // The documented residual, pinned. A page level is present again, a token it never declares is
        // re-tinted, and the next reload - to a document with no page level at all - still discards the tint:
        // the restore is not tracked per token.
        File.WriteAllText(stylePath, PageSchemeStyle("ice", "#00ff00"));
        Check(styleService.Reload("tint-style")?.Accepted == true, "a page level is applied again");
        Color residualTint = new Color(0.2f, 0.4f, 0.6f, 1f);
        themed.Base = residualTint;
        File.WriteAllText(stylePath, "<Styles Schema=\"1\"></Styles>");
        Check(styleService.Reload("tint-style")?.Accepted == true, "and the next reload drops the page level");
        Check(!SameColor(themed.Base, residualTint) && SameColor(themed.Base, baseBaseline),
            "RESIDUAL (documented): a re-tint of a token the outgoing page level never declared is discarded");
    }

    // --- rollback restores interaction state, not only the tree (P4 R1) -------------------------

    /// <summary>
    /// The P1 the external review reproduced: the tree rolled back, the user's uncommitted draft did not.
    /// The fix keeps the destructive half of a commit (pruning removed/kind-changed state and releasing a
    /// held capture) out of the staging step, so it only runs once the whole batch committed. This lane
    /// asserts both halves - the old document AND the old draft, named slots and scroll position.
    /// </summary>
    private static void VerifyRolledBackBatchKeepsInteractionState(string sandbox)
    {
        string dir = NewDir(sandbox, "rollback-state");
        string path = Path.Combine(dir, "shared.xml");
        string v1 = ScrollPage("scope-rollback", "");
        File.WriteAllText(path, v1);

        using var service = NewService(false);
        service.Add(new UiDocumentSource("rb", UiDocumentKind.Layout, path), v1);
        using var hostA = NewHost("host-a", UiLayoutManifest.Parse(v1), new UiBindings());
        using var hostB = NewHost("host-b", UiLayoutManifest.Parse(v1), new UiBindings());
        service.Attach(hostA, "rb");
        service.Attach(hostB, "rb");
        Draw(hostA);

        UiNode? draft = hostA.Session.GetNodeByElementId("keep");
        UiNode? scroll = hostA.Session.GetNodeByElementId("body");
        Check(draft != null && scroll != null, "the arranged element and its scroll container exist");
        draft!.State.EditText = "uncommitted-draft";
        draft.GetOrCreateState("slot").EditText = "slot-draft";
        hostA.Session.SetScrollPosition(scroll!, new Vector2(0f, 9f));
        UiLayoutManifest before = hostA.Manifest;

        string v2 = Page("scope-rollback",
            "<Column Id=\"root\"><Widget Id=\"replacement\" Kind=\"chrome/banner\" Text=\"replacement\"/></Column>");
        File.WriteAllText(path, v2);
        service.DocumentCommitFaultOverride = host =>
            ReferenceEquals(host, hostB) ? "planted host B commit failure" : "";
        UiReloadReport? report = service.Reload("rb");
        service.DocumentCommitFaultOverride = null;

        Check(report != null && report.Rejected, "the batch is refused when a later host fails its commit");
        Check(ReferenceEquals(hostA.Manifest, before) && HasElement(hostA.Manifest, "keep"),
            "the rolled-back host is back on the old document");
        Check(draft.State.EditText == "uncommitted-draft",
            "and its uncommitted draft survived the rollback");
        Check(draft.GetOrCreateState("slot").EditText == "slot-draft", "and its named state slots");
        Vector2 scrolled = hostA.Session.GetScrollPosition(scroll!);
        Check(Math.Abs(scrolled.y - 9f) < 0.001f, "and its scroll position");

        // The same candidate, with no fault, still commits and still cleans the removed element up.
        Check(service.Reload("rb")?.Accepted == true, "the same batch commits once no host fails");
        Draw(hostA);
        Check(!HasElement(hostA.Manifest, "keep"), "a committed batch still drops the removed element");
        Check(draft.State.EditText.Length == 0, "and still cleans its state");
    }

    // --- a transiently missing file keeps the last known good (P4 R2) ---------------------------

    /// <summary>
    /// The other P1: the embedded text is the FIRST-load source. Once an external version is in force, a
    /// deleted file (or an editor moving it aside mid-save) must be a failure that keeps that version, not a
    /// silent step backwards to the embedded copy.
    /// <para>
    /// Dedup contract, pinned here and stated in <c>20-api-and-xml.md</c>: deduplication is per refusing
    /// version and lasts only as long as the failure does. A continuing absence (the same version refused
    /// again with no successful validation in between) is <c>Duplicate=true</c> and does not touch the ring;
    /// a new absence after a successful recovery is a new event again, because the version was accepted or
    /// found current in between and the recorded failure was cleared. Consumers that count events read the
    /// returned/published report either way; consumers that read <c>Reports</c>/<c>LastReport</c> see
    /// distinct failure episodes, not one entry that outlives the incident.
    /// </para>
    /// </summary>
    private static void VerifyTransientlyMissingFileKeepsLastKnownGood(string sandbox)
    {
        string dir = NewDir(sandbox, "missing-lkg");
        string path = Path.Combine(dir, "page.xml");
        string external = Page("scope-missing",
            "<Column Id=\"root\"><Widget Id=\"external\" Kind=\"chrome/banner\" Text=\"external\"/></Column>");
        string embedded = Page("scope-missing",
            "<Column Id=\"root\"><Widget Id=\"embedded\" Kind=\"chrome/banner\" Text=\"embedded\"/></Column>");
        File.WriteAllText(path, external);

        using var service = NewService(false);
        service.Add(new UiDocumentSource("m", UiDocumentKind.Layout, path), embedded);
        using var host = NewHost("scope-missing", UiLayoutManifest.Parse(embedded), new UiBindings());
        service.Attach(host, "m");
        Check(HasElement(host.Manifest, "external"), "the external file is the version in force");

        int ringBefore = service.Reports.Count;
        File.Delete(path);
        UiReloadReport? missing = service.Reload("m");
        Check(missing != null && missing.Rejected, "a transiently missing file is a failure, not a silent fallback");
        Check(missing != null && missing.Reason.IndexOf("does not exist", StringComparison.Ordinal) >= 0,
            "and the report says the file is gone");
        Check(HasElement(host.Manifest, "external") && !HasElement(host.Manifest, "embedded"),
            "the last valid external version is kept");
        Check(service.Reports.Count == ringBefore + 1 && ReferenceEquals(service.LastReport, missing),
            "the first absence is the current report in the ring");

        UiReloadReport? again = service.Reload("m");
        Check(again != null && again.Duplicate && service.Reports.Count == ringBefore + 1,
            "a continuing absence is deduplicated rather than counted again");

        File.WriteAllText(path, external);
        Check(service.Reload("m")?.Skipped == true, "the file returning with the same bytes is already in force");

        // The sharp edge, pinned: the same file disappearing again after a successful recovery is a NEW
        // event. The recorded failure version is cleared when a version is accepted or found current, so the
        // ring and LastReport move again instead of hiding the second disappearance.
        File.Delete(path);
        UiReloadReport? second = service.Reload("m");
        Check(second != null && second.Rejected && !second.Duplicate,
            "a re-break after a successful recovery is reported as a new failure");
        Check(service.Reports.Count == ringBefore + 2 && ReferenceEquals(service.LastReport, second),
            "and it updates the ring and LastReport");

        string changed = Page("scope-missing",
            "<Column Id=\"root\"><Widget Id=\"changed\" Kind=\"chrome/banner\" Text=\"changed\"/></Column>");
        File.WriteAllText(path, changed);
        Check(service.Reload("m")?.Accepted == true, "and a changed file commits normally after recovery");
        Check(HasElement(host.Manifest, "changed"), "so the window follows the file again");
    }

    // --- first style attach validates like a reload (P4 R5) --------------------------------------

    /// <summary>
    /// One file, one meaning: the initial style application runs the same pre-check a reload does, so an
    /// unresolvable page level is refused on attach too - the host keeps its own document and the refusal is
    /// reported - and a whitespace-only edit afterwards cannot flip the same file from accepted to rejected.
    /// </summary>
    private static void VerifyFirstStyleAttachValidatesLikeReload(string sandbox)
    {
        string dir = NewDir(sandbox, "style-attach");
        string path = Path.Combine(dir, "theme.xml");
        const string invalid = "<Styles Schema=\"1\" Scheme=\"undefined\"/>";
        File.WriteAllText(path, invalid);

        using var service = NewService(false);
        service.Add(new UiDocumentSource("s5", UiDocumentKind.Style, path), "<Styles Schema=\"1\"/>");
        using var host = NewHost("style-first", UiLayoutManifest.Parse(KeepPage("style-first")), new UiBindings());
        UiStyleDocument hostDocument = host.StyleResolver.Document;
        int reportsBefore = service.Reports.Count;

        Check(service.Attach(host, null, "s5"), "the host attaches to the style document");
        Check(service.Reports.Count == reportsBefore + 1,
            "the first application is reported when its page level cannot resolve");
        Check(ReferenceEquals(host.StyleResolver.Document, hostDocument),
            "and the host keeps the document it already had");

        File.WriteAllText(path, invalid + "\n");
        UiReloadReport? reload = service.Reload("s5");
        Check(reload != null && reload.Rejected, "the whitespace-only reload is rejected by the same rule");
        Check(reload != null && reload.Reason.IndexOf("undefined", StringComparison.Ordinal) >= 0,
            "and it names the unresolvable page level");

        // A valid external style document still applies on first attach.
        string valid = PageSchemeStyle("ice", "#0000ff");
        string validPath = Path.Combine(dir, "valid.xml");
        File.WriteAllText(validPath, valid);
        UiTheme theme = UiTheme.DarkGold.Clone();
        using var validService = NewService(false);
        validService.Add(new UiDocumentSource("s5v", UiDocumentKind.Style, validPath), valid);
        using var validHost = NewHost(
            "style-first-valid", UiLayoutManifest.Parse(KeepPage("style-first-valid")), new UiBindings(), theme);
        Check(validService.Attach(validHost, null, "s5v"), "a valid style document still attaches");
        Check(SameColor(theme.Panel, new Color(0f, 0f, 1f, 1f)), "and its page level is applied on first attach");
    }

    // --- rebinding releases the old service (P4 R6) ----------------------------------------------

    /// <summary>
    /// A host binds to one service. Re-binding through a second service must release the first one's
    /// dependency, or the old service keeps a disposed host until the dependency cap blocks later attaches;
    /// re-binding to the same service must not detach the dependency it just created.
    /// </summary>
    private static void VerifyRebindingReleasesTheOldService(string sandbox)
    {
        string dir = NewDir(sandbox, "rebind");
        string path = Path.Combine(dir, "page.xml");
        string v1 = KeepPage("scope-rebind");
        File.WriteAllText(path, v1);

        using var first = NewService(false);
        using var second = NewService(false);
        first.Add(new UiDocumentSource("one", UiDocumentKind.Layout, path), v1);
        second.Add(new UiDocumentSource("two", UiDocumentKind.Layout, path), v1);
        using var host = NewHost("scope-rebind", UiLayoutManifest.Parse(v1), new UiBindings());

        Check(first.Attach(host, "one"), "the host attaches to the first service");
        Check(first.DependencyCount == 1, "the first service holds the dependency");
        Check(second.Attach(host, "two"), "re-attaching through the second service succeeds");
        Check(first.DependencyCount == 0, "rebinding releases the dependency the first service held");
        Check(second.DependencyCount == 1, "and the second service holds it");

        host.Dispose();
        Check(second.DependencyCount == 0, "closing the host releases the service it is bound to");
        Check(first.DependencyCount == 0, "and leaves the old one clean");

        using var again = NewHost("scope-rebind-2", UiLayoutManifest.Parse(v1), new UiBindings());
        first.Attach(again, "one");
        first.Attach(again, "one");
        Check(first.DependencyCount == 1, "re-attaching to the same service keeps exactly one dependency");
        again.Dispose();
        Check(first.DependencyCount == 0, "and closing it releases that one");
    }

    // --- the size bound is decided by the snapshot (P4 R7) ----------------------------------------

    /// <summary>
    /// The size bound is one of the things the single snapshot decides, and it is pinnable behaviourally:
    /// a document past <see cref="UiDocumentService.MaxDocumentBytes"/> is refused before it is parsed, the
    /// version in force is untouched, and the file's return is handled normally.
    /// </summary>
    private static void VerifyOversizedDocumentKeepsLastKnownGood(string sandbox)
    {
        string dir = NewDir(sandbox, "oversized");
        string path = Path.Combine(dir, "page.xml");
        string good = KeepPage("scope-oversized");
        File.WriteAllText(path, good);

        using var service = NewService(false);
        service.Add(new UiDocumentSource("big", UiDocumentKind.Layout, path), good);
        using var host = NewHost("scope-oversized", UiLayoutManifest.Parse(good), new UiBindings());
        service.Attach(host, "big");
        Draw(host);
        UiLayoutManifest before = host.Manifest;

        // Well-formed XML padded past the bound with a comment the parser would ignore anyway: the bound is
        // what refuses it, and it refuses before any parse happens.
        string oversized = Page("scope-oversized",
            "<Column Id=\"root\"><!--" + new string('x', UiDocumentService.MaxDocumentBytes + 1024) + "-->"
            + "<Widget Id=\"big\" Kind=\"chrome/banner\" Text=\"big\"/></Column>");
        File.WriteAllText(path, oversized);
        Check(new FileInfo(path).Length > UiDocumentService.MaxDocumentBytes,
            "the fixture really is larger than the bound");

        UiReloadReport? refused = service.Reload("big");
        Check(refused != null && refused.Rejected, "an oversized document is refused");
        Check(refused != null && refused.Reason.IndexOf("bound", StringComparison.Ordinal) >= 0,
            "and the report names the size bound rather than a parse failure");
        Check(ReferenceEquals(host.Manifest, before) && HasElement(host.Manifest, "keep"),
            "the last known good is kept");
        Check(Visible(Arrange(host), "root/keep"), "and still draws");

        File.WriteAllText(path, good);
        Check(service.Reload("big")?.Skipped == true,
            "the original file returning is the version already in force");
    }

    // --- the decode is the snapshot's, and it is BOM-aware (P4 R7) --------------------------------

    /// <summary>
    /// The other behaviour the snapshot decides: how the bytes decode. A UTF-8 byte-order mark must not reach
    /// the XML parser (it would not be legal before the root element), so a BOM-prefixed file must parse to
    /// exactly the tree its BOM-less equivalent does, while the content identity stays byte-based.
    /// </summary>
    private static void VerifyBomInputParsesLikePlain(string sandbox)
    {
        string dir = NewDir(sandbox, "bom");
        string plainPath = Path.Combine(dir, "plain.xml");
        string bomPath = Path.Combine(dir, "bom.xml");
        string xml = KeepPage("scope-bom");
        File.WriteAllText(plainPath, xml);

        byte[] preamble = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetPreamble();
        byte[] body = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(xml);
        var marked = new byte[preamble.Length + body.Length];
        Array.Copy(preamble, 0, marked, 0, preamble.Length);
        Array.Copy(body, 0, marked, preamble.Length, body.Length);
        File.WriteAllBytes(bomPath, marked);
        Check(preamble.Length == 3 && marked[0] == 0xEF && marked[1] == 0xBB && marked[2] == 0xBF,
            "the fixture really carries a UTF-8 byte-order mark");

        using var plainService = NewService(false);
        plainService.Add(new UiDocumentSource("plain", UiDocumentKind.Layout, plainPath), xml);
        using var plainHost = NewHost("scope-bom-plain", UiLayoutManifest.Parse(xml), new UiBindings());
        plainService.Attach(plainHost, "plain");

        using var bomService = NewService(false);
        bomService.Add(new UiDocumentSource("bom", UiDocumentKind.Layout, bomPath), xml);
        using var bomHost = NewHost("scope-bom-marked", UiLayoutManifest.Parse(xml), new UiBindings());
        bomService.Attach(bomHost, "bom");

        Check(plainHost.Manifest.Roots.Count == bomHost.Manifest.Roots.Count
            && string.Equals(plainHost.Manifest.Roots[0].Id, bomHost.Manifest.Roots[0].Id, StringComparison.Ordinal)
            && string.Equals(plainHost.Manifest.Source, bomHost.Manifest.Source, StringComparison.Ordinal),
            "a BOM-prefixed document parses to the same tree as its BOM-less equivalent");
        Check(Visible(Arrange(plainHost), "root/keep") && Visible(Arrange(bomHost), "root/keep"),
            "and both arrange the same element");

        UiReloadReport? plainReload = plainService.Reload("plain");
        UiReloadReport? bomReload = bomService.Reload("bom");
        Check(plainReload != null && plainReload.Skipped && bomReload != null && bomReload.Skipped,
            "both files are the version in force for their service");
        Check(plainReload != null && bomReload != null
            && !string.Equals(plainReload.Version, bomReload.Version, StringComparison.Ordinal),
            "and the BOM is a different content identity even though the parsed tree is the same");
    }

    // --- a host that vanishes between stage and seal ----------------------------------------------

    /// <summary>
    /// The stage/dispose/seal ordering, which the addendum probed: a batch stages host A, host B's turn runs
    /// consumer code that disposes A, and the seal pass then runs over both. The surviving host must be on
    /// the new tree, nothing may throw, the disposed host's dependency must be released by its own Close, and
    /// the accepted report describes the batch's commit (it counts both hosts; the service's DependencyCount
    /// is the axis that says how many remain). The rollback ordering is covered too: the vanish, then the
    /// other host's commit fails, so the rollback restores a live host while the disposed one is skipped.
    /// </summary>
    private static void VerifyDisposedHostBetweenStageAndSeal(string sandbox)
    {
        string v1 = KeepPage("scope-stage-dispose");
        string v2 = Page("scope-stage-dispose",
            "<Column Id=\"root\">"
            + "<Widget Id=\"keep\" Kind=\"chrome/banner\" Text=\"keep\"/>"
            + "<Widget Id=\"added\" Kind=\"chrome/banner\" Text=\"added\"/>"
            + "</Column>");

        string dir = NewDir(sandbox, "stage-dispose");
        string path = Path.Combine(dir, "shared.xml");
        File.WriteAllText(path, v1);

        using (var service = NewService(false))
        {
            service.Add(new UiDocumentSource("sd", UiDocumentKind.Layout, path), v1);
            var vanishing = NewHost("stage-dispose-a", UiLayoutManifest.Parse(v1), new UiBindings());
            using var survivor = NewHost("stage-dispose-b", UiLayoutManifest.Parse(v1), new UiBindings());
            service.Attach(vanishing, "sd");
            service.Attach(survivor, "sd");
            Check(service.DependencyCount == 2, "both hosts hold the dependency before the batch");
            File.WriteAllText(path, v2);

            // The commit seam is the only point where the batch yields control between staging one host and
            // the seal pass, so it is where this ordering can be reproduced.
            service.DocumentCommitFaultOverride = host =>
            {
                if (ReferenceEquals(host, survivor))
                {
                    vanishing.Dispose();
                }

                return "";
            };

            UiReloadReport? report = service.Reload("sd");
            service.DocumentCommitFaultOverride = null;

            Check(report != null && report.Accepted, "the batch completes instead of crashing on the vanished host");
            Check(report != null && report.HostsAffected == 2 && report.HostsCommitted == 2,
                "the report describes the batch's commit, so it still counts both hosts");
            Check(!vanishing.Session.IsActive, "the vanished host's session is down");
            Check(survivor.Session.IsActive && HasElement(survivor.Manifest, "added"),
                "and the surviving host is on the new tree");
            Check(service.DependencyCount == 1,
                "the vanished host was released by its own close, so the service leaks nothing");
            Check(ReferenceEquals(service.LastReport, report), "and the accepted report is the current one");

            survivor.Dispose();
            Check(service.DependencyCount == 0, "closing the survivor leaves the service clean");
        }

        string dir2 = NewDir(sandbox, "stage-dispose-rollback");
        string path2 = Path.Combine(dir2, "shared.xml");
        File.WriteAllText(path2, v1);

        using (var service = NewService(false))
        {
            service.Add(new UiDocumentSource("sdr", UiDocumentKind.Layout, path2), v1);
            var vanishing = NewHost("stage-dispose-c", UiLayoutManifest.Parse(v1), new UiBindings());
            using var failing = NewHost("stage-dispose-d", UiLayoutManifest.Parse(v1), new UiBindings());
            service.Attach(vanishing, "sdr");
            service.Attach(failing, "sdr");
            File.WriteAllText(path2, v2);

            service.DocumentCommitFaultOverride = host =>
            {
                if (ReferenceEquals(host, failing))
                {
                    vanishing.Dispose();
                    return "planted commit failure after the vanish";
                }

                return "";
            };

            UiReloadReport? rolledBack = service.Reload("sdr");
            service.DocumentCommitFaultOverride = null;

            Check(rolledBack != null && rolledBack.Rejected, "the batch is refused when the surviving host fails");
            Check(failing.Session.IsActive && HasElement(failing.Manifest, "keep"),
                "the surviving host is restored to the old tree without touching the disposed one");
            Check(service.DependencyCount == 1, "and the service tracks only the host that remains");
        }
    }

    // --- wiring ----------------------------------------------------------------------------------

    /// <summary>
    /// The single-read shape P4 R7 asks for: the service reads one file snapshot and parses that same
    /// snapshot, so the size bound, the version identity and the tree all describe one file state.
    /// <para>
    /// <b>What this guard is, exactly.</b> It is a lexical guard over the comment-stripped service source:
    /// it counts the file-reading entry points it knows about and requires exactly one snapshot read
    /// (<c>File.ReadAllBytes</c>) plus its one in-memory reader, and no other read shape - not
    /// <c>ParseFile</c>, not <c>ReadAllText</c>/<c>ReadAllLines</c>/<c>ReadLines</c>, not
    /// <c>OpenRead</c>/<c>OpenText</c>/<c>Open</c>, not a path-backed <c>FileStream</c>, not even a
    /// re-appearing <c>File.Exists</c> probe. Comments are stripped first, so naming a read in prose neither
    /// reddens the lane nor hides a real one.
    /// </para>
    /// <para>
    /// <b>What it cannot do.</b> It cannot prove at run time that the parse and the version came from one
    /// read. The TOCTOU it guards against is an ordering between a read and a parse with no yield point and
    /// no seam to interleave, so no behavioural lane can pin it from outside: this repository has no read
    /// hook, and adding one to make the race testable would be test scaffolding in the product. What the lane
    /// can pin behaviourally - and does, in the lanes below - is what a snapshot decides: the size bound and
    /// the decoded content. The guard is a regression guard against reintroducing a second read, not proof of
    /// atomicity.
    /// </para>
    /// </summary>
    private static void VerifySourceWiring()
    {
        string kernel = Path.Combine(RepoRoot(), "Source", "FerriteLib.UiKit", "Kernel");
        string service = File.ReadAllText(Path.Combine(kernel, "UiDocumentService.cs"));

        Check(IsSingleReadService(service),
            "the document path reads one snapshot and no other file-reading entry point");
        Check(service.IndexOf("using Verse", StringComparison.Ordinal) < 0
            && service.IndexOf("UnityEngine", StringComparison.Ordinal) < 0,
            "the document service carries no backend contact, so its watcher thread cannot touch the game");

        // The false negative the addendum measured: a second real read in a different shape.
        const string plantedSecondRead =
            "static void Read(string path) {\n"
            + "  byte[] first = File.ReadAllBytes(path);\n"
            + "  var a = UiLayoutManifest.Parse(Decode(first));\n"
            + "  var b = UiLayoutManifest.Parse(Decode(File.ReadAllBytes(path)));\n"
            + "}\n"
            + "static string Decode(byte[] bytes) { var s = new MemoryStream(bytes); var r = new StreamReader(s); return r.ReadToEnd(); }\n";
        Check(!IsSingleReadService(plantedSecondRead),
            "planted control: a Parse(ReadAllBytes) second read reddens the guard");

        // The false positive the addendum measured: a comment mentioning the old shape.
        const string commentedRead =
            "// UiLayoutManifest.ParseFile(path); File.ReadAllBytes(path); File.ReadAllText(path);\n"
            + "static void Read(string path) {\n"
            + "  byte[] first = File.ReadAllBytes(path);\n"
            + "  var a = UiLayoutManifest.Parse(Decode(first));\n"
            + "  var b = UiStyleDocument.Parse(Decode(first));\n"
            + "}\n"
            + "static string Decode(byte[] bytes) { var s = new MemoryStream(bytes); var r = new StreamReader(s); return r.ReadToEnd(); }\n";
        Check(IsSingleReadService(commentedRead),
            "planted control: a read named only in a comment does not count");
        Check(CountOccurrences(StripComments(commentedRead), "File.ReadAllBytes(") == 1,
            "and comment stripping leaves exactly the one real read");

        // Another read shape, to show the guard is not ParseFile-shaped.
        Check(!IsSingleReadService("var a = File.ReadAllBytes(p); var b = File.ReadAllText(p);"),
            "planted control: a ReadAllText second read reddens the guard too");
    }

    /// <summary>
    /// The read shapes the service must not contain beyond its one snapshot. Enumerated rather than inferred:
    /// a lexical guard that cannot name its own boundary is a gate you can walk under by typing a different
    /// API, and this list is the boundary it does cover.
    /// </summary>
    private static readonly string[] ForbiddenReadShapes =
    {
        "File.ReadAllText(",
        "File.ReadAllLines(",
        "File.ReadLines(",
        "File.OpenRead(",
        "File.OpenText(",
        "File.Open(",
        "new FileStream(",
        "ParseFile(",
        "File.Exists("
    };

    /// <summary>
    /// True when the comment-stripped source holds exactly one snapshot read (<c>File.ReadAllBytes</c>) and
    /// its single in-memory reader, and none of <see cref="ForbiddenReadShapes"/>.
    /// </summary>
    private static bool IsSingleReadService(string source)
    {
        string code = StripComments(source);
        if (CountOccurrences(code, "File.ReadAllBytes(") != 1) return false;
        if (CountOccurrences(code, "new StreamReader(") != 1) return false;
        for (int i = 0; i < ForbiddenReadShapes.Length; i++)
        {
            if (code.IndexOf(ForbiddenReadShapes[i], StringComparison.Ordinal) >= 0) return false;
        }

        return true;
    }

    private static int CountOccurrences(string text, string token)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }

        return count;
    }

    /// <summary>
    /// Removes line and block comments, outside string and character literals, so a read call named only in
    /// prose is not counted as one. A state machine rather than a regex: a regex cannot tell a comment from a
    /// literal, and this file's own source carries both.
    /// </summary>
    private static string StripComments(string text)
    {
        var builder = new StringBuilder(text.Length);
        bool inLine = false;
        bool inBlock = false;
        bool inString = false;
        bool inVerbatim = false;
        bool inChar = false;

        for (int i = 0; i < text.Length; i++)
        {
            char current = text[i];
            char next = i + 1 < text.Length ? text[i + 1] : '\0';

            if (inLine)
            {
                if (current == '\n') { inLine = false; builder.Append(current); }
                continue;
            }

            if (inBlock)
            {
                if (current == '*' && next == '/') { inBlock = false; i++; }
                else if (current == '\n') { builder.Append(current); }
                continue;
            }

            if (inString)
            {
                builder.Append(current);
                if (current == '\\' && next != '\0') { builder.Append(next); i++; }
                else if (current == '"') { inString = false; }
                continue;
            }

            if (inVerbatim)
            {
                builder.Append(current);
                if (current == '"')
                {
                    if (next == '"') { builder.Append(next); i++; }
                    else { inVerbatim = false; }
                }

                continue;
            }

            if (inChar)
            {
                builder.Append(current);
                if (current == '\\' && next != '\0') { builder.Append(next); i++; }
                else if (current == '\'') { inChar = false; }
                continue;
            }

            if (current == '/' && next == '/') { inLine = true; i++; continue; }
            if (current == '/' && next == '*') { inBlock = true; i++; continue; }
            if (current == '@' && next == '"') { inVerbatim = true; builder.Append(current); builder.Append(next); i++; continue; }
            if (current == '"') { inString = true; builder.Append(current); continue; }
            if (current == '\'') { inChar = true; builder.Append(current); continue; }
            builder.Append(current);
        }

        return builder.ToString();
    }

    // --- fixture ---------------------------------------------------------------------------------

    private sealed class Model
    {
        public string Draft = "original";

        public int Writes;

        public int Commands;
    }

    /// <summary>
    /// The lane's clock. Automatic reload scheduling is debounced by the policy's quiet period, and the
    /// harness's <c>Time.realtimeSinceStartup</c> stub is a constant zero, so a lane that wants a watcher
    /// signal to commit has to advance time explicitly. Nothing else is advanced to make time pass - no
    /// frame count, no host count, no pump count; <see cref="Settle"/> only moves this clock and pumps.
    /// </summary>
    private sealed class LaneClock : IUiTimeSource
    {
        public double Now;

        public double NowSeconds => Now;

        public void Advance(double seconds)
        {
            Now += seconds;
        }
    }

    /// <summary>
    /// Past every window in <see cref="UiReloadPolicy.Default"/>: the 0.25s quiet period, the 0.5s retry
    /// interval, and the 2.0s deferral ceiling. A lane that holds an interaction capture therefore still
    /// reaches the commit - the ceiling is what forces it - and a lane with no interaction simply passes the
    /// quiet window.
    /// </summary>
    private const double SettleSeconds = 3.0;

    private static readonly LaneClock Clock = new LaneClock();

    private static UiDocumentService NewService(bool? autoWatch = null)
    {
        return new UiDocumentService(autoWatch, UiReloadPolicy.Default, Clock);
    }

    /// <summary>
    /// The observe/advance/commit shape of an automatic reload. The first pump observes the signals the lane
    /// posted - that is when the quiet window opens, because the watcher thread reads no clock - the clock
    /// then advances past every scheduling window, and the second pump reads and commits. A single
    /// <c>Pump</c> after <c>Signal</c> is deliberately NOT a commit: that debounce is this round's contract,
    /// and the assertions below are unchanged from the 0.5 lane - only the timing that reaches them is now
    /// explicit. Returns true when either pump committed.
    /// </summary>
    private static bool Settle(UiDocumentService service)
    {
        bool committed = service.Pump();
        Clock.Advance(SettleSeconds);
        return service.Pump() || committed;
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

    /// <summary>A host built on a theme the lane holds, so a page-level application is observable.</summary>
    private static UiHost NewHost(string source, UiLayoutManifest manifest, IUiBindings bindings, UiTheme theme)
    {
        return new UiHost(source, manifest, bindings, theme, new FixedMetrics(), new FixedTranslation());
    }

    private static void Draw(UiHost host)
    {
        host.DrawFrame(new Rect(0f, 0f, 400f, 300f));
    }

    private static UiLayoutSnapshot Arrange(UiHost host)
    {
        return host.MeasureAndArrange(new Vector2(400f, 300f));
    }

    private static string Page(string source, string body)
    {
        return "<UiPage Schema=\"2\" Source=\"" + source + "\">" + body + "</UiPage>";
    }

    /// <summary>A style document whose page level names <paramref name="schemeName"/> and sets Panel.</summary>
    private static string PageSchemeStyle(string schemeName, string panelValue)
    {
        return "<Styles Schema=\"1\" Scheme=\"" + schemeName + "\">"
            + "<Scheme Name=\"" + schemeName + "\">"
            + "<Color Token=\"Panel\" Value=\"" + panelValue + "\"/>"
            + "</Scheme>"
            + "</Styles>";
    }

    private static string KeepPage(string source)
    {
        return Page(source, "<Column Id=\"root\"><Widget Id=\"keep\" Kind=\"chrome/banner\" Text=\"keep\"/></Column>");
    }

    private static string ScrollPage(string source, string extra, string keepElement = "<Widget Id=\"keep\" Kind=\"chrome/banner\" Text=\"keep\"/>")
    {
        return Page(source,
            "<Column Id=\"root\">"
            + "<Scroll Id=\"body\" Height=\"40\">"
            + keepElement
            + "<Widget Id=\"tall\" Kind=\"chrome/banner\" Text=\"tall\" Height=\"300\"/>"
            + "</Scroll>"
            + extra
            + "</Column>");
    }

    private static string ModelPage()
    {
        return Page("scope-side",
            "<Column Id=\"root\">"
            + "<Widget Id=\"field\" Kind=\"chrome/banner\" Text=\"field\" Bind=\"draft\"/>"
            + "<Widget Id=\"apply\" Kind=\"input/button\" Text=\"apply\" ActionBind=\"apply\"/>"
            + "</Column>");
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

    private static bool SameColor(Color left, Color right)
    {
        return Math.Abs(left.r - right.r) < 0.001f
            && Math.Abs(left.g - right.g) < 0.001f
            && Math.Abs(left.b - right.b) < 0.001f
            && Math.Abs(left.a - right.a) < 0.001f;
    }

    private static bool ContainsName(IReadOnlyCollection<string> names, string name)
    {
        foreach (string candidate in names)
        {
            if (string.Equals(candidate, name, StringComparison.Ordinal)) return true;
        }

        return false;
    }

    private static bool Visible(UiLayoutSnapshot snapshot, string path)
    {
        for (int i = 0; i < snapshot.VisibleIds.Count; i++)
        {
            if (string.Equals(snapshot.VisibleIds[i], path, StringComparison.Ordinal)) return true;
        }

        return false;
    }

    private static UiReloadReport? FindReport(UiDocumentService service, string documentId)
    {
        IReadOnlyList<UiReloadReport> reports = service.Reports;
        for (int i = reports.Count - 1; i >= 0; i--)
        {
            if (string.Equals(reports[i].DocumentId, documentId, StringComparison.Ordinal)) return reports[i];
        }

        return null;
    }

    private static string RepoRoot()
    {
        string current = Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(current))
        {
            if (Directory.Exists(Path.Combine(current, "Source", "FerriteLib.UiKit"))
                && File.Exists(Path.Combine(current, "About", "About.xml")))
            {
                return current;
            }

            DirectoryInfo? parent = Directory.GetParent(current);
            if (parent == null) break;
            current = parent.FullName;
        }

        return Directory.GetCurrentDirectory();
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

    private static void Run(string name, Action body)
    {
        try
        {
            body();
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
        }
        else
        {
            failures++;
            Console.Error.WriteLine("  FAIL: " + name);
        }
    }
}
