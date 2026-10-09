using System;
using System.Collections.Generic;

using FerriteLib.UiKit;
using FerriteLib.UiKit.Kernel;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// The receive-eligibility and capture-ownership lane (FL-IC1). Three failures the independent audit
/// witnessed in current source, the ruling that replaces them, and the paths that must keep working:
/// <list type="bullet">
/// <item><b>D1 / F09</b> - a menu covering its OWN trigger bar could not be chosen from: the trigger was
/// exempted from its own popup by owner id, took the press first, and the option row drew afterwards into an
/// event that was already used. Coverage is geometric now, so the menu wins where it covers and the trigger
/// keeps its toggle-to-close where it does not.</item>
/// <item><b>D2</b> - <c>chart/line</c> read the pointer and the native hot control directly, so a chart under
/// an open menu captured the press and committed a dragged value. A NEW press now asks the shared
/// eligibility rule; a drag already under way stays owned by its capture and ends on its own release.</item>
/// <item><b>D4</b> - <c>UiPopup.RectFor</c> made a menu as tall as its option count, so a long list ran
/// through the bottom of the window and the rows past the edge had no rect a pointer could reach. The menu is
/// bounded to whole rows inside the finite viewport and scrolls, and its geometry, its hit test and its
/// scroll read one rect.</item>
/// </list>
/// <para>
/// <b>What these lanes cannot show.</b> They drive the library's routing through the faithful stub event pump
/// - separate Layout / MouseDown / MouseUp / wheel events, native hot-control capture in the stub
/// <c>ButtonInvisible</c>, and the group-local pointer read-back - not a real Unity input device. The wheel is
/// pumped with the sign the native IMGUI scroll view uses (a positive <c>delta.y</c> asks for the later rows),
/// so the routing, the clamp and the ownership of a covered notch are proven against that protocol; a real
/// device's weight, and whether Unity's own scroll view under a menu reaches the notch before the library's
/// pass-boundary rule, is in-game behaviour the double does not model. A native <c>HorizontalSlider</c> drag,
/// IME text entry and the game's <c>WindowStack</c> input authority are likewise out of reach here. The in-game
/// half of every scenario in this file belongs to the short human pass, and <c>docs/api-tiers.md</c> says so
/// beside the contract.
/// </para>
/// </summary>
internal static class KernelInputEligibilityTests
{
    private const string Scope = "eligibility";
    private const string HeightKind = "test/height";
    private const string TriggerHeight = "28";
    private const float OptionHeight = 24f;

    private static int failures;

