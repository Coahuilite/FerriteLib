using System;
using System.Collections.Generic;
using FerriteLib.UiKit.Kernel;
using FerriteLib.UiKit.Kernel.Widgets;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// Greenfield core widget smoke tests: registration, typed bindings, session popup and responsive
/// mode-row. These are stub-harness evidence only; they do not claim in-game UI verification.
/// </summary>
internal static class KernelCoreWidgetTests
{
    public static int RunAll()
    {
        int failures = 0;
        failures += Run("Kernel core widgets measure/draw", VerifyCoreWidgetsMeasureDraw);
        failures += Run("Kernel dropdown opens session popup", VerifyDropdownOpensPopup);
        failures += Run("Banner/empty-state bands are the text atom's band", VerifyCompositeBandsComeFromTheAtom);
        failures += Run("Composites draw text/font/rect through the one outlet", VerifyCompositeOutletTriple);
        failures += Run("Composite kinds keep their vocabulary and their manifests", VerifyCompositeVocabularyUnchanged);
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

    private static void VerifyCoreWidgetsMeasureDraw()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();

        string xml =
            "<UiPage Schema=\"2\" Source=\"test\">"
            + "<Stack Id=\"root\" Gap=\"8\" Padding=\"8\">"
            + "<Widget Id=\"banner\" Kind=\"chrome/banner\" Bind=\"BannerText\" />"
            + "<Widget Id=\"mode\" Kind=\"input/mode-row\""
            + " Title1=\"A\" Value1=\"A\" Title2=\"B\" Value2=\"B\" />"
            + "<Widget Id=\"dropdown\" Kind=\"input/dropdown\" OptionsBind=\"Options\" />"
            + "<Widget Id=\"volume\" Kind=\"input/stepper-slider\" Min=\"0\" Max=\"1\" />"
            + "<Widget Id=\"chart\" Kind=\"chart/line\" Points=\"0,0;1,1\" />"
            + "</Stack>"
            + "</UiPage>";

        UiLayoutManifest manifest = UiLayoutManifest.Parse(xml);
        var bindings = new UiBindings();
        string banner = "hello";
        string mode = "A";
        string dropdown = "x";
        float volume = 0.5f;
        bindings.BindReadOnly("BannerText", () => banner);
        bindings.BindValue("mode", () => mode, v => mode = v);
        bindings.BindValue("dropdown", () => dropdown, v => dropdown = v);
        bindings.BindOptions("Options", () => new List<string> { "x", "y" });
        bindings.BindValue("volume", () => volume, v => volume = v);
        // chart/line validates its typed points binding by Id (Bind falls back to Id).
        bindings.BindReadOnly<IReadOnlyList<Vector2>>(
            "chart", () => new List<Vector2> { new(0f, 0f), new(1f, 1f) });

        using UiHost host = new("test", manifest, bindings, UiTheme.DarkGold, new StubMetrics(), new StubTranslation());
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(500f, 600f));
        if (!snapshot.RectById.ContainsKey("banner")
            || !snapshot.RectById.ContainsKey("mode")
            || !snapshot.RectById.ContainsKey("dropdown")
            || !snapshot.RectById.ContainsKey("volume")
            || !snapshot.RectById.ContainsKey("chart"))
        {
            throw new Exception("Missing one or more widget rects");
        }

