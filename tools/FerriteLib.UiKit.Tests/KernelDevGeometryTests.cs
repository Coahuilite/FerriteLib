using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

using FerriteLib.UiKit.Kernel;
using FerriteLib.UiKit.Kernel.Widgets;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// The development-only numeric instrument on the diagnostic surface: what the layout actually did, as
/// numbers, for the questions a screenshot cannot answer.
/// <para>
/// The instrument is compiled into a development build and out of a release one, so this lane has two halves
/// and each asserts its own truth, the way the watching lane does:
/// <list type="bullet">
/// <item><b>dev half</b> - every arranged node is reported with its arranged rect, the rect its widget was
/// handed, that rect converted into the pointer's own space, the origin the conversion used, its height mode
/// and the height that mode resolved to; a scoped container reports its viewport and its real content
/// extent; and a press is attributed to the element that claimed it. The mutation these assertions are
/// failure-sensitive to is a change to the <b>geometry producer</b> (the engine's resolved height) and not
/// to the instrument: an instrument whose numbers do not move when the layout moves is a green light with
/// no meaning.</item>
/// <item><b>release half</b> - the instrument is not compiled in, the opt-in is refused rather than
/// accepted, and a full frame leaves the dump empty. Fail-closed, because "enable it and read nothing" is
/// the diagnostic failure this whole surface exists to end.</item>
/// </list>
/// </para>
/// </summary>
internal static class KernelDevGeometryTests
{
    private const string Scope = "dev-geometry-lane";

    private static readonly Rect Viewport = new Rect(0f, 0f, 200f, 200f);

    /// <summary>
    /// net472 has no Split(char, StringSplitOptions) overload, and the one-argument call binds to it under
    /// this language version; the array form is the one that exists in the framework this payload ships on.
    /// </summary>
    private static readonly char[] Lines = new[] { '\n' };

    private static int failures;

    public static int RunAll()
    {
        failures = 0;
        Run("The event double agrees with the reference constants and consumption contract", VerifyEventSemantics);
#if FER_DEV
        Run("Every arranged node is reported with both rects, its height mode and its content extent", VerifyGeometryNumbers);
        Run("A press is attributed to the element that claimed it, in the pointer's own space", VerifyInputVerdicts);
        Run("An element under an open popup layer yields the press and says so", VerifyCoveredVerdict);
        Run("Consumed input remains visible without changing native dispatch", VerifyConsumedInput);
        Run("The outline switch is independent of the capture switch", VerifyOverlayIsIndependentOfCapture);
        Run("The structured snapshot and the text dump describe one captured pass", VerifyStructuredSnapshotMatchesTheDump);
        Run("The effective appearance is resolved through the kind's own seam, with provenance", VerifyEffectiveAppearance);
        Run("Boundaries and unknowns: effective clip, popup ownership, and what this site cannot see", VerifyBoundariesAndUnknowns);
        Run("A bounded capture reports its head and counts what it dropped", VerifyBoundedCaptureAndDroppedCount);
        Run("Shell chrome records the completed pass and outlines independently", VerifyShellChromePassOrder);
        Run("A snapshot is a copy, and two hosts never share one capture", VerifySnapshotIsACopyAndHostsAreSeparate);
#else
        Run("A release payload has no instrument and refuses to pretend it has one", VerifyAbsentInRelease);
#endif
        return failures;
    }

    private static void VerifyEventSemantics()
    {
        // ScrollWheel is in the list because the bounded option menu reads it (UiNative.PointerWheelNotches):
        // the constant has to agree on both sides of the double or the wheel routes to a phase nothing sees.
        foreach (var pair in new[] {
            (EventType.MouseDown, "MouseDown"), (EventType.MouseUp, "MouseUp"),
            (EventType.MouseDrag, "MouseDrag"), (EventType.KeyDown, "KeyDown"),
            (EventType.KeyUp, "KeyUp"), (EventType.ScrollWheel, "ScrollWheel"),
            (EventType.Repaint, "Repaint"), (EventType.Layout, "Layout"), (EventType.Used, "Used") })
        {
            Check(Enum.GetName(typeof(EventType), pair.Item1) == pair.Item2,
                "the runtime event double matches the compiler's reference constant: " + pair.Item2);
        }
        Event raised = Event.KeyboardEvent("");
        raised.type = EventType.MouseDown;
        raised.Use();
        Check(raised.type == EventType.Used, "Use changes the event type to Used, as the real IMGUI contract requires");
    }

#if FER_DEV
    // --- dev half ---------------------------------------------------------------------------------

    private static void VerifyGeometryNumbers()
    {
        using UiHost host = NewHost(Page);
        UiDiagnosticSubscription diagnostics = host.Diagnostics;
        Check(!diagnostics.GeometryEnabled, "the instrument is off until a subscription asks: default off");
        diagnostics.GeometryEnabled = true;
        Check(diagnostics.GeometryEnabled, "and the opt-in is remembered on the subscription that asked");

        UiLayoutSnapshot snapshot = Pass(host);
        string dump = diagnostics.DumpGeometry();
        Print(dump);

        Check(LineWith(dump, "path=root ") != null, "the root container is reported");
        Check(LineWith(dump, "path=root/list ") != null, "the scoped container is reported");
        Check(LineWith(dump, "path=root/list/inner ") != null, "and so is the element inside it");

        string band = Require(dump, "path=root/row/band ");
        Check(band.IndexOf("height=MatchContent", StringComparison.Ordinal) >= 0,
            "the MatchContent band reports the mode it declared: " + band);
        Check(band.IndexOf("container=no", StringComparison.Ordinal) >= 0, "and reports itself as a widget");

        // The height axis as a number rather than as prose: the mode's whole contract is that this element's
        // height IS its sibling's measured height.
        string caption = Require(dump, "path=root/row/caption ");
        float bandHeight = Number(band, " h=");
        float captionHeight = Number(caption, " h=");
        Check(bandHeight > 0f && Same(bandHeight, captionHeight),
            "the band's recorded height is the sibling's measured height: " + Text(bandHeight) + " vs " + Text(captionHeight));

        // Not a second source of truth: what it reports is the snapshot's own geometry.
        Rect fromSnapshot = snapshot.RectById["band"];
        TryRect(band, "arranged=", out Rect arranged);
        Check(SameRect(arranged, fromSnapshot),
            "and its reported arranged rect is the snapshot's own rect for that element: " + Show(arranged) + " vs " + Show(fromSnapshot));

        // At the page level the two spaces coincide, and a reader can now see that instead of assuming it.
        TryRect(band, "window=", out Rect bandWindow);
        Check(SameRect(arranged, bandWindow),
            "at the page level the arranged and drawn rects agree: " + Show(arranged) + " vs " + Show(bandWindow));

        // The trap the instrument exists for: inside the scoped container the two spaces genuinely differ,
        // and the dump says so with both numbers and the origin between them.
        string inner = Require(dump, "path=root/list/inner ");
        TryRect(inner, "arranged=", out Rect innerArranged);
        TryRect(inner, "draw=", out Rect innerDraw);
        Check(!SameRect(innerArranged, innerDraw),
            "an element inside the scoped container is reported in the other space, not in the page's: "
            + Show(innerArranged) + " vs " + Show(innerDraw));

        // The conversion itself, per line: this half is a future-regression guard (it holds by construction
        // today) rather than the mutation-proving half, which is the height equality above.
        foreach (string line in dump.Split(Lines, StringSplitOptions.None))
        {
            if (line.Length == 0 || line[0] == '[' || line.StartsWith("input ", StringComparison.Ordinal)) continue;
            if (!TryRect(line, "draw=", out Rect draw) || !TryRect(line, "window=", out Rect window)) continue;
            if (!TryPoint(line, "origin=", out Vector2 origin)) continue;
            Check(Same(window.x, draw.x + origin.x) && Same(window.y, draw.y + origin.y),
                "guard: the window rect is the drawn rect plus the reported origin: " + line);
        }

        // The scoped container's viewport and its real content extent, both from the arrange.
        string list = Require(dump, "path=root/list ");
        Check(list.StartsWith("viewport ", StringComparison.Ordinal),
            "a scoped container is reported as a viewport rather than as an ordinary rect: " + list);
        TryRect(list, "window=", out Rect viewport);
        Check(TryRect(list, "content=", out Rect content) && content.height > viewport.height,
            "and its content extent is the scrollable extent, not the visible band: " + Show(content) + " over " + Show(viewport));
    }

    private static void VerifyInputVerdicts()
    {
        using UiHost host = NewHost(Page);
        host.Diagnostics.GeometryEnabled = true;
        UiLayoutSnapshot snapshot = Pass(host);
        Rect band = snapshot.RectById["band"];

        string bandLine = Require(host.Diagnostics.DumpGeometry(), "path=root/row/band ");
        TryRect(bandLine, "window=", out Rect bandWindow);

        try
        {
            UiNative.DebugMousePositionEnabled = true;
            UiNative.DebugMouseDown = true;

            // A press that lands: the native control fires inside the band's own rect.
            UiNative.DebugMousePosition = band.center;
            UiNative.ButtonOverride = rect => true;
            host.DrawFrame(Viewport);
            string hit = Require(host.Diagnostics.DumpGeometry(), "verdict=hit");
            PrintInputs(host.Diagnostics.DumpGeometry());
            Check(hit.IndexOf("path=root/row/band ", StringComparison.Ordinal) >= 0,
                "a claimed press names the element that claimed it: " + hit);
            TryPoint(hit, "point=", out Vector2 hitPoint);
            Check(Same(hitPoint.x, bandWindow.center.x) && Same(hitPoint.y, bandWindow.center.y),
                "in the pointer's own space, so the two can be compared instead of guessed at: "
                + Show(hitPoint) + " vs " + Show(bandWindow.center));

            // The chokepoint agreement that matters for "the click did nothing": the rect the funnel
            // queried is the same window-space rect the engine reported drawing.
            TryRect(hit, "rect=", out Rect queried);
            Check(SameRect(queried, bandWindow),
                "and the queried rect is the drawn rect the engine reported, in one space: "
                + Show(queried) + " vs " + Show(bandWindow));

            // A press that misses: still sampled, because the pointer is down. This is what answers
            // "the click did nothing" instead of an empty log that answers nothing.
            UiNative.ButtonOverride = _ => false;
            host.DrawFrame(Viewport);
            string miss = Require(host.Diagnostics.DumpGeometry(), "verdict=miss");
            PrintInputs(host.Diagnostics.DumpGeometry());
            Check(miss.IndexOf("path=root/row/band ", StringComparison.Ordinal) >= 0,
                "a press the element ignored is reported as ignored, with the element named: " + miss);
        }
        finally
        {
            UiNative.ButtonOverride = null;
            UiNative.DebugMousePosition = default;
            UiNative.DebugMouseDown = false;
            UiNative.DebugMousePositionEnabled = false;
        }

        // A refusal reports its reason: the funnel's own disabled rule.
        using UiHost disabled = NewHost(Page, commandEnabled: false);
        disabled.Diagnostics.GeometryEnabled = true;
        try
        {
            UiNative.DebugMousePositionEnabled = true;
            UiNative.DebugMouseDown = true;
            UiNative.DebugMousePosition = band.center;
            disabled.DrawFrame(Viewport);
            string blocked = Require(disabled.Diagnostics.DumpGeometry(), "verdict=disabled");
            PrintInputs(disabled.Diagnostics.DumpGeometry());
            Check(blocked.IndexOf("path=root/row/band ", StringComparison.Ordinal) >= 0,
                "a disabled element reports why the press went nowhere: " + blocked);
        }
        finally
        {
            UiNative.DebugMousePosition = default;
            UiNative.DebugMouseDown = false;
            UiNative.DebugMousePositionEnabled = false;
        }
    }