    public static int RunAll()
    {
        failures = 0;

        // D1: one geometric ruling, both directions, on both option-list forms.
        failures += Run("An option row drawn over its own trigger selects instead of closing the menu",
            VerifyOptionUnderOwnTriggerSelects);
        failures += Run("A typed option row drawn over its own trigger selects through the typed path",
            VerifyTypedOptionUnderOwnTriggerSelects);
        failures += Run("The strip of the trigger the menu does not cover still closes it, and writes nothing",
            VerifyUncoveredTriggerStripStillCloses);

        // D4: the bounded, scrollable menu, on both option-list forms.
        failures += Run("A forty-option menu stays inside the viewport and reaches its first and last option",
            VerifyLongMenuStaysInViewportAndReachesEnds);
        failures += Run("The typed list is bounded, scrolled and clamped by the same window as the string list",
            VerifyTypedLongMenuScrollsLikeTheStringList);
        failures += Run("The wheel belongs to the open menu: inside it the menu moves, outside it nothing does",
            VerifyWheelBelongsToTheOpenMenu);

        // D2: the chart's three pointer questions.
        failures += Run("A chart under an open menu captures nothing, writes nothing and leaves the native slot alone",
            VerifyChartUnderMenuNeverCapturesOrWrites);
        failures += Run("The same chart drag works after the menu closes: capture, one change, release",
            VerifyChartDragWorksAfterTheMenuCloses);
        failures += Run("A drag already under way is not ended by the pointer leaving the chart",
            VerifyHeldChartDragContinuesOutsideTheRect);
        failures += Run("A disabled chart takes no press, and takes it again when its command can run",
            VerifyDisabledChartTakesNoPress);
        failures += Run("A chart band the scroll clipped away takes no press",
            VerifyChartOutsideTheEffectiveClipTakesNoPress);

        // Capture ownership.
        failures += Run("An owner that stops being drawn ends its own capture and no other session's",
            VerifyCaptureReleasedWhenOwnerStopsDrawing);
        failures += Run("An identity the definition releases cannot keep holding a capture",
            VerifyRemovedIdentityReleasesItsCapture);

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

    private static void Check(bool condition, string claim)
    {
        if (!condition) throw new Exception(claim);
    }

    // --- D1: the menu outranks the trigger it belongs to ---------------------------------------------

    /// <summary>
    /// The F09 geometry reproduced: a short window leaves the menu neither room below nor room above, so it
    /// pins to the viewport top and its last row lands on the trigger that owns it. Before this slice the
    /// trigger answered "that popup is mine", consumed the press, and the row never saw a live event; the
    /// model kept its old value while the menu vanished.
    /// </summary>
    private static void VerifyOptionUnderOwnTriggerSelects()
    {
        string[] options = { "v0", "v1", "v2", "v3", "v4", "v5" };
        int writes = 0;
        string current = "v0";
        using Fixture page = SixOptionDropdown(() => current, value => { writes++; current = value; }, options);

        Rect trigger = page.RectOf("dropdown");
        page.OpenMenuAt(trigger);
        Rect menu = page.RequirePopupLayer();
        Vector2 coveredRow = new Vector2(trigger.x + 10f, menu.yMax - OptionHeight / 2f);

        // The premise, asserted rather than assumed: one point, two claimants, menu on top.
        Check(Over(menu, coveredRow), "the chosen point is inside the published menu: " + menu);
        Check(Over(trigger, coveredRow), "and the same point is inside the trigger that owns the menu: " + trigger);

        var trace = new List<string>();
        UiNative.Trace = trace.Add;
        try
        {
            page.Click(coveredRow);

            Check(string.Equals(current, "v5", StringComparison.Ordinal),
                "the option row under the pointer took the click and wrote its value (current='" + current + "')");
            Check(writes == 1, "the selection wrote the model exactly once (writes=" + writes + ")");
            Check(page.Session.OpenPopupId == null, "the menu closed WITH its option, not before it");
            Check(trace.Exists(line => line.IndexOf("id=dropdown", StringComparison.Ordinal) >= 0
                && line.IndexOf("ownPopup=true", StringComparison.Ordinal) >= 0
                && line.IndexOf("yields=true", StringComparison.Ordinal) >= 0),
                "the trigger recorded that it yielded to the menu it owns: " + string.Join(" | ", trace));
            Check(trace.Exists(line => line.IndexOf("option fired", StringComparison.Ordinal) >= 0),
                "the option row reported the fire that changed the value");
            KernelTripGuard.ExpectNoTrips(page.Session, "option row over its own trigger");
        }
        finally
        {
            UiNative.Trace = null;
            Event.current = null;
            GUIUtility.hotControl = 0;
        }
    }

    private enum TestMode
    {
        Alpha,
        Beta,
        Gamma,
        Delta,
        Epsilon,
        Zeta
    }

    /// <summary>
    /// The typed sibling of the same overlap: one ruling covers the typed and the string menu, so the same
    /// geometry drives <see cref="UiPopup.DrawChoiceList"/> and must hand the option's real value back - not a
    /// label spelled from it, and not the trigger's close.
    /// </summary>
    private static void VerifyTypedOptionUnderOwnTriggerSelects()
    {
        UiChoice<TestMode>[] choices =
        {
            new UiChoice<TestMode>("alpha", TestMode.Alpha),
            new UiChoice<TestMode>("beta", TestMode.Beta),
            new UiChoice<TestMode>("gamma", TestMode.Gamma),
            new UiChoice<TestMode>("delta", TestMode.Delta),
            new UiChoice<TestMode>("epsilon", TestMode.Epsilon),
            new UiChoice<TestMode>("zeta", TestMode.Zeta),
        };

        TestMode current = TestMode.Alpha;
        TestMode? received = null;
        int writes = 0;

        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        UiWidgetRegistry.Register(Scope, HeightKind, () => new SpacerWidget());

        var bindings = new UiBindings();
        bindings.BindValue("dropdown", () => current, value => { writes++; received = value; current = value; });
        bindings.BindOptions("modes", () => choices);

        using Fixture page = new Fixture(
            new UiHost(Scope, UiLayoutManifest.Parse(LowDropdownPage("modes")), bindings, UiTheme.Vanilla,
                new LaneMetrics(), new LaneTranslation()),
            new Rect(0f, 0f, 300f, 220f));

        Rect trigger = page.RectOf("dropdown");
        page.OpenMenuAt(trigger);
        Rect menu = page.RequirePopupLayer();
        Vector2 coveredRow = new Vector2(trigger.x + 10f, menu.yMax - OptionHeight / 2f);
        Check(Over(menu, coveredRow) && Over(trigger, coveredRow),
            "the typed fixture reproduces the same overlap (menu " + menu + ", trigger " + trigger + ")");

        page.Click(coveredRow);

        Check(received.HasValue && received.Value == TestMode.Zeta,
            "the typed row handed back the option's own value, not a label (received=" + received + ")");
        Check(writes == 1, "the typed setter ran exactly once (writes=" + writes + ")");
        Check(current == TestMode.Zeta && page.Session.OpenPopupId == null,
            "the typed selection closed its own menu");
        KernelTripGuard.ExpectNoTrips(page.Session, "typed option row over its own trigger");
    }

    private static void VerifyUncoveredTriggerStripStillCloses()
    {
        string[] options = { "v0", "v1", "v2", "v3", "v4", "v5" };
        string current = "v0";
        int writes = 0;
        using Fixture page = SixOptionDropdown(() => current, value => { writes++; current = value; }, options);

        Rect trigger = page.RectOf("dropdown");
        page.OpenMenuAt(trigger);
        Rect menu = page.RequirePopupLayer();

        Check(trigger.yMax > menu.yMax + 0.01f,
            "the fixture leaves a strip of the trigger outside the menu (trigger " + trigger + ", menu " + menu + ")");
        Vector2 uncovered = new Vector2(trigger.x + 10f, (menu.yMax + trigger.yMax) / 2f);
        Check(Over(trigger, uncovered) && !Over(menu, uncovered),
            "the close-click point is on the trigger and off the menu: " + uncovered);

        page.Click(uncovered);

        Check(page.Session.OpenPopupId == null, "the uncovered strip still closed its own menu");
        Check(writes == 0 && string.Equals(current, "v0", StringComparison.Ordinal),
            "and a toggle-close never wrote a value (writes=" + writes + ", current='" + current + "')");
        KernelTripGuard.ExpectNoTrips(page.Session, "uncovered trigger strip closes");
    }

    // --- D4: bounded height, reachable ends ------------------------------------------------------------

    private static void VerifyLongMenuStaysInViewportAndReachesEnds()
    {
        List<string> options = LongOptions(40);
        string current = options[0];
        int writes = 0;
        using Fixture page = DropdownPage(options, () => current, value => { writes++; current = value; }, 300f);

        Rect trigger = page.RectOf("dropdown");
        page.OpenMenuAt(trigger);
        Rect menu = page.RequirePopupLayer();

        Check(menu.height <= page.Viewport.height + 0.01f
                && menu.y >= page.Viewport.y - 0.01f
                && menu.yMax <= page.Viewport.yMax + 0.01f,
            "a forty-option menu is bounded inside the viewport instead of running past it: " + menu);
        Check(Math.Abs(menu.height % OptionHeight) < 0.01f,
            "the bounded height is a whole number of rows, so every point inside the layer is inside a row: " + menu);

        int visible = (int)(menu.height / OptionHeight);
        Check(visible >= 1 && visible < options.Count,
            "the premise this slice exists for: the list is longer than the menu shows (visible=" + visible
            + " of " + options.Count + ")");

        // Each lane of this file leaves the shared native hot control as it found it: the doubles clear it
        // only when the same control id sees the release, so a lane that ended mid-press would poison the next.
        GUIUtility.hotControl = 0;
        Vector2 bottomSlot = new Vector2(menu.x + 10f, menu.yMax - OptionHeight / 2f);
        page.Click(bottomSlot);
        Check(string.Equals(current, options[visible - 1], StringComparison.Ordinal),
            "the bottom slot selects the row it actually shows (" + options[visible - 1] + "), not the row it hides");
        Check(page.Session.OpenPopupId == null, "and closes the menu with that selection");

        page.OpenMenuAt(trigger);
        Check(page.Session.OpenPopupScrollRows == 0, "a menu that opens starts at its first option");
        for (int notch = 0; notch < 20; notch++)
        {
            Check(page.Wheel(bottomSlot, down: true), "a notch inside the open menu is owned by that menu");
        }

        Check(page.Session.OpenPopupScrollRows == options.Count - visible,
            "the scroll stops at the end of the list instead of running past it (rows="
            + page.Session.OpenPopupScrollRows + ")");

        // Backward in the SAME open, not a reopen: the round trip belongs to one menu, and a notch past either
        // end stays clamped there instead of travelling on into the page underneath.
        for (int notch = 0; notch < 20; notch++) page.Wheel(bottomSlot, down: false);
        Check(page.Session.OpenPopupScrollRows == 0,
            "scrolling back inside one open stops at the top, and a past-the-top notch is clamped, not passed on");
        Check(page.Session.OpenPopupId != null, "and no amount of owned scrolling dismissed the menu");
        page.Click(new Vector2(menu.x + 10f, menu.y + OptionHeight / 2f));
        Check(string.Equals(current, options[0], StringComparison.Ordinal),
            "so the FIRST option is selectable from the same open state that reached the last one");

        // And forward in a fresh open, the last option is selectable at the bottom slot.
        page.OpenMenuAt(trigger);
        for (int notch = 0; notch < 20; notch++) page.Wheel(bottomSlot, down: true);
        page.Click(bottomSlot);
        Check(string.Equals(current, options[options.Count - 1], StringComparison.Ordinal),
            "the LAST option of a forty-item menu is reachable and selectable (current='" + current + "')");
        Check(writes == 3, "each reachable selection wrote once (writes=" + writes + ")");
        KernelTripGuard.ExpectNoTrips(page.Session, "bounded scrollable menu");
    }

    /// <summary>
    /// The parity claim, tested rather than asserted in prose: the bounded window, the published scroll range
    /// and the one-open round trip belong to <see cref="UiPopup"/>'s row window, which BOTH option lists go
    /// through - so the typed list, with forty typed values and no string anywhere in its selection, has to
    /// behave row-for-row like the string list above.
    /// </summary>
    private static void VerifyTypedLongMenuScrollsLikeTheStringList()
    {
        var values = new List<int>();
        for (int i = 1; i <= 40; i++) values.Add(i);

        int current = 1;
        int writes = 0;
        using Fixture page = TypedIntDropdown(IntChoices(values), () => current,
            value => { writes++; current = value; }, 300f);

        GUIUtility.hotControl = 0;
        Rect trigger = page.RectOf("dropdown");
        page.OpenMenuAt(trigger);
        Rect menu = page.RequirePopupLayer();

        Check(menu.height <= page.Viewport.height + 0.01f && menu.yMax <= page.Viewport.yMax + 0.01f
                && Math.Abs(menu.height % OptionHeight) < 0.01f,
            "the typed menu is bounded to whole rows by the same rule: " + menu);
        int visible = (int)(menu.height / OptionHeight);
        Check(visible >= 1 && visible < values.Count,
            "and the typed list is longer than the typed menu shows (visible=" + visible + " of " + values.Count + ")");

        Vector2 bottomSlot = new Vector2(menu.x + 10f, menu.yMax - OptionHeight / 2f);
        for (int notch = 0; notch < 20; notch++)
        {
            Check(page.Wheel(bottomSlot, down: true), "a notch inside the typed menu is owned by it, as the string list's");
        }

        Check(page.Session.OpenPopupScrollRows == values.Count - visible,
            "the typed list publishes the same scroll range (rows=" + page.Session.OpenPopupScrollRows
            + ", visible=" + visible + ")");
        page.Click(bottomSlot);
        Check(current == 40 && writes == 1,
            "the typed last option is selectable at the scrolled bottom slot (current=" + current + ")");

        page.OpenMenuAt(trigger);
        for (int notch = 0; notch < 20; notch++) page.Wheel(bottomSlot, down: true);
        for (int notch = 0; notch < 20; notch++) page.Wheel(bottomSlot, down: false);
        Check(page.Session.OpenPopupScrollRows == 0 && page.Session.OpenPopupId != null,
            "and the typed menu returns to its first row inside one open, without the scroll leaving the top");
        page.Click(new Vector2(menu.x + 10f, menu.y + OptionHeight / 2f));
        Check(current == 1 && writes == 2,
            "with the typed first option selectable there (current=" + current + ")");
        KernelTripGuard.ExpectNoTrips(page.Session, "typed menu scrolls like the string menu");
    }

    private static void VerifyWheelBelongsToTheOpenMenu()
    {
        List<string> options = LongOptions(40);
        string current = options[0];
        using Fixture page = DropdownPage(options, () => current, value => current = value, 300f);

        Rect trigger = page.RectOf("dropdown");
        page.OpenMenuAt(trigger);
        Rect menu = page.RequirePopupLayer();

        Check(menu.yMax < page.Viewport.yMax - 1f,
            "the fixture leaves room under the menu for the outside-notch case: " + menu);
        Vector2 outside = new Vector2(menu.x + 10f, page.Viewport.yMax - 6f);
        Check(!Over(menu, outside), "the outside point is not inside the menu: " + outside);

        GUIUtility.hotControl = 0;
        Check(!page.Wheel(outside, down: true), "a notch outside the open menu is not the menu's event");
        Check(page.Session.OpenPopupScrollRows == 0, "and it does not scroll the menu");

        Vector2 inside = new Vector2(menu.x + 10f, menu.yMax - OptionHeight / 2f);
        Check(page.Wheel(inside, down: true), "a notch inside the menu IS the menu's event");
        Check(page.Session.OpenPopupScrollRows > 0,
            "and the menu moved, which is the priority the ruling asks for");

        int moved = page.Session.OpenPopupScrollRows;
        Check(!page.Wheel(outside, down: true),
            "and a notch that lands outside the menu leaves both the scroll and the event alone");
        page.Click(outside);
        Check(page.Session.OpenPopupId != null && page.Session.OpenPopupScrollRows == moved,
            "a click outside the menu neither dismissed it nor moved it: only a row or the trigger acts");

        // The end of the list is not a release of the priority: a notch that cannot move the menu is still
        // owned by it, or the page under the menu starts scrolling under the player's eyes. The menu is closed
        // here the way a row closes it - a forty-item menu covers its own trigger completely, so the
        // uncovered-strip route belongs to the short-menu lane above and not to this one.
        page.Click(inside);
        Check(page.Session.OpenPopupId == null, "the bottom row closed the menu by selecting, as every row does");
        page.OpenMenuAt(trigger);
        for (int notch = 0; notch < 40; notch++) page.Wheel(inside, down: true);
        int saturated = page.Session.OpenPopupScrollRows;
        Check(saturated > 0, "the menu reached its own end (rows=" + saturated + ")");
        Check(page.Wheel(inside, down: true),
            "a notch at the saturated end is still owned by the menu instead of falling through to the content under it");
        Check(page.Session.OpenPopupScrollRows == saturated,
            "and owning it does not push the scroll past the end of the list");
        KernelTripGuard.ExpectNoTrips(page.Session, "wheel priority over the menu");
    }

    // --- D2: the chart asks before it takes --------------------------------------------------------------

    private static void VerifyChartUnderMenuNeverCapturesOrWrites()
    {
        var points = new List<Vector2> { new(0f, 1f), new(0.5f, 0.5f), new(1f, 0f) };
        var changes = new List<UiChartPointChange>();
        string chosen = "x";

        using Fixture page = ChartUnderDropdown(points, changes, () => chosen, value => chosen = value);
        Rect chartRect = page.RectOf("chart");
        Vector2 point = PlotCentre(chartRect);

        page.OpenMenuAt(page.RectOf("dd"));
        Rect menu = page.RequirePopupLayer();
        Check(Over(menu, point),
            "the D2 premise: the editable chart point sits under the open menu (menu " + menu + ", point " + point + ")");

        // Checked DURING the gesture, not after it: a chart that captured would have released by the MouseUp as
        // well, and an assertion that holds on both sides of the fix is a guard, not the proof.
        page.Pump(EventType.Layout, point);
        page.Pump(EventType.MouseDown, point);
        Check(page.Session.OwnedHotControl == null && page.Session.OwnedHotControlOwner == null,
            "the covered chart captured nothing into its session");

        page.Pump(EventType.MouseDrag, point + new Vector2(12f, 8f));
        Check(changes.Count == 0, "the covered chart committed no drag (changes=" + changes.Count + ")");

        page.Pump(EventType.MouseUp, point + new Vector2(12f, 8f));
        // Refusing is not stealing. The press the chart would have taken lands inside the menu, so the event
        // survives to the option row under it: the row fires, the model takes that option's value, and the menu
        // closes by ITS rule - not because the chart grabbed the pointer underneath.
        Check(string.Equals(chosen, "z", StringComparison.Ordinal),
            "the option row under the covered chart point took the click instead (chosen='" + chosen + "')");
        Check(page.Session.OpenPopupId == null, "and the menu closed by selecting, exactly once");
        KernelTripGuard.ExpectNoTrips(page.Session, "chart under an open menu");
    }

    private static void VerifyChartDragWorksAfterTheMenuCloses()
    {
        var points = new List<Vector2> { new(0f, 1f), new(0.5f, 0.5f), new(1f, 0f) };
        var changes = new List<UiChartPointChange>();
        string chosen = "x";

        using Fixture page = ChartUnderDropdown(points, changes, () => chosen, value => chosen = value);
        Rect chartRect = page.RectOf("chart");
        Vector2 point = PlotCentre(chartRect);

        // Close the menu the way a player does: choose the option that was covering the chart.
        page.OpenMenuAt(page.RectOf("dd"));
        Rect menu = page.RequirePopupLayer();
        page.Click(new Vector2(menu.x + 10f, menu.y + OptionHeight / 2f));
        Check(page.Session.OpenPopupId == null, "the menu closed on its option");
        Check(string.Equals(chosen, "x", StringComparison.Ordinal),
            "and the row that was chosen is the one the model now holds (chosen='" + chosen + "')");

        page.Pump(EventType.Layout, point);
        page.Pump(EventType.MouseDown, point);
        page.Pump(EventType.Repaint, point);
        Check(page.Session.OwnedHotControl != null, "the same chart takes the press once nothing covers it");

        page.Pump(EventType.MouseDrag, point + new Vector2(24f, -16f));
        Check(changes.Count == 1, "the drag emitted exactly one typed change (changes=" + changes.Count + ")");
        // The kind never writes the consumer's list: it emits one typed change and the page applies it.
        Check(changes[0].Index == 1,
            "the change names the control point the drag holds (index=" + changes[0].Index + ")");
        Check(changes[0].X > 0.5f && changes[0].Y > 0.5f,
            "and it moved toward the pointer on both axes - a pixel rise is a normalized climb ("
            + changes[0].X.ToString("F3") + ", " + changes[0].Y.ToString("F3") + ")");
        Check(Math.Abs(points[1].x - 0.5f) < 0.0001f && Math.Abs(points[1].y - 0.5f) < 0.0001f,
            "and the consumer's own list still holds what it was handed: the change is the only write channel");

        page.Pump(EventType.MouseUp, point + new Vector2(24f, -16f));
        Check(page.Session.OwnedHotControl == null && GUIUtility.hotControl == 0,
            "the release ended the hold, in the session and in the native slot");
        KernelTripGuard.ExpectNoTrips(page.Session, "chart drag after the menu closes");
    }

    private static void VerifyHeldChartDragContinuesOutsideTheRect()
    {
        var points = new List<Vector2> { new(0f, 1f), new(0.5f, 0.5f), new(1f, 0f) };
        var changes = new List<UiChartPointChange>();

        using Fixture page = ChartOnly(points, changes);
        Rect chartRect = page.RectOf("chart");
        Vector2 point = PlotCentre(chartRect);
        Vector2 farOutside = new Vector2(chartRect.x - 40f, chartRect.yMax + 60f);
        Check(!Over(chartRect, farOutside), "the continuation point is outside the chart's own rect: " + farOutside);

        page.Pump(EventType.MouseDown, point);
        page.Pump(EventType.Repaint, point);
        Check(page.Session.OwnedHotControl != null, "the drag is under way");

        page.Pump(EventType.MouseDrag, farOutside);
        Check(changes.Count == 1,
            "a held drag is not ended by the pointer leaving the rect - continuation follows the capture, not hover");
        Check(changes[0].Index == 1 && changes[0].X < 0.0001f && changes[0].Y < 0.0001f,
            "and the point clamps to the plot corner instead of running off the data (x="
            + changes[0].X.ToString("F3") + ", y=" + changes[0].Y.ToString("F3") + ")");

        page.Pump(EventType.MouseUp, farOutside);
        Check(page.Session.OwnedHotControl == null && GUIUtility.hotControl == 0,
            "the release outside the rect still ends the hold: the release is not gated by hover either");
        KernelTripGuard.ExpectNoTrips(page.Session, "held chart drag continues outside the rect");
    }

    private static void VerifyDisabledChartTakesNoPress()
    {
        var points = new List<Vector2> { new(0f, 1f), new(0.5f, 0.5f), new(1f, 0f) };
        var changes = new List<UiChartPointChange>();
        bool runnable = false;

        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();

        const string xml =
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Widget Id=\"chart\" Kind=\"chart/line\" Bind=\"points\" ActionBind=\"changed\" Editable=\"true\""
            + " EditablePoints=\"1\" Height=\"120\" />"
            + "</UiPage>";

        var bindings = new UiBindings();
        bindings.BindReadOnly<IReadOnlyList<Vector2>>("points", () => points);
        bindings.BindAction<UiChartPointChange>("changed", changes.Add);
        // The engine publishes an element's disabled state from the command its ActionBind names, so the
        // chart's own key answers CanExecute: one disabled rule for every kind, no per-widget branch.
        bindings.BindCommand("changed", () => { }, () => runnable);

        using Fixture page = new Fixture(
            new UiHost(Scope, UiLayoutManifest.Parse(xml), bindings, UiTheme.Vanilla,
                new LaneMetrics(), new LaneTranslation()),
            new Rect(0f, 0f, 300f, 300f));

        Vector2 point = PlotCentre(page.RectOf("chart"));

        page.Pump(EventType.MouseDown, point);
        page.Pump(EventType.MouseDrag, point + new Vector2(10f, 0f));
        page.Pump(EventType.MouseUp, point + new Vector2(10f, 0f));
        Check(page.Session.OwnedHotControl == null && changes.Count == 0,
            "a disabled chart took no capture and committed no change");

        runnable = true;
        page.Session.BumpContentRevision();
        page.Pump(EventType.Repaint, point);
        page.Pump(EventType.MouseDown, point);
        page.Pump(EventType.Repaint, point);
        Check(page.Session.OwnedHotControl != null,
            "and the same chart takes presses again the moment its command can run");
        page.Pump(EventType.MouseUp, point);
        KernelTripGuard.ExpectNoTrips(page.Session, "disabled chart refuses the press");
    }

    private static void VerifyChartOutsideTheEffectiveClipTakesNoPress()
    {
        var points = new List<Vector2> { new(0f, 1f), new(0.5f, 0.5f), new(1f, 0f) };
        var changes = new List<UiChartPointChange>();

        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();

        // A 40px scroll window over a 120px chart: the chart's middle band - where its editable point sits -
        // is clipped away, so a press there is a press on paint nobody can see.
        const string xml =
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Scroll Id=\"window\" Height=\"40\" Padding=\"0\" Gap=\"0\">"
            + "<Widget Id=\"chart\" Kind=\"chart/line\" Bind=\"points\" ActionBind=\"changed\" Editable=\"true\""
            + " EditablePoints=\"1\" Height=\"120\" />"
            + "</Scroll>"
            + "</UiPage>";

        var bindings = new UiBindings();
        bindings.BindReadOnly<IReadOnlyList<Vector2>>("points", () => points);
        bindings.BindAction<UiChartPointChange>("changed", changes.Add);

        using Fixture page = new Fixture(
            new UiHost(Scope, UiLayoutManifest.Parse(xml), bindings, UiTheme.Vanilla,
                new LaneMetrics(), new LaneTranslation()),
            new Rect(0f, 0f, 300f, 300f));

        Rect chartRect = page.RectOf("chart");
        Vector2 point = PlotCentre(chartRect);
        Check(point.y > page.Viewport.y + 40f,
            "the fixture puts the chart's own point below the scroll window that clips it (chart " + chartRect + ")");

        page.Pump(EventType.MouseDown, point);
        page.Pump(EventType.MouseDrag, point + new Vector2(10f, 0f));
        page.Pump(EventType.MouseUp, point + new Vector2(10f, 0f));
        Check(page.Session.OwnedHotControl == null && changes.Count == 0,
            "a press in the clipped band captures nothing and writes nothing");

        // Counterfactual: scroll that band into view and the very same control point is pressable again - the
        // press point moves with the content, which is what a real container does to a clipped chart.
        UiNode scrollNode = page.Session.GetNodeByElementId("window")
            ?? throw new Exception("the scroll window has no node");
        page.Session.SetScrollPosition(scrollNode, new Vector2(0f, 40f));
        Vector2 visiblePress = new Vector2(point.x, point.y - 40f);
        Check(visiblePress.y < page.Viewport.y + 40f,
                "the scroll brought the point inside the window: " + visiblePress);
        page.Pump(EventType.Repaint, visiblePress);
        page.Pump(EventType.MouseDown, visiblePress);
        page.Pump(EventType.Repaint, visiblePress);
        Check(page.Session.OwnedHotControl != null,
            "and once the container has scrolled that band into the viewport, the same point is pressable again");
        page.Pump(EventType.MouseUp, visiblePress);
        KernelTripGuard.ExpectNoTrips(page.Session, "chart outside the effective clip");
    }

    // --- capture ownership -------------------------------------------------------------------------------

    private static void VerifyCaptureReleasedWhenOwnerStopsDrawing()
    {
        var points = new List<Vector2> { new(0f, 1f), new(0.5f, 0.5f), new(1f, 0f) };
        var changes = new List<UiChartPointChange>();
        bool visible = true;

        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();

        const string xml =
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Widget Id=\"chart\" Kind=\"chart/line\" Bind=\"points\" ActionBind=\"changed\" Editable=\"true\""
            + " EditablePoints=\"1\" Height=\"120\" VisibleKey=\"chart-visible\" />"
            + "</UiPage>";

        var bindings = new UiBindings();
        bindings.BindReadOnly<IReadOnlyList<Vector2>>("points", () => points);
        bindings.BindAction<UiChartPointChange>("changed", changes.Add);
        bindings.BindReadOnly("chart-visible", () => visible);

        using Fixture page = new Fixture(
            new UiHost(Scope, UiLayoutManifest.Parse(xml), bindings, UiTheme.Vanilla,
                new LaneMetrics(), new LaneTranslation()),
            new Rect(0f, 0f, 300f, 300f));

        using UiSession other = new UiSession();
        Vector2 point = PlotCentre(page.RectOf("chart"));

        try
        {
            page.Pump(EventType.MouseDown, point);
            page.Pump(EventType.Repaint, point);
            Check(page.Session.OwnedHotControl != null, "the chart holds the capture a drag holds");

            // A second session takes the native slot afterwards; the release must not sweep it away.
            other.CaptureHotControl(777);
            int held = page.Session.OwnedHotControl!.Value;

            visible = false;
            page.Session.BumpContentRevision();
            page.Pump(EventType.Layout, point);

            Check(page.Session.OwnedHotControl == null,
                "an owner that is no longer drawn ends its own capture instead of holding it forever (held " + held + ")");
            Check(page.Session.OwnedHotControlOwner == null, "and its owner record goes with it");
            Check(other.OwnedHotControl == 777 && GUIUtility.hotControl == 777,
                "the other session's capture is untouched, in its record and in the native slot");
            KernelTripGuard.ExpectNoTrips(page.Session, "capture released when the owner stops drawing");
        }
        finally
        {
            Event.current = null;
            GUIUtility.hotControl = 0;
        }
    }

    private static void VerifyRemovedIdentityReleasesItsCapture()
    {
        var points = new List<Vector2> { new(0f, 1f), new(0.5f, 0.5f), new(1f, 0f) };
        var changes = new List<UiChartPointChange>();

        using Fixture page = ChartOnly(points, changes);
        Vector2 point = PlotCentre(page.RectOf("chart"));
        UiNode chartNode = page.Session.GetNodeByElementId("chart")
            ?? throw new Exception("the arranged chart has no node");

        try
        {
            page.Pump(EventType.MouseDown, point);
            page.Pump(EventType.Repaint, point);
            Check(ReferenceEquals(page.Session.OwnedHotControlOwner, chartNode),
                "the capture records the element that took it, not an anonymous id");
            // The chart keeps its drag in the state slot its own element id names, and the capture names the
            // node, so "this element is the owner" is readable from both sides of the session.
            Check(chartNode.GetOrCreateState("chart").Dragging,
                "and the element's own state says a drag is in progress");

            int held = page.Session.OwnedHotControl!.Value;

            // The definition no longer declares this identity: the same release a reloaded page or a removed
            // repeater row performs, reached through the session's own prune of the arranged tree.
            page.Session.PruneNodesExcept(new HashSet<UiNodeId>());

            Check(page.Session.OwnedHotControl == null,
                "a released identity cannot keep the capture it was holding (held " + held + ")");
            Check(GUIUtility.hotControl == 0, "and the native slot it owned is clear");
        }
        finally
        {
            Event.current = null;
            GUIUtility.hotControl = 0;
        }
    }

    // --- fixtures ------------------------------------------------------------------------------------------

    private static UiChoice<int>[] IntChoices(List<int> values)
    {
        var choices = new UiChoice<int>[values.Count];
        for (int i = 0; i < values.Count; i++)
        {
            choices[i] = new UiChoice<int>(
                "n" + values[i].ToString(System.Globalization.CultureInfo.InvariantCulture), values[i]);
        }

        return choices;
    }

    /// <summary>
    /// The same page as <see cref="DropdownPage"/>, driven by a TYPED value binding and typed choices, so the
    /// dropdown takes the <see cref="UiPopup.DrawChoiceList"/> route and nothing about the geometry differs.
    /// </summary>
    private static Fixture TypedIntDropdown(UiChoice<int>[] choices, Func<int> get, Action<int> set,
        float viewportHeight)
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        UiWidgetRegistry.Register(Scope, HeightKind, () => new SpacerWidget());

        var bindings = new UiBindings();
        bindings.BindValue("dropdown", get, set);
        bindings.BindOptions("modes", () => choices);

        return new Fixture(
            new UiHost(Scope, UiLayoutManifest.Parse(LowDropdownPage("modes")), bindings, UiTheme.Vanilla,
                new LaneMetrics(), new LaneTranslation()),
            new Rect(0f, 0f, 300f, viewportHeight));
    }

