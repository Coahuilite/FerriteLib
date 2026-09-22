using System;
using System.Collections.Generic;

using FerriteLib.UiKit.Kernel;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// The height axis' content-relative mode: <c>Height="MatchContent"</c> (0.7.x). The width axis has carried a
/// whole family (Width/WidthKey/MinWidth/MaxWidth) while the height axis accepted only a number or Auto, so a
/// consumer whose hit area had to COVER a sibling's measured height could only stack hand-sized bare bands and
/// under-covered the moment the text wrapped. The claims, in the order this lane checks them:
/// <list type="bullet">
/// <item>a <c>MatchContent</c> column inside the citation's <c>Overlay</c> takes the text column's measured
/// height - the capability the consumer could not express;</item>
/// <item>the same page WITHOUT the declaration still under-covers: the positive control that the equality above
/// measures the mode rather than an accident of the fixture;</item>
/// <item>the creation-time matrix: a parent whose content height is the SUM of its children, a Wrap (whose line
/// membership is discovered from the children), and a root have no reference and are refused, while the two
/// max-based parents accept it;</item>
/// <item>a parent that narrows into a vertical flow degrades the mode to the element's own content instead of
/// throwing or collapsing to zero.</item>
/// </list>
/// </summary>
internal static class KernelContentHeightTests
{
    private const string Scope = "content-height-lane";

    /// <summary>Thirty ideographs at the shared half-width model: far more than the bands' own single lines.</summary>
    private static readonly string LongText = new string('测', 30);

    private static int failures;

    public static int RunAll()
    {
        failures = 0;
        Run("A MatchContent column takes its text column's measured height", VerifyHitColumnMatchesTheText);
        Run("Without the mode the same page under-covers (positive control)", VerifyControlPageUnderCovers);
        Run("The mode is refused where the parent's content height is the sum of its children", VerifyRefusalMatrix);
        Run("A parent that narrows into a vertical flow degrades the mode, not the page", VerifyNarrowDegradation);
        return failures;
    }

    // --- lanes -----------------------------------------------------------------------------------

    /// <summary>
    /// The citation's shape: an Overlay holding a text column and a hit column of two bare bands. The hit
    /// column declares the mode, so it must end up exactly as tall as the text it covers.
    /// </summary>
    private static void VerifyHitColumnMatchesTheText()
    {
        using UiHost host = NewHost(OverlayPage(" Height=\"MatchContent\""));
        UiLayoutSnapshot snapshot = Arrange(host, 320f);

        Rect text = snapshot.RectById["row-text"];
        Rect hit = snapshot.RectById["row-hit"];
        float ownContent = snapshot.RectById["hit-one"].height + snapshot.RectById["hit-two"].height;

        Check(Near(hit.height, text.height),
            "the hit column takes the text column's measured height (" + hit.height + " vs " + text.height + ")");
        Check(text.height > ownContent,
            "and the text column is genuinely the taller one: " + text.height + " against the bands' own "
            + ownContent + " - the under-coverage this mode removes");
    }

    /// <summary>
    /// The positive control, and the reason the mode exists: the identical page with the declaration removed
    /// keeps the hit column at its own content height and under-covers the text.
    /// </summary>
    private static void VerifyControlPageUnderCovers()
    {
        using UiHost host = NewHost(OverlayPage(""));
        UiLayoutSnapshot snapshot = Arrange(host, 320f);

        Rect text = snapshot.RectById["row-text"];
        Rect hit = snapshot.RectById["row-hit"];

        Check(hit.height > 0f, "the control page still arranges both columns");
        Check(hit.height < text.height,
            "without the mode the hit column keeps its own content height and under-covers (" + hit.height
            + " < " + text.height + ")");
    }

