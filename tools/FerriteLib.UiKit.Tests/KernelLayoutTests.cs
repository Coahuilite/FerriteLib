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
        ctx.Session.SetScrollPosition("scroll", new Vector2(0f, 3f));
        engine.Draw(ctx, snapshot, new Rect(0f, 0f, 200f, 200f));

        Vector2 stored = ctx.Session.GetScrollPosition("scroll");
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
        ctx.Session.SetScrollPosition("scroll", new Vector2(0f, 1000f));

        UiLayoutSnapshot snapshot = engine.ArrangeRoots(ctx, new Vector2(200f, 600f), Parse(xml).Roots);
        Vector2 pos = ctx.Session.GetScrollPosition("scroll");
        Check(Near(pos.y, 10f), "Scroll position clamps to contentHeight - viewportHeight (60-50)");

        ctx.Session.SetScrollPosition("scroll", new Vector2(0f, -5f));
        snapshot = engine.ArrangeRoots(ctx, new Vector2(210f, 600f), Parse(xml).Roots);
        pos = ctx.Session.GetScrollPosition("scroll");
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

        Check(threw, "Clip child exception propagates");
        Check(ReadStaticInt(typeof(GUI), "GroupDepth") == 0, "Clip group depth restored after exception");
        Check(ReadStaticInt(typeof(GUI), "BeginGroupCalls") == ReadStaticInt(typeof(GUI), "EndGroupCalls"),
            "Clip Begin/EndGroup balanced after exception");
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
