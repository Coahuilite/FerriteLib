using System.Linq;
using UnityEngine;
using Verse;
using System;
using System.Collections.Generic;
using FerriteLib.UiKit;
using FerriteLib.UiKit.Kernel;

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
        UiWidgetRegistry.Clear();UiWidgetRegistry.InitializeCore();
        UiWidgetRegistry.Define(new UiWidgetDefinition(Scope,HeightWidgetKind,()=>new HeightWidget()));
        string current="x";int requests=0;var bindings=new UiBindings();
        bindings.BindValue("dropdown",()=>current,value=>{current=value;requests++;});
        bindings.BindChoices("Options",()=>new[]{new UiChoice<string>("x","x"),new UiChoice<string>("y","y")});
        using var host=new UiHost(Scope,UiLayoutManifest.Parse(Xml),bindings,UiTheme.Vanilla,new StubMetrics(),new StubTranslation());
        host.MeasureAndArrange(new UiLayoutPoint(300,300));host.Session.SetScrollPosition(host.Session.GetNodeByElementId("scroll")!,new UiLayoutPoint(0,20));
        var viewport=new UiLayoutRect(20,30,300,300);var expected=new UiLayoutRect(20,110,284,28);
        void Click(UiLayoutPoint point)=>NativeEvents.Click(point,()=>NativeFixture.DrawFrame(host,viewport));
        Click(new UiLayoutPoint(30,124));
        if(!host.Session.IsPopupOpen("dropdown")||!SameRect(host.Session.OpenPopupAnchor!.Value,expected))throw new Exception("scrolled trigger did not open at its actual Host-window anchor");
        Click(new UiLayoutPoint(30,174));
        if(host.Session.OpenPopupId!=null||current!="y"||requests!=1)throw new Exception("window-space row did not select exactly once");
        Click(new UiLayoutPoint(30,124));
        // The old exact-rectangle override was not a physical click. The actual content-local point is
        // outside the Host-window menu: F3 dismisses it, without selecting or replaying a lower operation.
        Click(new UiLayoutPoint(10,164));
        if(requests!=1||current!="y"||host.Session.OpenPopupId!=null)throw new Exception("wrong-space click selected a row or failed actual outside-press dismissal");
        Click(new UiLayoutPoint(30,124));Click(new UiLayoutPoint(30,174));
        if(requests!=2||host.Session.OpenPopupId!=null)throw new Exception("correct window-space row did not recover after wrong-space dismissal");
    }
    private static bool SameRect(UiLayoutRect a, UiLayoutRect b)
    {
        return Math.Abs(a.X - b.X) < 0.01f
            && Math.Abs(a.Y - b.Y) < 0.01f
            && Math.Abs(a.Width - b.Width) < 0.01f
            && Math.Abs(a.Height - b.Height) < 0.01f;
    }

    /// <summary>
    /// The reported in-game failure: a dropdown option row overlaps the next row's own trigger, and the
    /// content pass runs before the popup pass, so the lower trigger used to eat the click and open
    /// itself instead of the option being selected. Hit-testing is point-based here on purpose — the
    /// existing lane's exact-rect override cannot express two rects claiming one point.
    /// </summary>
    private static void VerifyPopupWinsOverCoveredTrigger()
    {
        UiWidgetRegistry.Clear();UiWidgetRegistry.InitializeCore();
        string first="x",second="x";int writes=0;var bindings=new UiBindings();
        bindings.BindValue("first",()=>first,value=>{first=value;writes++;});bindings.BindValue("second",()=>second,value=>second=value);
        bindings.BindChoices("Options",()=>new[]{new UiChoice<string>("x","x"),new UiChoice<string>("y","y")});
        var manifest=UiLayoutManifest.Parse("<UiPage Schema='2' Source='popup-test'><Column Padding='0' Gap='2'><Widget Id='first' Kind='input/dropdown' OptionsBind='Options'/><Widget Id='second' Kind='input/dropdown' OptionsBind='Options'/></Column></UiPage>");
        using var host=new UiHost(Scope,manifest,bindings,UiTheme.Vanilla,new StubMetrics(),new StubTranslation());
        void Click(UiLayoutPoint point)=>NativeEvents.Click(point,()=>NativeFixture.DrawFrame(host,new UiLayoutRect(0,0,300,300)));
        Click(new UiLayoutPoint(10,10));var point=new UiLayoutPoint(10,55);
        NativeEvents.Begin(EventType.Layout,point);NativeFixture.DrawFrame(host,new UiLayoutRect(0,0,300,300));
        UiNode lower=host.Session.GetNodeByElementId("second")!;
        if(!lower.Rect!.Value.Contains(point)||!host.Session.IsPointerOverHigherLayer(lower,point))throw new Exception("actual lower trigger/popup overlap premise failed: lower="+lower.Rect+" popup="+PopupLayer(host.Session)+" open="+host.Session.OpenPopupId);
        Click(point);
        if(first!="y"||second!="x"||writes!=1||host.Session.OpenPopupId!=null)throw new Exception("covered lower trigger stole or replayed the popup selection");
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
        bindings.BindChoices("Options", ()=> (new List<string> { "x", "y" }).Select(value=>new UiChoice<string>(value,value,value)).ToArray());

        using UiHost host = new(Scope, manifest, bindings, UiTheme.Vanilla, new StubMetrics(), new StubTranslation());

        var trace = new List<string>();
        NativeFixture.Trace = trace.Add;
        UiLayoutPoint openClick = new(10f, 10f);
        UiLayoutPoint optionClick = new(10f, 55f);
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
            bool lowerCovered=host.Session.IsPointerOverHigherLayer(host.Session.GetNodeByElementId("second")!,optionClick);
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
            NativeFixture.Trace = null;
            Event.current = null;
            GUIUtility.hotControl = 0;
        }
    }

    /// <summary>
    /// The topmost popup layer of the session's hit stack, or null when none is pushed. This is the
    /// diagnostic read of the stack (the lane's former "published popup rect"); input dispatch itself only
    /// ever asks <see cref="UiSession.IsPointerOverHigherLayer"/>.
    /// </summary>
    private static UiLayoutRect? PopupLayer(UiSession session)
    {
        for (int i = session.HitLayers.Count - 1; i >= 0; i--)
        {
            if (session.HitLayers[i].IsPopup) return session.HitLayers[i].Rect;
        }

        return null;
    }

    private static void Pump(UiHost host, EventType type, UiLayoutPoint point)
    {
        // Compiled against the real UnityEngine surface (Krafs ref): Event has no public constructor,
        // so take an instance from the static factory and overwrite the fields the lanes need. At
        // runtime this binds to the stub Event, which mirrors the same shape; a fresh instance per
        // pass is also what makes the stub's control-id counter reset per pass.
        Event e = Event.KeyboardEvent("space");
        e.type = type;
        e.button = 0;
        e.mousePosition = VerseUiExecution.Point(point);
        Event.current = e;
        NativeFixture.DrawFrame(        host,new UiLayoutRect(0f, 0f, 300f, 300f));
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
UiWidgetRegistry.Define(new UiWidgetDefinition(Scope, HeightWidgetKind, () => new HeightWidget()));

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
        bindings.BindChoices("Options", ()=> (new List<string> { "x", "y" }).Select(value=>new UiChoice<string>(value,value,value)).ToArray());

        using UiHost host = new(Scope, manifest, bindings, UiTheme.Vanilla, new StubMetrics(), new StubTranslation());

        // Warm arrange: the node the scroll offset belongs to exists only after the page has been
        // arranged once.
        host.MeasureAndArrange(new UiLayoutPoint(300f, 300f));
        UiNode scrollNode = host.Session.GetNodeByElementId("scroll")
            ?? throw new Exception("the arranged scroll element has no node");
        host.Session.SetScrollPosition(scrollNode, new UiLayoutPoint(0f, 160f));

        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new UiLayoutPoint(300f, 300f));
        host.Session.OpenPopup("first", snapshot.RectById["first"]);

        var trace = new List<string>();
        NativeFixture.Trace = trace.Add;
        try
        {
            // First pass publishes the popup rect; the trace also reveals the container origin,
            // because a draw-space point at (0,0) maps to the origin in window space.
            Pump(host, EventType.Repaint, new UiLayoutPoint(0f, 0f));

            UiLayoutRect? scrolledPopup = PopupLayer(host.Session);
            if (!host.Session.IsPopupOpen("first") || !scrolledPopup.HasValue)
            {
                throw new Exception("scrolled container did not open/push the popup; trace: " + string.Join(" | ", trace));
            }

            UiLayoutRect popup = scrolledPopup.Value;

            // The pumped pointer is window space; the stub group model presents it to the content
            // pass in container-local space and to the popup pass in window space, exactly like the
            // real IMGUI groups do in game.
            UiLayoutPoint optionClick = new(popup.X + 10f, popup.Y + 27f);
            Pump(host, EventType.Layout, optionClick);
            bool lowerCovered=host.Session.IsPointerOverHigherLayer(host.Session.GetNodeByElementId("second")!,optionClick);
            Pump(host, EventType.MouseDown, optionClick);
            Pump(host, EventType.MouseUp, optionClick);
            Pump(host, EventType.Repaint, optionClick);

            // Premise: the covered trigger must have seen the converted pointer inside the popup,
            // otherwise this lane would pass without the overlap ever existing.
            if(!lowerCovered)throw new Exception("actual current popup did not cover the lower trigger before dispatch");

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
            NativeFixture.Trace = null;
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
UiWidgetRegistry.Define(new UiWidgetDefinition(Scope, HeightWidgetKind, () => new HeightWidget()));

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
        bindings.BindChoices("Options", ()=> (new List<string> { "x", "y" }).Select(value=>new UiChoice<string>(value,value,value)).ToArray());

        using UiHost host = new(Scope, manifest, bindings, UiTheme.Vanilla, new StubMetrics(), new StubTranslation());

        try
        {
            NativeFixture.DebugMousePositionEnabled = true;
            NativeFixture.DebugMousePosition = new UiLayoutPoint(5f, 70f);
            NativeEvents.Click(new UiLayoutPoint(5,70),()=>NativeFixture.DrawFrame(host,new UiLayoutRect(0,0,300,100)));

            UiLayoutRect popup = PopupLayer(host.Session) ?? new UiLayoutRect(0f, 0f, 0f, 0f);
            if (popup.Height <= 0f)
            {
                throw new Exception("popup layer was not pushed");
            }

            if (popup.YMax > 100f + 0.01f || popup.Y < 0f)
            {
                throw new Exception("popup leaves the viewport: " + popup);
            }

            if (popup.Y > 60f)
            {
                throw new Exception("popup was not flipped above the trigger that starts at y 60: " + popup);
            }
        }
        finally
        {
            NativeFixture.ButtonOverride = null;
            NativeFixture.DebugMousePositionEnabled = false;
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
        UiLayoutRect viewport = new(0f, 0f, 400f, 300f);

        UiLayoutRect below = UiPopup.RectFor(new UiLayoutRect(100f, 200f, 80f, 24f), 2, viewport);
        if (below.Y != 224f || below.Height != 48f)
        {
            throw new Exception("a popup that fits below must stay below its anchor: " + below);
        }

        UiLayoutRect flipped = UiPopup.RectFor(new UiLayoutRect(100f, 270f, 80f, 24f), 2, viewport);
        if (Math.Abs(flipped.YMax - 270f) > 0.01f)
        {
            throw new Exception("a popup that cannot fit below must flip above its anchor: " + flipped);
        }

        UiLayoutRect pinned = UiPopup.RectFor(new UiLayoutRect(100f, 100f, 80f, 24f), 20, viewport);
        if (pinned.Y != viewport.Y)
        {
            throw new Exception("a popup taller than both sides' room must pin to the viewport top: " + pinned);
        }

        UiLayoutRect right = UiPopup.RectFor(new UiLayoutRect(360f, 100f, 80f, 24f), 2, viewport);
        if (right.XMax > viewport.XMax + 0.01f)
        {
            throw new Exception("a popup past the right edge must clamp horizontally: " + right);
        }

        UiLayoutRect unpublished = UiPopup.RectFor(new UiLayoutRect(100f, 200f, 80f, 24f), 2, new UiLayoutRect(0f, 0f, 0f, 0f));
        if (unpublished.Y != 224f)
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
UiWidgetRegistry.Define(new UiWidgetDefinition(Scope, HeightWidgetKind, () => new HeightWidget()));

        const string xml =
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Column Padding=\"0\" Gap=\"0\"><Widget Kind=\"" + HeightWidgetKind + "\" Height=\"60\" />"
            + "<Scroll Id=\"scroll\" Padding=\"0\" Gap=\"0\" Height=\"200\">"
            + "<Widget Id=\"spacer\" Kind=\"" + HeightWidgetKind + "\" Height=\"100\" />"
            + "<Widget Id=\"dropdown\" Kind=\"input/dropdown\" OptionsBind=\"Options\" VisibleBind=\"dropdown-visible\" />"
            + "<Widget Id=\"tail\" Kind=\"" + HeightWidgetKind + "\" Height=\"300\" />"
            + "</Scroll></Column>"
            + "</UiPage>";

        var bindings = new UiBindings();
        string current = "x";
        bool visible = true;
        bindings.BindValue("dropdown", () => current, value => current = value);
        bindings.BindChoices("Options", ()=> (new List<string> { "x", "y" }).Select(value=>new UiChoice<string>(value,value,value)).ToArray());
        bindings.BindReadOnly("dropdown-visible", () => visible);

        using UiHost host = new(Scope, UiLayoutManifest.Parse(xml), bindings, UiTheme.Vanilla, new StubMetrics(), new StubTranslation());

        // A NON-ZERO Host viewport origin on purpose: a zero one hides double-offset bugs (same reason the
        // scrolled-anchor lane above uses one).
        UiLayoutRect viewport = new(20f, 30f, 300f, 300f);
        host.MeasureAndArrange(new UiLayoutPoint(300f, 300f));
        UiNode scrollNode = host.Session.GetNodeByElementId("scroll")
            ?? throw new Exception("the arranged scroll element has no node");

        // Content-local trigger rect at scroll 0: 100px spacer above it, 284 wide because vertical overflow
        // reserves the scrollbar (the same geometry the scrolled-anchor lane documents).
        UiLayoutRect triggerContentLocal = new(0f, 100f, 284f, 28f);
        try
        {
            Event.current = null;

            // Frame 1: click the trigger, popup opens and its layer is published.
            NativeEvents.Click(new UiLayoutPoint(viewport.X+10,viewport.Y+60+100-host.Session.GetScrollPosition(scrollNode).Y+14),()=>NativeFixture.DrawFrame(host,viewport));
            if (!host.Session.IsPopupOpen("dropdown"))
            {
                throw new Exception("the scrolled trigger did not open its popup");
            }

            UiLayoutRect? opened = PopupLayer(host.Session);
            if (!opened.HasValue)
            {
                throw new Exception("the popup pass pushed no hit layer, so nothing can follow the trigger");
            }

            // Frame 2: the container scrolls by 20 WITH THE POPUP ALREADY OPEN - the reported case. No press
            // at all: the anchor must be refreshed by the trigger's own draw.
            host.Session.SetScrollPosition(scrollNode, new UiLayoutPoint(0f, 20f));
            NativeEvents.Begin(EventType.Repaint,new UiLayoutPoint(-10,-10));NativeFixture.DrawFrame(host,viewport);

            UiLayoutRect? scrolled = PopupLayer(host.Session);
            if (!scrolled.HasValue)
            {
                throw new Exception("the popup layer vanished when the container scrolled: the menu was dismissed rather than followed");
            }

            // (A) MUTATION PROOF: without the per-frame refresh this difference is 0.
            if (Math.Abs(scrolled.Value.Y - (opened.Value.Y - 20f)) > 0.01f)
            {
                throw new Exception("the popup follows its trigger through a scroll: opened at y="
                    + opened.Value.Y + ", after a 20px scroll it is at y=" + scrolled.Value.Y
                    + " where the trigger's own -20 requires " + (opened.Value.Y - 20f));
            }

            // Frame 3: the owner is no longer arranged, so no call site can report it. The announce is
            // explicit because a read-only VisibleBind cannot invalidate the arrange cache by itself.
            visible = false;
            host.Session.BumpContentRevision();
            NativeEvents.Begin(EventType.Repaint,new UiLayoutPoint(-10,-10));NativeFixture.DrawFrame(host,viewport);

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
            UiLayoutPoint stale = new(scrolled.Value.X + 10f, scrolled.Value.Y + 10f);
            string before = current;
            NativeEvents.Click(stale,()=>NativeFixture.DrawFrame(host,viewport));
            if (!string.Equals(current, before, StringComparison.Ordinal) || host.Session.IsPopupOpen("dropdown"))
            {
                throw new Exception("the stale popup location still acted (value now '" + current + "')");
            }

            // Recovery: arranged again, the trigger opens its popup again.
            visible = true;
            host.Session.BumpContentRevision();
            NativeEvents.Click(new UiLayoutPoint(viewport.X+10,viewport.Y+60+100-host.Session.GetScrollPosition(scrollNode).Y+14),()=>NativeFixture.DrawFrame(host,viewport));
            if (!host.Session.IsPopupOpen("dropdown"))
            {
                throw new Exception("the trigger did not reopen its popup after its owner came back");
            }

            // (C) The owner leaves the inner scroll clip but remains in the Host viewport.
            // At offset 130 its bottom is window y=88, inside the host but before the scroll viewport y=90.
            host.Session.SetScrollPosition(scrollNode, new UiLayoutPoint(0f, 130f));
            NativeEvents.Begin(EventType.Repaint,new UiLayoutPoint(-10,-10));NativeFixture.DrawFrame(host,viewport);
            if (host.Session.IsPopupOpen("dropdown") || PopupLayer(host.Session).HasValue)
                throw new Exception("the inner scroll clip left an orphan popup/hit layer");
            host.Session.SetScrollPosition(scrollNode, UiLayoutPoint.Zero);
            NativeEvents.Click(new UiLayoutPoint(viewport.X+10,viewport.Y+60+100-host.Session.GetScrollPosition(scrollNode).Y+14),()=>NativeFixture.DrawFrame(host,viewport));
            if (!host.Session.IsPopupOpen("dropdown")) throw new Exception("clipped owner did not recover");

            // The viewport branch of the same rule, driven directly against the published viewport: an owner
            // rect that leaves the Host viewport releases the popup at once, so an owner that is scrolled
            // clear of the window cannot leave a menu behind even while it is still being drawn.
            host.Session.NotePopupOwnerDrawn("dropdown", new UiLayoutRect(-1000f, -1000f, 10f, 10f));
            if (host.Session.IsPopupOpen("dropdown"))
            {
                throw new Exception("an owner rect outside the Host viewport did not release its popup");
            }
        }
        finally
        {
            NativeFixture.ButtonOverride = null;
            Event.current = null;
        }
    }

    /// <summary>Point containment, written out because the stub Rect has no Contains to lean on.</summary>
    private static bool Over(UiLayoutRect rect, UiLayoutPoint point)
    {
        return point.X >= rect.X && point.X <= rect.XMax && point.Y >= rect.Y && point.Y <= rect.YMax;
    }

    // Native-call seam guard (not game rendering evidence). Removing the popup refusal must redden it.
    private static void VerifyCoveredValuePrimitives()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
UiWidgetRegistry.Define(new UiWidgetDefinition(Scope, HeightWidgetKind, () => new HeightWidget()));
        using UiHost host = new(Scope, UiLayoutManifest.Parse(Xml),
            MakePrimitiveBindings(), UiTheme.Vanilla, new StubMetrics(), new StubTranslation());
        host.MeasureAndArrange(new UiLayoutPoint(300f, 300f));
        UiSession session = host.Session;
        UiNode node = session.GetNodeByElementId("dropdown")!;
        session.SetCurrentWindowOrigin(new UiLayoutPoint(20f, 30f));
        session.PushHitLayer(node, new UiLayoutRect(20f, 30f, 100f, 100f), true, "owner");
        session.BeginHitPass();
        int nativeCalls = 0;
        try
        {
            NativeFixture.DebugMousePositionEnabled = true;
            NativeFixture.DebugMousePosition = new UiLayoutPoint(30f, 40f);
            NativeFixture.DebugMouseDown = true;
            NativeFixture.TextFieldOverride=(_,value)=>{nativeCalls++;return value;};
            var ctx=host.CreateContext(300).WithNode(node);
            var codec=new UiNumericEdit(0,2);
            static bool Parse(string text,out string value){value=text;return true;}
            void DrawEditors()
            {
                UiLayoutRect rect=new(0,0,100,30);
                NativeFixture.Edit(rect,ctx,"sibling-text","value",s=>s,Parse,false,false,_=>throw new Exception("unexpected text write"),()=>"value");
                NativeFixture.Edit(rect,ctx,"sibling-number",1f,codec.Format,codec.Parse,false,false,_=>throw new Exception("unexpected number write"),()=>1f);
            }
            NativeFixture.BeginDebugEvent();DrawEditors();
            var blocked=NativeFixture.Range(new UiLayoutRect(0,0,100,30),ctx,"sibling-slider",1,0,2);
            if(nativeCalls!=0||blocked.Captured||blocked.Request||GUIUtility.hotControl!=0)throw new Exception("covered controls reached native edit/capture/request");
            session.BeginHitPass();
            // A distinct next native press after the popup disappeared; never revive the prior event.
            NativeFixture.BeginDebugEvent();DrawEditors();
            var available=NativeFixture.Range(new UiLayoutRect(0,0,100,30),ctx,"sibling-slider",1,0,2);
            if(nativeCalls!=2||!available.Captured)throw new Exception("native input did not recover when popup layer disappeared");        }
        finally
        {
            NativeFixture.DebugMousePositionEnabled = false;
            NativeFixture.DebugMouseDown = false;
            NativeFixture.DebugHotControl = 0;
            NativeFixture.SliderOverride = null;
            NativeFixture.TextFieldOverride = null;
        }
    }

    private static UiBindings MakePrimitiveBindings()
    {
        var bindings = new UiBindings();
        bindings.BindValue("dropdown", () => "x", _ => { });
        bindings.BindChoices("Options", ()=> (new List<string> { "x", "y" }).Select(value=>new UiChoice<string>(value,value,value)).ToArray());
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
UiWidgetRegistry.Define(new UiWidgetDefinition(Scope, "test/composite", () => widget));
            string body = "<Widget Id=\"composite\" Kind=\"test/composite\" />";
            if (scroll) body = "<Scroll Id=\"viewport\" Height=\"160\" Padding=\"0\">" + body + "</Scroll>";
            var manifest = UiLayoutManifest.Parse("<UiPage Schema=\"2\" Source=\"" + Scope + "\">" + body + "</UiPage>");
            using UiHost host = new(Scope, manifest, new UiBindings(), UiTheme.Vanilla, new StubMetrics(), new StubTranslation());
            UiLayoutRect viewport = new(20f, 30f, 300f, 300f);
            void Frame(EventType type, UiLayoutPoint point)
            {
                Event e = Event.KeyboardEvent("space");
                e.type = type;
                e.button = 0;
                e.mousePosition = VerseUiExecution.Point(point);
                Event.current = e;
                NativeFixture.DrawFrame(                host,viewport);
            }

            try
            {
                Frame(EventType.Repaint, new UiLayoutPoint(0f, 0f));
                if (scroll)
                {
                    host.Session.SetScrollPosition(host.Session.GetNodeByElementId("viewport")!, new UiLayoutPoint(0f, 10f));
                    Frame(EventType.Repaint, new UiLayoutPoint(0f, 0f));
                }
                UiLayoutPoint trigger = new(widget.First.X + 10f, widget.First.Y + 12f);
                Frame(EventType.MouseDown, trigger);
                Frame(EventType.MouseUp, trigger);
                Frame(EventType.Repaint, trigger);
                UiLayoutRect popup = PopupLayer(host.Session) ?? throw new Exception("composite popup did not publish");
                UiLayoutPoint option = new(popup.X + 10f, popup.Y + 24f + optionOffset);
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
        public UiLayoutRect First;
        public UiLayoutRect Sibling;
        public void Configure(UiElementSpec spec) { }
        public void Validate(IUiBindingAccess bindings, string elementPath) { }
        public float Measure(UiWidgetContext ctx) => 220f;
        public void Draw(UiLayoutRect rect, UiWidgetContext ctx)
        {
            UiLayoutRect first = new(rect.X, rect.Y + 20f, 150f, 28f);
            UiLayoutRect sibling = new(rect.X, rect.Y + 60f, 150f, 48f);
            First = ctx.ToWindowRect(first);
            Sibling = ctx.ToWindowRect(sibling);
            ctx.Controls.DropdownButton(first, "composite-first", ctx,"first");
            if (ButtonSibling)
            {
                if (ctx.Controls.Button(sibling, ctx)) ButtonClicks++;
            }
            else ctx.Controls.DropdownButton(sibling, "composite-second", ctx,"second");
            if (!ctx.Session.IsPopupOpen("composite-first")) return;
            UiLayoutRect anchor = ctx.Session.OpenPopupAnchor!.Value;
            ctx.Session.RegisterPopupDraw(() => UiPopup.DrawChoices(
                UiPopup.RectFor(anchor, 2, ctx.Session.HostViewport), "composite-first", ctx,
                new List<UiChoice<string>> { new("X", "x"), new("Y", "y") },
                Selected, value => Selected = value));
        }
    }

    private sealed class HeightWidget : IUiWidget
    {
        public string Kind => HeightWidgetKind;

        public void Configure(UiElementSpec spec)
        {
        }

        public void Validate(IUiBindingAccess bindings, string elementPath)
        {
        }

        public float Measure(UiWidgetContext ctx) => 10f;

        public void Draw(UiLayoutRect rect, UiWidgetContext ctx)
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