    /// <summary>
    /// The funnel's fourth verdict. <c>hit</c>, <c>miss</c> and <c>disabled</c> are driven above;
    /// <c>covered</c> - the element sits under another element's open popup layer - was implemented and
    /// printed by the instrument with no lane driving it, and an unexercised branch is unproven surface.
    /// <para>
    /// The press point is <b>point-shaped</b> on purpose, and the reason is read from the code rather than
    /// assumed. The trigger draws before the covered element, so a rect-blind override would make the
    /// trigger's own button fire; <c>DropdownButtonCore</c> then closes the popup it owns, and
    /// <c>UiSession.ClosePopup</c> drops the popup layer from BOTH <c>hitLayers</c> and
    /// <c>dispatchLayers</c> (<c>UiSession.cs:296-303</c>) - so the covered element, drawn after the
    /// trigger, would see no layer, <c>Require("verdict=covered")</c> would throw, and this lane would go
    /// RED rather than falsely green. The point shape is what keeps the lane measuring the verdict it
    /// names, and it is also what keeps the "it did not dispatch" assertion honest: the point IS inside
    /// the covered element's rect, so if the yield were removed the override would fire it and the counter
    /// would move.
    /// </para>
    /// <para>
    /// Around the one press the lane asserts the verdict, the ABSENCE of a hit sample for the same element
    /// in the same pass, and the mechanism: the option row consumed the click (the value binding moved) and
    /// the trigger did not toggle the popup shut. The last frame is the counterfactual - the same press with
    /// no popup layer left hits the element and dispatches exactly once - so the covered verdict is shown to
    /// depend on the layer rather than on the seam.
    /// </para>
    /// </summary>
    private static void VerifyCoveredVerdict()
    {
        const string xml =
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Column Id=\"root\" Padding=\"0\" Gap=\"0\">"
            + "<Widget Id=\"trigger\" Kind=\"input/dropdown\" OptionsBind=\"Options\" />"
            + "<Widget Id=\"under\" Kind=\"input/button\" Text=\"Under\" Height=\"28\" ActionBind=\"under-act\" />"
            + "</Column>"
            + "</UiPage>";

        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();

        int underActions = 0;
        var bindings = new UiBindings();
        // "y", not "x": the press lands on the popup's first option row, whose value is "x", so the value
        // binding moving to "x" is what proves the ROW consumed the click rather than the trigger.
        string current = "y";
        bindings.BindValue("trigger", () => current, value => current = value);
        bindings.BindOptions("Options", () => new List<string> { "x", "y" });
        bindings.BindCommand("under-act", () => underActions++);

        using UiHost host = new UiHost(
            Scope, UiLayoutManifest.Parse(xml), bindings, UiTheme.Vanilla, new StubMetrics(), new StubTranslation());
        host.Diagnostics.GeometryEnabled = true;

        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(Viewport.width, Viewport.height));
        host.Session.OpenPopup("trigger", snapshot.RectById["trigger"]);