        host.BeginFrame();
        host.Draw(new Rect(0f, 0f, 500f, 600f), snapshot);
        host.EndFrame();
    }

    private static void VerifyDropdownOpensPopup()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();

        string xml =
            "<UiPage Schema=\"2\" Source=\"test\">"
            + "<Widget Id=\"dropdown\" Kind=\"input/dropdown\" OptionsBind=\"Options\" />"
            + "</UiPage>";

        UiLayoutManifest manifest = UiLayoutManifest.Parse(xml);
        var bindings = new UiBindings();
        string current = "x";
        bindings.BindValue("dropdown", () => current, v => current = v);
        bindings.BindOptions("Options", () => new List<string> { "x", "y" });

        using UiHost host = new("test", manifest, bindings, UiTheme.DarkGold, new StubMetrics(), new StubTranslation());
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(300f, 200f));
        Rect fieldRect = snapshot.RectById["dropdown"];

        try
        {
            UiNative.ButtonOverride = rect => Math.Abs(rect.x - fieldRect.x) < 0.01f
                && Math.Abs(rect.y - fieldRect.y) < 0.01f
                && Math.Abs(rect.width - fieldRect.width) < 0.01f
                && Math.Abs(rect.height - fieldRect.height) < 0.01f;

            host.BeginFrame();
            host.Draw(new Rect(0f, 0f, 300f, 200f), snapshot);
            host.EndFrame();

            if (!host.Session.IsPopupOpen("dropdown"))
            {
                throw new Exception("Dropdown did not open a session popup");
            }
        }
        finally
        {
            UiNative.ButtonOverride = null;
        }
    }

    // --- 0.4.x leaf vocabulary: the two composites rebuilt over the text atom --------------------
    // chrome/banner and state/empty used to carry their own copy of "wrap the string, reserve the
    // height". They are now compositions over text/wrapped: same kind, same schema, same label set,
    // same painted band, one arithmetic. These lanes pin the pre-refactor numbers with an oracle
    // written out here (not a call into the new code) and pin what reaches the single text outlet.

    private const string LongCjk = "这是一段用于验证换行高度的文本内容";

    private static void VerifyCompositeBandsComeFromTheAtom()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        using UiSession session = new();
        UiTheme theme = UiTheme.DarkGold;

        var cases = new (string Text, float Width)[]
        {
            ("", 120f),
            ("ok", 120f),
            (LongCjk, 120f),
            (LongCjk, 200f),
            (LongCjk, 400f),
            ("abcdefghijklmnopqrstuvwxyz0123456789", 120f)
        };

        foreach ((string text, float width) in cases)
        {
            UiWidgetContext ctx = Context(session, theme, width);

            ChromeBannerWidget banner = new();
            banner.Configure(new UiElementSpec("banner", ChromeBannerWidget.Kind, Attributes(("Text", text))));
            CheckClose(Oracle(text, UiFont.Tiny, width, 6f, 22f), banner.Measure(ctx),
                "chrome/banner keeps its pre-refactor band at " + width + "px for " + Describe(text));

            EmptyStateWidget empty = new();
            empty.Configure(new UiElementSpec("empty", EmptyStateWidget.Kind, Attributes(("Text", text))));
            CheckClose(Oracle(text, UiFont.Small, width, 12f, 48f), empty.Measure(ctx),
                "state/empty keeps its pre-refactor band at " + width + "px for " + Describe(text));

            // The number both composites got is the atom's one function, at their own font and lead.
            CheckClose(SharedBandOracle(text, UiFont.Tiny, width, 6f),
                WrappedTextWidget.MeasureBand(ctx, text, UiFont.Tiny, 6f),
                "the banner's band is the atom's band contract at Tiny");
            CheckClose(SharedBandOracle(text, UiFont.Small, width, 12f),
                WrappedTextWidget.MeasureBand(ctx, text, UiFont.Small, 12f),
                "the empty state's band is the same function at Small");
        }

        // A refactor that quietly standardised both composites onto the atom's default font would make
        // these two bands equal; keeping them distinct is what proves each still measures its own font.
        UiWidgetContext narrow = Context(session, theme, 120f);
        ChromeBannerWidget tinyBanner = new();
        tinyBanner.Configure(new UiElementSpec("b", ChromeBannerWidget.Kind, Attributes(("Text", LongCjk))));
        EmptyStateWidget smallState = new();
        smallState.Configure(new UiElementSpec("e", EmptyStateWidget.Kind, Attributes(("Text", LongCjk))));
        Check(Near(30f, tinyBanner.Measure(narrow)), "the banner's band is the Tiny row (2 lines x 12 + 6)");
        Check(Near(60f, smallState.Measure(narrow)), "the empty state's band is the Small row (3 lines x 16 + 12)");
        Check(!Near(tinyBanner.Measure(narrow), smallState.Measure(narrow)),
            "the two composites still measure with their own fonts, not the atom's default");

        // The atom itself is unchanged by the refactor: same string, same width, same model.
        WrappedTextWidget atom = new();
        atom.Configure(new UiElementSpec("note", WrappedTextWidget.Kind, Attributes(("Text", LongCjk))));
        CheckClose(60f, atom.Measure(Context(session, theme, 120f)),
            "the atom's own band is still its wrapped height plus its vertical padding");
    }

    /// <summary>
    /// The drawing half. <see cref="UiThemeDraw.Label"/> is the library's only text outlet, so the
    /// (rect, text, font) triple reaching it is the drawing result that matters; the fit audit is the
    /// harness seam that exposes that triple. A band squeezed below the wrapped need makes the audit
    /// speak, and the reserved band makes it stay silent — the second half is the positive control
    /// that Measure and Draw still agree after the composites were rebuilt.
    /// </summary>
    private static void VerifyCompositeOutletTriple()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        using UiSession session = new();
        UiTheme theme = UiTheme.DarkGold;
        var reports = new List<UiOverflowReport>();

        UiFitAudit.Attach(new WrappingMetrics(), report => reports.Add(report));
        try
        {
            foreach ((Type kind, string text, UiFont font) in new (Type, string, UiFont)[]
            {
                (typeof(ChromeBannerWidget), LongCjk, UiFont.Tiny),
                (typeof(EmptyStateWidget), LongCjk, UiFont.Small)
            })
            {
                reports.Clear();
                UiFitAudit.Reset();
                UiFitAudit.Enabled = true;

                IUiWidget widget = kind == typeof(ChromeBannerWidget)
                    ? new ChromeBannerWidget()
                    : new EmptyStateWidget();
                widget.Configure(new UiElementSpec("probe", KindOf(kind), Attributes(("Text", text))));

                // 1) the squeezed band: the audit reports the exact string, font and width the outlet got.
                widget.Draw(new Rect(0f, 0f, 200f, 4f), Context(session, theme, 200f));
                Check(reports.Count == 1, KindOf(kind) + " reports its one squeezed-band finding");
                if (reports.Count == 1)
                {
                    Check(reports[0].Text == text, KindOf(kind) + " draws the resolved string it measured");
                    Check(reports[0].Font == font, KindOf(kind) + " draws in its own font (" + font + ")");
                    Check(reports[0].Axis == UiOverflowAxis.Height, KindOf(kind) + " overflows on the height axis");
                    CheckClose(200f, reports[0].RectWidth, KindOf(kind) + " draws into the rect it was handed");
                }

                // 2) the reserved band: Measure's answer is exactly what Draw needs, so nothing reports.
                reports.Clear();
                UiFitAudit.Reset();
                float band = widget.Measure(Context(session, theme, 200f));
                widget.Draw(new Rect(0f, 0f, 200f, band), Context(session, theme, 200f));
                Check(reports.Count == 0,
                    KindOf(kind) + " drawing into its own measured band overflows nothing (measure meets draw)");
            }
        }
        finally
        {
            UiFitAudit.Detach();
            UiFitAudit.Reset();
        }
    }

    /// <summary>
    /// The compatibility half of "keep the names": the kind strings, the allowed-attribute sets and the
    /// declared label sets are the frozen vocabulary, and a manifest that used every declared attribute
    /// must still create, arrange and draw.
    /// </summary>
    private static void VerifyCompositeVocabularyUnchanged()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();

        CheckSchema(ChromeBannerWidget.Kind, new[] { "Id", "Kind", "Bind", "Text", "TextKey", "Height", "Tab", "Hidden" });
        CheckSchema(EmptyStateWidget.Kind, new[] { "Id", "Kind", "Text", "TextKey", "Height", "Tab", "Hidden" });
        CheckLabels(ChromeBannerWidget.Kind);
        CheckLabels(EmptyStateWidget.Kind);

        string xml =
            "<UiPage Schema=\"2\" Source=\"composite-test\">"
            + "<Stack Id=\"root\">"
            + "<Widget Id=\"banner\" Kind=\"chrome/banner\" Bind=\"BannerText\" Height=\"Auto\" />"
            + "<Widget Id=\"bannerKeyed\" Kind=\"chrome/banner\" TextKey=\"banner.key\" />"
            + "<Widget Id=\"empty\" Kind=\"state/empty\" Text=\"nothing here\" />"
            + "</Stack>"
            + "</UiPage>";

        string bannerText = LongCjk;
        var bindings = new UiBindings();
        bindings.BindReadOnly("BannerText", () => bannerText);

        using UiHost host = new("composite-test", UiLayoutManifest.Parse(xml), bindings, UiTheme.DarkGold, new WrappingMetrics(), new StubTranslation());
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(300f, 400f));

        foreach (string id in new[] { "banner", "bannerKeyed", "empty" })
        {
            Check(snapshot.RectById.ContainsKey(id), "an existing manifest still arranges '" + id + "'");
        }

        CheckClose(Oracle(bannerText, UiFont.Tiny, 300f, 6f, 22f), snapshot.RectById["banner"].height,
            "the manifest-arranged banner band is the atom band at the banner's own font");
        CheckClose(Oracle("[banner.key]", UiFont.Tiny, 300f, 6f, 22f), snapshot.RectById["bannerKeyed"].height,
            "the keyed banner resolves through the same translation path before the band is measured");

        host.DrawFrame(new Rect(0f, 0f, 300f, 400f));
        Check(host.Session.TrippedComponentIds.Count == 0, "both composites draw their whole path without a trip");
    }

    private static void CheckSchema(string kind, string[] expected)
    {
        IReadOnlyCollection<string>? schema = UiWidgetRegistry.GetAttributeSchema(UiWidgetRegistry.CoreScope, kind);
        if (schema == null)
        {
            Check(false, kind + " still declares an attribute schema");
            return;
        }

        Check(schema.Count == expected.Length, kind + " schema has " + expected.Length + " names, as before");
        foreach (string name in expected)
        {
            Check(Contains(schema, name), kind + " schema still allows '" + name + "'");
        }
    }

    private static void CheckLabels(string kind)
    {
        IReadOnlyCollection<string>? labels = UiWidgetRegistry.GetLabelAttributes(UiWidgetRegistry.CoreScope, kind);
        if (labels == null)
        {
            Check(false, kind + " still declares a label set");
            return;
        }

        Check(labels.Count == 2 && Contains(labels, "Text") && Contains(labels, "TextKey"),
            kind + " still declares exactly Text/TextKey for Width=Auto");
    }

    private static bool Contains(IReadOnlyCollection<string> set, string name)
    {
        foreach (string candidate in set)
        {
            if (string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    private static string KindOf(Type type)
    {
        return type == typeof(ChromeBannerWidget) ? ChromeBannerWidget.Kind : EmptyStateWidget.Kind;
    }

    private static string Describe(string text)
    {
        if (text.Length == 0) return "an empty string";
        return "'" + (text.Length <= 12 ? text : text.Substring(0, 12) + "...") + "'";
    }

    private static Dictionary<string, string> Attributes(params (string Name, string Value)[] pairs)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach ((string name, string value) in pairs)
        {
            attributes[name] = value;
        }

        return attributes;
    }

    private static UiWidgetContext Context(UiSession session, UiTheme theme, float viewWidth)
    {
        return new UiWidgetContext(
            "composite-test", session, new WrappingMetrics(), theme, new StubTranslation(),
            new UiBindings(), viewWidth, "root");
    }

    /// <summary>
    /// The band as <c>chrome/banner</c> and <c>state/empty</c> computed it before this change, written
    /// out here independently of the production types. An implementation that only agrees with another
    /// implementation proves nothing, so the oracle is the old arithmetic over the shared glyph model,
    /// not a call into the new seam.
    /// </summary>
    private static float Oracle(string text, UiFont font, float width, float verticalLead, float floor)
    {
        if (text.Length == 0) return floor;
        return Math.Max(floor, WrappedLines(text, font, width) * Em(font) + verticalLead);
    }

    /// <summary>The same inputs as the atom's shared band contract; empty text is 0 there, floors live in callers.</summary>
    private static float SharedBandOracle(string text, UiFont font, float width, float verticalLead)
    {
        if (text.Length == 0) return 0f;
        return Math.Max(1f, WrappedLines(text, font, width) * Em(font)) + verticalLead;
    }

    private static int WrappedLines(string text, UiFont font, float width)
    {
        float advance = StubTextWidth.Of(text, font);
        return Math.Max(1, (int)Math.Ceiling(advance / Math.Max(1f, width)));
    }

    private static float Em(UiFont font)
    {
        return font switch
        {
            UiFont.Tiny => 12f,
            UiFont.Medium => 18f,
            _ => 16f
        };
    }

    private static bool Near(float expected, float actual) => Math.Abs(expected - actual) <= 0.0001f;

    private static void Check(bool condition, string name)
    {
        if (condition)
        {
            Console.WriteLine("  ok: " + name);
        }
        else
        {
            Console.Error.WriteLine("  FAIL: " + name);
            throw new Exception(name);
        }
    }

    private static void CheckClose(float expected, float actual, string name)
    {
        if (!Near(expected, actual))
        {
            Console.Error.WriteLine("  FAIL: " + name + " (expected '" + expected + "', got '" + actual + "')");
            throw new Exception(name + " (expected '" + expected + "', got '" + actual + "')");
        }

        Console.WriteLine("  ok: " + name);
    }

    /// <summary>
    /// The lane's ruler: width from the shared half-width glyph model, height from the wrapped line
    /// count times that model's em. A constant-per-font stub could not tell 2 wrapped lines from 3,
    /// which is the whole distinction these bands are about.
    /// </summary>
    private sealed class WrappingMetrics : ITextMetrics
    {
        public float MeasureText(string text, UiFont font, float width)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            return WrappedLines(text, font, width) * Em(font);
        }

        public float MeasureWidth(string text, UiFont font) => StubTextWidth.Of(text, font);
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