    /// <summary>
    /// The refusal matrix and its principle: the reference exists only when the parent's content height is the
    /// MAXIMUM over its children. A parent that sums its children includes the declaring element itself, a Wrap
    /// discovers a line's membership from the children in it, and a root has no parent at all.
    /// </summary>
    private static void VerifyRefusalMatrix()
    {
        Check(CreationFailure(OverlayPage(" Height=\"MatchContent\"")) == null,
            "an Overlay child may declare it (the citation's parent, a max over its children)");
        Check(CreationFailure(RowPage(" Height=\"MatchContent\"")) == null,
            "a Row child may declare it (a max over its children)");

        foreach (string parent in new[] { "Column", "Stack", "Section", "Surface", "Scroll" })
        {
            ExpectRefused("a " + parent + " child",
                Page("<" + parent + " Id=\"box\">"
                    + "<Widget Id=\"note\" Kind=\"text/wrapped\" Bind=\"caption\" />"
                    + "<Widget Id=\"band\" Kind=\"display/progress\" Bind=\"pct\" Height=\"MatchContent\" />"
                    + "</" + parent + ">"));
        }

        ExpectRefused("a Wrap child",
            Page("<Wrap Id=\"box\">"
                + "<Widget Id=\"note\" Kind=\"text/wrapped\" Bind=\"caption\" Width=\"120\" />"
                + "<Widget Id=\"band\" Kind=\"display/progress\" Bind=\"pct\" Width=\"120\" Height=\"MatchContent\" />"
                + "</Wrap>"));

        ExpectRefused("a root element",
            Page("<Widget Id=\"band\" Kind=\"display/progress\" Bind=\"pct\" Height=\"MatchContent\" />"));

        ExpectRefused("a parent whose every child declares it",
            Page("<Row Id=\"box\">"
                + "<Widget Id=\"band\" Kind=\"display/progress\" Bind=\"pct\" Width=\"40\" Height=\"MatchContent\" />"
                + "<Widget Id=\"band2\" Kind=\"display/progress\" Bind=\"pct\" Width=\"40\" Height=\"MatchContent\" />"
                + "</Row>"));

        ExpectRefused("a value outside the closed vocabulary",
            Page("<Row Id=\"box\">"
                + "<Widget Id=\"fixed\" Kind=\"chrome/rule\" Width=\"24\" Height=\"24\" />"
                + "<Widget Id=\"band\" Kind=\"display/progress\" Bind=\"pct\" Height=\"Match\" />"
                + "</Row>"));
    }

    /// <summary>
    /// A Row that narrows into a Column is a legal declaration whose parent becomes a vertical flow at the
    /// breakpoint. The mode then has no reference, and the engine must degrade to the element's own content -
    /// no throw, and no silent collapse to a zero-height band.
    /// </summary>
    private static void VerifyNarrowDegradation()
    {
        using UiHost host = NewHost(NarrowPage);
        UiLayoutSnapshot wide = Arrange(host, 320f);
        Check(Near(wide.RectById["band"].height, wide.RectById["note"].height),
            "wide (still a Row): the mode resolves against the row's content");

        UiLayoutSnapshot narrow = Arrange(host, 150f);
        float rowHeight = UiTheme.DarkGold.Geometry.RowHeight;
        Check(Near(narrow.RectById["band"].height, rowHeight),
            "narrow (the Row became a Column): the mode degrades to the element's own content band ("
            + narrow.RectById["band"].height + " vs the theme row height " + rowHeight + ")");
        Check(!Near(narrow.RectById["band"].height, narrow.RectById["note"].height),
            "and nothing pretends to match: " + narrow.RectById["band"].height + " vs "
            + narrow.RectById["note"].height);
    }

    // --- pages -----------------------------------------------------------------------------------

    private static string Page(string body) =>
        "<UiPage Schema=\"2\" Source=\"" + Scope + "\">" + body + "</UiPage>";