        // A pass the engine is not part of must not decide what this lane measures: a leftover Event.current
        // from an earlier lane is an input, and checking the instrument's inputs is the rule this phase keeps
        // paying for. The debug seam supplies the pointer instead.
        Event? previousEvent = Event.current;
        Event.current = null;
        try
        {
            // One frame publishes the popup layer; dispatch reads the pass that just finished, so the covered
            // decision is only reachable from the second frame on. That ordering is the mechanism, not a
            // detail of this lane.
            host.DrawFrame(Viewport);
            Rect? popup = PopupLayer(host.Session);
            if (!popup.HasValue)
            {
                throw new Exception("the open popup pushed no hit layer, so nothing can be covered");
            }

            Rect under = snapshot.RectById["under"];
            Vector2 point = new(under.center.x, Math.Max(popup.Value.y + 2f, under.y + 2f));
            if (!Over(popup.Value, point) || !Over(under, point))
            {
                throw new Exception("test premise broken: " + Show(point) + " is not inside both the popup "
                    + Show(popup.Value) + " and the element it covers " + Show(under));
            }

            UiNative.DebugMousePositionEnabled = true;
            UiNative.DebugMouseDown = true;
            UiNative.DebugMousePosition = point;
            UiNative.ButtonOverride = rect => Over(rect, point);

            // Frame two: the press lands inside the popup layer AND inside the covered element.
            host.DrawFrame(Viewport);
            string dump = host.Diagnostics.DumpGeometry();
            PrintInputs(dump);
            string covered = Require(dump, "verdict=covered");
            Check(covered.IndexOf("path=root/under ", StringComparison.Ordinal) >= 0,
                "an element under another element's open popup layer reports the covered verdict: " + covered);
            // The assertion that separates "yielded" from "recorded and did not yield": the same pass must
            // carry no hit sample for the same element (a non-yielding run records two).
            Check(!HasInputVerdict(dump, "root/under", "hit"),
                "and it records no hit sample in the same pass: " + ShowInputs(dump, "root/under"));
            Check(underActions == 0,
                "and it does not dispatch its command while the layer is above it (fired "
                + underActions.ToString(CultureInfo.InvariantCulture) + " time(s))");
            // The mechanism, measured rather than described: the option row is the top layer, so IT consumed
            // the click and closed the popup. A trigger that had stolen the click would close the popup too,
            // which is exactly why the value write is what tells the two apart.
            Check(string.Equals(current, "x", StringComparison.Ordinal) && !host.Session.IsPopupOpen("trigger"),
                "the option row consumed the click and closed the popup, not the trigger: value='" + current
                + "' popup-open=" + host.Session.IsPopupOpen("trigger").ToString());

            // Frame three: the counterfactual. The same press with no popup layer left must hit the element
            // and dispatch exactly once, which is what makes the covered verdict a property of the layer.
            host.DrawFrame(Viewport);
            string after = host.Diagnostics.DumpGeometry();
            PrintInputs(after);
            string hit = Require(after, "verdict=hit");
            Check(hit.IndexOf("path=root/under ", StringComparison.Ordinal) >= 0 && underActions == 1,
                "and with no popup layer left the same press hits it and dispatches once (fired "
                + underActions.ToString(CultureInfo.InvariantCulture) + " time(s)): " + hit);
        }
        finally
        {
            UiNative.ButtonOverride = null;
            UiNative.DebugMousePosition = default;
            UiNative.DebugMouseDown = false;
            UiNative.DebugMousePositionEnabled = false;
            Event.current = previousEvent;
        }
    }

    /// <summary>
    /// The topmost popup layer of the session's hit stack, or null when none is pushed. The lane reads the
    /// stack directly, the way the popup lanes do, because the covered rule is a property of the layer.
    /// </summary>
    private static Rect? PopupLayer(UiSession session)
    {
        for (int i = session.HitLayers.Count - 1; i >= 0; i--)
        {
            if (session.HitLayers[i].IsPopup) return session.HitLayers[i].Rect;
        }

        return null;
    }

    /// <summary>Point containment, written out because the stub Rect has no Contains to lean on.</summary>
    private static bool Over(Rect rect, Vector2 point)
    {
        return point.x >= rect.x && point.x <= rect.xMax && point.y >= rect.y && point.y <= rect.yMax;
    }

    /// <summary>True when that pass recorded an input sample for that element carrying that verdict.</summary>
    private static bool HasInputVerdict(string dump, string elementPath, string verdict)
    {
        foreach (string line in dump.Split(Lines, StringSplitOptions.None))
        {
            if (!line.StartsWith("input ", StringComparison.Ordinal)) continue;
            if (line.IndexOf("path=" + elementPath + " ", StringComparison.Ordinal) < 0) continue;
            if (line.IndexOf("verdict=" + verdict + " ", StringComparison.Ordinal) >= 0) return true;
        }

        return false;
    }

    /// <summary>The input samples of one pass that mention an element, for a failure message.</summary>
    private static string ShowInputs(string dump, string elementPath)
    {
        var shown = new List<string>();
        foreach (string line in dump.Split(Lines, StringSplitOptions.None))
        {
            if (line.StartsWith("input ", StringComparison.Ordinal)
                && line.IndexOf("path=" + elementPath + " ", StringComparison.Ordinal) >= 0)
            {
                shown.Add(line);
            }
        }

        return shown.Count == 0 ? "(no input sample for " + elementPath + ")" : string.Join(" | ", shown);
    }

    private static void VerifyConsumedInput()
    {
        const string xml = "<UiPage Schema=\"2\" Source=\"" + Scope + "\"><Column Id=\"root\" Padding=\"0\" Gap=\"0\">"
            + "<Widget Id=\"first\" Kind=\"input/button\" Text=\"First\" Height=\"30\" ActionBind=\"act\" />"
            + "<Widget Id=\"later\" Kind=\"input/button\" Text=\"Later\" Height=\"30\" ActionBind=\"other\" />"
            + "</Column></UiPage>";
        int actions = 0, otherActions = 0;
        var bindings = new UiBindings();
        bindings.BindCommand("act", () => actions++);
        bindings.BindCommand("other", () => otherActions++);
        using var host = new UiHost(Scope, UiLayoutManifest.Parse(xml), bindings,
            UiTheme.Vanilla, new StubMetrics(), new StubTranslation());
        Rect first = Pass(host).RectById["first"];
        try
        {
            foreach (bool enabled in new[] { true, false })
            {
                host.Diagnostics.GeometryEnabled = enabled;
                foreach (EventType phase in new[] { EventType.MouseDown, EventType.MouseUp })
                {
                    Event raised = Event.KeyboardEvent("");
                    raised.type = phase;
                    raised.button = 0;
                    raised.mousePosition = first.center;
                    Event.current = raised;
                    host.DrawFrame(Viewport);
                    Check(raised.type == EventType.Used, "the native button consumes the event with diagnostics=" + enabled);
                    if (!enabled) continue;
                    string dump = host.Diagnostics.DumpGeometry();
                    string firstInput = Require(dump, "input path=root/first ");
                    string laterInput = Require(dump, "input path=root/later ");
                    Check(firstInput.Contains("event-before=" + phase + " event-after=Used"),
                        "the consuming control reports the phase transition: " + firstInput);
                    Check(laterInput.Contains("verdict=miss event-before=Used event-after=Used"),
                        "a later query remains visible after consumption: " + laterInput);
                    Check(dump.Contains(" event=" + phase), "the host retains the original event phase");
                    PrintInputs(dump);
                }
            }
            Check(actions == 2 && otherActions == 0, "diagnostics on and off dispatch exactly one command per native click");
            host.Diagnostics.GeometryEnabled = true;
            Event repaint = Event.KeyboardEvent("");
            repaint.type = EventType.Repaint;
            Event.current = repaint;
            host.DrawFrame(Viewport);
            Check(!host.Diagnostics.DumpGeometry().Contains("input path="), "a repaint does not inherit consumed input from the previous pass");
        }
        finally { Event.current = null; GUIUtility.hotControl = 0; }
    }

    /// <summary>
    /// SA1.5: the outline no longer waits for the capture. Turning the picture on must not quietly start
    /// recording, and the recording stopping must not silence a picture someone is reading.
    /// <para>
    /// MUTATION-TARGET: the acceptance itself (a revert to "refused until the instrument is on" throws in
    /// the first lines here), the outline-only pass storing nothing (a revert that creates a capture as a
    /// side effect reddens the empty-dump and reader assertions), and capture-off keeping the switch on (a
    /// revert that clears the flag with the capture reddens the survival assertions). The retained-entry
    /// faithfulness WHILE a capture is live stays pinned by VerifyBoundedCaptureAndDroppedCount, which the
    /// both-on case here deliberately re-measures through the same helpers.
    /// </para>
    /// </summary>
    private static void VerifyOverlayIsIndependentOfCapture()
    {
        using UiHost host = NewHost(Page);
        UiDiagnosticSubscription diagnostics = host.Diagnostics;
        Check(!diagnostics.GeometryOverlay, "the outline is off by default, with or without the capture");

        diagnostics.GeometryOverlay = true; // this call threw before SA1.5
        Check(diagnostics.GeometryOverlay, "turning the outline on without the instrument is accepted now");
        Check(!diagnostics.GeometryEnabled, "and accepting it did not quietly turn the capture on");

        ClearSolids();
        Pass(host);
        int outlineOnly = SolidCount();
        Check(diagnostics.DumpGeometry().Length == 0,
            "the outline-only pass leaves the dump empty: there is no capture to store it in");
        Check(!diagnostics.TryGetGeometrySnapshot(out UiDevGeometrySnapshot? nothing) && nothing == null,
            "and the structured reader keeps saying nothing was captured, so no report sample is retained");

        // The capture joining changes no pixels: both modes render the SAME engine walk.
        diagnostics.GeometryEnabled = true;
        ClearSolids();
        Pass(host);
        int withCapture = SolidCount();
        Check(withCapture == outlineOnly,
            "turning capture on adds no pixels of its own: one geometry source, not a second");

        // The both-on case: the picture is the third rendering of the bounded capture, exactly as before.
        diagnostics.GeometryOverlay = false;
        ClearSolids();
        Pass(host);
        int withoutOverlay = SolidCount();
        diagnostics.GeometryOverlay = true;
        ClearSolids();
        Pass(host);
        int withOverlay = SolidCount();
        UiDevGeometrySnapshot retained = SnapshotOf(diagnostics);
        // DT1 closed the stated viewport limit: the outline paints EVERY retained entry at its own draw
        // step, the scoped container's own band included (the engine outlines it after the nested walk,
        // same rect, same retained answer). MUTATION-TARGET: delete that moved call in DrawEntries' scoped
        // branch and this equality loses exactly scoped*4 hairlines; the fixture genuinely carries a
        // scoped container (scoped > 0 asserted), so the count cannot satisfy this for free.
        int scoped = 0;
        foreach (UiDevNodeSnapshot node in retained.Nodes)
        {
            if (node.IsScopedContainer) scoped++;
        }

        Check(retained.Nodes.Count > 0 && scoped > 0
                && withOverlay - withoutOverlay == retained.Nodes.Count * OverlayHairlinesPerNode,
            "with the capture live the outline paints every retained entry at its own step, scoped "
            + "viewports included: "
            + (withOverlay - withoutOverlay).ToString(CultureInfo.InvariantCulture) + " solids over "
            + retained.Nodes.Count.ToString(CultureInfo.InvariantCulture) + " retained ("
            + scoped.ToString(CultureInfo.InvariantCulture) + " of them scoped viewports)");
        Check(outlineOnly == withOverlay,
            "and the standalone outline painted the same set: nothing was dropped, both modes read one walk ("
            + outlineOnly.ToString(CultureInfo.InvariantCulture)
            + " vs " + withOverlay.ToString(CultureInfo.InvariantCulture) + ")");

        // The capture leaving must not silence the picture.
        diagnostics.GeometryEnabled = false;
        Check(diagnostics.GeometryOverlay, "switching the capture off leaves the outline switch on");
        ClearSolids();
        Pass(host);
        int afterCaptureOff = SolidCount();
        Check(afterCaptureOff == withOverlay, "and the outline keeps painting the same entries");
        Check(diagnostics.DumpGeometry().Length == 0,
            "while the report channel returns to honestly saying nothing was captured");

        diagnostics.GeometryOverlay = false;
        Check(!diagnostics.GeometryOverlay, "and it is turned off again without any capture present");

        using UiHost other = NewHost(OtherPage);
        Check(!other.Diagnostics.GeometryOverlay,
            "and the switch is per subscription: another host's outline stays off");
    }

    // --- structured snapshot (R3-A) ----------------------------------------------------------------

    /// <summary>
    /// R3-A.1 and R3-A.5(1): the human dump and the machine-readable snapshot are two renderings of ONE
    /// capture. The assertions compare them fact by fact rather than trusting that they agree, and the
    /// provenance assertions run on a <b>non-default scope</b> so the scheme/density/font fields have
    /// something real to report.
    /// <para>
    /// MUTATION-TARGET: the per-node agreement assertions - if the snapshot were built from a second
    /// collection (or a replayed pass) these are what redden. GUARD: the pass/count agreement, which holds
    /// under any single-capture implementation.
    /// </para>
    /// </summary>
    private static void VerifyStructuredSnapshotMatchesTheDump()
    {
        // The registry is reset before the first host rather than between the two: this lane must not depend
        // on what an earlier lane left registered, and a host built against a cleared registry would fail for
        // a reason that has nothing to do with what is being measured here.
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();

        // (a) The page-level case: an element that names no scheme and no density keeps the injected theme.
        using (UiHost plain = NewHost(Page))
        {
            plain.Diagnostics.GeometryEnabled = true;
            Pass(plain);
            string dump = plain.Diagnostics.DumpGeometry();
            UiDevGeometrySnapshot snapshot = SnapshotOf(plain.Diagnostics);
            CompareEveryNode(dump, snapshot, "plain page");

            UiDevNodeSnapshot band = RequireNode(snapshot, "root/row/band");
            Check(band.ThemeOrigin == "page" && band.Scheme.Length == 0 && band.Density.Length == 0,
                "an element that styles nothing draws with the injected theme: origin=" + band.ThemeOrigin
                + " scheme='" + band.Scheme + "' density='" + band.Density + "'");
            Check(band.Tone.Length == 0 && band.Emphasis.Length == 0,
                "and the sample reports an absent authored role as absent rather than as a default name");
            Check(RequireNode(snapshot, "root/row/caption").Emphasis == "Muted",
                "while a declared role is reported as the AUTHORED text read from the declaration "
                + "(the caption declares Emphasis=\"Muted\")");
            Check(band.ScopeFont == UiTheme.Vanilla.DefaultFont,
                "the reported font is the injected theme's own selection: " + band.ScopeFont);
            Check(!band.PaintedFont.HasValue,
                "and the font finally painted is reported as unobservable rather than filled in with it: "
                + (band.PaintedFont.HasValue ? band.PaintedFont.Value.ToString() : "null"));
            Check(band.AppearanceSource == UiDevNodeSnapshot.NoAppearanceSource && band.Appearance.Length == 0,
                "a kind that declares no look set is reported as having none, which is a fact about the kind: "
                + band.AppearanceSource + " look='" + band.Appearance + "'");
        }

        // (b) A non-default scope, so provenance and the selected font are exercised for real.
        using (UiHost scoped = NewHost(ScopedPage))
        {
            scoped.Diagnostics.GeometryEnabled = true;
            Pass(scoped);
            string dump = scoped.Diagnostics.DumpGeometry();
            Print(dump);
            UiDevGeometrySnapshot snapshot = SnapshotOf(scoped.Diagnostics);
            CompareEveryNode(dump, snapshot, "scoped page");

            UiDevNodeSnapshot band = RequireNode(snapshot, "root/band");
            Check(band.Scheme == "tinted" && band.Density == "cozy",
                "the sample names the scheme and density the element resolved through: '"
                + band.Scheme + "'/'" + band.Density + "'");
            Check(band.ThemeOrigin == "region",
                "and says the appearance came from a resolver-built region: " + band.ThemeOrigin);
            Check(band.ScopeFont == UiFont.Medium,
                "and the font that scope selected is the scope font the sample reports: " + band.ScopeFont);
            Check(!band.PaintedFont.HasValue,
                "while the painted font stays a documented unknown instead of being filled in with the scope "
                + "default: a kind that passes its own constant would otherwise be misreported");
            Check(band.NodeKey.IndexOf(UiNodeId.KeySeparator) >= 0 && band.NodeKey != band.Path,
                "the identity is the node's CANONICAL key rather than a re-derived display string: key='"
                + band.NodeKey + "' path='" + band.Path + "'");
            Check(RequireNode(snapshot, "root").Scheme == "tinted",
                "a container that declares the scope reports it too, so inheritance is visible rather than assumed");
        }
    }

    /// <summary>
    /// R3-A.2 (CORRECTION): the EFFECTIVE appearance, not the authored role text. Real draws, three cases:
    /// the kind's default look, an explicit declaration selecting the other look, and a scope that
    /// re-registers the same kind with its own look set - so the registry's scope-first rule is what
    /// answered. MUTATION-TARGET: the declared-vs-default pair and the scoped-alias provenance; an
    /// instrument that reported the kind's default regardless, or that ignored the scope's own set, reddens
    /// them. GUARD: the no-seam case in the fixture lane, which holds for any faithful read.
    /// </summary>
    private static void VerifyEffectiveAppearance()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();

        // (a) The core registration, reached from a scope that registers nothing of its own: the default and
        // the declared look are two DIFFERENT effective values of the same kind.
        using (UiHost host = NewBoolHost(AppearancePage))
        {
            host.Diagnostics.GeometryEnabled = true;
            Pass(host);
            string dump = host.Diagnostics.DumpGeometry();
            UiDevGeometrySnapshot snapshot = SnapshotOf(host.Diagnostics);
            Print(dump);
            CompareEveryNode(dump, snapshot, "appearance page");

            UiDevNodeSnapshot plain = RequireNode(snapshot, "root/plain");
            UiDevNodeSnapshot chosen = RequireNode(snapshot, "root/chosen");
            Check(plain.Appearance == CheckboxWidget.SwitchLook && !plain.AppearanceDeclared,
                "an element that declares no look reports the kind's default: " + plain.Appearance);
            Check(chosen.Appearance == CheckboxWidget.CheckboxLook && chosen.AppearanceDeclared,
                "and one that declares the other look reports the declared value: " + chosen.Appearance);
            Check(plain.Appearance != chosen.Appearance,
                "so one kind resolves two different effective appearances: '" + plain.Appearance
                + "' vs '" + chosen.Appearance + "'");
            Check(plain.AppearanceDefault == CheckboxWidget.SwitchLook
                    && plain.AppearanceSupported.IndexOf(CheckboxWidget.CheckboxLook, StringComparison.Ordinal) >= 0,
                "with the kind's own default and accepted set beside it: " + plain.AppearanceDefault
                + " / " + plain.AppearanceSupported);
            Check(plain.AppearanceSource == UiDevNodeSnapshot.CoreAppearanceSource
                    && !plain.AppearancePairRegistered,
                "and the provenance says core, because this scope registers no look set for the kind: source="
                + plain.AppearanceSource + " pair=" + plain.AppearancePairRegistered);
        }

        // (b) The scoped alias: the SAME kind and the SAME element declaration, re-registered in this scope
        // with its own look set - so the registry's scope-first rule is what answered, and the answer differs
        // from the core one while the element declares nothing at all.
        try
        {
            UiWidgetRegistry.Register(
                Scope,
                CheckboxWidget.Kind,
                () => new CheckboxWidget(),
                new[] { "Id", "Kind", "Bind", "Appearance", "Height" },
                null,
                null,
                new UiAppearanceResolver(CheckboxWidget.CheckboxLook, CheckboxWidget.SwitchLook));

            using UiHost host = NewBoolHost(AliasPage);
            host.Diagnostics.GeometryEnabled = true;
            Pass(host);
            UiDevGeometrySnapshot snapshot = SnapshotOf(host.Diagnostics);

            UiDevNodeSnapshot alias = RequireNode(snapshot, "root/alias");
            Check(alias.Appearance == CheckboxWidget.CheckboxLook && !alias.AppearanceDeclared,
                "a scope that registers its own look set answers with ITS default: " + alias.Appearance);
            Check(alias.AppearanceSource == UiDevNodeSnapshot.ScopeAppearanceSource
                    && alias.AppearancePairRegistered,
                "and the provenance names that scope rather than the core fallback: source="
                + alias.AppearanceSource + " pair=" + alias.AppearancePairRegistered);
            Check(alias.AppearanceDefault == CheckboxWidget.CheckboxLook
                    && alias.AppearanceSupported.IndexOf(CheckboxWidget.SwitchLook, StringComparison.Ordinal) >= 0,
                "with the scoped set's own default and accepted names: " + alias.AppearanceDefault
                + " / " + alias.AppearanceSupported);
        }
        finally
        {
            // This lane is the only one that registers a scoped alias for a core kind. Leaving it registered
            // would change what every later lane resolves for input/checkbox, so it is removed here rather
            // than relied on to be overwritten.
            UiWidgetRegistry.Clear();
            UiWidgetRegistry.InitializeCore();
        }
    }

    /// <summary>
    /// R3-A.2/A.5(3): the boundaries, and the values that genuinely cannot be observed.
    /// <list type="bullet">
    /// <item>MUTATION-TARGET: the effective-clip assertions. Reverting the sample to "the innermost rect the
    /// element passed through" (or to the element's own draw rect) reddens the containment and height
    /// assertions, because the scroll's viewport would no longer appear on its children.</item>
    /// <item>MUTATION-TARGET: the unknown-reason assertions. Filling any of those boundaries with a plausible
    /// default - a zero rect, the element's own rect - reddens them, which is the honesty rule this lane
    /// exists to keep.</item>
    /// <item>GUARD: the popup ownership pair, which holds for any faithful session read.</item>
    /// </list>
    /// </summary>
    private static void VerifyBoundariesAndUnknowns()
    {
        using UiHost host = NewHost(Page);
        host.Diagnostics.GeometryEnabled = true;
        Pass(host);
        UiDevGeometrySnapshot snapshot = SnapshotOf(host.Diagnostics);

        UiDevNodeSnapshot root = RequireNode(snapshot, "root");
        UiDevNodeSnapshot list = RequireNode(snapshot, "root/list");
        UiDevNodeSnapshot inner = RequireNode(snapshot, "root/list/inner");

        Check(root.Clip.IsKnown && list.Clip.IsKnown && inner.Clip.IsKnown,
            "every sampled element reports the clip that was in force");
        Check(ContainsRect(root.Clip.Rect, inner.Clip.Rect),
            "the effective clip is the intersection, so the nested one stays inside the outer one: "
            + Show(inner.Clip.Rect) + " inside " + Show(root.Clip.Rect));
        Check(inner.Clip.Rect.height < root.Clip.Rect.height,
            "the scroll's own viewport constrains its children - a boundary the innermost rect alone does not "
            + "describe: " + Text(inner.Clip.Rect.height) + " < " + Text(root.Clip.Rect.height));
        Check(SameRect(list.Clip.Rect, root.Clip.Rect),
            "and a scoped container's own sample carries the clip it was drawn INSIDE, while its viewport "
            + "appears on its children: " + Show(list.Clip.Rect));

        Check(list.IsScopedContainer && list.Content.IsKnown,
            "a scoped container publishes its content extent, and the sample carries it: " + list.Content);
        Check(!inner.IsScopedContainer && !inner.Content.IsKnown
                && inner.Content.UnknownReason == UiDevNodeSnapshot.ContentUnknownReason,
            "an element that is not a scoped container reports no content rect, with the documented reason");

        Check(!inner.Hover.IsKnown && inner.Hover.UnknownReason == UiDevNodeSnapshot.HoverUnknownReason,
            "hover is unknown at a geometry sample, with the reason that says why: " + inner.Hover);
        Check(!inner.Focus.IsKnown && inner.Focus.UnknownReason == UiDevNodeSnapshot.FocusUnknownReason,
            "and focus is unknown because this library publishes no focus owner to report: " + inner.Focus);
        Check(inner.OpenPopupId.Length == 0 && !inner.OwnsOpenPopup && !inner.PopupAnchor.IsKnown
                && inner.PopupAnchor.UnknownReason == UiDevNodeSnapshot.NoOpenPopupUnknownReason,
            "with no popup open the anchor is unknown for its own documented reason, and nothing claims ownership");

        // The AVAILABLE half of the same contract, on a page whose trigger really owns a session popup.
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        string current = "y";
        var bindings = new UiBindings();
        bindings.BindValue("trigger", () => current, value => current = value);
        bindings.BindOptions("Options", () => new List<string> { "x", "y" });
        bindings.BindCommand("act", () => { });
        using UiHost popupHost = new UiHost(
            Scope, UiLayoutManifest.Parse(PopupPage), bindings, UiTheme.Vanilla, new StubMetrics(), new StubTranslation());
        popupHost.Diagnostics.GeometryEnabled = true;
        UiLayoutSnapshot arranged = popupHost.MeasureAndArrange(new Vector2(Viewport.width, Viewport.height));
        popupHost.Session.OpenPopup("trigger", arranged.RectById["trigger"]);
        popupHost.DrawFrame(Viewport);
        UiDevGeometrySnapshot withPopup = SnapshotOf(popupHost.Diagnostics);

        UiDevNodeSnapshot trigger = RequireNode(withPopup, "root/trigger");
        Check(trigger.OpenPopupId == "trigger" && trigger.OwnsOpenPopup,
            "a popup owned by an element is reported as owned by it: '" + trigger.OpenPopupId + "'");
        Check(trigger.PopupAnchor.IsKnown && SameRect(trigger.PopupAnchor.Rect, trigger.Window),
            "and its anchor is reportable, in the same window space as the element's own drawn rect: "
            + trigger.PopupAnchor);
        UiDevNodeSnapshot under = RequireNode(withPopup, "root/under");
        Check(under.OpenPopupId == "trigger" && !under.OwnsOpenPopup,
            "while a sibling sees the same open popup and does not claim to own it");
        popupHost.Session.ClosePopup();
    }

    /// <summary>
    /// R3-A.3/A.5(4): the capture stays bounded and says what it lost, on a REAL overflow.
    /// <para>
    /// The manifest validator counts DECLARED elements and the capture counts ARRANGED/DRAWN entries, so this
    /// fixture keeps the declaration tiny - one page, one template, one <c>Repeat</c> - and lets the runtime
    /// expansion produce a Row plus a rule per item. Nothing here weakens the parser's limit; the overflow is
    /// produced by a valid page that the engine expands.
    /// </para>
    /// <list type="bullet">
    /// <item>MUTATION-TARGET: the dropped-count assertions. A capture that keeps the whole pass, or that
    /// reports a zero drop while dropping, reddens them.</item>
    /// <item>MUTATION-TARGET: the overlay delta, which is what makes the overlay a rendering of the retained
    /// capture rather than of the pass.</item>
    /// <item>GUARD: the retained-head and text/data agreement assertions, which hold for any faithful
    /// bounded capture.</item>
    /// </list>
    /// </summary>
    private static void VerifyBoundedCaptureAndDroppedCount()
    {
        // ~2 entries per item (the template's Row and its rule), so 320 items is comfortably past the bound
        // while staying a cheap frame.
        const int items = 320;
        var keys = new List<string>();
        for (int i = 0; i < items; i++)
        {
            keys.Add("item" + i.ToString("000", CultureInfo.InvariantCulture));
        }

        // The expansion RATE is measured from two small runs rather than assumed, and the big run is then
        // checked against it: nothing here compares the capture's own constant with itself.
        List<string> three = keys.GetRange(0, 3);
        List<string> five = keys.GetRange(0, 5);
        UiDevGeometrySnapshot smallRun = RunOverflow(three);
        UiDevGeometrySnapshot fiveRun = RunOverflow(five);
        int small = ProducedEntries(smallRun);
        int fiveEntries = ProducedEntries(fiveRun);
        Check(fiveEntries > small && (fiveEntries - small) % 2 == 0,
            "the fixture expands linearly, so the rate is measurable: " + small + " entries for 3 items, "
            + fiveEntries + " for 5");
        int perItem = (fiveEntries - small) / 2;
        long produced = small + ((long)(items - 3) * perItem);

        using UiHost host = NewHost(OverflowPage, keys);
        host.Diagnostics.GeometryEnabled = true;
        Pass(host);
        UiDevGeometrySnapshot snapshot = SnapshotOf(host.Diagnostics);
        string dump = host.Diagnostics.DumpGeometry();

        Check(snapshot.Nodes.Count == UiDevGeometryCapture.MaxGeometrySamples,
            "the capture keeps exactly its bound of nodes, not the whole pass: " + snapshot.Nodes.Count);
        Check(snapshot.Nodes.Count + snapshot.NodesDropped == produced,
            "and accounts for the whole pass: kept=" + snapshot.Nodes.Count.ToString(CultureInfo.InvariantCulture)
            + " dropped=" + snapshot.NodesDropped.ToString(CultureInfo.InvariantCulture) + " produced="
            + produced.ToString(CultureInfo.InvariantCulture) + " (" + perItem.ToString(CultureInfo.InvariantCulture)
            + " per item)");
        Check(snapshot.NodesDropped == produced - UiDevGeometryCapture.MaxGeometrySamples,
            "so the dropped count is the real remainder, not a marker that happens to be non-zero: "
            + snapshot.NodesDropped.ToString(CultureInfo.InvariantCulture) + " dropped of "
            + produced.ToString(CultureInfo.InvariantCulture));
        Check(HeaderCount(dump, "nodes=") == snapshot.Nodes.Count
                && HeaderCount(dump, "nodes-dropped=") == snapshot.NodesDropped,
            "and the text rendering reports the same head and the same dropped count as the data");
        Check(snapshot.Nodes[0].Path == "rows",
            "paint order is preserved and the retained part is the head of the pass: first=" + snapshot.Nodes[0].Path);
        Check(snapshot.Nodes[UiDevGeometryCapture.MaxGeometrySamples - 1].Path.StartsWith("rows/", StringComparison.Ordinal),
            "and the head ends inside the expanded rows rather than at the page root: last retained="
            + snapshot.Nodes[UiDevGeometryCapture.MaxGeometrySamples - 1].Path);

        // The tail, at its FULL path, and non-vacuously. The derivation is proved on the small run first:
        // there the LAST item's rule IS retained, so the very same construction finds a real node - which is
        // what stops a misspelled path from satisfying the absence assertion below for free.
        string lastSmallRule = ItemRulePath(three[2]);
        Check(smallRun.NodeByPath(lastSmallRule) != null,
            "the derived full path finds the last item's rule when it is retained, so the lookup is proved: "
            + lastSmallRule);
        string firstRule = ItemRulePath(keys[0]);
        Check(snapshot.NodeByPath(firstRule) != null,
            "and it finds the first item's rule in the overflowing pass: " + firstRule);
        string droppedTail = ItemRulePath(keys[items - 1]);
        Check(snapshot.NodeByPath(droppedTail) == null,
            "while the LAST item's rule - same shape, full path, really produced by this pass - is what the "
            + "capture did not keep: " + droppedTail);
        Check(dump.IndexOf(droppedTail, StringComparison.Ordinal) < 0,
            "and the text rendering does not carry it either");

        // The overlay is the third rendering of the same capture, so it must paint exactly what was RETAINED:
        // measured as the difference the overlay switch makes, because the widgets paint their own solids too.
        ClearSolids();
        host.DrawFrame(Viewport);
        int withoutOverlay = SolidCount();
        host.Diagnostics.GeometryOverlay = true;
        ClearSolids();
        host.DrawFrame(Viewport);
        int withOverlay = SolidCount();
        host.Diagnostics.GeometryOverlay = false;
        UiDevGeometrySnapshot overlayPass = SnapshotOf(host.Diagnostics);
        Check(withOverlay - withoutOverlay == overlayPass.Nodes.Count * OverlayHairlinesPerNode,
            "the overlay outlines exactly the entries the bounded capture kept, and nothing it dropped: "
            + (withOverlay - withoutOverlay).ToString(CultureInfo.InvariantCulture) + " solids over "
            + overlayPass.Nodes.Count.ToString(CultureInfo.InvariantCulture) + " retained nodes ("
            + (overlayPass.Nodes.Count + overlayPass.NodesDropped).ToString(CultureInfo.InvariantCulture) + " drew)");
    }

    /// <summary>
    /// Runs the overflow fixture with one item list and returns the snapshot of the pass it drew. The host is
    /// disposed here; the snapshot is a copy, which is exactly the property that makes that safe.
    /// </summary>
    private static UiDevGeometrySnapshot RunOverflow(List<string> itemKeys)
    {
        using UiHost host = NewHost(OverflowPage, itemKeys);
        host.Diagnostics.GeometryEnabled = true;
        Pass(host);
        return SnapshotOf(host.Diagnostics);
    }

    /// <summary>What one pass produced: what the capture kept plus what it counted as dropped.</summary>
    private static int ProducedEntries(UiDevGeometrySnapshot snapshot)
    {
        return snapshot.Nodes.Count + (int)snapshot.NodesDropped;
    }

    /// <summary>
    /// The full path of the rule the template instantiates for one item. The engine composes a per-item
    /// identity as <c>&lt;declaredId&gt;#&lt;itemKey&gt;</c> for the template root and every element inside it
    /// (<c>UiLayoutEngine.BuildItemSpec</c>), and a node's path is its declared/derived Id chain - so the
    /// fixture's own naming, read from the source above, fixes this string without reading a dump.
    /// </summary>
    private static string ItemRulePath(string itemKey)
    {
        return "rows/row#" + itemKey + "/r#" + itemKey;
    }

    /// <summary>Four hairlines per outlined entry: one per edge of its draw rect.</summary>
    private const int OverlayHairlinesPerNode = 4;

    /// <summary>
    /// The stub's recorded solids. Reached by reflection because the harness compiles against the game's
    /// reference assembly, which does not declare the stub's recording hooks.
    /// </summary>
    private static int SolidCount()
    {
        FieldInfo? field = typeof(Verse.Widgets).GetField(
            "DrawBoxSolidColors", BindingFlags.Public | BindingFlags.Static);
        if (field == null) throw new Exception("the stub no longer exposes DrawBoxSolidColors");
        return ((System.Collections.IList)field.GetValue(null)!).Count;
    }

    private static void ClearSolids()
    {
        MethodInfo? clear = typeof(Verse.Widgets).GetMethod(
            "ClearDrawBoxSolidCalls", BindingFlags.Public | BindingFlags.Static);
        if (clear == null) throw new Exception("the stub no longer exposes ClearDrawBoxSolidCalls");
        clear.Invoke(null, null);
    }

    /// <summary>
    /// R3-A.3/A.5(5): a snapshot is a copy, two hosts never share a capture, and disposal releases only its
    /// own. MUTATION-TARGET: the same-host redraw - the snapshot taken before the SECOND draw on the same
    /// host must still report the earlier pass, which is what hands out a live view over the capture's buffer
    /// instead of a copy. GUARD: the cross-host isolation and the identity fields, which hold either way (a
    /// live view would pass both, which is exactly why the same-host case was needed).
    /// </summary>
    private static void VerifySnapshotIsACopyAndHostsAreSeparate()
    {
        using UiHost a = NewHost(Page);
        using UiHost b = NewHost(OtherPage);
        UiDiagnosticSubscription aSubscription = a.Diagnostics;
        UiDiagnosticSubscription bSubscription = b.Diagnostics;
        aSubscription.GeometryEnabled = true;
        bSubscription.GeometryEnabled = true;

        Pass(a);
        UiDevGeometrySnapshot first = SnapshotOf(aSubscription);
        int firstPass = first.Pass;
        int firstNodePass = first.Nodes[0].Pass;
        Check(first.Host == Scope && first.SessionId == a.Session.Identity,
            "the snapshot names the host and the session it came from");
        // Both directions, and both non-vacuous: A's own page has its band (so the lookup mechanism is
        // live), and A has none of B's ids - while B below has its own and neither of A's.
        Check(first.NodeByPath("root/row/band") != null && first.NodeByPath("root/other") == null,
            "host A's snapshot carries its own page (its nested band) and none of host B's elements"
            + " (A band=" + (first.NodeByPath("root/row/band") != null) + ", A other="
            + (first.NodeByPath("root/other") != null) + ")");

        UiLayoutSnapshot aBefore = a.MeasureAndArrange(new Vector2(Viewport.width, Viewport.height));
        Pass(b);
        UiDevGeometrySnapshot afterOtherHost = SnapshotOf(aSubscription);
        UiDevGeometrySnapshot bSnapshot = SnapshotOf(bSubscription);

        Check(bSnapshot.SessionId == b.Session.Identity && bSnapshot.SessionId != first.SessionId,
            "each host's capture belongs to its own session");
        Check(bSnapshot.NodeByPath("root/other") != null && bSnapshot.NodeByPath("root/row/band") == null,
            "and B's carries B's page, so one host's frame never lands in another host's capture"
            + " (B other=" + (bSnapshot.NodeByPath("root/other") != null) + ", B band="
            + (bSnapshot.NodeByPath("root/row/band") != null) + ")");
        Check(afterOtherHost.Pass == firstPass,
            "another host's draw does not move this host's capture: " + firstPass + " -> " + afterOtherHost.Pass);

        // The decisive case for the copy property: the SAME host draws again, so its capture really does
        // move on. A snapshot handed out before that must still describe the earlier pass - a live view over
        // the capture's buffer would follow it, and this is the assertion that would notice.
        Pass(a);
        UiDevGeometrySnapshot afterSameHost = SnapshotOf(aSubscription);
        Check(afterSameHost.Pass == firstPass + 1,
            "a second draw on the SAME host advances the capture's pass: " + firstPass + " -> " + afterSameHost.Pass);
        Check(first.Pass == firstPass && first.Nodes[0].Pass == firstNodePass
                && first.Nodes[0].Pass == first.Pass,
            "and the snapshot taken before it still describes the EARLIER pass, unchanged, with its node agree-"
            + "ing with it: held=" + first.Pass.ToString(CultureInfo.InvariantCulture) + " node="
            + first.Nodes[0].Pass.ToString(CultureInfo.InvariantCulture) + " capture="
            + afterSameHost.Pass.ToString(CultureInfo.InvariantCulture));
        Check(!ReferenceEquals(first, afterSameHost), "each call hands out a fresh snapshot object");
        Check(!ReferenceEquals(first.Nodes[0], afterSameHost.Nodes[0]),
            "and fresh node objects inside it, so a later pass cannot rewrite an earlier snapshot's nodes");
        Check(SameRect(first.Nodes[0].Arranged, aBefore.RectById["root"]),
            "which is the pass it was taken in: the first node's arranged rect is the page root's own rect");

        // Disposal releases one capture and leaves its sibling alone.
        aSubscription.Dispose();
        Check(!aSubscription.IsActive, "disposing a subscription deactivates it");
        Check(!aSubscription.TryGetGeometrySnapshot(out UiDevGeometrySnapshot? gone) && gone == null,
            "and a disposed subscription answers no snapshot rather than an empty-looking one");
        Check(bSubscription.IsActive && SnapshotOf(bSubscription).Nodes.Count > 0,
            "while the other host's capture is untouched");
    }

    /// <summary>
    /// Compares one dump against one snapshot topic by topic, for EVERY retained node. This is the same-source
    /// assertion: it would fail if either rendering dropped a field, reordered the pass, or read a different
    /// collection.
    /// </summary>
    private static void CompareEveryNode(string dump, UiDevGeometrySnapshot snapshot, string label)
    {
        string header = HeaderLine(dump);
        Check(Same(Number(header, "pass="), snapshot.Pass),
            label + ": the dump header and the snapshot name the same pass (" + header + ")");
        Check(HeaderCount(dump, "nodes=") == snapshot.Nodes.Count,
            label + ": and the same number of retained nodes");
        Check(HeaderCount(dump, "nodes-dropped=") == snapshot.NodesDropped,
            label + ": and the same dropped-node count");
        Check(CountLines(dump, "input ") == snapshot.Inputs.Count,
            label + ": and the same number of input samples");

        int compared = 0;
        foreach (UiDevNodeSnapshot node in snapshot.Nodes)
        {
            string line = RequireNodeLine(dump, node.Path);
            Check(line.IndexOf("key=" + node.NodeKey, StringComparison.Ordinal) >= 0,
                label + ": the dump carries the same canonical key for " + node.Path);
            Check(line.IndexOf("kind=" + node.Kind + " ", StringComparison.Ordinal) >= 0,
                label + ": and the same kind for " + node.Path);
            TryRect(line, "arranged=", out Rect arranged);
            TryRect(line, "draw=", out Rect draw);
            Check(SameRect(arranged, node.Arranged) && SameRect(draw, node.Draw),
                label + ": and the same arranged/draw rects for " + node.Path);
            Check(line.IndexOf("scope-font=" + node.ScopeFont.ToString() + " ", StringComparison.Ordinal) >= 0,
                label + ": and the same scope font for " + node.Path);
            // The painted font is compared through its DOCUMENTED unknown text, so a rendering that started
            // printing the scope font there reddens here rather than passing as "close enough".
            Check(line.IndexOf(
                    "painted-font="
                    + (node.PaintedFont.HasValue
                        ? node.PaintedFont.Value.ToString()
                        : "unknown(" + UiDevNodeSnapshot.PaintedFontUnknownReason + ")") + " ",
                    StringComparison.Ordinal) >= 0,
                label + ": and the same painted-font answer for " + node.Path);
            Check(line.IndexOf("appearance=" + Dash(node.Appearance) + " ", StringComparison.Ordinal) >= 0
                    && line.IndexOf("appearance-declared=" + (node.AppearanceDeclared ? "yes" : "no") + " ",
                        StringComparison.Ordinal) >= 0
                    && line.IndexOf("appearance-default=" + Dash(node.AppearanceDefault) + " ",
                        StringComparison.Ordinal) >= 0
                    && line.IndexOf("appearance-supports=" + Dash(node.AppearanceSupported) + " ",
                        StringComparison.Ordinal) >= 0
                    && line.IndexOf("appearance-source=" + node.AppearanceSource + " ",
                        StringComparison.Ordinal) >= 0
                    && line.IndexOf("appearance-pair=" + (node.AppearancePairRegistered ? "yes" : "no") + " ",
                        StringComparison.Ordinal) >= 0,
                label + ": and the same effective appearance and provenance for " + node.Path
                + " (look='" + node.Appearance + "' source=" + node.AppearanceSource + ")");
            Check(line.IndexOf("theme=" + node.ThemeOrigin + " ", StringComparison.Ordinal) >= 0,
                label + ": and the same appearance origin for " + node.Path);
            Check(line.IndexOf("clip=" + node.Clip.ToString() + " ", StringComparison.Ordinal) >= 0,
                label + ": and the same effective clip for " + node.Path);
            // Every remaining boundary, INCLUDING the two that are always unknown: the point of comparing
            // them is that a snapshot that started reporting hover as known while the text still said
            // "unknown" must redden here rather than pass on a subset of fields.
            Check(line.IndexOf("content=" + node.Content.ToString() + " ", StringComparison.Ordinal) >= 0,
                label + ": and the same content boundary for " + node.Path);
            Check(line.IndexOf("hover=" + node.Hover.ToString() + " ", StringComparison.Ordinal) >= 0,
                label + ": and the same hover boundary for " + node.Path);
            Check(line.IndexOf("focus=" + node.Focus.ToString() + " ", StringComparison.Ordinal) >= 0,
                label + ": and the same focus boundary for " + node.Path);
            Check(line.IndexOf("popup-anchor=" + node.PopupAnchor.ToString() + " ", StringComparison.Ordinal) >= 0,
                label + ": and the same popup-anchor boundary for " + node.Path);
            compared++;
        }

        Check(compared == snapshot.Nodes.Count && compared > 0,
            label + ": every retained node was compared, not a sample of them (" + compared + ")");
    }

    private static UiDevGeometrySnapshot SnapshotOf(UiDiagnosticSubscription diagnostics)
    {
        if (!diagnostics.TryGetGeometrySnapshot(out UiDevGeometrySnapshot? snapshot) || snapshot == null)
        {
            throw new Exception("the instrument is on but answered no snapshot");
        }

        return snapshot;
    }

    private static UiDevNodeSnapshot RequireNode(UiDevGeometrySnapshot snapshot, string path)
    {
        UiDevNodeSnapshot? node = snapshot.NodeByPath(path);
        if (node == null)
        {
            throw new Exception("the snapshot carries no node with path '" + path + "'");
        }

        return node;
    }

    /// <summary>The dump's line for one node path. Input lines carry a path too, so they are excluded.</summary>
    private static string RequireNodeLine(string dump, string path)
    {
        foreach (string line in dump.Split(Lines, StringSplitOptions.None))
        {
            if (line.StartsWith("input ", StringComparison.Ordinal)) continue;
            if (line.IndexOf("path=" + path + " ", StringComparison.Ordinal) >= 0) return line;
        }

        throw new Exception("the dump carries no node line with 'path=" + path + "'");
    }

    private static string HeaderLine(string dump)
    {
        int end = dump.IndexOf('\n');
        return end < 0 ? dump : dump.Substring(0, end);
    }

    private static float HeaderCount(string dump, string name)
    {
        return Number(HeaderLine(dump), name);
    }

    private static int CountLines(string dump, string prefix)
    {
        int found = 0;
        foreach (string line in dump.Split(Lines, StringSplitOptions.None))
        {
            if (line.StartsWith(prefix, StringComparison.Ordinal)) found++;
        }

        return found;
    }

    /// <summary>An empty text field as the dump spells it, so the two renderings compare like for like.</summary>
    private static string Dash(string value)
    {
        return string.IsNullOrEmpty(value) ? "-" : value;
    }

    /// <summary>Rect containment, written out because the stub Rect has no Contains to lean on.</summary>
    private static bool ContainsRect(Rect outer, Rect inner)
    {
        return inner.x >= outer.x - 0.001f && inner.y >= outer.y - 0.001f
            && inner.xMax <= outer.xMax + 0.001f && inner.yMax <= outer.yMax + 0.001f;
    }

    /// <summary>The non-default scope: a scheme with a selected font and a named density, on a container.</summary>
    private const string ScopedPage =
        "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
        + "<Styles Schema=\"1\">"
        + "<Scheme Name=\"tinted\"><Color Token=\"Panel\" Value=\"#112233ff\"/>"
        + "<Metric Token=\"Font\" Value=\"Medium\" /></Scheme>"
        + "<Density Name=\"cozy\"><Metric Token=\"RowHeight\" Value=\"40\" /></Density>"
        + "</Styles>"
        + "<Column Id=\"root\" Scheme=\"tinted\" Density=\"cozy\" Padding=\"0\" Gap=\"0\">"
        + "<Widget Id=\"band\" Kind=\"input/button\" Text=\"Band\" ActionBind=\"act\" />"
        + "</Column>"
        + "</UiPage>";

    /// <summary>A second page with a distinct element id, for the host-separation lane.</summary>
    private const string OtherPage =
        "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
        + "<Column Id=\"root\" Padding=\"0\" Gap=\"0\">"
        + "<Widget Id=\"other\" Kind=\"chrome/banner\" Text=\"Other\" Height=\"20\" />"
        + "</Column>"
        + "</UiPage>";

    /// <summary>A trigger that can really own a session popup, and a sibling that does not.</summary>
    private const string PopupPage =
        "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
        + "<Column Id=\"root\" Padding=\"0\" Gap=\"0\">"
        + "<Widget Id=\"trigger\" Kind=\"input/dropdown\" OptionsBind=\"Options\" />"
        + "<Widget Id=\"under\" Kind=\"input/button\" Text=\"Under\" Height=\"28\" ActionBind=\"act\" />"
        + "</Column>"
        + "</UiPage>";

    /// <summary>
    /// The overflow fixture: ONE template and ONE <c>Repeat</c> at the page root, so the DECLARED element
    /// count is four (page, templates section, template Row, template rule, repeat) - far under the manifest
    /// validator's node limit - while every bound item expands to a Row plus a rule at arrange/draw time,
    /// which is what the capture's 512-entry bound counts. The rule's declared Id is <c>r</c> and the
    /// template root's is <c>row</c>, so the composed per-item paths are <c>rows/row#&lt;key&gt;/r#&lt;key&gt;</c>.
    /// </summary>
    private const string OverflowPage =
        "<UiPage Schema=\"2\" Source=\"" + Scope + "\"><Templates>"
        + "<Row Id=\"row\"><Widget Id=\"r\" Kind=\"chrome/rule\" Height=\"2\" /></Row>"
        + "</Templates><Repeat Id=\"rows\" Items=\"items\" Template=\"row\" /></UiPage>";

    /// <summary>Two checkboxes of one kind: one takes the default look, one declares the other.</summary>
    private const string AppearancePage =
        "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
        + "<Column Id=\"root\" Padding=\"0\" Gap=\"0\">"
        + "<Widget Id=\"plain\" Kind=\"input/checkbox\" Bind=\"flag\" Height=\"20\" />"
        + "<Widget Id=\"chosen\" Kind=\"input/checkbox\" Bind=\"box\" Height=\"20\" Appearance=\"checkbox\" />"
        + "</Column>"
        + "</UiPage>";

    /// <summary>One checkbox that declares no look, so the scope's own default is what answers.</summary>
    private const string AliasPage =
        "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
        + "<Column Id=\"root\" Padding=\"0\" Gap=\"0\">"
        + "<Widget Id=\"alias\" Kind=\"input/checkbox\" Bind=\"flag\" Height=\"20\" />"
        + "</Column>"
        + "</UiPage>";

    /// <summary>
    /// DT1 pass-order lane (r2-calibrated): drives REAL UiWindowHost passes through the game's own
    /// entrant. Rimsage Source/Verse/Window.cs, InnerWindowOnGUI Lines 205-309: the game computes
    /// <c>windowRect.AtZero()</c>, contracts it by <c>Margin</c>, calls <c>BeginGroup</c>, and hands
    /// <c>DoWindowContents</c> the ZERO-BASED face - and this shell seals <c>Margin</c> to 0, so the
    /// production entrant is <c>windowRect.AtZero()</c>. The stub's own <c>WindowOnGUI</c> hands the
    /// position-offset face instead (kept for the older lanes, never widened here); driving through the
    /// game-shaped call is what lets the drawn/outline rects below be claims about the product rather than
    /// about the double. The first frame builds the host, the HostAttached door lets a subscription see
    /// that very frame, and the report is read from INSIDE BeforeDraw - exactly where the consumer's
    /// panel publishes the previous completed pass.
    /// <list type="bullet">
    /// <item><b>MUTATION-TARGET (order)</b> - the report taken in frame two's BeforeDraw still describes
    /// frame one: same header pass, same OLD chrome rect, and it never carries frame two's resized rect.
    /// Moving the chrome record back to DrawChrome time (the pre-integration position) splices frame two
    /// onto frame one and reddens this.</item>
    /// <item><b>MUTATION-TARGET (entrant)</b> - driving through the stub's position-offset entrant
    /// instead of the game-shaped zero-based face reddens the draw-local assertion below: the outline
    /// would start at (30,40) while the capture says the chrome lives at those screen coordinates - the
    /// mismatch PM review r2 caught by reading the stub against the game.</item>
    /// <item><b>MUTATION-TARGET (record / paint)</b> - deleting the RecordShellChrome call drops the
    /// chrome to its explicit none-state while the paint remains; deleting the
    /// OutlineShellChrome call removes the twelve hairlines and the chain check with them while the
    /// record stays.</item>
    /// <item><b>GUARD</b> - the frame-one copy is re-checked AFTER frame two for real (chrome rect, pass
    /// number and root window all still frame one's); the first frame's BeforeDraw reads an empty report;
    /// the switch-off pass adds zero chrome hairlines. These are independent guards. A record deletion
    /// removes the copy check's chrome prerequisite and can also fail it; that failure is not a
    /// mutation proof of snapshot immutability.</item>
    /// </list>
    /// </summary>
    private static void VerifyShellChromePassOrder()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();

        ChromeWindow window = new ChromeWindow();
        window.windowRect = new Rect(0f, 0f, 200f, 200f);

        window.DriveProductionPass();
        Check(window.BeforeDrawDumps.Count == 1 && window.BeforeDrawDumps[0].Length == 0,
            "the first frame's BeforeDraw reads an empty report - no completed pass exists yet");
        UiDiagnosticSubscription? subscription = window.Subscription;
        Check(subscription != null && subscription.GeometryEnabled,
            "the subscription made through HostAttached sees the very first frame");
        if (subscription == null) return;

        string frameOne = subscription.DumpGeometry();
        int passOne = HeaderPass(frameOne);
        Check(frameOne.IndexOf("chrome space=window outer=(0,0,200,200)", StringComparison.Ordinal) >= 0,
            "frame one's capture carries its chrome beside its nodes: " + Head(frameOne));
        Check(frameOne.IndexOf("viewport path=root/list", StringComparison.Ordinal) >= 0,
            "and the scoped viewport node of the same pass");
        UiDevGeometrySnapshot copyOne = SnapshotOf(subscription);
        Check(copyOne.ShellChrome is UiWindowChrome oneChrome
                && SameChrome(oneChrome.Outer, new Rect(0f, 0f, 200f, 200f)),
            "the data rendering hands out the same frame-one chrome as the text one");

        window.windowRect = new Rect(30f, 40f, 220f, 210f); // drag + resize between frames
        window.DriveProductionPass();

        string prior = window.BeforeDrawDumps[1];
        Check(HeaderPass(prior) == passOne && passOne > 0,
            "what frame two's BeforeDraw reports is frame ONE's pass, header included: "
            + HeaderPass(prior) + " vs " + passOne);
        Check(prior.IndexOf("outer=(0,0,200,200)", StringComparison.Ordinal) >= 0
                && prior.IndexOf("outer=(30,40,220,210)", StringComparison.Ordinal) < 0,
            "and its chrome is the OLD rect - recording chrome at DrawChrome time would splice frame two's"
            + " geometry onto frame one");
        Check(!ReferenceEquals(SnapshotOf(subscription), copyOne),
            "each read hands out a fresh copy, never a live view");
        UiDevGeometrySnapshot afterTwo = SnapshotOf(subscription);
        Check(copyOne.ShellChrome is UiWindowChrome stillOne
                && SameChrome(stillOne.Outer, new Rect(0f, 0f, 200f, 200f))
                && copyOne.Pass == passOne && afterTwo.Pass != passOne
                && afterTwo.ShellChrome is UiWindowChrome nowChrome
                && !SameChrome(nowChrome.Outer, stillOne.Outer),
            "GUARD, checked across frames after the resize: the frame-one copy still answers frame one "
            + "(chrome, pass) while frame two records its own");
        string frameTwo = subscription.DumpGeometry();
        Check(frameTwo.IndexOf("chrome space=window outer=(30,40,220,210)", StringComparison.Ordinal) >= 0
                && HeaderPass(frameTwo) != passOne,
            "frame two, once complete, records its OWN pass and its OWN chrome: " + Head(frameTwo));

        // The chrome outline runs on the same switch and ink, independent of the record, and its
        // draw-local rects must lift through the real window origin onto the screen chrome recorded.
        subscription.GeometryOverlay = false;
        ClearSolids();
        window.DriveProductionPass();
        int chromeInkOff = SolidCount();
        subscription.GeometryOverlay = true;
        ClearSolids();
        window.DriveProductionPass();
        int chromeInkOn = SolidCount();
        UiDevGeometrySnapshot inkPass = SnapshotOf(subscription);
        Check(inkPass.Nodes.Count > 0
                && chromeInkOn - chromeInkOff == (inkPass.Nodes.Count + 3) * OverlayHairlinesPerNode,
            "the chrome bands add their three rects on top of the element outlines the SAME switch paints: "
            + (chromeInkOn - chromeInkOff).ToString(CultureInfo.InvariantCulture) + " = ("
            + inkPass.Nodes.Count.ToString(CultureInfo.InvariantCulture) + " entries + 3 chrome) x 4");

        // The LAST twelve hairlines are the three chrome bands, painted after the page walk. Through the
        // game-shaped entrant they are face-local: the outer frame's top edge sits at x=0 with the full
        // face width, the title band's bottom edge at y=TitleBar-1. The window's (30,40) must NOT leak
        // into them - and adding that origin must land exactly on the screen chrome the capture recorded.
        System.Collections.IList inkRects = SolidRectsRecorded();
        int first = inkRects.Count - 12;
        Check(first >= 0, "the overlay pass recorded the twelve chrome hairlines");
        Rect outerTop = (Rect)inkRects[first]!;
        Rect titleBottom = (Rect)inkRects[first + 5]!;
        UiWindowChrome recordedChrome = inkPass.ShellChrome!.Value;
        Check(Same(outerTop.x, 0f) && Same(outerTop.y, 0f) && Same(outerTop.width, 220f)
                && Same(titleBottom.y, 55f) && Same(titleBottom.width, 220f),
            "the chrome bands paint FACE-LOCAL - the window position never leaks into a draw-local rect: "
            + "outerTop=(" + outerTop.x + "," + outerTop.y + "," + outerTop.width + ") titleBottom.y="
            + titleBottom.y);
        Check(SameChrome(
                new Rect(outerTop.x + recordedChrome.Outer.x, outerTop.y + recordedChrome.Outer.y,
                    outerTop.width, outerTop.height),
                new Rect(recordedChrome.Outer.x, recordedChrome.Outer.y, recordedChrome.Outer.width, 1f))
                && SameChrome(
                new Rect(titleBottom.x + recordedChrome.Outer.x, titleBottom.y + recordedChrome.Outer.y,
                    titleBottom.width, titleBottom.height),
                new Rect(recordedChrome.Outer.x, recordedChrome.Outer.y + recordedChrome.TitleBarHeight - 1f,
                    recordedChrome.Outer.width, 1f)),
            "draw-local outline + the real window origin == the screen chrome the same pass recorded "
            + "- one geometry, two renderings, through the production space conversion");
        Check(subscription.DumpGeometry().IndexOf("chrome space=window", StringComparison.Ordinal) >= 0,
            "the record flows whatever the paint switch says");
        subscription.GeometryOverlay = false;
        ClearSolids();
        window.DriveProductionPass();
        Check(SolidCount() == chromeInkOff, "and the chrome ink leaves with the switch");

        window.Close();
    }

    /// <summary>The stub's recorded solid rects - paint order, the same list SolidCount counts.</summary>
    private static System.Collections.IList SolidRectsRecorded()
    {
        FieldInfo? field = typeof(Verse.Widgets).GetField(
            "DrawBoxSolidRects", BindingFlags.Public | BindingFlags.Static);
        if (field == null) throw new Exception("the stub no longer exposes DrawBoxSolidRects");
        return (System.Collections.IList)field.GetValue(null)!;
    }

    /// <summary>The header pass number of a dump, or -2 when there is none.</summary>
    private static int HeaderPass(string dump)
    {
        int at = dump.IndexOf("pass=", StringComparison.Ordinal);
        if (at < 0) return -2;
        int start = at + 5;
        int end = start;
        while (end < dump.Length && (char.IsDigit(dump[end]) || dump[end] == '-')) end++;
        return int.Parse(dump.Substring(start, end - start), CultureInfo.InvariantCulture);
    }

    /// <summary>First line of a dump, bounded, for assertion messages.</summary>
    private static string Head(string dump)
    {
        int nl = dump.IndexOf('\n');
        string line = nl < 0 ? dump : dump.Substring(0, nl);
        return line.Length > 140 ? line.Substring(0, 140) : line;
    }

    private static bool SameChrome(Rect left, Rect right)
    {
        return Math.Abs(left.x - right.x) <= 0.01f && Math.Abs(left.y - right.y) <= 0.01f
            && Math.Abs(left.width - right.width) <= 0.01f && Math.Abs(left.height - right.height) <= 0.01f;
    }

    /// <summary>
    /// A titled, closable shell over the standard Page, recording what BeforeDraw sees. The subscription
    /// is made inside HostAttached - the documented door that lets a consumer see the very first frame.
    /// </summary>
    private sealed class ChromeWindow : UiWindowHost
    {
        private readonly UiBindings bindings = new UiBindings();

        internal readonly List<string> BeforeDrawDumps = new List<string>();

        internal ChromeWindow()
        {
            bindings.BindCommand("act", () => { });
            HostAttached += attached =>
            {
                Subscription = UiDiagnosticHub.Subscribe(attached);
                Subscription.GeometryEnabled = true;
            };
        }

        internal UiDiagnosticSubscription? Subscription { get; private set; }

        /// <summary>
        /// The game-shaped pass: Rimsage Source/Verse/Window.cs InnerWindowOnGUI (Lines 205-309) hands
        /// DoWindowContents the ZERO-BASED contracted face, and this shell seals Margin to 0 - so the
        /// production entrant is exactly windowRect.AtZero(). The stub's own WindowOnGUI keeps its
        /// position-offset plumbing for the older lanes (not widened here); this fixture calls through the
        /// real virtual the game calls, so draw-local geometry in this lane is a product claim.
        /// </summary>
        internal void DriveProductionPass()
        {
            // windowRect.AtZero(), spelled out so the fixture needs no Verse-using for the extension.
            DoWindowContents(new Rect(0f, 0f, windowRect.width, windowRect.height));
        }

        protected override UiTheme Theme => UiTheme.Vanilla;

        protected override string Title => "chrome-lane";

        protected override string CloseText => "x";

        protected override ITextMetrics Metrics => new StubMetrics();

        protected override UiHost CreateHost()
        {
            return new UiHost(
                Scope,
                UiLayoutManifest.Parse(Page),
                bindings,
                UiTheme.Vanilla,
                new StubMetrics(),
                new StubTranslation());
        }

        protected override void DrawNotice(Rect rect, UiWindowNotice notice)
        {
        }

        protected override void BeforeDraw(Rect contentRect)
        {
            UiDiagnosticSubscription? ambient = UiDiagnosticHub.ActiveSubscription;
            BeforeDrawDumps.Add(ambient == null ? "" : ambient.DumpGeometry());
        }
    }