    private static List<string> LongOptions(int count)
    {
        var options = new List<string>(count);
        for (int i = 0; i < count; i++)
        {
            options.Add("item" + i.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        return options;
    }

    private static string LowDropdownPage(string optionsKey)
    {
        return "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Column Id=\"col\" Padding=\"0\" Gap=\"0\">"
            + "<Widget Id=\"spacer\" Kind=\"" + HeightKind + "\" Height=\"120\" />"
            + "<Widget Id=\"dropdown\" Kind=\"input/dropdown\" OptionsBind=\"" + optionsKey
            + "\" Height=\"" + TriggerHeight + "\" />"
            + "</Column>"
            + "</UiPage>";
    }

    private static Fixture SixOptionDropdown(Func<string> get, Action<string> set, string[] options)
        => DropdownPage(options, get, set, 220f);

    private static Fixture DropdownPage(
        IReadOnlyList<string> options,
        Func<string> get,
        Action<string> set,
        float viewportHeight)
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        UiWidgetRegistry.Register(Scope, HeightKind, () => new SpacerWidget());

        var bindings = new UiBindings();
        bindings.BindValue("dropdown", get, set);
        bindings.BindOptions("Options", () => options);

        return new Fixture(
            new UiHost(Scope, UiLayoutManifest.Parse(LowDropdownPage("Options")), bindings, UiTheme.Vanilla,
                new LaneMetrics(), new LaneTranslation()),
            new Rect(0f, 0f, 300f, viewportHeight));
    }

    private static Fixture ChartUnderDropdown(
        List<Vector2> points,
        List<UiChartPointChange> changes,
        Func<string> get,
        Action<string> set)
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();

        const string xml =
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Column Id=\"col\" Padding=\"0\" Gap=\"2\">"
            + "<Widget Id=\"dd\" Kind=\"input/dropdown\" OptionsBind=\"Options\" Height=\"" + TriggerHeight + "\" />"
            + "<Widget Id=\"chart\" Kind=\"chart/line\" Bind=\"points\" ActionBind=\"changed\" Editable=\"true\""
            + " EditablePoints=\"1\" Height=\"120\" />"
            + "</Column>"
            + "</UiPage>";

        var bindings = new UiBindings();
        bindings.BindValue("dd", get, set);
        bindings.BindOptions("Options", () => new List<string> { "x", "y", "z", "w" });
        bindings.BindReadOnly<IReadOnlyList<Vector2>>("points", () => points);
        bindings.BindAction<UiChartPointChange>("changed", changes.Add);

        return new Fixture(
            new UiHost(Scope, UiLayoutManifest.Parse(xml), bindings, UiTheme.Vanilla,
                new LaneMetrics(), new LaneTranslation()),
            new Rect(0f, 0f, 300f, 300f));
    }

