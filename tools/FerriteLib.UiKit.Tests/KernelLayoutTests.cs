using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FerriteLib.UiKit;
using FerriteLib.UiKit.Kernel;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// Greenfield kernel layout tests for Schema=2 containers: Stack/Column/Row/Wrap/Overlay/Scroll/
/// Clip hidden-element behavior, scroll clamp and structural scope cleanup.
/// </summary>
internal static class KernelLayoutTests
{
    private const string Scope = "layout-test";
    private const string TestWidgetKind = "test/widget";
    private static int failures;

    public static int RunAll()
    {
        failures = 0;
        Run("Row/Column/Stack measure", VerifyRowColumnStackMeasure);
        Run("Wrap line breaking", VerifyWrapLineBreaking);
        Run("Overlay stacking", VerifyOverlayStacking);
        Run("Scroll viewport/content rects", VerifyScrollRects);
        Run("Scroll without overflow keeps full content width", VerifyNonOverflowingScrollKeepsWidth);
        Run("Scroll draw uses session position and balanced Begin/End", VerifyScrollDrawScope);
        Run("Scroll clamp", VerifyScrollClamp);
        Run("Hidden elements", VerifyHiddenElements);
        Run("Clip structure cleanup on exception", VerifyClipCleanupOnException);
        Run("Vertical Fill allocates remainder after fixed/natural siblings", VerifyVerticalFillAllocation);
        Run("Multiple Fill siblings share the remainder", VerifyMultipleFillsShareRemainder);
        Run("Row widths subtract sibling gaps", VerifyRowWidthsSubtractGaps);
        Run("Tab-gated elements use active-tab", VerifyTabVisibility);
        Run("Auto column hugs its label's measured width (N1)", VerifyAutoWidthTracksGlyphModel);
        Run("MinWidth/MaxWidth clamp the natural width (N1)", VerifyAutoMinWidthMaxWidthClamp);
        Run("Auto widths are capped by the budget fixed siblings leave (N1)", VerifyAutoCappedByBudget);
        Run("Stack child with Width=Auto hugs its label (N1)", VerifyStackAutoChildHugsLabel);
        Run("Core stepper-slider is Auto-measurable through its declared label set (N1+N3)", VerifyCoreKindAutoMeasurable);
        Run("Breakpoint flips Row to a stack without reopening (N2)", VerifyBreakpointDirectionFlip);
        Run("NarrowHidden children vanish only in the narrow state (N2)", VerifyNarrowHidden);
        Run("Wrap Cols/NarrowCols pin the column count per state (N2)", VerifyWrapColsResponsive);
        Run("Responsive vocabulary is rejected before it can silently no-op (N2 grammar)", VerifyResponsiveValidation);
        return failures;
    }

