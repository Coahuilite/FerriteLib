using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using FerriteLib.UiKit.Kernel;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// Style-scope lane (0.4.x batch B, phase 2). Phase 1 landed the document and the resolver; this lane
/// holds the half that makes them part of a live page: an element's <c>Scheme</c>/<c>Density</c>
/// attributes build a nearest-first chain the engine carries down the tree, a region's subtree measures
/// and draws with that region's own theme instance (the same instance on every later frame), the
/// document's page level lands on the injected theme before the first arrangement so the first frame
/// already follows it, and every dropped style value reaches the fit audit's appearance channel while
/// the page keeps rendering. Colours are read back from the engine's own painting calls, so a document
/// that parses but never reaches a pixel cannot pass.
/// </summary>
internal static class KernelStyleScopeTests
{
    private const string Scope = "style-scope";
    private const string ProbeKind = "style/probe";

    /// <summary>17 ideographs: wide enough that a font change is a different measured band, not a rounding.</summary>
    private const string LongCjk = "这是一段用于验证换行高度的文本内容";

    private static readonly Color IcePanel = new(0f, 0f, 1f, 1f);

    private static int failures;

    // What the probe element was handed, per half of a pass. Cleared per lane; the last matching entry is
    // the one the assertion reads.
    private static readonly List<string> MeasuredPaths = new();
    private static readonly List<UiTheme> MeasuredThemes = new();
    private static readonly List<string> DrawnPaths = new();
    private static readonly List<UiTheme> DrawnThemes = new();

    public static int RunAll()
    {
        failures = 0;
        Run("A region scopes its subtree to the region's own theme", VerifyRegionScope);
        Run("One scope keeps one theme instance across frames", VerifyScopeInstanceIsStable);
        Run("A region theme expires on the baseline layout clock", VerifyScopeFollowsLayoutClock);
        Run("The document's page level is in force before the first arrange", VerifyResolveBeforeMeasure);
        Run("Every dropped style value is visible in its own frame", VerifyDropsAreVisible);
        Run("A correct document is silent and an unknown scope name is not", VerifySilentWhenCorrect);
        Run("Scheme and Density are vocabulary on containers and widgets", VerifyScopeVocabulary);
        Run("Two style sources at once is reported, not silent", VerifyTwoSourcesAreVisible);
        Reset();
        return failures;
    }

    // --- a region scopes its subtree ---------------------------------------------------------------

    private static void VerifyRegionScope()
    {
        PrepareRegistry();
        ResetProbe();
        UiTheme theme = UiTheme.DarkGold;
        using UiHost host = new(Scope, RegionManifest(), new UiBindings(), theme, new StubMetrics(), new StubTranslation());

        ClearBoxes();
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(320f, 240f));
        host.DrawFrame(new Rect(0f, 0f, 320f, 240f));

        UiTheme? inside = LastTheme(MeasuredPaths, MeasuredThemes, "/inside");
        UiTheme? outside = LastTheme(MeasuredPaths, MeasuredThemes, "/outside");

