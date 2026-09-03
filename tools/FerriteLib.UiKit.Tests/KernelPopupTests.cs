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
        + "<Scroll Id=\"scroll\" Height=\"200\">"
        + "<Widget Id=\"spacer\" Kind=\"" + HeightWidgetKind + "\" Height=\"100\" />"
        + "<Widget Id=\"dropdown\" Kind=\"input/dropdown\" OptionsBind=\"Options\" />"
        + "<Widget Id=\"tail\" Kind=\"" + HeightWidgetKind + "\" Height=\"100\" />"
        + "</Scroll>"
        + "</UiPage>";

    public static int RunAll()
    {
        int failures = 0;
        failures += Run("Scroll dropdown anchor/popup share Host window space", VerifyScrollDropdownWindowSpace);
        failures += Run("Popup row over a lower trigger selects, and does not open that trigger", VerifyPopupWinsOverCoveredTrigger);
        failures += Run("Popup flips above the trigger instead of leaving the viewport", VerifyPopupFlipsIntoViewport);
        failures += Run("UiPopup rect rules: below, flip, pin, clamp, unpublished viewport", VerifyUiPopupRectRules);
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

        using UiHost host = new(Scope, manifest, bindings, UiTheme.DarkGold, new StubMetrics(), new StubTranslation());

        // Content is 100 + 28 + 100 = 228 tall; viewport is 200, so the scroll offset clamps to 28.
        // Vertical overflow reserves the 16px scrollbar width, leaving a 284px trigger.
        host.Session.SetScrollPosition("scroll", new Vector2(0f, 20f));

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
            + "<Column Id=\"col\" Gap=\"2\">"
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

        using UiHost host = new(Scope, manifest, bindings, UiTheme.DarkGold, new StubMetrics(), new StubTranslation());

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

            if (!host.Session.OpenPopupRect.HasValue)
            {
                throw new Exception("popup pass did not publish its rect, so nothing can yield to it");
            }

            Rect popup = host.Session.OpenPopupRect.Value;
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
            + "<Column Id=\"col\">"
            + "<Widget Id=\"spacer\" Kind=\"" + HeightWidgetKind + "\" Height=\"60\" />"
            + "<Widget Id=\"low\" Kind=\"input/dropdown\" OptionsBind=\"Options\" />"
            + "</Column>"
            + "</UiPage>";

        UiLayoutManifest manifest = UiLayoutManifest.Parse(xml);
        var bindings = new UiBindings();
        string current = "x";
        bindings.BindValue("low", () => current, value => current = value);
        bindings.BindOptions("Options", () => new List<string> { "x", "y" });

        using UiHost host = new(Scope, manifest, bindings, UiTheme.DarkGold, new StubMetrics(), new StubTranslation());

        try
        {
            UiNative.DebugMousePositionEnabled = true;
            UiNative.DebugMousePosition = new Vector2(5f, 70f);
            UiNative.ButtonOverride = rect => Over(rect, new Vector2(5f, 70f));

            host.DrawFrame(new Rect(0f, 0f, 300f, 100f));

            Rect popup = host.Session.OpenPopupRect ?? new Rect(0f, 0f, 0f, 0f);
            if (popup.height <= 0f)
            {
                throw new Exception("popup rect was not published");
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

    /// <summary>Point containment, written out because the stub Rect has no Contains to lean on.</summary>
    private static bool Over(Rect rect, Vector2 point)
    {
        return point.x >= rect.x && point.x <= rect.xMax && point.y >= rect.y && point.y <= rect.yMax;
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