#else
    // --- release half -----------------------------------------------------------------------------

    /// <summary>
    /// The shipped configuration. The instrument does not exist here, and the enable path says so instead of
    /// accepting the request and then answering every question with emptiness.
    /// </summary>
    private static void VerifyAbsentInRelease()
    {
        using UiHost host = NewHost(Page);
        UiDiagnosticSubscription diagnostics = host.Diagnostics;
        Check(!diagnostics.GeometryEnabled, "a release payload reports the instrument as unavailable");

        bool refused = false;
        try
        {
            diagnostics.GeometryEnabled = true;
        }
        catch (InvalidOperationException)
        {
            refused = true;
        }

        Check(refused, "and refuses the opt-in rather than accepting it");
        Check(!diagnostics.GeometryEnabled, "so it stays off");

        Pass(host);
        Check(diagnostics.DumpGeometry().Length == 0,
            "and a full frame leaves the dump empty, which is what compiling it out means");
        Check(!diagnostics.TryGetGeometrySnapshot(out UiDevGeometrySnapshot? none) && none == null,
            "and the structured reader answers 'nothing captured' rather than an empty-looking snapshot: "
            + (none == null ? "null" : none.ToString()));

        bool overlayRefused = false;
        try
        {
            diagnostics.GeometryOverlay = true;
        }
        catch (InvalidOperationException)
        {
            overlayRefused = true;
        }

        Check(overlayRefused, "the overlay is refused the same way");
    }