    private static Fixture ChartOnly(List<Vector2> points, List<UiChartPointChange> changes)
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();

        const string xml =
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Widget Id=\"chart\" Kind=\"chart/line\" Bind=\"points\" ActionBind=\"changed\" Editable=\"true\""
            + " EditablePoints=\"1\" Height=\"120\" />"
            + "</UiPage>";

        var bindings = new UiBindings();
        bindings.BindReadOnly<IReadOnlyList<Vector2>>("points", () => points);
        bindings.BindAction<UiChartPointChange>("changed", changes.Add);

        return new Fixture(
            new UiHost(Scope, UiLayoutManifest.Parse(xml), bindings, UiTheme.Vanilla,
                new LaneMetrics(), new LaneTranslation()),
            new Rect(0f, 0f, 300f, 300f));
    }

    /// <summary>
    /// The pixel a normalized control point sits at: the element rect, inset by the chart's own plot padding.
    /// The padding is the kind's geometry and is written here as the number the kind uses, so the lane aims at
    /// a real control point instead of guessing.
    /// </summary>
    private static Vector2 PlotCentre(Rect chartRect)
    {
        const float plotPadding = 8f;
        return new Vector2(
            chartRect.x + plotPadding + Math.Max(1f, chartRect.width - plotPadding * 2f) * 0.5f,
            chartRect.y + plotPadding + Math.Max(1f, chartRect.height - plotPadding * 2f) * 0.5f);
    }

    private static Rect RequireLayerRect(UiSession session)
    {
        for (int i = session.HitLayers.Count - 1; i >= 0; i--)
        {
            if (session.HitLayers[i].IsPopup) return session.HitLayers[i].Rect;
        }

        throw new Exception("the popup pass published no hit layer");
    }

    private static bool Over(Rect rect, Vector2 point)
    {
        return point.x >= rect.x && point.x <= rect.xMax && point.y >= rect.y && point.y <= rect.yMax;
    }

    /// <summary>One host, the viewport its frames draw against, and the event pump the lanes share.</summary>
    private sealed class Fixture : IDisposable
    {
        private bool arranged;

        public Fixture(UiHost host, Rect viewport)
        {
            Host = host;
            Viewport = viewport;
            Session = host.Session;
        }

        public UiHost Host { get; }

        public UiSession Session { get; }

        public Rect Viewport { get; }

        public Rect RectOf(string elementId)
        {
            Arrange();
            return Host.MeasureAndArrange(new Vector2(Viewport.width, Viewport.height)).RectById[elementId];
        }

        public void Arrange()
        {
            if (arranged) return;
            arranged = true;
            Host.MeasureAndArrange(new Vector2(Viewport.width, Viewport.height));
        }

        public void Pump(EventType type, Vector2 point)
        {
            Arrange();
            Event e = Event.KeyboardEvent("space");
            e.type = type;
            e.button = 0;
            e.mousePosition = point;
            Event.current = e;
            Host.DrawFrame(Viewport);
            Event.current = null;
        }

        /// <summary>One click as the game dispatches it: separate down and up events, not one flag.</summary>
        public void Click(Vector2 point)
        {
            Pump(EventType.Layout, point);
            Pump(EventType.MouseDown, point);
            Pump(EventType.MouseUp, point);
            Pump(EventType.Repaint, point);
        }

        /// <summary>
        /// One wheel notch at <paramref name="point"/>, pumped with the sign the native IMGUI scroll view uses:
        /// a POSITIVE <c>delta.y</c> asks for the LATER rows, which is what <see cref="UiNative.PointerWheelNotches"/>
        /// hands to the menu. Returns whether the event was consumed - the difference between the menu owning
        /// its own rect and the notch falling through to the content underneath. The harness injects the value,
        /// so these lanes prove the routing, the clamp and the ownership; a real device's weight and direction
        /// remain the playtest's.
        /// </summary>
        public bool Wheel(Vector2 point, bool down)
        {
            Arrange();
            Event e = Event.KeyboardEvent("space");
            e.type = EventType.ScrollWheel;
            e.button = 0;
            e.mousePosition = point;
            e.delta = new Vector2(0f, down ? 1f : -1f);
            Event.current = e;
            Host.DrawFrame(Viewport);
            // The reference Event exposes no 'used' getter; consumption is what Use() leaves behind: the type
            // flips to Used. The same read answers for the double and for the game.
            bool consumed = e.type == EventType.Used;
            Event.current = null;
            return consumed;
        }

        /// <summary>Opens the page's dropdown by clicking the middle of its own trigger band.</summary>
        public void OpenMenuAt(Rect trigger)
        {
            Click(new Vector2(trigger.x + 10f, trigger.y + trigger.height / 2f));
            if (Session.OpenPopupId == null)
            {
                throw new Exception("the fixture's dropdown did not open at " + trigger);
            }
        }

        /// <summary>The rect the open menu published, read from the pass that drew it.</summary>
        public Rect RequirePopupLayer() => RequireLayerRect(Session);

        public void Dispose() => Host.Dispose();
    }

    private sealed class SpacerWidget : IUiWidget
    {
        private UiElementSpec spec = UiElementSpec.Empty;

        public string Kind => HeightKind;

        public void Configure(UiElementSpec spec)
        {
            this.spec = spec ?? throw new ArgumentNullException(nameof(spec));
        }

        public void Validate(IUiBindings bindings, string elementPath) { }

        public float Measure(UiWidgetContext ctx)
        {
            return spec.TryGetAttribute("Height", out string raw)
                && float.TryParse(raw, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float height)
                ? height
                : 10f;
        }

        public void Draw(Rect rect, UiWidgetContext ctx) { }
    }

    private sealed class LaneMetrics : ITextMetrics
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

    private sealed class LaneTranslation : IUiTranslation
    {
        public string Translate(string key) => "[" + key + "]";

        public int TranslationRevision => 0;
    }
}