    private static void Run(string name, Action action)
    {
        try
        {
            action();
            Console.WriteLine("  ok: " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.Error.WriteLine("  FAIL: " + name + " :: " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static void VerifyRowColumnStackMeasure()
    {
        RegisterTestWidget(10f, null, false);

        string xml =
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Stack Id=\"stack\" Gap=\"4\" Padding=\"6\">"
            + "<Widget Id=\"a\" Kind=\"" + TestWidgetKind + "\" Height=\"10\" />"
            + "<Widget Id=\"b\" Kind=\"" + TestWidgetKind + "\" Height=\"20\" />"
            + "</Stack>"
            + "<Column Id=\"column\" Gap=\"2\" Padding=\"1\">"
            + "<Widget Id=\"c\" Kind=\"" + TestWidgetKind + "\" Height=\"5\" />"
            + "<Widget Id=\"d\" Kind=\"" + TestWidgetKind + "\" Height=\"7\" />"
            + "</Column>"
            + "<Row Id=\"row\" Gap=\"5\" Padding=\"3\">"
            + "<Widget Id=\"e\" Kind=\"" + TestWidgetKind + "\" Width=\"30\" Height=\"10\" />"
            + "<Widget Id=\"f\" Kind=\"" + TestWidgetKind + "\" Width=\"40\" Height=\"20\" />"
            + "</Row>"
            + "</UiPage>";

        UiLayoutSnapshot snapshot = Arrange(xml, 400f, 600f).Snapshot;

        Check(Near(snapshot.RectById["stack"].height, 46f), "Stack height = padding 6 + 10 + gap 4 + 20 + padding 6");
        Check(Near(snapshot.RectById["a"].y, 6f) && Near(snapshot.RectById["b"].y, 20f),
            "Stack children stack vertically");
        Check(Near(snapshot.RectById["column"].height, 16f), "Column height = 1 + 5 + 2 + 7 + 1");
        Check(Near(snapshot.RectById["c"].y, 47f) && Near(snapshot.RectById["d"].y, 54f),
            "Column children stack vertically after the preceding stack root (c.y=" + snapshot.RectById["c"].y + ", d.y=" + snapshot.RectById["d"].y + ")");
        Check(Near(snapshot.RectById["row"].height, 26f), "Row height = max(10,20) + vertical padding 6");
        Check(Near(snapshot.RectById["e"].x, 3f) && Near(snapshot.RectById["f"].x, 38f),
            "Row children sit side by side");
        Check(Near(snapshot.ContentSize.y, 88f), "Root content height accumulates stack+column+row");
    }

    private static void VerifyWrapLineBreaking()
    {
        RegisterTestWidget(10f, null, false);

        string xml =
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Wrap Id=\"wrap\" Gap=\"10\">"
            + "<Widget Id=\"a\" Kind=\"" + TestWidgetKind + "\" Width=\"120\" Height=\"10\" />"
            + "<Widget Id=\"b\" Kind=\"" + TestWidgetKind + "\" Width=\"120\" Height=\"10\" />"
            + "<Widget Id=\"c\" Kind=\"" + TestWidgetKind + "\" Width=\"120\" Height=\"10\" />"
            + "</Wrap>"
            + "</UiPage>";

        UiLayoutSnapshot snapshot = Arrange(xml, 260f, 600f).Snapshot;

        Check(Near(snapshot.RectById["a"].x, 0f) && Near(snapshot.RectById["a"].y, 0f),
            "Wrap first child starts at origin");
        Check(Near(snapshot.RectById["b"].x, 130f) && Near(snapshot.RectById["b"].y, 0f),
            "Wrap second child stays on first line");
        Check(Near(snapshot.RectById["c"].x, 0f) && Near(snapshot.RectById["c"].y, 20f),
            "Wrap third child breaks to second line");
        Check(Near(snapshot.RectById["wrap"].height, 30f), "Wrap height covers two lines of 10px children plus row gap 10");
    }

    private static void VerifyOverlayStacking()
    {
        RegisterTestWidget(10f, null, false);

        string xml =
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Overlay Id=\"overlay\">"
            + "<Widget Id=\"back\" Kind=\"" + TestWidgetKind + "\" Height=\"10\" />"
            + "<Widget Id=\"front\" Kind=\"" + TestWidgetKind + "\" Height=\"20\" />"
            + "</Overlay>"
            + "</UiPage>";

        UiLayoutSnapshot snapshot = Arrange(xml, 200f, 600f).Snapshot;

        Check(Near(snapshot.RectById["back"].x, snapshot.RectById["front"].x)
            && Near(snapshot.RectById["back"].y, snapshot.RectById["front"].y),
            "Overlay children share the same top-left origin");
        Check(Near(snapshot.RectById["overlay"].height, 20f),
            "Overlay height is the max child height, not the sum");
        Check(Near(snapshot.RectById["front"].height, 20f), "Overlay front child keeps its own height");
    }

    private static void VerifyScrollRects()
    {
        RegisterTestWidget(10f, null, false);

        string xml = ScrollXml();

        UiLayoutSnapshot snapshot = Arrange(xml, 200f, 600f).Snapshot;

        Check(snapshot.Viewports.TryGetValue("scroll", out Rect viewport)
            && Near(viewport.x, 0f) && Near(viewport.y, 0f)
            && Near(viewport.width, 200f) && Near(viewport.height, 50f),
            "Scroll viewport rect is the arranged fixed-height rect");
        Check(snapshot.ScrollContents.TryGetValue("scroll", out Rect content)
            && Near(content.x, 0f) && Near(content.y, 0f)
            && Near(content.width, 184f) && Near(content.height, 60f),
            "Overflowing Scroll reserves the 16px vertical scrollbar width");
        Check(Near(snapshot.RectById["a"].width, 184f) && Near(snapshot.RectById["b"].width, 184f),
            "Overflowing Scroll remeasures children at the visible content width");
        Check(Near(snapshot.RectById["scroll"].height, 50f), "Scroll element rect height is viewport height");
    }

    private static void VerifyNonOverflowingScrollKeepsWidth()
    {
        RegisterTestWidget(10f, null, false);

        string xml =
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Scroll Id=\"scroll\" Height=\"80\">"
            + "<Widget Id=\"a\" Kind=\"" + TestWidgetKind + "\" Height=\"30\" />"
            + "<Widget Id=\"b\" Kind=\"" + TestWidgetKind + "\" Height=\"30\" />"
            + "</Scroll></UiPage>";

        UiLayoutSnapshot snapshot = Arrange(xml, 200f, 600f).Snapshot;
        Check(snapshot.ScrollContents.TryGetValue("scroll", out Rect content)
            && Near(content.width, 200f) && Near(content.height, 60f),
            "Non-overflowing Scroll keeps the full viewport width");
        Check(Near(snapshot.RectById["a"].width, 200f) && Near(snapshot.RectById["b"].width, 200f),
            "Non-overflowing Scroll keeps children at the full width");
    }

    private static void VerifyScrollDrawScope()
    {
        RegisterTestWidget(10f, null, false);

        string xml = ScrollXml();
        UiLayoutEngine engine = new(Scope);
        UiWidgetContext ctx = CreateContext();
        UiLayoutSnapshot snapshot = engine.ArrangeRoots(ctx, new Vector2(200f, 600f), Parse(xml).Roots);

        ResetScrollCounters();
        UiNode scrollNode = ctx.Session.GetNodeByElementId("scroll")
            ?? throw new Exception("the arranged scroll element has no node");
        ctx.Session.SetScrollPosition(scrollNode, new Vector2(0f, 3f));
        engine.Draw(ctx, snapshot, new Rect(0f, 0f, 200f, 200f));

        Vector2 stored = ctx.Session.GetScrollPosition(scrollNode);
        Check(Near(stored.y, 3f), "Scroll draw preserves the session scroll position");
        Check(ReadStaticInt(typeof(Verse.Widgets), "ScrollViewDepth") == 0,
            "Scroll draw leaves no open scroll view");
        Check(ReadStaticInt(typeof(Verse.Widgets), "BeginScrollViewCalls")
            == ReadStaticInt(typeof(Verse.Widgets), "EndScrollViewCalls"),
            "Scroll draw balances Begin/EndScrollView");
    }

    private static void VerifyScrollClamp()
    {
        RegisterTestWidget(10f, null, false);

        string xml = ScrollXml();
        UiLayoutEngine engine = new(Scope);
        UiWidgetContext ctx = CreateContext();

        // The scroll position belongs to the scroll element's node, which exists once that element has
        // been arranged; the clamp under test then runs on the arrange that follows.
        engine.ArrangeRoots(ctx, new Vector2(200f, 600f), Parse(xml).Roots);
        UiNode scrollNode = ctx.Session.GetNodeByElementId("scroll")
            ?? throw new Exception("the arranged scroll element has no node");
        ctx.Session.SetScrollPosition(scrollNode, new Vector2(0f, 1000f));

        UiLayoutSnapshot snapshot = engine.ArrangeRoots(ctx, new Vector2(200f, 600f), Parse(xml).Roots);
        Vector2 pos = ctx.Session.GetScrollPosition(scrollNode);
        Check(Near(pos.y, 10f), "Scroll position clamps to contentHeight - viewportHeight (60-50)");

        ctx.Session.SetScrollPosition(scrollNode, new Vector2(0f, -5f));
        snapshot = engine.ArrangeRoots(ctx, new Vector2(210f, 600f), Parse(xml).Roots);
        pos = ctx.Session.GetScrollPosition(scrollNode);
        Check(Near(pos.x, 0f) && Near(pos.y, 0f), "Negative scroll clamps to zero");
    }

    private static void VerifyHiddenElements()
    {
        RegisterTestWidget(10f, null, false);

        string xml =
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Stack Id=\"stack\" Gap=\"4\">"
            + "<Widget Id=\"a\" Kind=\"" + TestWidgetKind + "\" Height=\"10\" />"
            + "<Widget Id=\"b\" Kind=\"" + TestWidgetKind + "\" Height=\"20\" Hidden=\"true\" />"
            + "<Widget Id=\"c\" Kind=\"" + TestWidgetKind + "\" Height=\"30\" />"
            + "</Stack>"
            + "</UiPage>";

        UiLayoutSnapshot snapshot = Arrange(xml, 200f, 600f).Snapshot;

        Check(Near(snapshot.ContentSize.y, 44f), "Hidden child contributes no height");
        Check(!snapshot.RectById.ContainsKey("b"), "Hidden child has no arranged rect");
        Check(!snapshot.VisibleIds.Contains("stack/b"), "Hidden child is absent from VisibleIds");
        Check(snapshot.VisibleIds.Contains("stack/a") && snapshot.VisibleIds.Contains("stack/c"),
            "Visible children remain in VisibleIds");
    }

    private static void VerifyVerticalFillAllocation()
    {
        RegisterTestWidget(10f, null, false);

        string xml =
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Column Id=\"column\" Gap=\"4\">"
            + "<Widget Id=\"a\" Kind=\"" + TestWidgetKind + "\" Height=\"20\" />"
            + "<Scroll Id=\"fill\" Fill=\"true\" />"
            + "<Widget Id=\"b\" Kind=\"" + TestWidgetKind + "\" Height=\"30\" />"
            + "</Column>"
            + "</UiPage>";

        UiLayoutSnapshot snapshot = Arrange(xml, 200f, 200f).Snapshot;

        // Available 200: fixed 20 + natural 30 + 2 gaps of 4 leave 142 for the Fill scroll.
        Check(snapshot.Viewports.TryGetValue("fill", out Rect viewport)
            && Near(viewport.height, 142f),
            "Fill child absorbs exactly the remainder after fixed/natural siblings");
        Check(Near(snapshot.RectById["a"].y, 0f) && Near(snapshot.RectById["b"].y, 170f),
            "Fixed/natural siblings keep their flow positions around the Fill child");
        Check(Near(snapshot.RectById["column"].height, 200f),
            "Column with Fill children fits the available height exactly");
        Check(Near(snapshot.ContentSize.y, 200f),
            "Root content height with a Fill child equals the window height");
    }

    private static void VerifyMultipleFillsShareRemainder()
    {
        RegisterTestWidget(10f, null, false);

        string xml =
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Column Id=\"column\" Gap=\"4\">"
            + "<Scroll Id=\"fill-a\" Fill=\"true\" />"
            + "<Scroll Id=\"fill-b\" Fill=\"true\" />"
            + "</Column>"
            + "</UiPage>";

        UiLayoutSnapshot snapshot = Arrange(xml, 200f, 200f).Snapshot;

        // Available 200 minus one gap of 4 is shared by the two Fill scrolls.
        Check(snapshot.Viewports.TryGetValue("fill-a", out Rect viewportA)
            && Near(viewportA.height, 98f),
            "First Fill sibling gets its share of the remainder");
        Check(snapshot.Viewports.TryGetValue("fill-b", out Rect viewportB)
            && Near(viewportB.height, 98f),
            "Second Fill sibling gets the same share");
        Check(Near(viewportB.y, 102f), "Second Fill sibling sits below the first plus the gap");
        Check(Near(snapshot.RectById["column"].height, 200f),
            "Column with two Fill children fits the available height exactly");
    }

    private static void VerifyRowWidthsSubtractGaps()
    {
        RegisterTestWidget(10f, null, false);
        string xml =
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Row Id=\"row\" Gap=\"12\">"
            + "<Widget Id=\"left\" Kind=\"" + TestWidgetKind + "\" Width=\"176\" Height=\"10\" />"
            + "<Widget Id=\"middle\" Kind=\"" + TestWidgetKind + "\" Height=\"10\" />"
            + "<Widget Id=\"right\" Kind=\"" + TestWidgetKind + "\" Width=\"200\" Height=\"10\" />"
            + "</Row></UiPage>";

        UiLayoutSnapshot snapshot = Arrange(xml, 800f, 100f).Snapshot;
        Rect right = snapshot.RectById["right"];
        Check(Near(snapshot.RectById["middle"].width, 400f), "Auto width subtracts fixed siblings and both gaps");
        Check(Near(right.xMax, 800f), "Right sibling ends exactly at the Row boundary");
    }

    // --- US->FL round 3: N1 (Auto width) and N2 (Breakpoint) lanes -------------------------------
    // StubTextWidth: Small em=16 → a Latin letter is 8px, a CJK ideograph 16px. Asserting exact
    // numbers against that model is the positive control: a column that tracked a constant instead
    // of the glyph advance could not pass both the CJK and the Latin case.

    private const string TestLabelKind = "test/label";

    private static void RegisterLabeledTestWidget()
    {
        RegisterTestWidget(10f, null, false);
        UiWidgetRegistry.Register(
            Scope,
            TestLabelKind,
            () => new RecordingWidget(10f, null, false),
            new[] { "Height", "Label" },
            new[] { "Label" });
    }

    private static void VerifyAutoWidthTracksGlyphModel()
    {
        RegisterLabeledTestWidget();

        string Row(string label) =>
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Row Id=\"row\">"
            + "<Widget Id=\"cap\" Kind=\"" + TestLabelKind + "\" Width=\"Auto\" Label=\"" + label + "\" Height=\"10\" />"
            + "<Widget Id=\"rest\" Kind=\"" + TestWidgetKind + "\" Height=\"10\" />"
            + "</Row></UiPage>";

        UiLayoutSnapshot cjk = Arrange(Row("音量"), 400f, 600f).Snapshot;
        Check(Near(cjk.RectById["cap"].width, 32f), "CJK two-ideograph label column = 32px (2×16)");
        Check(Near(cjk.RectById["rest"].width, 368f), "unsized sibling takes the remainder");

        UiLayoutSnapshot latin = Arrange(Row("ab"), 400f, 600f).Snapshot;
        Check(Near(latin.RectById["cap"].width, 16f), "same character count, Latin advance = 16px — the column tracks glyphs, not a constant");
    }

    private static void VerifyAutoMinWidthMaxWidthClamp()
    {
        RegisterLabeledTestWidget();

        string xml =
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Row Id=\"row\">"
            + "<Widget Id=\"floor\" Kind=\"" + TestLabelKind + "\" Width=\"Auto\" Label=\"ab\" MinWidth=\"40\" Height=\"10\" />"
            + "<Widget Id=\"ceil\" Kind=\"" + TestLabelKind + "\" Width=\"Auto\" Label=\"音量\" MaxWidth=\"10\" Height=\"10\" />"
            + "<Widget Id=\"rest\" Kind=\"" + TestWidgetKind + "\" Height=\"10\" />"
            + "</Row></UiPage>";

        UiLayoutSnapshot snapshot = Arrange(xml, 400f, 600f).Snapshot;
        Check(Near(snapshot.RectById["floor"].width, 40f), "MinWidth raises the 16px natural width to 40");
        Check(Near(snapshot.RectById["ceil"].width, 10f), "MaxWidth caps the 32px natural width to 10");
        Check(Near(snapshot.RectById["rest"].width, 350f), "flex sibling shares what the clamped autos leave");
    }

    private static void VerifyAutoCappedByBudget()
    {
        RegisterLabeledTestWidget();

        string xml =
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Row Id=\"row\">"
            + "<Widget Id=\"fixed\" Kind=\"" + TestWidgetKind + "\" Width=\"90\" Height=\"10\" />"
            + "<Widget Id=\"cap\" Kind=\"" + TestLabelKind + "\" Width=\"Auto\" Label=\"音量音量\" Height=\"10\" />"
            + "</Row></UiPage>";

        UiLayoutSnapshot snapshot = Arrange(xml, 100f, 600f).Snapshot;
        Check(Near(snapshot.RectById["fixed"].width, 90f), "fixed siblings are never squeezed");
        Check(Near(snapshot.RectById["cap"].width, 10f), "the Auto column is capped by the budget the fixed sibling leaves, not by its 64px natural width");
    }

    private static void VerifyStackAutoChildHugsLabel()
    {
        RegisterLabeledTestWidget();

        string xml =
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Column Id=\"column\">"
            + "<Widget Id=\"cap\" Kind=\"" + TestLabelKind + "\" Width=\"Auto\" Label=\"ab\" Height=\"10\" />"
            + "<Widget Id=\"full\" Kind=\"" + TestWidgetKind + "\" Height=\"10\" />"
            + "</Column></UiPage>";

        UiLayoutSnapshot snapshot = Arrange(xml, 400f, 600f).Snapshot;
        Check(Near(snapshot.RectById["cap"].width, 16f), "stack child with Width=Auto hugs its label");
        Check(Near(snapshot.RectById["full"].width, 400f), "unsized stack children keep the full width");
    }

    private static void VerifyCoreKindAutoMeasurable()
    {
        RegisterTestWidget(10f, null, false);

        string xml =
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Row Id=\"row\">"
            + "<Widget Id=\"slider\" Kind=\"input/stepper-slider\" Width=\"Auto\" Label=\"音量\" Height=\"28\" />"
            + "<Widget Id=\"rest\" Kind=\"" + TestWidgetKind + "\" Height=\"10\" />"
            + "</Row></UiPage>";

        UiLayoutSnapshot snapshot = Arrange(xml, 400f, 600f).Snapshot;
        Check(Near(snapshot.RectById["slider"].width, 32f),
            "the core stepper-slider's declared label set feeds the same Auto seam (N3's reshape is N1 proving itself)");
    }

    private static void VerifyBreakpointDirectionFlip()
    {
        RegisterTestWidget(10f, null, false);

        string xml =
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Row Id=\"row\" Breakpoint=\"200\" Narrow=\"Column\" Gap=\"4\">"
            + "<Widget Id=\"a\" Kind=\"" + TestWidgetKind + "\" Height=\"10\" />"
            + "<Widget Id=\"b\" Kind=\"" + TestWidgetKind + "\" Height=\"20\" />"
            + "</Row></UiPage>";

        // One engine, one session: re-arranged at two widths. This is the acceptance shape the
        // rebuild contract demands — a width change re-lays out, nothing is reopened.
        UiLayoutEngine engine = new(Scope);
        UiWidgetContext ctx = CreateContext();
        UiLayoutManifest manifest = Parse(xml);

        UiLayoutSnapshot wide = engine.ArrangeRoots(ctx, new Vector2(400f, 600f), manifest.Roots);
        Check(Near(wide.RectById["a"].y, wide.RectById["b"].y), "wide: Row lays its children side by side");
        Check(wide.RectById["b"].x > wide.RectById["a"].x, "wide: siblings advance in x");

        UiLayoutSnapshot narrow = engine.ArrangeRoots(ctx, new Vector2(150f, 600f), manifest.Roots);
        Check(narrow.RectById["b"].y > narrow.RectById["a"].y, "narrow: the same engine re-arranges to one-per-line");
        Check(Near(narrow.RectById["a"].x, narrow.RectById["b"].x), "narrow: siblings share the column's x");

        UiLayoutSnapshot restored = engine.ArrangeRoots(ctx, new Vector2(400f, 600f), manifest.Roots);
        Check(Near(restored.RectById["b"].y, restored.RectById["a"].y), "wide again: the flip is stateless, no residue from the narrow pass");
    }

    private static void VerifyNarrowHidden()
    {
        RegisterTestWidget(10f, null, false);

        string xml =
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Row Id=\"row\" Breakpoint=\"200\">"
            + "<Widget Id=\"a\" Kind=\"" + TestWidgetKind + "\" Height=\"10\" />"
            + "<Widget Id=\"b\" Kind=\"" + TestWidgetKind + "\" Height=\"10\" NarrowHidden=\"true\" />"
            + "</Row></UiPage>";

        UiLayoutSnapshot wide = Arrange(xml, 400f, 600f).Snapshot;
        Check(wide.RectById.ContainsKey("b"), "wide: the NarrowHidden child is present");

        UiLayoutSnapshot narrow = Arrange(xml, 150f, 600f).Snapshot;
        Check(!narrow.RectById.ContainsKey("b"), "narrow: it is gone from the snapshot");
        Check(narrow.RectById.ContainsKey("a"), "and only it — the sibling is untouched");
    }

    private static void VerifyWrapColsResponsive()
    {
        RegisterTestWidget(10f, null, false);

        string xml =
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Wrap Id=\"wrap\" Breakpoint=\"200\" Cols=\"2\" NarrowCols=\"1\">"
            + "<Widget Id=\"a\" Kind=\"" + TestWidgetKind + "\" Height=\"10\" />"
            + "<Widget Id=\"b\" Kind=\"" + TestWidgetKind + "\" Height=\"10\" />"
            + "<Widget Id=\"c\" Kind=\"" + TestWidgetKind + "\" Height=\"10\" />"
            + "<Widget Id=\"d\" Kind=\"" + TestWidgetKind + "\" Height=\"10\" />"
            + "</Wrap></UiPage>";

        UiLayoutSnapshot wide = Arrange(xml, 400f, 600f).Snapshot;
        Check(Near(wide.RectById["b"].x, 200f) && Near(wide.RectById["b"].y, 0f), "wide: Cols=2 puts the second child in the second column");
        Check(Near(wide.RectById["c"].x, 0f) && Near(wide.RectById["c"].y, 10f), "wide: the third child wraps to the second row");

        UiLayoutSnapshot narrow = Arrange(xml, 150f, 600f).Snapshot;
        Check(Near(narrow.RectById["b"].x, 0f) && Near(narrow.RectById["b"].y, 10f), "narrow: NarrowCols=1 forces one column — the 响应式列数 acceptance row");
        Check(Near(narrow.RectById["d"].y, 30f), "narrow: all four children stack");
    }

    private static void VerifyResponsiveValidation()
    {
        RegisterTestWidget(10f, null, false);
        UiWidgetRegistry.Register(
            Scope, "test/sized", () => new RecordingWidget(10f, null, false), new[] { "Height" });

        // The latent N1 specimen: a schematized kind could not carry Width at all before round 3.
        Host("<Row Id=\"r\"><Widget Id=\"a\" Kind=\"test/sized\" Width=\"Auto\" Height=\"10\" /></Row>");
        Check(true, "Width=Auto passes creation on a kind whose schema never listed it");

        Reject("<Row Id=\"r\"><Widget Id=\"a\" Kind=\"test/sized\" Width=\"wide\" Height=\"10\" /></Row>", "invalid Width grammar");
        Reject("<Widget Id=\"a\" Kind=\"test/sized\" Cols=\"2\" Height=\"10\" />", "Cols is container vocabulary, rejected on a widget");
        Reject("<Wrap Id=\"w\" Breakpoint=\"200\" Narrow=\"Column\"><Widget Id=\"a\" Kind=\"test/sized\" /></Wrap>", "Narrow=Column rejected on a Wrap");
        Reject("<Wrap Id=\"w\" NarrowCols=\"1\"><Widget Id=\"a\" Kind=\"test/sized\" /></Wrap>", "NarrowCols without Cols rejected");
        Reject("<Row Id=\"r\"><Widget Id=\"a\" Kind=\"test/sized\" Width=\"Auto\" MinWidth=\"50\" MaxWidth=\"20\" Height=\"10\" /></Row>", "MinWidth above MaxWidth rejected");
        Reject("<Row Id=\"r\" Breakpoint=\"soon\"><Widget Id=\"a\" Kind=\"test/sized\" /></Row>", "non-numeric Breakpoint rejected");
        Reject("<Wrap Id=\"w\" Cols=\"2\" NarrowCols=\"1\"><Widget Id=\"a\" Kind=\"test/sized\" /></Wrap>", "NarrowCols without Breakpoint rejected (the narrow state could never arrive)");
        Reject("<Row Id=\"r\"><Widget Id=\"a\" Kind=\"test/sized\" NarrowHidden=\"true\" /></Row>", "NarrowHidden under a parent without Breakpoint rejected");
        Reject("<Column Id=\"c\" Narrow=\"Row\"><Widget Id=\"a\" Kind=\"test/sized\" /></Column>", "Narrow without Breakpoint rejected");
    }

    private static void Host(string body)
    {
        using UiHost host = new(
            Scope,
            UiLayoutManifest.Parse("<UiPage Schema=\"2\" Source=\"" + Scope + "\">" + body + "</UiPage>"),
            new UiBindings(),
            UiTheme.DarkGold,
            new StubMetrics(),
            new StubTranslation());
    }

    private static void Reject(string body, string what)
    {
        try
        {
            Host(body);
            Check(false, what + " — but the host accepted it");
        }
        catch (UiContractException)
        {
            Check(true, what + " — rejected at creation");
        }
    }

    private static void VerifyTabVisibility()
    {
        RegisterTestWidget(10f, null, false);
        string xml =
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Column Id=\"column\" Gap=\"4\">"
            + "<Widget Id=\"basic\" Kind=\"" + TestWidgetKind + "\" Height=\"10\" Tab=\"Basic\" />"
            + "<Widget Id=\"packs\" Kind=\"" + TestWidgetKind + "\" Height=\"20\" Tab=\"Packs\" />"
            + "</Column></UiPage>";
        UiLayoutEngine engine = new(Scope);
        UiSession session = new();
        string activeTab = "Packs";
        var bindings = new UiBindings();
        bindings.BindValue("active-tab", () => activeTab, value => activeTab = value);
        UiWidgetContext ctx = new(Scope, session, new StubMetrics(), UiTheme.DarkGold, new StubTranslation(), bindings, 200f, "root");

        UiLayoutSnapshot snapshot = engine.ArrangeRoots(ctx, new Vector2(200f, 100f), Parse(xml).Roots);
        Check(!snapshot.RectById.ContainsKey("basic"), "Inactive Tab element is absent from layout");
        Check(snapshot.RectById.ContainsKey("packs"), "Active Tab element remains visible");
    }

    private static void VerifyClipCleanupOnException()
    {
        RegisterTestWidget(10f, null, true);

        string xml =
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Clip Id=\"clip\" Height=\"50\">"
            + "<Widget Id=\"boom\" Kind=\"" + TestWidgetKind + "\" Height=\"20\" />"
            + "</Clip>"
            + "</UiPage>";

        UiLayoutEngine engine = new(Scope);
        UiWidgetContext ctx = CreateContext();
        UiLayoutSnapshot snapshot = engine.ArrangeRoots(ctx, new Vector2(200f, 600f), Parse(xml).Roots);

        ResetGroupCounters();
        bool threw = false;
        try
        {
            engine.Draw(ctx, snapshot, new Rect(0f, 0f, 200f, 200f));
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        // Item C moved this contract: a child draw failure is now recovered by the tree, so the clip
        // group must NOT see an exception at all. The invariant this test exists for — Begin/EndGroup
        // balanced and depth restored — is what it asserts, whichever way the failure travels.
        Check(!threw, "Clip child exception is recovered by the engine, not leaked through the group");
        Check(ReadStaticInt(typeof(GUI), "GroupDepth") == 0, "Clip group depth restored after the child failure");
        Check(ReadStaticInt(typeof(GUI), "BeginGroupCalls") == ReadStaticInt(typeof(GUI), "EndGroupCalls"),
            "Clip Begin/EndGroup balanced after the child failure");
        Check(ctx.Session.TrippedNodes.Count > 0, "and the recovery is recorded in the session");
    }

    private static string ScrollXml()
    {
        return "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Scroll Id=\"scroll\" Height=\"50\">"
            + "<Widget Id=\"a\" Kind=\"" + TestWidgetKind + "\" Height=\"30\" />"
            + "<Widget Id=\"b\" Kind=\"" + TestWidgetKind + "\" Height=\"30\" />"
            + "</Scroll>"
            + "</UiPage>";
    }

    private static void RegisterTestWidget(float height, List<Rect>? drawn, bool throwOnDraw)
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        UiWidgetRegistry.Register(Scope, TestWidgetKind, () => new RecordingWidget(height, drawn, throwOnDraw));
    }

    private static UiLayoutManifest Parse(string xml)
    {
        return UiLayoutManifest.Parse(xml);
    }

    private static (UiLayoutEngine Engine, UiWidgetContext Context, UiLayoutSnapshot Snapshot) Arrange(
        string xml,
        float width,
        float height)
    {
        UiLayoutEngine engine = new(Scope);
        UiWidgetContext ctx = CreateContext();
        UiLayoutSnapshot snapshot = engine.ArrangeRoots(ctx, new Vector2(width, height), Parse(xml).Roots);
        return (engine, ctx, snapshot);
    }

    private static UiWidgetContext CreateContext()
    {
        return new UiWidgetContext(
            Scope,
            new UiSession(),
            new StubMetrics(),
            UiTheme.DarkGold,
            new StubTranslation(),
            new UiBindings(),
            400f,
            "root");
    }

    private static void ResetGroupCounters()
    {
        WriteStaticInt(typeof(GUI), "GroupDepth", 0);
        WriteStaticInt(typeof(GUI), "BeginGroupCalls", 0);
        WriteStaticInt(typeof(GUI), "EndGroupCalls", 0);
    }

    private static void ResetScrollCounters()
    {
        WriteStaticInt(typeof(Verse.Widgets), "ScrollViewDepth", 0);
        WriteStaticInt(typeof(Verse.Widgets), "BeginScrollViewCalls", 0);
        WriteStaticInt(typeof(Verse.Widgets), "EndScrollViewCalls", 0);
    }

    private static int ReadStaticInt(Type type, string fieldName)
    {
        FieldInfo field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("Missing stub field " + type.FullName + "." + fieldName);
        return (int)field.GetValue(null)!;
    }

    private static void WriteStaticInt(Type type, string fieldName, int value)
    {
        FieldInfo field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("Missing stub field " + type.FullName + "." + fieldName);
        field.SetValue(null, value);
    }

    private static bool Near(float a, float b)
    {
        return Math.Abs(a - b) < 0.01f;
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

    private sealed class RecordingWidget : IUiWidget
    {
        private readonly float height;
        private readonly List<Rect>? drawn;
        private readonly bool throwOnDraw;

        public RecordingWidget(float height, List<Rect>? drawn, bool throwOnDraw)
        {
            this.height = height;
            this.drawn = drawn;
            this.throwOnDraw = throwOnDraw;
        }

        public string Kind => TestWidgetKind;

        public void Configure(UiElementSpec spec)
        {
        }

        public void Validate(IUiBindings bindings, string elementPath)
        {
        }

        public float Measure(UiWidgetContext ctx)
        {
            return height;
        }

        public void Draw(Rect rect, UiWidgetContext ctx)
        {
            if (throwOnDraw) throw new InvalidOperationException("boom");
            drawn?.Add(rect);
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