    /// <summary>
    /// The citation's page (US S4-2's race-layer row): an Overlay because the hit area must COVER the text
    /// rather than sit beside it, with the hit column's own content being two bare bands. The height attribute
    /// is a parameter so the control page is the identical tree with the declaration removed.
    /// </summary>
    private static string OverlayPage(string hitHeightAttribute) =>
        Page("<Overlay Id=\"row\" Padding=\"0\">"
            + "<Column Id=\"row-text\" AlignX=\"Left\" Width=\"120\">"
            + "<Widget Id=\"note\" Kind=\"text/wrapped\" Bind=\"caption\" />"
            + "</Column>"
            + "<Column Id=\"row-hit\" AlignX=\"Left\" Width=\"120\"" + hitHeightAttribute + ">"
            + "<Widget Id=\"hit-one\" Kind=\"input/button\" ActionBind=\"one\" Chrome=\"none\" Height=\"Auto\" Text=\"one\" />"
            + "<Widget Id=\"hit-two\" Kind=\"input/button\" ActionBind=\"two\" Chrome=\"none\" Height=\"Auto\" Text=\"two\" />"
            + "</Column>"
            + "</Overlay>");

    private static string RowPage(string bandHeightAttribute) =>
        Page("<Row Id=\"row\" Padding=\"0\">"
            + "<Widget Id=\"note\" Kind=\"text/wrapped\" Bind=\"caption\" Width=\"120\" />"
            + "<Widget Id=\"band\" Kind=\"display/progress\" Bind=\"pct\" Width=\"40\"" + bandHeightAttribute + " />"
            + "</Row>");

    private const string NarrowPage =
        "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
        + "<Row Id=\"row\" Padding=\"0\" Gap=\"4\" Breakpoint=\"200\" Narrow=\"Column\">"
        + "<Widget Id=\"note\" Kind=\"text/wrapped\" Bind=\"caption\" Width=\"120\" />"
        + "<Widget Id=\"band\" Kind=\"display/progress\" Bind=\"pct\" Width=\"40\" Height=\"MatchContent\" />"
        + "</Row>"
        + "</UiPage>";

    // --- helpers ---------------------------------------------------------------------------------

    private static UiHost NewHost(string xml)
    {
        return new UiHost(
            Scope, UiLayoutManifest.Parse(xml), MakeBindings(), UiTheme.DarkGold, new WrappedMetrics(),
            new StubTranslation());
    }

    private static UiBindings MakeBindings()
    {
        var bindings = new UiBindings();
        bindings.BindReadOnly<string>("caption", () => LongText);
        bindings.BindReadOnly<float>("pct", () => 1f);
        bindings.BindCommand("one", () => { });
        bindings.BindCommand("two", () => { });
        return bindings;
    }

    private static UiLayoutSnapshot Arrange(UiHost host, float width)
    {
        host.DrawFrame(new Rect(0f, 0f, width, 400f));
        return host.MeasureAndArrange(new Vector2(width, 400f));
    }

    private static string? CreationFailure(string xml)
    {
        try
        {
            using var host = NewHost(xml);
            return null;
        }
        catch (Exception ex)
        {
            return ex.GetType().Name + ": " + ex.Message;
        }
    }

    /// <summary>
    /// A refused declaration must say so where the element, the attribute and the path are all still known, and
    /// the message has to name the mode - a bare "invalid Height" would leave the author guessing which of the
    /// accepted values they meant.
    /// </summary>
    private static void ExpectRefused(string what, string xml)
    {
        string? failure = CreationFailure(xml);
        Check(failure != null && failure.IndexOf("MatchContent", StringComparison.Ordinal) >= 0,
            what + " is refused at creation, naming the mode: " + (failure ?? "NO FAILURE"));
    }

    private static bool Near(float left, float right) => Math.Abs(left - right) <= 0.001f;

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

    /// <summary>
    /// The lane's ruler: width from the shared half-width glyph model, height from the wrapped line count times
    /// that model's em. A constant-per-font stub could not express "the same string at two widths", which is what
    /// every assertion here compares.
    /// </summary>
    private sealed class WrappedMetrics : ITextMetrics
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

    private sealed class StubTranslation : IUiTranslation
    {
        public string Translate(string key) => "[" + key + "]";

        public int TranslationRevision => 0;
    }
}
