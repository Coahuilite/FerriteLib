using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using FerriteLib.UiKit.Kernel;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// Style-document lane (0.4.x batch B): the standalone file and the manifest's <c>&lt;Styles&gt;</c>
/// section are one document type through one parser, the written precedence chain
/// (<c>state &gt; element &gt; container &gt; page &gt; theme &gt; default</c>) resolves in that order,
/// scheme and density inherit while roles never do, the document is parsed before the first arrangement
/// so layout follows it, and every appearance failure falls back loudly instead of throwing or staying
/// quiet. Colours are read back through the theme's own tokens and the fitted audit, so a document that
/// parses but does not reach the values cannot pass.
/// </summary>
internal static class KernelStyleDocumentTests
{
    private static int failures;

    public static int RunAll()
    {
        failures = 0;
        Run("The two text origins share one parser and one vocabulary", VerifyTwoOrigins);
        Run("The precedence chain resolves level by level", VerifyPrecedenceChain);
        Run("Inheritance is asymmetric: scheme/density inherit, roles do not", VerifyInheritanceAsymmetry);
        Run("A document is resolved before Measure and layout follows it", VerifyResolveBeforeMeasure);
        Run("Appearance failures fall back softly and loudly", VerifyFailSoftAndLoud);
        Run("A dropped <Styles> section is loud, not silent", VerifyDroppedStylesSectionIsLoud);
        Run("Both file entries load what they promise", VerifyFileOrigins);
        Run("Clone copies values and never shares a store", VerifyCloneContract);
        ResetAudit();
        return failures;
    }

    private static void VerifyTwoOrigins()
    {
        const string body =
            "<Scheme Name=\"ice\">"
            + "<Color Token=\"Panel\" Value=\"#0000ff\"/>"
            + "<Color Token=\"Bogus\" Value=\"#ffffff\"/>"
            + "</Scheme>";

        UiStyleDocument standalone = UiStyleDocument.Parse("<Styles Schema=\"1\">" + body + "</Styles>");
        UiLayoutManifest manifest = UiLayoutManifest.Parse(
            "<UiPage Schema=\"2\" Source=\"two-origins\">"
            + "<Styles Schema=\"1\">" + body + "</Styles>"
            + "<Widget Id=\"w\" Kind=\"chrome/banner\" Text=\"x\"/>"
            + "</UiPage>");
        UiStyleDocument embedded = manifest.Styles;

        Check(standalone.SchemeNames.Contains("ice"), "the standalone document declares its scheme");
        Check(embedded.SchemeNames.Contains("ice"), "the embedded section declares the same scheme");

        var standaloneResolver = new UiStyleResolver(UiTheme.DarkGold, standalone);
        var embeddedResolver = new UiStyleResolver(UiTheme.DarkGold, embedded);
        Color standalonePanel = standaloneResolver.ThemeFor(new[] { new UiStyleDeclaration(scheme: "ice") }).Panel;
        Color embeddedPanel = embeddedResolver.ThemeFor(new[] { new UiStyleDeclaration(scheme: "ice") }).Panel;
        Check(
            SameColor(standalonePanel, embeddedPanel) && SameColor(standalonePanel, new Color(0f, 0f, 1f, 1f)),
            "the same content through either origin lands on the same value (one parser, one vocabulary)");

        Check(
            standalone.Issues.Count == 1 && embedded.Issues.Count == 1
                && string.Equals(standalone.Issues[0].Message, embedded.Issues[0].Message, StringComparison.Ordinal),
            "an unknown token is dropped and reported identically by both origins");

        Check(UiLayoutManifest.Parse("<UiPage Schema=\"2\" Source=\"none\"><Widget Id=\"w\" Kind=\"chrome/banner\" Text=\"x\"/></UiPage>").Styles.IsEmpty,
            "a manifest without the section carries the empty document");

        bool duplicateRefused = false;
        try
        {
            UiLayoutManifest.Parse(
                "<UiPage Schema=\"2\" Source=\"dup\"><Styles Schema=\"1\"/><Styles Schema=\"1\"/></UiPage>");
        }
        catch (FormatException)
        {
            duplicateRefused = true;
        }

        Check(duplicateRefused, "a second <Styles> section is a structural (page-level) failure, not a soft one");
    }