#endif

    // --- page and helpers --------------------------------------------------------------------------

    private const string Page =
        "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
        + "<Column Id=\"root\" Padding=\"0\" Gap=\"0\">"
        + "<Row Id=\"row\" Padding=\"0\" Gap=\"0\">"
        + "<Widget Id=\"band\" Kind=\"input/button\" Text=\"Band\" Chrome=\"none\""
        + " Height=\"MatchContent\" ActionBind=\"act\" />"
        + "<Widget Id=\"caption\" Kind=\"chrome/banner\" Text=\"Band\" Emphasis=\"Muted\" />"
        + "</Row>"
        + "<Scroll Id=\"list\" Padding=\"0\" Gap=\"0\" Height=\"40\">"
        + "<Widget Id=\"inner\" Kind=\"chrome/banner\" Text=\"Inner\" Height=\"120\" />"
        + "</Scroll>"
        + "</Column>"
        + "</UiPage>";

    private static UiHost NewHost(string xml, bool commandEnabled = true)
    {
        var bindings = new UiBindings();
        if (commandEnabled)
        {
            bindings.BindCommand("act", () => { });
        }
        else
        {
            bindings.BindCommand("act", () => { }, () => false);
        }

        return new UiHost(
            Scope, UiLayoutManifest.Parse(xml), bindings, UiTheme.Vanilla, new StubMetrics(), new StubTranslation());
    }

    /// <summary>
    /// The same host with a collection binding, which is what a <c>Repeat</c> expands: the item list is the
    /// only thing the runtime multiplies, so the declared page stays tiny while the pass does not.
    /// </summary>
    private static UiHost NewHost(string xml, List<string> itemKeys)
    {
        var bindings = new UiBindings();
        bindings.BindCommand("act", () => { });
        bindings.BindReadOnly<IReadOnlyList<string>>("items", () => itemKeys, UiInvalidation.Structure);
        return new UiHost(
            Scope, UiLayoutManifest.Parse(xml), bindings, UiTheme.Vanilla, new StubMetrics(), new StubTranslation());
    }

    /// <summary>
    /// The same host with the two writable boolean bindings the appearance fixtures' checkboxes declare. The
    /// state is writable because that is the kind's own contract (a checkbox toggles it), and a lane that made
    /// it read-only would be measuring a fixture difference rather than the appearance.
    /// </summary>
    private static UiHost NewBoolHost(string xml)
    {
        bool flag = true;
        bool box = false;
        var bindings = new UiBindings();
        bindings.BindValue<bool>("flag", () => flag, value => flag = value);
        bindings.BindValue<bool>("box", () => box, value => box = value);
        return new UiHost(
            Scope, UiLayoutManifest.Parse(xml), bindings, UiTheme.Vanilla, new StubMetrics(), new StubTranslation());
    }

    /// <summary>One complete frame (draw included), then the snapshot the same geometry produced.</summary>
    private static UiLayoutSnapshot Pass(UiHost host)
    {
        host.DrawFrame(Viewport);
        return host.MeasureAndArrange(new Vector2(Viewport.width, Viewport.height));
    }

    private static void Print(string dump)
    {
        foreach (string line in dump.Split(Lines, StringSplitOptions.None))
        {
            if (line.Length > 0) Console.WriteLine("    | " + line);
        }
    }

    /// <summary>
    /// Prints only the sampled presses of a dump. The geometry block is printed in full once; the input half
    /// answers the other question this instrument was asked, so its lines are evidence the gate can see too.
    /// </summary>
    private static void PrintInputs(string dump)
    {
        foreach (string line in dump.Split(Lines, StringSplitOptions.None))
        {
            if (line.StartsWith("input ", StringComparison.Ordinal)) Console.WriteLine("    | " + line);
        }
    }

    private static string Require(string dump, string token)
    {
        string? line = LineWith(dump, token);
        if (line == null) throw new Exception("the dump carries no line with '" + token + "':" + Environment.NewLine + dump);
        return line;
    }

    private static string? LineWith(string dump, string token)
    {
        foreach (string line in dump.Split(Lines, StringSplitOptions.None))
        {
            if (line.IndexOf(token, StringComparison.Ordinal) >= 0) return line;
        }

        return null;
    }

    private static bool TryRect(string line, string name, out Rect rect)
    {
        rect = default;
        if (!Slice(line, name, 4, out string[] parts)) return false;
        rect = new Rect(Value(parts[0]), Value(parts[1]), Value(parts[2]), Value(parts[3]));
        return true;
    }

    private static bool TryPoint(string line, string name, out Vector2 point)
    {
        point = default;
        if (!Slice(line, name, 2, out string[] parts)) return false;
        point = new Vector2(Value(parts[0]), Value(parts[1]));
        return true;
    }

    private static bool Slice(string line, string name, int count, out string[] parts)
    {
        parts = Array.Empty<string>();
        int start = line.IndexOf(name + "(", StringComparison.Ordinal);
        if (start < 0) return false;
        start += name.Length + 1;
        int end = line.IndexOf(')', start);
        if (end < 0) return false;
        parts = line.Substring(start, end - start).Split(new[] { ',' }, StringSplitOptions.None);
        return parts.Length == count;
    }

    private static float Number(string line, string name)
    {
        int start = line.IndexOf(name, StringComparison.Ordinal);
        if (start < 0) return float.NaN;
        start += name.Length;
        int end = start;
        while (end < line.Length && (char.IsDigit(line[end]) || line[end] == '.' || line[end] == '-')) end++;
        return Value(line.Substring(start, end - start));
    }

    private static float Value(string text)
    {
        return float.Parse(text, CultureInfo.InvariantCulture);
    }

    private static bool Same(float left, float right)
    {
        return Math.Abs(left - right) <= 0.001f;
    }

    private static bool SameRect(Rect left, Rect right)
    {
        return Same(left.x, right.x) && Same(left.y, right.y)
            && Same(left.width, right.width) && Same(left.height, right.height);
    }

    private static string Text(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string Show(Rect rect)
    {
        return "(" + Text(rect.x) + "," + Text(rect.y) + "," + Text(rect.width) + "," + Text(rect.height) + ")";
    }

    private static string Show(Vector2 point)
    {
        return "(" + Text(point.x) + "," + Text(point.y) + ")";
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

    private sealed class StubMetrics : ITextMetrics
    {
        public float MeasureText(string text, UiFont font, float width)
        {
            return string.IsNullOrEmpty(text) ? 0f : StubTextWidth.Of(text, font);
        }

        public float MeasureWidth(string text, UiFont font)
        {
            return string.IsNullOrEmpty(text) ? 0f : StubTextWidth.Of(text, font);
        }
    }

    private sealed class StubTranslation : IUiTranslation
    {
        public string Translate(string key) => "[" + key + "]";

        public int TranslationRevision => 0;
    }
}
