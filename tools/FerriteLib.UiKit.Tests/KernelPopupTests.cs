using System;
using System.Collections.Generic;
using FerriteLib.UiKit;
using FerriteLib.UiKit.Kernel;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// Popup coordinate contract: a dropdown trigger drawn inside a scrolled container stores its
/// popup anchor in the Host's final usable window space (the engine translates draw rects by the
/// scroll offset), and the popup pass draws and hit-tests rows in that same stored space. A
/// content-local row position must not hit while the popup is open under a scroll offset.
/// </summary>
internal static class KernelPopupTests
{
    private const string Scope = "popup-test";
    private const string HeightWidgetKind = "test/height";
    private static readonly string Xml =
        "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
        + "<Scroll Id=\"scroll\" Padding=\"0\" Gap=\"0\" Height=\"200\">"
        + "<Widget Id=\"spacer\" Kind=\"" + HeightWidgetKind + "\" Height=\"100\" />"
        + "<Widget Id=\"dropdown\" Kind=\"input/dropdown\" OptionsBind=\"Options\" />"
        + "<Widget Id=\"tail\" Kind=\"" + HeightWidgetKind + "\" Height=\"100\" />"
        + "</Scroll>"
        + "</UiPage>";

    public static int RunAll()
    {
        int failures = 0;
        failures += Run("Same-node composite siblings yield to popup options", VerifyCompositeSiblingsYield);
        failures += Run("Covered value primitives skip native input and recover after close", VerifyCoveredValuePrimitives);
        failures += Run("Covered trigger yields under a real IMGUI event pump", VerifyCoveredTriggerYieldsUnderRealEventPump);
        failures += Run("Covered trigger yields inside a scrolled, offset container", VerifyCoveredTriggerYieldsInsideScrolledOffsetContainer);
        failures += Run("Scroll dropdown anchor/popup share Host window space", VerifyScrollDropdownWindowSpace);
        failures += Run("Popup row over a lower trigger selects, and does not open that trigger", VerifyPopupWinsOverCoveredTrigger);
        failures += Run("Popup flips above the trigger instead of leaving the viewport", VerifyPopupFlipsIntoViewport);
        failures += Run("UiPopup rect rules: below, flip, pin, clamp, unpublished viewport", VerifyUiPopupRectRules);
        failures += Run("An open popup follows its trigger through a scroll, and is released when the trigger leaves", VerifyOpenPopupFollowsTheTrigger);
        return failures;
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
            Console.Error.WriteLine("  FAIL: " + name + " :: " + ex.Message);
            return 1;
        }
    }

    private static void VerifyScrollDropdownWindowSpace()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        UiWidgetRegistry.Register(Scope, HeightWidgetKind, () => new HeightWidget());

        UiLayoutManifest manifest = UiLayoutManifest.Parse(Xml);
        var bindings = new UiBindings();
        string current = "x";
        bindings.BindValue("dropdown", () => current, value => current = value);
        bindings.BindOptions("Options", () => new List<string> { "x", "y" });

        using UiHost host = new(Scope, manifest, bindings, UiTheme.Vanilla, new StubMetrics(), new StubTranslation());

        // Content is 100 + 28 + 100 = 228 tall; viewport is 200, so the scroll offset clamps to 28.
        // Vertical overflow reserves the 16px scrollbar width, leaving a 284px trigger.
        // The scroll element's node exists once the page has been arranged, so the offset is written
        // against that node before the frame under test runs.
        host.MeasureAndArrange(new Vector2(300f, 300f));
        UiNode scrollNode = host.Session.GetNodeByElementId("scroll")
            ?? throw new Exception("the arranged scroll element has no node");
        host.Session.SetScrollPosition(scrollNode, new Vector2(0f, 20f));

        // The Host viewport starts at (20, 30). The trigger draws at content-local
        // (0, 100, 284, 28), so its window-space rect is (20, 30 + 100 - 20, 284, 28).
        // A non-zero viewport is required here: a zero origin would hide double-offset bugs.
        Rect triggerContentLocal = new(0f, 100f, 284f, 28f);
        Rect expectedAnchor = new(20f, 110f, 284f, 28f);

        try
        {
            // Frame 1: click the trigger (content-local rect) -> popup opens anchored in window space.
            UiNative.ButtonOverride = rect => SameRect(rect, triggerContentLocal);
            host.DrawFrame(new Rect(20f, 30f, 300f, 300f));

            if (!host.Session.IsPopupOpen("dropdown"))
            {
                throw new Exception("Dropdown did not open from the scrolled trigger");
            }

            if (!host.Session.OpenPopupAnchor.HasValue || !SameRect(host.Session.OpenPopupAnchor.Value, expectedAnchor))
            {
                throw new Exception("Popup anchor is not the Host window-space trigger rect: "
                    + (host.Session.OpenPopupAnchor.HasValue ? host.Session.OpenPopupAnchor.Value.ToString() : "<null>")
                    + " (expected " + expectedAnchor + ")");
            }

            // Frame 2: click the popup row at its window-space position.
            // Row 1 = (20, 138 + 24, 284, 24) = (20, 162, 284, 24); selecting it writes "y".
            Rect windowRow1 = new(20f, 162f, 284f, 24f);
            UiNative.ButtonOverride = rect => SameRect(rect, windowRow1);
            host.DrawFrame(new Rect(20f, 30f, 300f, 300f));

            if (host.Session.IsPopupOpen("dropdown"))
            {
                throw new Exception("Popup did not close after a window-space row selection");
            }

            if (!string.Equals(current, "y", StringComparison.Ordinal))
            {
                throw new Exception("Window-space row selection did not write the binding (current='" + current + "')");
            }

            // Frame 3: reopen the popup.
            UiNative.ButtonOverride = rect => SameRect(rect, triggerContentLocal);
            host.DrawFrame(new Rect(20f, 30f, 300f, 300f));
            if (!host.Session.IsPopupOpen("dropdown"))
            {
                throw new Exception("Dropdown did not reopen");
            }

            // Frame 4: a click at the content-local row position (0, 152, 284, 24) must NOT hit:
            // the popup lives in window space under the scroll offset.
            Rect contentSpaceRow1 = new(0f, 152f, 284f, 24f);
            UiNative.ButtonOverride = rect => SameRect(rect, contentSpaceRow1);
            host.DrawFrame(new Rect(20f, 30f, 300f, 300f));

            if (!host.Session.IsPopupOpen("dropdown"))
            {
                throw new Exception("Content-local row position wrongly hit the window-space popup");
            }

            // Frame 5: the window-space row still selects and closes.
            UiNative.ButtonOverride = rect => SameRect(rect, windowRow1);
            host.DrawFrame(new Rect(20f, 30f, 300f, 300f));
            if (host.Session.IsPopupOpen("dropdown") || !string.Equals(current, "y", StringComparison.Ordinal))
            {
                throw new Exception("Window-space row hit failed after the content-space miss");
            }
        }
        finally
        {
            UiNative.ButtonOverride = null;
        }
    }

    private static bool SameRect(Rect a, Rect b)
    {
        return Math.Abs(a.x - b.x) < 0.01f
            && Math.Abs(a.y - b.y) < 0.01f
            && Math.Abs(a.width - b.width) < 0.01f
            && Math.Abs(a.height - b.height) < 0.01f;
    }

    /// <summary>
    /// The reported in-game failure: a dropdown option row overlaps the next row's own trigger, and the
    /// content pass runs before the popup pass, so the lower trigger used to eat the click and open
    /// itself instead of the option being selected. Hit-testing is point-based here on purpose — the
    /// existing lane's exact-rect override cannot express two rects claiming one point.
    /// </summary>
    private static void VerifyPopupWinsOverCoveredTrigger()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();

        const string xml =
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Column Id=\"col\" Padding=\"0\" Gap=\"2\">"
            + "<Widget Id=\"first\" Kind=\"input/dropdown\" OptionsBind=\"Options\" />"
            + "<Widget Id=\"second\" Kind=\"input/dropdown\" OptionsBind=\"Options\" />"
            + "</Column>"
            + "</UiPage>";

        UiLayoutManifest manifest = UiLayoutManifest.Parse(xml);
        var bindings = new UiBindings();
        string first = "x";
        string second = "x";
        bindings.BindValue("first", () => first, value => first = value);
        bindings.BindValue("second", () => second, value => second = value);
        bindings.BindOptions("Options", () => new List<string> { "x", "y" });

        using UiHost host = new(Scope, manifest, bindings, UiTheme.Vanilla, new StubMetrics(), new StubTranslation());

        // Triggers: first (0,0,300,28), second (0,30,300,28). First's popup covers y 28..76, so its
        // second option row (y 52..76) sits on top of the second trigger.
        var trace = new List<string>();
        UiNative.Trace = trace.Add;
        var click = new Vector2(10f, 55f);
        try
        {
            UiNative.DebugMousePositionEnabled = true;
            UiNative.DebugMousePosition = new Vector2(10f, 10f);
            UiNative.ButtonOverride = rect => Over(rect, new Vector2(10f, 10f));
            host.DrawFrame(new Rect(0f, 0f, 300f, 300f));

            if (!host.Session.IsPopupOpen("first"))
            {
                throw new Exception("first dropdown did not open");
            }

            Rect? popupLayer = PopupLayer(host.Session);
            if (!popupLayer.HasValue)
            {
                throw new Exception("popup pass pushed no hit layer, so nothing can yield to it");
            }

            Rect popup = popupLayer.Value;
            if (!Over(popup, click))
            {
                throw new Exception("test premise broken: the click " + click + " is not inside the popup " + popup);
            }

            Rect secondTrigger = new(0f, 30f, 300f, 28f);
            if (!Over(secondTrigger, click))
            {
                throw new Exception("test premise broken: the click does not overlap the lower trigger " + secondTrigger);
            }

            UiNative.DebugMousePosition = click;
            UiNative.ButtonOverride = rect => Over(rect, click);
            host.DrawFrame(new Rect(0f, 0f, 300f, 300f));

            if (!string.Equals(first, "y", StringComparison.Ordinal))
            {
                throw new Exception("option under a lower trigger did not select (first='" + first + "')");
            }

            if (host.Session.IsPopupOpen("second"))
            {
                throw new Exception("the covered lower trigger wrongly took the click and opened its own popup");
            }

            if (host.Session.IsPopupOpen("first"))
            {
                throw new Exception("popup stayed open after its option was selected");
            }

            if (!string.Equals(second, "x", StringComparison.Ordinal))
            {
                throw new Exception("the lower dropdown's value changed without being opened (second='" + second + "')");
            }

            if (!trace.Exists(line => line.IndexOf("yields=true", StringComparison.Ordinal) >= 0))
            {
                throw new Exception("the covered trigger never logged a yield decision: " + string.Join(" | ", trace));
            }

            if (!trace.Exists(line => line.IndexOf("option fired", StringComparison.Ordinal) >= 0))
            {
                throw new Exception("the option row never logged a fire: " + string.Join(" | ", trace));
            }
        }
        finally
        {
            UiNative.ButtonOverride = null;
            UiNative.Trace = null;
            UiNative.DebugMousePositionEnabled = false;
        }
    }

    /// <summary>
    /// The overlap lane above clicks through UiNative.ButtonOverride, which proves our yield logic but
    /// not its interaction with the native control contract the game actually runs. This lane pumps
    /// real event passes (Layout/MouseDown/MouseUp/Repaint) through the faithful stub ButtonInvisible
    /// (hot-control capture on down, activation on up, event consumption) with no override and no
    /// debug seam: if a control drawn earlier in the pass can still steal the option click, it steals
    /// it here.
    /// </summary>
    private static void VerifyCoveredTriggerYieldsUnderRealEventPump()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();

        const string xml =
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Column Id=\"col\" Padding=\"0\" Gap=\"2\">"
            + "<Widget Id=\"first\" Kind=\"input/dropdown\" OptionsBind=\"Options\" />"
            + "<Widget Id=\"second\" Kind=\"input/dropdown\" OptionsBind=\"Options\" />"
            + "</Column>"
            + "</UiPage>";

        UiLayoutManifest manifest = UiLayoutManifest.Parse(xml);
        var bindings = new UiBindings();
        string first = "x";
        string second = "x";
        bindings.BindValue("first", () => first, value => first = value);
        bindings.BindValue("second", () => second, value => second = value);
        bindings.BindOptions("Options", () => new List<string> { "x", "y" });

        using UiHost host = new(Scope, manifest, bindings, UiTheme.Vanilla, new StubMetrics(), new StubTranslation());

        var trace = new List<string>();
        UiNative.Trace = trace.Add;
        Vector2 openClick = new(10f, 10f);
        Vector2 optionClick = new(10f, 55f);
        try
        {
            Pump(host, EventType.Layout, openClick);
            Pump(host, EventType.MouseDown, openClick);
            Pump(host, EventType.MouseUp, openClick);
            Pump(host, EventType.Repaint, openClick);

            if (!host.Session.IsPopupOpen("first"))
            {
                throw new Exception("real-event pump did not open the first dropdown; trace: " + string.Join(" | ", trace));
            }

            if (!PopupLayer(host.Session).HasValue)
            {
                throw new Exception("popup layer was not pushed under the real-event pump");
            }

            Pump(host, EventType.Layout, optionClick);
            Pump(host, EventType.MouseDown, optionClick);
            Pump(host, EventType.MouseUp, optionClick);
            Pump(host, EventType.Repaint, optionClick);

            if (!string.Equals(first, "y", StringComparison.Ordinal))
            {
                throw new Exception("option under a lower trigger did not select under the real-event pump (first='" + first + "'); trace: " + string.Join(" | ", trace));
            }

            if (host.Session.IsPopupOpen("second"))
            {
                throw new Exception("the covered trigger stole the click under the real-event pump; trace: " + string.Join(" | ", trace));
            }

            if (host.Session.IsPopupOpen("first"))
            {
                throw new Exception("popup stayed open after its option was selected under the real-event pump");
            }
        }
        finally
        {
            UiNative.Trace = null;
            Event.current = null;
            GUIUtility.hotControl = 0;
        }
    }

    /// <summary>
    /// The topmost popup layer of the session's hit stack, or null when none is pushed. This is the
    /// diagnostic read of the stack (the lane's former "published popup rect"); input dispatch itself only
    /// ever asks <see cref="UiSession.IsPointerOverHigherLayer"/>.
    /// </summary>
    private static Rect? PopupLayer(UiSession session)
    {
        for (int i = session.HitLayers.Count - 1; i >= 0; i--)
        {
            if (session.HitLayers[i].IsPopup) return session.HitLayers[i].Rect;
        }

        return null;
    }

    private static void Pump(UiHost host, EventType type, Vector2 point)
    {
        // Compiled against the real UnityEngine surface (Krafs ref): Event has no public constructor,
        // so take an instance from the static factory and overwrite the fields the lanes need. At
        // runtime this binds to the stub Event, which mirrors the same shape; a fresh instance per
        // pass is also what makes the stub's control-id counter reset per pass.
        Event e = Event.KeyboardEvent("space");
        e.type = type;
        e.button = 0;
        e.mousePosition = point;
        Event.current = e;
        host.DrawFrame(new Rect(0f, 0f, 300f, 300f));
        Event.current = null;
    }


    /// <summary>
    /// Regression for the 2026-09-04 in-game failure: inside a scroll container under a non-zero
    /// viewport origin, the event pointer lives in the container's draw space while the published
    /// popup rect lives in Host window space. Comparing them raw made every yield decision false and
    /// let the covered trigger steal the option click. The lane derives the container origin from a
    /// probe pass (trace reports both spaces), then clicks the overlapping option in draw space and
    /// demands the selection land without the lower dropdown opening.
    /// </summary>
    private static void VerifyCoveredTriggerYieldsInsideScrolledOffsetContainer()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        UiWidgetRegistry.Register(Scope, HeightWidgetKind, () => new HeightWidget());

        const string xml =
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Scroll Id=\"scroll\" Padding=\"0\" Gap=\"0\" Height=\"200\">"
            + "<Column Id=\"col\" Padding=\"0\" Gap=\"2\">"
            + "<Widget Id=\"spacer\" Kind=\"" + HeightWidgetKind + "\" Height=\"300\" />"
            + "<Widget Id=\"first\" Kind=\"input/dropdown\" OptionsBind=\"Options\" />"
            + "<Widget Id=\"second\" Kind=\"input/dropdown\" OptionsBind=\"Options\" />"
            + "</Column>"
            + "</Scroll>"
            + "</UiPage>";

        UiLayoutManifest manifest = UiLayoutManifest.Parse(xml);
        var bindings = new UiBindings();
        string first = "x";
        string second = "x";
        bindings.BindValue("first", () => first, value => first = value);
        bindings.BindValue("second", () => second, value => second = value);
        bindings.BindOptions("Options", () => new List<string> { "x", "y" });

        using UiHost host = new(Scope, manifest, bindings, UiTheme.Vanilla, new StubMetrics(), new StubTranslation());

        // Warm arrange: the node the scroll offset belongs to exists only after the page has been
        // arranged once.
        host.MeasureAndArrange(new Vector2(300f, 300f));
        UiNode scrollNode = host.Session.GetNodeByElementId("scroll")
            ?? throw new Exception("the arranged scroll element has no node");
        host.Session.SetScrollPosition(scrollNode, new Vector2(0f, 160f));

        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(300f, 300f));
        host.Session.OpenPopup("first", snapshot.RectById["first"]);

        var trace = new List<string>();
        UiNative.Trace = trace.Add;
        try
        {
            // First pass publishes the popup rect; the trace also reveals the container origin,
            // because a draw-space point at (0,0) maps to the origin in window space.
            Pump(host, EventType.Repaint, new Vector2(0f, 0f));

            Rect? scrolledPopup = PopupLayer(host.Session);
            if (!host.Session.IsPopupOpen("first") || !scrolledPopup.HasValue)
            {
                throw new Exception("scrolled container did not open/push the popup; trace: " + string.Join(" | ", trace));
            }

            Rect popup = scrolledPopup.Value;

            // The pumped pointer is window space; the stub group model presents it to the content
            // pass in container-local space and to the popup pass in window space, exactly like the
            // real IMGUI groups do in game.
            Vector2 optionClick = new(popup.x + 10f, popup.y + 27f);
            Pump(host, EventType.Layout, optionClick);
            Pump(host, EventType.MouseDown, optionClick);
            Pump(host, EventType.MouseUp, optionClick);
            Pump(host, EventType.Repaint, optionClick);

            // Premise: the covered trigger must have seen the converted pointer inside the popup,
            // otherwise this lane would pass without the overlap ever existing.
            if (!trace.Exists(line => line.IndexOf("id=second", StringComparison.Ordinal) >= 0
                && line.IndexOf("yields=true", StringComparison.Ordinal) >= 0))
            {
                throw new Exception("the lower trigger never saw the pointer inside the popup; overlap premise broken; trace: " + string.Join(" | ", trace));
            }

            if (!string.Equals(first, "y", StringComparison.Ordinal))
            {
                throw new Exception("option under a lower trigger did not select inside the scrolled container (first='" + first + "'); trace: " + string.Join(" | ", trace));
            }

            if (host.Session.IsPopupOpen("second"))
            {
                throw new Exception("the covered trigger stole the click inside the scrolled container; trace: " + string.Join(" | ", trace));
            }
        }
        finally
        {
            UiNative.Trace = null;
            Event.current = null;
            GUIUtility.hotControl = 0;
        }
    }
    /// <summary>
    /// A popup that would run past the bottom of the Host viewport cannot be clicked at all, so it must
    /// flip above its trigger rather than be clipped away.
    /// </summary>
    private static void VerifyPopupFlipsIntoViewport()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        UiWidgetRegistry.Register(Scope, HeightWidgetKind, () => new HeightWidget());

        // A 60px spacer puts the trigger low, so keeping two 24px options inside a 100px viewport is
        // only possible above it.
        const string xml =
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Column Id=\"col\" Padding=\"0\" Gap=\"0\">"
            + "<Widget Id=\"spacer\" Kind=\"" + HeightWidgetKind + "\" Height=\"60\" />"
            + "<Widget Id=\"low\" Kind=\"input/dropdown\" OptionsBind=\"Options\" />"
            + "</Column>"
            + "</UiPage>";

        UiLayoutManifest manifest = UiLayoutManifest.Parse(xml);
        var bindings = new UiBindings();
        string current = "x";
        bindings.BindValue("low", () => current, value => current = value);
        bindings.BindOptions("Options", () => new List<string> { "x", "y" });

        using UiHost host = new(Scope, manifest, bindings, UiTheme.Vanilla, new StubMetrics(), new StubTranslation());

        try
        {
            UiNative.DebugMousePositionEnabled = true;
            UiNative.DebugMousePosition = new Vector2(5f, 70f);
            UiNative.ButtonOverride = rect => Over(rect, new Vector2(5f, 70f));

            host.DrawFrame(new Rect(0f, 0f, 300f, 100f));

            Rect popup = PopupLayer(host.Session) ?? new Rect(0f, 0f, 0f, 0f);
            if (popup.height <= 0f)
            {
                throw new Exception("popup layer was not pushed");
            }

            if (popup.yMax > 100f + 0.01f || popup.y < 0f)
            {
                throw new Exception("popup leaves the viewport: " + popup);
            }

            if (popup.y > 60f)
            {
                throw new Exception("popup was not flipped above the trigger that starts at y 60: " + popup);
            }
        }
        finally
        {
            UiNative.ButtonOverride = null;
            UiNative.DebugMousePositionEnabled = false;
        }
    }

    /// <summary>
    /// The rect rule every popup consumer shares, asserted branch by branch because each host-driven
    /// lane only exercises one: below the anchor when it fits, flipped above when only that fits,
    /// pinned to the viewport top when it fits neither, clamped horizontally past the right edge, and
    /// untouched while the host has published no viewport at all.
    /// </summary>
    private static void VerifyUiPopupRectRules()
    {
        Rect viewport = new(0f, 0f, 400f, 300f);

        Rect below = UiPopup.RectFor(new Rect(100f, 200f, 80f, 24f), 2, viewport);
        if (below.y != 224f || below.height != 48f)
        {
            throw new Exception("a popup that fits below must stay below its anchor: " + below);
        }

        Rect flipped = UiPopup.RectFor(new Rect(100f, 270f, 80f, 24f), 2, viewport);
        if (Math.Abs(flipped.yMax - 270f) > 0.01f)
        {
            throw new Exception("a popup that cannot fit below must flip above its anchor: " + flipped);
        }

        Rect pinned = UiPopup.RectFor(new Rect(100f, 100f, 80f, 24f), 20, viewport);
        if (pinned.y != viewport.y)
        {
            throw new Exception("a popup taller than both sides' room must pin to the viewport top: " + pinned);
        }

        Rect right = UiPopup.RectFor(new Rect(360f, 100f, 80f, 24f), 2, viewport);
        if (right.xMax > viewport.xMax + 0.01f)
        {
            throw new Exception("a popup past the right edge must clamp horizontally: " + right);
        }

        Rect unpublished = UiPopup.RectFor(new Rect(100f, 200f, 80f, 24f), 2, new Rect(0f, 0f, 0f, 0f));
        if (unpublished.y != 224f)
        {
            throw new Exception("without a published viewport the plain below-anchor placement must stay: " + unpublished);
        }
    }

    /// <summary>
    /// The open popup's anchor is a per-frame fact, not an open-time one. A trigger inside a scroll keeps
    /// moving while its menu is open, so the menu follows the trigger; and a popup whose owner stops being
    /// drawn is released at the end of the same hit pass instead of surviving as a menu attached to nothing.
    /// <para>
    /// <b>Mutation proof.</b> Remove the <c>openPopupAnchor = windowRect</c> refresh in
    /// <c>UiSession.NotePopupOwnerDrawn</c> and assertion A reddens (the y difference becomes 0 instead
    /// of -20). Replace the close in <c>UiSession.EndHitPass</c> with a ledger read and assertion B
    /// reddens (the hidden owner's popup survives). Remove the scroll scope's clip intersection and
    /// assertion C reddens (an owner outside the inner viewport leaves a popup). Each was run separately.
    /// Reopening, hit-layer cleanup and host-boundary assertions are additional regression guards.
    /// </para>
    /// </summary>
    private static void VerifyOpenPopupFollowsTheTrigger()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        UiWidgetRegistry.Register(Scope, HeightWidgetKind, () => new HeightWidget());

        const string xml =
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Column Padding=\"0\" Gap=\"0\"><Widget Kind=\"" + HeightWidgetKind + "\" Height=\"60\" />"
            + "<Scroll Id=\"scroll\" Padding=\"0\" Gap=\"0\" Height=\"200\">"
            + "<Widget Id=\"spacer\" Kind=\"" + HeightWidgetKind + "\" Height=\"100\" />"
            + "<Widget Id=\"dropdown\" Kind=\"input/dropdown\" OptionsBind=\"Options\" VisibleKey=\"dropdown-visible\" />"
            + "<Widget Id=\"tail\" Kind=\"" + HeightWidgetKind + "\" Height=\"300\" />"
            + "</Scroll></Column>"
            + "</UiPage>";

        var bindings = new UiBindings();
        string current = "x";
        bool visible = true;
        bindings.BindValue("dropdown", () => current, value => current = value);
        bindings.BindOptions("Options", () => new List<string> { "x", "y" });
        bindings.BindReadOnly("dropdown-visible", () => visible);

        using UiHost host = new(Scope, UiLayoutManifest.Parse(xml), bindings, UiTheme.Vanilla, new StubMetrics(), new StubTranslation());

        // A NON-ZERO Host viewport origin on purpose: a zero one hides double-offset bugs (same reason the
        // scrolled-anchor lane above uses one).
        Rect viewport = new(20f, 30f, 300f, 300f);
        host.MeasureAndArrange(new Vector2(300f, 300f));
        UiNode scrollNode = host.Session.GetNodeByElementId("scroll")
            ?? throw new Exception("the arranged scroll element has no node");

        // Content-local trigger rect at scroll 0: 100px spacer above it, 284 wide because vertical overflow
        // reserves the scrollbar (the same geometry the scrolled-anchor lane documents).
        Rect triggerContentLocal = new(0f, 100f, 284f, 28f);
        try
        {
            Event.current = null;

            // Frame 1: click the trigger, popup opens and its layer is published.
            UiNative.ButtonOverride = rect => SameRect(rect, triggerContentLocal);
            host.DrawFrame(viewport);
            if (!host.Session.IsPopupOpen("dropdown"))
            {
                throw new Exception("the scrolled trigger did not open its popup");
            }

            Rect? opened = PopupLayer(host.Session);
            if (!opened.HasValue)
            {
                throw new Exception("the popup pass pushed no hit layer, so nothing can follow the trigger");
            }

            // Frame 2: the container scrolls by 20 WITH THE POPUP ALREADY OPEN - the reported case. No press
            // at all: the anchor must be refreshed by the trigger's own draw.
            host.Session.SetScrollPosition(scrollNode, new Vector2(0f, 20f));
            UiNative.ButtonOverride = _ => false;
            host.DrawFrame(viewport);

            Rect? scrolled = PopupLayer(host.Session);
            if (!scrolled.HasValue)
            {
                throw new Exception("the popup layer vanished when the container scrolled: the menu was dismissed rather than followed");
            }

            // (A) MUTATION PROOF: without the per-frame refresh this difference is 0.
            if (Math.Abs(scrolled.Value.y - (opened.Value.y - 20f)) > 0.01f)
            {
                throw new Exception("the popup follows its trigger through a scroll: opened at y="
                    + opened.Value.y + ", after a 20px scroll it is at y=" + scrolled.Value.y
                    + " where the trigger's own -20 requires " + (opened.Value.y - 20f));
            }

            // Frame 3: the owner is no longer arranged, so no call site can report it. The announce is
            // explicit because a read-only VisibleKey cannot invalidate the arrange cache by itself.
            visible = false;
            host.Session.BumpContentRevision();
            host.DrawFrame(viewport);

            // (B) The orphan release, before this pass draws deferred menus.
            if (host.Session.IsPopupOpen("dropdown"))
            {
                throw new Exception("a popup whose owner is no longer drawn survived as an orphan menu");
            }

            if (PopupLayer(host.Session).HasValue)
            {
                throw new Exception("the orphan popup's hit layer outlived the popup");
            }

            // The stale location must be inert: the menu is gone, and the press must not reach the (hidden)
            // trigger either.
            Vector2 stale = new(scrolled.Value.x + 10f, scrolled.Value.y + 10f);
            string before = current;
            UiNative.ButtonOverride = rect => Over(rect, stale);
            host.DrawFrame(viewport);
            if (!string.Equals(current, before, StringComparison.Ordinal) || host.Session.IsPopupOpen("dropdown"))
            {
                throw new Exception("the stale popup location still acted (value now '" + current + "')");
            }

            // Recovery: arranged again, the trigger opens its popup again.
            visible = true;
            host.Session.BumpContentRevision();
            UiNative.ButtonOverride = rect => SameRect(rect, triggerContentLocal);
            host.DrawFrame(viewport);
            if (!host.Session.IsPopupOpen("dropdown"))
            {
                throw new Exception("the trigger did not reopen its popup after its owner came back");
            }

            // (C) The owner leaves the inner scroll clip but remains in the Host viewport.
            // At offset 130 its bottom is window y=88, inside the host but before the scroll viewport y=90.
            host.Session.SetScrollPosition(scrollNode, new Vector2(0f, 130f));
            UiNative.ButtonOverride = _ => false;
            host.DrawFrame(viewport);
            if (host.Session.IsPopupOpen("dropdown") || PopupLayer(host.Session).HasValue)
                throw new Exception("the inner scroll clip left an orphan popup/hit layer");
            host.Session.SetScrollPosition(scrollNode, Vector2.zero);
            UiNative.ButtonOverride = rect => SameRect(rect, triggerContentLocal);
            host.DrawFrame(viewport);
            if (!host.Session.IsPopupOpen("dropdown")) throw new Exception("clipped owner did not recover");

            // The viewport branch of the same rule, driven directly against the published viewport: an owner
            // rect that leaves the Host viewport releases the popup at once, so an owner that is scrolled
            // clear of the window cannot leave a menu behind even while it is still being drawn.
            host.Session.NotePopupOwnerDrawn("dropdown", new Rect(-1000f, -1000f, 10f, 10f));
            if (host.Session.IsPopupOpen("dropdown"))
            {
                throw new Exception("an owner rect outside the Host viewport did not release its popup");
            }
        }
        finally
        {
            UiNative.ButtonOverride = null;
            Event.current = null;
        }
    }

    /// <summary>Point containment, written out because the stub Rect has no Contains to lean on.</summary>
    private static bool Over(Rect rect, Vector2 point)
    {
        return point.x >= rect.x && point.x <= rect.xMax && point.y >= rect.y && point.y <= rect.yMax;
    }

    // Native-call seam guard (not game rendering evidence). Removing the popup refusal must redden it.
    private static void VerifyCoveredValuePrimitives()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        UiWidgetRegistry.Register(Scope, HeightWidgetKind, () => new HeightWidget());
        using UiHost host = new(Scope, UiLayoutManifest.Parse(Xml),
            MakePrimitiveBindings(), UiTheme.Vanilla, new StubMetrics(), new StubTranslation());
        host.MeasureAndArrange(new Vector2(300f, 300f));
        UiSession session = host.Session;
        UiNode node = session.GetNodeByElementId("dropdown")!;
        session.SetCurrentWindowOrigin(new Vector2(20f, 30f));
        session.PushHitLayer(node, new Rect(20f, 30f, 100f, 100f), true, "owner");
        session.BeginHitPass();
        int nativeCalls = 0;
        try
        {
            UiNative.DebugMousePositionEnabled = true;
            UiNative.DebugMousePosition = new Vector2(10f, 10f);
            UiNative.DebugMouseDown = true;
            UiNative.SliderOverride = (_, value, _, _) => { nativeCalls++; return value; };
            UiNative.TextFieldOverride = (_, value) => { nativeCalls++; return value; };
            void DrawControls()
            {
                Rect rect = new(0f, 0f, 100f, 30f);
                UiNative.Slider(rect, "sibling-slider", session, 1f, 0f, 2f, out _);
                UiNative.TextField(rect, "sibling-text", session, "value", out _);
                UiNative.NumberField(rect, "sibling-number", session, 1f, 0f, 2f, "0.##", out _);
            }
            DrawControls();
            if (nativeCalls != 0 || UiNative.DebugHotControl != 0)
                throw new Exception("covered primitives reached native input/capture");
            session.BeginHitPass();
            DrawControls();
            if (nativeCalls != 3) throw new Exception("native input did not recover when popup layer disappeared");
        }
        finally
        {
            UiNative.DebugMousePositionEnabled = false;
            UiNative.DebugMouseDown = false;
            UiNative.DebugHotControl = 0;
            UiNative.SliderOverride = null;
            UiNative.TextFieldOverride = null;
        }
    }

    private static UiBindings MakePrimitiveBindings()
    {
        var bindings = new UiBindings();
        bindings.BindValue("dropdown", () => "x", _ => { });
        bindings.BindOptions("Options", () => new List<string> { "x", "y" });
        return bindings;
    }

    // Real event-pump regression: unlike the separate-node lanes above, both controls share one context.
    // Mutation proof: restoring the element-identity exemption must make the option assertion fail.
    private static void VerifyCompositeSiblingsYield()
    {
        foreach (bool scroll in new[] { false, true })
        foreach (bool button in new[] { false, true })
        foreach (float optionOffset in new[] { 1f, 12f, 23f })
        {
            UiWidgetRegistry.Clear();
            UiWidgetRegistry.InitializeCore();
            var widget = new CompositeWidget { ButtonSibling = button };
            UiWidgetRegistry.Register(Scope, "test/composite", () => widget);
            string body = "<Widget Id=\"composite\" Kind=\"test/composite\" />";
            if (scroll) body = "<Scroll Id=\"viewport\" Height=\"160\" Padding=\"0\">" + body + "</Scroll>";
            var manifest = UiLayoutManifest.Parse("<UiPage Schema=\"2\" Source=\"" + Scope + "\">" + body + "</UiPage>");
            using UiHost host = new(Scope, manifest, new UiBindings(), UiTheme.Vanilla, new StubMetrics(), new StubTranslation());
            Rect viewport = new(20f, 30f, 300f, 300f);
            void Frame(EventType type, Vector2 point)
            {
                Event e = Event.KeyboardEvent("space");
                e.type = type;
                e.button = 0;
                e.mousePosition = point;
                Event.current = e;
                host.DrawFrame(viewport);
            }

            try
            {
                Frame(EventType.Repaint, new Vector2(0f, 0f));
                if (scroll)
                {
                    host.Session.SetScrollPosition(host.Session.GetNodeByElementId("viewport")!, new Vector2(0f, 10f));
                    Frame(EventType.Repaint, new Vector2(0f, 0f));
                }
                Vector2 trigger = new(widget.First.x + 10f, widget.First.y + 12f);
                Frame(EventType.MouseDown, trigger);
                Frame(EventType.MouseUp, trigger);
                Frame(EventType.Repaint, trigger);
                Rect popup = PopupLayer(host.Session) ?? throw new Exception("composite popup did not publish");
                Vector2 option = new(popup.x + 10f, popup.y + 24f + optionOffset);
                if (!Over(widget.Sibling, option)) throw new Exception("fixture does not overlap the sibling");
                Frame(EventType.Layout, option);
                Frame(EventType.MouseDown, option);
                Frame(EventType.MouseUp, option);
                Frame(EventType.Repaint, option);
                if (widget.Selected != "y" || widget.ButtonClicks != 0 || host.Session.OpenPopupId != null)
                    throw new Exception("same-node option lost to sibling: button=" + button + ", scroll=" + scroll + ", offset=" + optionOffset);

                // No stale occlusion after close: the underlying control must work again.
                Frame(EventType.Repaint, option);
                Frame(EventType.MouseDown, option);
                Frame(EventType.MouseUp, option);
                if (button ? widget.ButtonClicks != 1 : !host.Session.IsPopupOpen("composite-second"))
                    throw new Exception("sibling did not recover input after popup close");
            }
            finally
            {
                Event.current = null;
                GUIUtility.hotControl = 0;
            }
        }
    }

    private sealed class CompositeWidget : IUiWidget
    {
        public string Kind => "test/composite";
        public bool ButtonSibling;
        public int ButtonClicks;
        public string Selected = "x";
        public Rect First;
        public Rect Sibling;
        public void Configure(UiElementSpec spec) { }
        public void Validate(IUiBindings bindings, string elementPath) { }
        public float Measure(UiWidgetContext ctx) => 220f;
        public void Draw(Rect rect, UiWidgetContext ctx)
        {
            Rect first = new(rect.x, rect.y + 20f, 150f, 28f);
            Rect sibling = new(rect.x, rect.y + 60f, 150f, 48f);
            First = ctx.ToWindowRect(first);
            Sibling = ctx.ToWindowRect(sibling);
            UiNative.DropdownButton(first, "composite-first", ctx);
            if (ButtonSibling)
            {
                if (UiNative.Button(sibling, ctx)) ButtonClicks++;
            }
            else UiNative.DropdownButton(sibling, "composite-second", ctx);
            if (!ctx.Session.IsPopupOpen("composite-first")) return;
            Rect anchor = ctx.Session.OpenPopupAnchor!.Value;
            ctx.Session.RegisterPopupDraw(() => UiPopup.DrawOptionList(
                UiPopup.RectFor(anchor, 2, ctx.Session.HostViewport), "composite-first", ctx,
                new List<KeyValuePair<string, string>> { new("X", "x"), new("Y", "y") },
                Selected, value => Selected = value));
        }
    }

    private sealed class HeightWidget : IUiWidget
    {
        public string Kind => HeightWidgetKind;

        public void Configure(UiElementSpec spec)
        {
        }

        public void Validate(IUiBindings bindings, string elementPath)
        {
        }

        public float Measure(UiWidgetContext ctx) => 10f;

        public void Draw(Rect rect, UiWidgetContext ctx)
        {
        }
    }

    private sealed class StubMetrics : ITextMetrics
    {
        public float MeasureText(string text, UiFont font, float width)
        {
            return font switch
            {
                UiFont.Tiny => 16f,
                UiFont.Small => 24f,
                _ => 32f
            };
        }

        public float MeasureWidth(string text, UiFont font) => StubTextWidth.Of(text, font);
    }

    private sealed class StubTranslation : IUiTranslation
    {
        public string Translate(string key)
        {
            return "[" + key + "]";
        }

        public int TranslationRevision => 0;
    }
}