        Check(inside != null && SameColor(inside.Panel, IcePanel),
            "the region's scheme reaches the element inside it");
        Check(inside != null && !ReferenceEquals(inside, theme),
            "and that theme is the region's own bag, not the injected instance");
        CheckClose(20f, snapshot.RectById["inside"].height,
            "the region's density reaches that element's measured band");
        Check(outside != null && ReferenceEquals(outside, theme),
            "an element outside the region keeps the injected theme itself (the page level is the injected bag)");
        CheckClose(UiGeometry.Default.RowHeight, snapshot.RectById["outside"].height,
            "and keeps the page's density");
        Check(AnyRecordedBoxColor(IcePanel),
            "the region paints its own chrome with that theme: the region's Panel fill is on the wire");
    }

    private static void VerifyScopeInstanceIsStable()
    {
        PrepareRegistry();
        UiTheme theme = UiTheme.DarkGold;
        using UiHost host = new(Scope, RegionManifest(), new UiBindings(), theme, new StubMetrics(), new StubTranslation());

        ResetProbe();
        host.DrawFrame(new Rect(0f, 0f, 320f, 240f));
        UiTheme? firstMeasure = LastTheme(MeasuredPaths, MeasuredThemes, "/inside");
        UiTheme? firstDraw = LastTheme(DrawnPaths, DrawnThemes, "/inside");

        // A different viewport height is a different available size, so the second frame really arranges
        // again - otherwise "the same instance" would be a claim about a cached snapshot nobody measured.
        ResetProbe();
        host.DrawFrame(new Rect(0f, 0f, 320f, 260f));
        UiTheme? secondMeasure = LastTheme(MeasuredPaths, MeasuredThemes, "/inside");
        UiTheme? secondDraw = LastTheme(DrawnPaths, DrawnThemes, "/inside");

        Check(MeasuredPaths.Count > 0 && DrawnPaths.Count > 0,
            "the probe element was actually measured and drawn on the second frame");
        Check(firstMeasure != null && ReferenceEquals(firstMeasure, secondMeasure),
            "one scope keeps one theme instance across frames instead of allocating per frame");
        Check(firstMeasure != null && ReferenceEquals(firstMeasure, firstDraw),
            "and Measure and Draw of one element are handed that same instance");
        Check(secondMeasure != null && ReferenceEquals(secondMeasure, secondDraw),
            "on the second frame too");
    }

    // --- the cache expires on the layout clock, and only on it -------------------------------------

    private static void VerifyScopeFollowsLayoutClock()
    {
        PrepareRegistry();
        UiTheme theme = UiTheme.DarkGold;
        using UiHost host = new(Scope, ClockManifest(), new UiBindings(), theme, new FontMetrics(), new StubTranslation());

        // Frame 1: the scope is built from the baseline as it stands.
        UiLayoutSnapshot first = Frame(host, 320f, 240f);
        UiTheme? firstScope = LastTheme(MeasuredPaths, MeasuredThemes, "/inside");
        float firstBand = first.RectById["note"].height;

        // The control: the layout clock has not moved, so a scope is still one instance and its atom still
        // measures the same band. The expiry rule must not turn reuse into a per-frame rebuild.
        UiLayoutSnapshot second = Frame(host, 320f, 260f);
        UiTheme? secondScope = LastTheme(MeasuredPaths, MeasuredThemes, "/inside");
        Check(firstScope != null && ReferenceEquals(firstScope, secondScope),
            "while the layout clock stands still one scope keeps one instance across frames");
        CheckClose(firstBand, second.RectById["note"].height,
            "and the atom inside it keeps the band it measured");

        // The clock moves: a layout-bearing token on the injected theme. Every band measured against the
        // old value is stale, so a cached scope that kept measuring with it would disagree with the page
        // around it - that is the defect this read exists to close.
        theme.DefaultFont = UiFont.Medium;

        UiLayoutSnapshot third = Frame(host, 320f, 280f);
        UiTheme? thirdScope = LastTheme(MeasuredPaths, MeasuredThemes, "/inside");
        Check(thirdScope != null && !ReferenceEquals(firstScope, thirdScope),
            "a baseline layout-clock move drops the cached scope instead of handing the stale instance back");
        Check(thirdScope != null && thirdScope.DefaultFont == UiFont.Medium,
            "the rebuilt scope carries the value the move introduced");
        Check(third.RectById["note"].height > firstBand + 0.01f,
            "and the real atom inside the region measures a taller band with it: "
            + third.RectById["note"].height + " vs " + firstBand);

        // The page level is the injected bag itself, and it moved in that same frame. A region lagging
        // behind it is exactly the inconsistency the clock read removes.
        UiTheme? pageLevel = LastTheme(MeasuredPaths, MeasuredThemes, "/outside");
        Check(pageLevel != null && ReferenceEquals(pageLevel, theme) && pageLevel.DefaultFont == UiFont.Medium,
            "the page level moved in that same frame, which the region used to lag behind");

        // The new epoch is stable the same way the old one was.
        UiLayoutSnapshot fourth = Frame(host, 320f, 300f);
        UiTheme? fourthScope = LastTheme(MeasuredPaths, MeasuredThemes, "/inside");
        Check(thirdScope != null && ReferenceEquals(thirdScope, fourthScope),
            "and the rebuilt scope is reused from then on");
        CheckClose(third.RectById["note"].height, fourth.RectById["note"].height,
            "with the band it was rebuilt under");
    }

    // --- the page level, before the first arrange --------------------------------------------------

    private static void VerifyResolveBeforeMeasure()
    {
        PrepareRegistry();

        // The standalone origin: a document handed to the host, with no <Styles> in the manifest at all.
        UiStyleDocument standalone = UiStyleDocument.Parse(
            "<Styles Schema=\"1\" Density=\"compact\">"
            + "<Density Name=\"compact\"><Metric Token=\"RowHeight\" Value=\"20\"/></Density>"
            + "</Styles>");
        UiTheme standaloneTheme = UiTheme.DarkGold;
        int standaloneRevision = standaloneTheme.LayoutRevision;
        using (UiHost host = new(Scope, ProbeManifest(""), new UiBindings(), standaloneTheme, new StubMetrics(), new StubTranslation(), standalone))
        {
            Check(standaloneTheme.LayoutRevision > standaloneRevision,
                "the page level moves the theme's own layout clock, before any frame - the clock the cache already reads");
            Check(ReferenceEquals(host.StyleResolver.Document, standalone),
                "the handed-in document is the one the host resolved");
            UiLayoutSnapshot first = host.MeasureAndArrange(new Vector2(300f, 200f));
            CheckClose(20f, first.RectById["probe"].height,
                "so the very first arrangement already measures in the document's density");
        }

        // The embedded origin: the same values through the manifest's own <Styles> section, no parameter.
        UiTheme embeddedTheme = UiTheme.DarkGold;
        int embeddedRevision = embeddedTheme.LayoutRevision;
        using (UiHost host = new(Scope, ProbeManifest(CompactDensitySection), new UiBindings(), embeddedTheme, new StubMetrics(), new StubTranslation()))
        {
            Check(embeddedTheme.LayoutRevision > embeddedRevision,
                "the manifest's own <Styles> section takes the same path into the same theme");
            UiLayoutSnapshot first = host.MeasureAndArrange(new Vector2(300f, 200f));
            CheckClose(20f, first.RectById["probe"].height,
                "and that first arrangement follows it too (one parser, one clock)");
        }
    }

    // --- fail-soft, and loud -----------------------------------------------------------------------

    private static void VerifyDropsAreVisible()
    {
        PrepareRegistry();
        var records = new List<UiStyleFallbackReport>();
        UiFitAudit.AttachStyleFallback(records.Add);
        UiFitAudit.Reset();
        try
        {
            // Five drops in one document: an unknown <Styles> attribute, an unreadable colour, an unknown
            // colour token, an unknown font size, and a page-level scheme nobody declared. Each is
            // appearance-class - none of them takes the page down - and each has to be seen.
            UiStyleDocument broken = UiStyleDocument.Parse(
                "<Styles Schema=\"1\" Scheme=\"missing\" Bogus=\"1\">"
                + "<Scheme Name=\"partial\">"
                + "<Color Token=\"Panel\" Value=\"#00ff00\"/>"
                + "<Color Token=\"Raised\" Value=\"not-a-colour\"/>"
                + "<Color Token=\"Nope\" Value=\"#ffffff\"/>"
                + "<Font Value=\"Huge\"/>"
                + "</Scheme>"
                + "</Styles>");
            Check(broken.Issues.Count == 4,
                "the parser recorded its own four drops (got " + broken.Issues.Count + ")");

            UiTheme theme = UiTheme.DarkGold;
            ResetProbe();
            using UiHost host = new(Scope, RegionManifest("partial", ""), new UiBindings(), theme, new StubMetrics(), new StubTranslation(), broken);

            Check(records.Count == 5,
                "each drop - the parser's four plus the page-level scheme nobody declared - is one report (got " + records.Count + ")");
            Check(UiFitAudit.StyleFallbackCount == 5, "and the audit's own count moved with it");
            Check(UiFitAudit.LastStyleFallbackDiagnostic != null
                    && UiFitAudit.LastStyleFallbackDiagnostic.IndexOf("missing", StringComparison.Ordinal) >= 0,
                "the report names the drop, not just a count: " + (UiFitAudit.LastStyleFallbackDiagnostic ?? "(none)"));

            UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(320f, 200f));
            Check(snapshot.RectById.ContainsKey("inside"),
                "the page still arranges - appearance never takes a page down");
            UiTheme? inside = LastTheme(MeasuredPaths, MeasuredThemes, "/inside");
            Check(inside != null && SameColor(inside.Panel, new Color(0f, 1f, 0f, 1f)),
                "and the readable declaration beside the dropped ones still landed");
        }
        finally
        {
            UiFitAudit.Detach();
            UiFitAudit.Reset();
        }
    }

    private static void VerifySilentWhenCorrect()
    {
        PrepareRegistry();
        var records = new List<UiStyleFallbackReport>();
        UiFitAudit.AttachStyleFallback(records.Add);
        UiFitAudit.Reset();
        try
        {
            UiTheme theme = UiTheme.DarkGold;
            using (UiHost host = new(Scope, RegionManifest(), new UiBindings(), theme, new StubMetrics(), new StubTranslation()))
            {
                host.DrawFrame(new Rect(0f, 0f, 320f, 240f));
                Check(records.Count == 0 && UiFitAudit.StyleFallbackCount == 0,
                    "a correct document says nothing on the appearance channel (fail-soft must not mean noisy)");
            }

            // The other end: a scope naming a scheme nobody declared is the same kind of drop, and it is
            // reported by the frame that resolved it - the host publishes the resolver's record right
            // after the arrangement that produced it, not one frame later.
            UiTheme naming = UiTheme.DarkGold;
            using (UiHost host = new(Scope, RegionManifest("stranger"), new UiBindings(), naming, new StubMetrics(), new StubTranslation()))
            {
                Check(records.Count == 0, "nothing is reported before an arrangement resolves the scope");
                UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(320f, 200f));
                Check(records.Count == 1,
                    "the unknown scheme name is reported in the frame that resolved it (got " + records.Count + ")");
                Check(snapshot.RectById.ContainsKey("inside"),
                    "and the page keeps rendering on the values it would have had without it");
            }
        }
        finally
        {
            UiFitAudit.Detach();
            UiFitAudit.Reset();
        }
    }

    // --- one source at a time, and both at once ----------------------------------------------------

    private static void VerifyTwoSourcesAreVisible()
    {
        PrepareRegistry();
        var records = new List<UiStyleFallbackReport>();
        UiFitAudit.AttachStyleFallback(records.Add);
        UiFitAudit.Reset();
        try
        {
            // The manifest carries a <Styles> section AND the caller hands in a document. One of the two is
            // necessarily the source; silently picking one is the failure mode this library refuses, so the
            // displaced section is reported through the appearance channel instead of vanishing.
            UiStyleDocument handedIn = UiStyleDocument.Parse(
                "<Styles Schema=\"1\">"
                + "<Density Name=\"compact\"><Metric Token=\"RowHeight\" Value=\"20\"/></Density>"
                + "</Styles>");
            UiTheme theme = UiTheme.DarkGold;
            using (UiHost host = new(Scope, ProbeManifest(IceAndCompactSection), new UiBindings(), theme, new StubMetrics(), new StubTranslation(), handedIn))
            {
                Check(records.Count == 1,
                    "the displaced manifest section is one report (got " + records.Count + ")");
                Check(UiFitAudit.LastStyleFallbackDiagnostic != null
                        && UiFitAudit.LastStyleFallbackDiagnostic.IndexOf("<Styles>", StringComparison.Ordinal) >= 0
                        && UiFitAudit.LastStyleFallbackDiagnostic.IndexOf("ignored", StringComparison.Ordinal) >= 0,
                    "and it names both sources rather than only a count: " + (UiFitAudit.LastStyleFallbackDiagnostic ?? "(none)"));
                Check(ReferenceEquals(host.StyleResolver.Document, handedIn),
                    "the handed-in document is the one in force");
            }

            // Either source on its own is silent: the report is about the collision, not about one origin
            // being second class.
            UiFitAudit.Reset();
            records.Clear();

            UiTheme embedded = UiTheme.DarkGold;
            using (UiHost host = new(Scope, ProbeManifest(IceAndCompactSection), new UiBindings(), embedded, new StubMetrics(), new StubTranslation()))
            {
                Check(records.Count == 0, "a manifest <Styles> section on its own says nothing");
            }

            UiTheme standalone = UiTheme.DarkGold;
            using (UiHost host = new(Scope, ProbeManifest(""), new UiBindings(), standalone, new StubMetrics(), new StubTranslation(), handedIn))
            {
                Check(records.Count == 0, "a handed-in document on its own says nothing");
            }
        }
        finally
        {
            UiFitAudit.Detach();
            UiFitAudit.Reset();
        }
    }

    // --- vocabulary --------------------------------------------------------------------------------

    private static void VerifyScopeVocabulary()
    {
        PrepareRegistry();

        // Both carriers are engine vocabulary, not a kind's: a container is a region its subtree inherits,
        // and a widget narrows its own scope. Neither has to be declared in a kind's schema.
        UiTheme containerTheme = UiTheme.DarkGold;
        using (UiHost host = new(Scope, RegionManifest(), new UiBindings(), containerTheme, new StubMetrics(), new StubTranslation()))
        {
            Check(host.Manifest.Roots.Count == 1, "Scheme/Density are accepted on a container");
        }

        UiTheme widgetTheme = UiTheme.DarkGold;
        using (UiHost host = new(Scope, ProbeManifest("", " Scheme=\"ice\" Density=\"compact\""), new UiBindings(), widgetTheme, new StubMetrics(), new StubTranslation()))
        {
            Check(host.Manifest.Roots.Count == 1, "and on a widget");
        }

        bool refused = false;
        try
        {
            UiTheme typoTheme = UiTheme.DarkGold;
            using UiHost host = new(Scope, ProbeManifest("", " Scheem=\"ice\""), new UiBindings(), typoTheme, new StubMetrics(), new StubTranslation());
        }
        catch (UiContractException)
        {
            refused = true;
        }

        Check(refused,
            "while a misspelled scope attribute name stays a creation-time contract failure, not a silent no-op");

        // A declaration with no document at all is still a declaration: the host resolves scopes even when
        // the page carries no style file and no <Styles> section, so naming a scheme nobody declared is
        // reported instead of being silently ignored.
        var records = new List<UiStyleFallbackReport>();
        UiFitAudit.AttachStyleFallback(records.Add);
        UiFitAudit.Reset();
        try
        {
            UiTheme orphan = UiTheme.DarkGold;
            using UiHost host = new(Scope, ProbeManifest("", " Scheme=\"ice\""), new UiBindings(), orphan, new StubMetrics(), new StubTranslation());
            Check(records.Count == 0, "nothing is reported before the scope is resolved");
            host.MeasureAndArrange(new Vector2(300f, 200f));
            Check(records.Count == 1,
                "a scope named on a page with no document is reported, not ignored (got " + records.Count + ")");
            Check(host.StyleResolver.Document.IsEmpty,
                "and the document it resolved is the empty one the manifest carries");
        }
        finally
        {
            UiFitAudit.Detach();
            UiFitAudit.Reset();
        }
    }

    // --- the page under test -----------------------------------------------------------------------

    private const string CompactDensitySection =
        "<Styles Schema=\"1\" Density=\"compact\">"
        + "<Density Name=\"compact\"><Metric Token=\"RowHeight\" Value=\"20\"/></Density>"
        + "</Styles>";

    private const string IceAndCompactSection =
        "<Styles Schema=\"1\">"
        + "<Scheme Name=\"ice\"><Color Token=\"Panel\" Value=\"#0000ff\"/></Scheme>"
        + "<Density Name=\"compact\"><Metric Token=\"RowHeight\" Value=\"20\"/></Density>"
        + "</Styles>";

    /// <summary>One probe element at the page root, behind an optional <c>&lt;Styles&gt;</c> section.</summary>
    private static UiLayoutManifest ProbeManifest(string styleSection, string probeAttributes = "")
    {
        return UiLayoutManifest.Parse(
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + styleSection
            + "<Widget Id=\"probe\" Kind=\"" + ProbeKind + "\"" + probeAttributes + "/>"
            + "</UiPage>");
    }

    /// <summary>
    /// A region around one probe element, plus a probe element outside it: the same region schema, the
    /// same page, only the scope name differs.
    /// </summary>
    private static UiLayoutManifest RegionManifest(string scheme = "ice", string styleSection = IceAndCompactSection)
    {
        return UiLayoutManifest.Parse(
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + styleSection
            + "<Stack Id=\"root\">"
            + "<Section Id=\"region\" Scheme=\"" + scheme + "\" Density=\"compact\">"
            + "<Widget Id=\"inside\" Kind=\"" + ProbeKind + "\"/>"
            + "</Section>"
            + "<Widget Id=\"outside\" Kind=\"" + ProbeKind + "\"/>"
            + "</Stack>"
            + "</UiPage>");
    }

    /// <summary>
    /// A region holding both the probe element and a real text atom, plus a probe at the page level: the
    /// atom is what turns "the region's font moved" into a measured band instead of an identity claim.
    /// </summary>
    private static UiLayoutManifest ClockManifest()
    {
        return UiLayoutManifest.Parse(
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + IceAndCompactSection
            + "<Stack Id=\"root\">"
            + "<Section Id=\"region\" Scheme=\"ice\" Density=\"compact\">"
            + "<Widget Id=\"inside\" Kind=\"" + ProbeKind + "\"/>"
            + "<Widget Id=\"note\" Kind=\"text/wrapped\" Text=\"" + LongCjk + "\"/>"
            + "</Section>"
            + "<Widget Id=\"outside\" Kind=\"" + ProbeKind + "\"/>"
            + "</Stack>"
            + "</UiPage>");
    }

    // --- the probe ---------------------------------------------------------------------------------

    /// <summary>
    /// Records what one element was handed in each half of a pass, and returns the density of the theme it
    /// was handed as its own band - so "the region's density reached this element" is visible in the
    /// arranged rect and not only in the bag.
    /// </summary>
    private sealed class ScopeProbe : IUiWidget
    {
        public string Kind => ProbeKind;

        public void Configure(UiElementSpec spec)
        {
        }

        public void Validate(IUiBindings bindings, string elementPath)
        {
        }

        public float Measure(UiWidgetContext ctx)
        {
            MeasuredPaths.Add(ctx.ElementPath);
            MeasuredThemes.Add(ctx.Theme);
            return ctx.Theme.Geometry.RowHeight;
        }

        public void Draw(Rect rect, UiWidgetContext ctx)
        {
            DrawnPaths.Add(ctx.ElementPath);
            DrawnThemes.Add(ctx.Theme);
        }
    }

    // --- helpers -----------------------------------------------------------------------------------

    private static void PrepareRegistry()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        UiWidgetRegistry.Register(Scope, ProbeKind, () => new ScopeProbe(), new[] { "Height" });
    }

    private static void ResetProbe()
    {
        MeasuredPaths.Clear();
        MeasuredThemes.Clear();
        DrawnPaths.Clear();
        DrawnThemes.Clear();
    }

    /// <summary>
    /// One frame: measure, then draw, with the probe's records cleared first so the lane reads this frame's
    /// observations rather than an earlier frame's.
    /// </summary>
    private static UiLayoutSnapshot Frame(UiHost host, float width, float height)
    {
        ResetProbe();
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(width, height));
        host.DrawFrame(new Rect(0f, 0f, width, height));

        // A frame that recovered an element is not a frame this lane may call healthy: the theme and band
        // assertions below would still pass while the element under test was replaced by its recovery band
        // (the 2026-09-12 stub hole). None of these lanes trips on purpose, so no switch is passed.
        KernelTripGuard.ExpectNoTrips(host.Session, "style-scope frame");
        return snapshot;
    }

    /// <summary>The last recorded theme whose element path ends with <paramref name="suffix"/>.</summary>
    private static UiTheme? LastTheme(List<string> paths, List<UiTheme> themes, string suffix)
    {
        for (int i = paths.Count - 1; i >= 0; i--)
        {
            if (paths[i].EndsWith(suffix, StringComparison.Ordinal)) return themes[i];
        }

        return null;
    }

    private static void ClearBoxes()
    {
        MethodInfo? clear = typeof(Verse.Widgets).GetMethod("ClearDrawBoxSolidCalls", BindingFlags.Public | BindingFlags.Static);
        if (clear == null) throw new Exception("Verse stub is missing ClearDrawBoxSolidCalls");
        clear.Invoke(null, null);
    }

    private static bool AnyRecordedBoxColor(Color wanted)
    {
        FieldInfo? field = typeof(Verse.Widgets).GetField("DrawBoxSolidColors", BindingFlags.Public | BindingFlags.Static);
        if (field == null) throw new Exception("Verse stub is missing DrawBoxSolidColors");
        var colors = (IList?)field.GetValue(null);
        if (colors == null) return false;

        for (int i = 0; i < colors.Count; i++)
        {
            object? recorded = colors[i];
            if (recorded is Color color && SameColor(color, wanted)) return true;
        }

        return false;
    }

    private static bool SameColor(Color left, Color right)
    {
        return Near(left.r, right.r) && Near(left.g, right.g) && Near(left.b, right.b) && Near(left.a, right.a);
    }

    private static bool Near(float expected, float actual)
    {
        return Math.Abs(expected - actual) <= 0.0001f;
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

    private static void CheckClose(float expected, float actual, string name)
    {
        Check(Near(expected, actual), name + " (expected " + expected + ", got " + actual + ")");
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

    private static void Reset()
    {
        UiFitAudit.Detach();
        UiFitAudit.Reset();
    }

    private sealed class StubMetrics : ITextMetrics
    {
        public float MeasureText(string text, UiFont font, float width) => 16f;

        public float MeasureWidth(string text, UiFont font) => StubTextWidth.Of(text, font);
    }

    private sealed class StubTranslation : IUiTranslation
    {
        public string Translate(string key) => key;

        public int TranslationRevision => 0;
    }

    /// <summary>
    /// A ruler whose wrapped band follows the font: the same deterministic model the other lanes use, so
    /// "the region rebuilt its theme with the new font" is observable as a taller arranged band.
    /// </summary>
    private sealed class FontMetrics : ITextMetrics
    {
        public float MeasureText(string text, UiFont font, float width)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            float advance = StubTextWidth.Of(text, font);
            int lines = Math.Max(1, (int)Math.Ceiling(advance / Math.Max(1f, width)));
            return lines * Em(font);
        }

        public float MeasureWidth(string text, UiFont font) => StubTextWidth.Of(text, font);

        private static float Em(UiFont font) => font switch
        {
            UiFont.Tiny => 12f,
            UiFont.Medium => 18f,
            _ => 16f
        };
    }
}