    private static void VerifyPrecedenceChain()
    {
        const string schemes =
            "<Scheme Name=\"page\"><Color Token=\"Panel\" Value=\"#0000ff\"/></Scheme>"
            + "<Scheme Name=\"container\"><Color Token=\"Panel\" Value=\"#00ff00\"/></Scheme>"
            + "<Scheme Name=\"element\"><Color Token=\"Panel\" Value=\"#ff0000\"/></Scheme>";

        // default: nothing declared anywhere -> the library's own value.
        Color libraryDefault = UiTheme.DarkGold.Panel;
        Check(
            SameColor(new UiStyleResolver(UiTheme.DarkGold, UiStyleDocument.Empty).ThemeFor(null).Panel, libraryDefault),
            "default: with no source at all the library value stands");

        // theme > default: the injected theme is the baseline under every document.
        UiTheme tinted = UiTheme.DarkGold;
        tinted.Panel = new Color(0.01f, 0.02f, 0.03f, 1f);
        Check(
            SameColor(new UiStyleResolver(tinted, UiStyleDocument.Empty).ThemeFor(null).Panel, tinted.Panel),
            "theme > default: the consumer's token wins over the library one");

        var document = UiStyleDocument.Parse("<Styles Schema=\"1\" Scheme=\"page\">" + schemes + "</Styles>");
        var resolver = new UiStyleResolver(tinted, document);

        Check(
            SameColor(resolver.ThemeFor(null).Panel, new Color(0f, 0f, 1f, 1f)),
            "page > theme: the page-level scheme overrides the injected theme");

        var container = new UiStyleDeclaration(scheme: "container");
        Check(
            SameColor(resolver.ThemeFor(new[] { default(UiStyleDeclaration), container }).Panel, new Color(0f, 1f, 0f, 1f)),
            "container > page: a container's scheme beats the page-level one");

        var element = new UiStyleDeclaration(scheme: "element");
        Check(
            SameColor(resolver.ThemeFor(new[] { element, container }).Panel, new Color(1f, 0f, 0f, 1f)),
            "element > container: the nearest declaration wins");

        // state > element: a value the player cannot write is painted disabled whatever was authored.
        var danger = new[] { new UiStyleDeclaration(tone: UiStatusTone.Danger) };
        UiTheme theme = UiTheme.DarkGold;
        Check(
            SameColor(resolver.Resolve(theme, danger, writable: true).Fill, theme.Danger),
            "element: an authored tone decides when nothing overrides it");
        Check(
            SameColor(resolver.Resolve(theme, danger, writable: false).Fill, theme.Base),
            "state > element: a read-only value is painted disabled whatever tone was authored");
    }

    private static void VerifyInheritanceAsymmetry()
    {
        const string scheme = "<Scheme Name=\"ice\"><Color Token=\"Panel\" Value=\"#0000ff\"/></Scheme>";
        const string density = "<Density Name=\"compact\"><Metric Token=\"RowHeight\" Value=\"20\"/></Density>";
        var document = UiStyleDocument.Parse("<Styles Schema=\"1\">" + scheme + density + "</Styles>");
        var resolver = new UiStyleResolver(UiTheme.DarkGold, document);

        var container = new UiStyleDeclaration(scheme: "ice", density: "compact");
        var silentElement = default(UiStyleDeclaration);
        UiTheme inherited = resolver.ThemeFor(new[] { silentElement, container });

        Check(SameColor(inherited.Panel, new Color(0f, 0f, 1f, 1f)),
            "scheme inherits: an element that declares nothing takes its container's palette");
        Check(Math.Abs(inherited.Geometry.RowHeight - 20f) <= 0.0001f,
            "density inherits the same way");

        // and the counter-example the ruling asks for: a region tagged danger does not tag its children.
        var dangerRegion = new UiStyleDeclaration(tone: UiStatusTone.Danger);
        UiTheme theme = UiTheme.DarkGold;
        UiResolvedStyle child = resolver.Resolve(theme, new[] { silentElement, dangerRegion });
        Check(
            SameColor(child.Fill, theme.Raised) && !SameColor(child.Fill, theme.Danger),
            "roles do not inherit: a danger-tagged region leaves its children on the neutral treatment");

        UiResolvedStyle regionChrome = resolver.Resolve(theme, new[] { dangerRegion });
        Check(SameColor(regionChrome.Fill, theme.Danger),
            "while the region's own chrome does carry the tag");
    }

    private static void VerifyResolveBeforeMeasure()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();

        // The density is the page-level default here; a scheme/density that is merely declared applies
        // when a scope names it, which is what makes the page level a source rather than a global switch.
        const string documentXml =
            "<Styles Schema=\"1\" Density=\"compact\">"
            + "<Density Name=\"compact\"><Metric Token=\"RowHeight\" Value=\"20\"/></Density>"
            + "</Styles>";
        UiStyleDocument document = UiStyleDocument.Parse(documentXml);

        UiTheme theme = UiTheme.DarkGold;
        var resolver = new UiStyleResolver(theme, document);

        // The resolve-before-Measure slot: the document is applied before the first arrangement, and the
        // theme's own layout clock is what makes the very first snapshot see it.
        int revisionBefore = theme.LayoutRevision;
        resolver.ApplyTo(theme);
        Check(theme.LayoutRevision > revisionBefore, "applying a document moves the layout clock the host compares");

        UiLayoutManifest manifest = UiLayoutManifest.Parse(
            "<UiPage Schema=\"2\" Source=\"style-doc\">"
            + "<Widget Id=\"dd\" Kind=\"input/dropdown\" Option1=\"A\" Value1=\"A\"/>"
            + "<Widget Id=\"banner\" Kind=\"chrome/banner\" Text=\"ok\" Height=\"30\"/>"
            + "</UiPage>");
        var bindings = new UiBindings();
        bindings.BindValue("dd", () => "A", _ => { });

        var reports = new List<UiOverflowReport>();
        UiFitAudit.Attach(new ModelMetrics(), reports.Add);
        UiFitAudit.Reset();
        UiFitAudit.Enabled = true;

        using UiHost host = new("style-doc", manifest, bindings, theme, new ModelMetrics(), new FixedTranslation());
        UiLayoutSnapshot first = host.MeasureAndArrange(new Vector2(240f, 200f));
        Check(Math.Abs(first.RectById["dd"].height - 20f) <= 0.01f,
            "the first arrangement already uses the document's density (no stale default)");

        host.DrawFrame(new Rect(0f, 0f, 240f, 200f));
        Check(reports.Count == 0, "a page drawn under the document reports no new fitting findings");

        // One scope, one theme instance: the region theme is built once and reused across frames.
        var chain = new[] { new UiStyleDeclaration(density: "compact") };
        Check(ReferenceEquals(resolver.ThemeFor(chain), resolver.ThemeFor(chain)),
            "a scope reuses one theme instance across frames instead of allocating per frame");
        Check(!ReferenceEquals(resolver.ThemeFor(chain), theme),
            "and that scope theme is its own bag, not the host's");

        UiFitAudit.Detach();
    }

    /// <summary>
    /// The failure this lane exists for: a <c>&lt;Styles&gt;</c> section whose <c>Schema</c> is missing is
    /// refused as a whole, so every value it carried - a density above all - silently does not apply while
    /// the page draws on defaults. "Seen" is what gets asserted: one Warning-level line naming the section
    /// and the parser's reason, with a correct document staying silent as the counter-example.
    /// </summary>
    private static void VerifyDroppedStylesSectionIsLoud()
    {
        var warnings = new List<string>();
        UiHost.StyleWarningOverride = warnings.Add;
        try
        {
            UiLayoutManifest dropped = UiLayoutManifest.Parse(
                "<UiPage Schema=\"2\" Source=\"dropped-styles\">"
                + "<Styles><Density Name=\"compact\"><Metric Token=\"RowHeight\" Value=\"20\"/></Density></Styles>"
                + "<Widget Id=\"banner\" Kind=\"chrome/banner\" Text=\"x\" Height=\"30\"/>"
                + "</UiPage>");

            Check(dropped.Styles.IsEmpty,
                "the section without Schema is still refused as a whole (schema semantics unchanged)");

            using (UiHost host = new("dropped-styles", dropped, new UiBindings(), UiTheme.DarkGold,
                new ModelMetrics(), new FixedTranslation()))
            {
                host.MeasureAndArrange(new Vector2(240f, 200f));
            }

            Check(warnings.Count > 0, "a dropped <Styles> section produces a warning instead of silence");

            bool namesSection = false;
            bool namesReason = false;
            foreach (string warning in warnings)
            {
                if (warning.IndexOf("Styles", StringComparison.Ordinal) >= 0) namesSection = true;
                if (warning.IndexOf("Schema", StringComparison.Ordinal) >= 0) namesReason = true;
            }

            Check(namesSection, "the warning names the section the author has to look at");
            Check(namesReason, "and the reason the parser gave for dropping it");

            // Counter-example: the same content with its Schema emits nothing (fail-soft must not be noisy).
            warnings.Clear();
            UiLayoutManifest good = UiLayoutManifest.Parse(
                "<UiPage Schema=\"2\" Source=\"good-styles\">"
                + "<Styles Schema=\"1\"><Density Name=\"compact\"><Metric Token=\"RowHeight\" Value=\"20\"/></Density></Styles>"
                + "<Widget Id=\"banner\" Kind=\"chrome/banner\" Text=\"x\" Height=\"30\"/>"
                + "</UiPage>");

            using (UiHost host = new("good-styles", good, new UiBindings(), UiTheme.DarkGold,
                new ModelMetrics(), new FixedTranslation()))
            {
                host.MeasureAndArrange(new Vector2(240f, 200f));
            }

            Check(warnings.Count == 0, "a document that parses stays silent");
        }
        finally
        {
            UiHost.StyleWarningOverride = null;
        }
    }

    private static void VerifyFailSoftAndLoud()
    {
        // Every shape of wrong value, in one document: each is dropped, each is recorded, nothing throws.
        const string broken =
            "<Styles Schema=\"1\" Bogus=\"1\">"
            + "<Scheme Name=\"partial\">"
            + "<Color Token=\"Panel\" Value=\"#00ff00\"/>"
            + "<Color Token=\"Raised\" Value=\"not-a-colour\"/>"
            + "<Color Token=\"Nope\" Value=\"#ffffff\"/>"
            + "<Font Value=\"Huge\"/>"
            + "</Scheme>"
            + "<Density Name=\"bad\"><Metric Token=\"RowHeight\" Value=\"-4\"/></Density>"
            + "<Mystery/>"
            + "</Styles>";

        UiStyleDocument document = UiStyleDocument.Parse(broken);
        Check(document.Issues.Count >= 5, "each dropped declaration is recorded (got " + document.Issues.Count + " issues)");
        Check(document.SchemeNames.Contains("partial"), "the rest of a partly-bad scheme still applies");

        var resolver = new UiStyleResolver(UiTheme.DarkGold, document);
        UiTheme partial = resolver.ThemeFor(new[] { new UiStyleDeclaration(scheme: "partial") });
        Check(SameColor(partial.Panel, new Color(0f, 1f, 0f, 1f)), "the readable value in that scheme landed");
        Check(SameColor(partial.Raised, UiTheme.DarkGold.Raised), "the unreadable one fell back to the default");

        resolver.ThemeFor(new[] { new UiStyleDeclaration(scheme: "nobody-declared-this") });
        Check(resolver.Issues.Count == 1, "a scope naming an unknown scheme is recorded at resolution time");
        Check(SameColor(resolver.ThemeFor(new[] { new UiStyleDeclaration(scheme: "nobody-declared-this") }).Panel, UiTheme.DarkGold.Panel),
            "and the scope keeps the values it would have had without it (the page still renders)");

        // The other end: a correct document is silent on both records.
        UiStyleDocument good = UiStyleDocument.Parse(
            "<Styles Schema=\"1\"><Scheme Name=\"ice\"><Color Token=\"Panel\" Value=\"0.1,0.2,0.3\"/></Scheme></Styles>");
        var goodResolver = new UiStyleResolver(UiTheme.DarkGold, good);
        goodResolver.ThemeFor(new[] { new UiStyleDeclaration(scheme: "ice") });
        Check(good.Issues.Count == 0 && goodResolver.Issues.Count == 0,
            "a correct document records nothing (fail-soft must not mean noisy)");

        bool malformedWasSoft = true;
        try
        {
            UiStyleDocument malformed = UiStyleDocument.Parse("<Styles Schema=\"1\"><Scheme Name=\"x\">");
            malformedWasSoft = malformed.Issues.Count > 0 && malformed.IsEmpty;
        }
        catch (Exception)
        {
            malformedWasSoft = false;
        }

        Check(malformedWasSoft, "a malformed standalone document is appearance-class, never a throw");
    }

    private static void VerifyFileOrigins()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ferritelib-style-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string stylePath = Path.Combine(directory, "style.xml");
            File.WriteAllText(stylePath,
                "<Styles Schema=\"1\"><Scheme Name=\"ice\"><Color Token=\"Panel\" Value=\"#0000ff\"/></Scheme></Styles>");
            UiStyleDocument fromFile = UiStyleDocument.ParseFile(stylePath);
            Check(fromFile.SchemeNames.Contains("ice"), "the standalone file entry reads a real file");

            string brokenPath = Path.Combine(directory, "broken.xml");
            File.WriteAllText(brokenPath, "<Styles Schema=\"1\"><Scheme");
            UiStyleDocument broken = UiStyleDocument.ParseFile(brokenPath);
            Check(broken.Issues.Count > 0 && broken.IsEmpty, "a broken style file is soft and says why");

            UiStyleDocument missing = UiStyleDocument.ParseFile(Path.Combine(directory, "absent.xml"));
            Check(missing.Issues.Count == 1, "a missing style file is the same appearance-class fallback");

            string manifestPath = Path.Combine(directory, "page.xml");
            File.WriteAllText(manifestPath,
                "<UiPage Schema=\"2\" Source=\"file-origin\">"
                + "<Styles Schema=\"1\"><Density Name=\"compact\"><Metric Token=\"RowHeight\" Value=\"18\"/></Density></Styles>"
                + "<Widget Id=\"banner\" Kind=\"chrome/banner\" Text=\"x\"/>"
                + "</UiPage>");
            UiLayoutManifest fromFileManifest = UiLayoutManifest.ParseFile(manifestPath);
            Check(fromFileManifest.Roots.Count == 1 && fromFileManifest.Styles.DensityNames.Contains("compact"),
                "the manifest file entry carries both the tree and its style section");
        }
        finally
        {
            try { Directory.Delete(directory, true); } catch (IOException) { }
        }
    }

    private static void VerifyCloneContract()
    {
        UiTheme original = UiTheme.DarkGold;
        original.Panel = new Color(0.11f, 0.12f, 0.13f, 1f);
        original.Geometry = new UiGeometry(3f, 4f, 5f, 26f, 1f);

        UiTheme copy = original.Clone();
        Check(SameColor(copy.Panel, original.Panel), "clone copies token values");
        Check(copy.Geometry == original.Geometry, "clone copies the density bundle");
        Check(!ReferenceEquals(copy.Styles, original.Styles), "clone has its own resolved-value store");
        Check(copy.Styles.Resolve(UiStatusTone.Neutral).Text.r == original.Styles.Resolve(UiStatusTone.Neutral).Text.r,
            "both stores answer from their own theme");

        copy.Panel = new Color(0.9f, 0.9f, 0.9f, 1f);
        Check(!SameColor(copy.Panel, original.Panel), "moving a token on the copy leaves the original alone");
    }

    private static void ResetAudit()
    {
        UiFitAudit.Detach();
    }

    private static bool SameColor(Color left, Color right)
    {
        return Math.Abs(left.r - right.r) <= 0.0001f
            && Math.Abs(left.g - right.g) <= 0.0001f
            && Math.Abs(left.b - right.b) <= 0.0001f
            && Math.Abs(left.a - right.a) <= 0.0001f;
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

    private sealed class ModelMetrics : ITextMetrics
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

    private sealed class FixedTranslation : IUiTranslation
    {
        public string Translate(string key) => key;

        public int TranslationRevision => 0;
    }
}
