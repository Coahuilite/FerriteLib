using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using FerriteLib.UiKit.Kernel;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// <c>section/header</c>'s chrome: the 1px surface it paints along its own bottom edge. The kind is a title
/// plus a line, and the line used to be unconditional - not in its schema, reachable by no manifest attribute -
/// so a page could not turn it off, and a borderless/whitespace-separated design (the maintainer's S6
/// direction: no frames, no dividers, separated by whitespace) was blocked by an atom painting something the
/// manifest could not control. The claims, in the order this lane checks them:
/// <list type="bullet">
/// <item>the divider is painted by default, inside the element's own rect, in the theme's <c>Divider</c> token -
/// themed, not hard-coded;</item>
/// <item><c>Chrome="none"</c> paints <b>nothing</b> while the title is still drawn: the assertion is "nothing
/// was drawn", never "it was drawn in another colour";</item>
/// <item>any other <c>Chrome</c> value is refused at creation, naming the one value this kind implements (the
/// same fail-closed shape <c>input/button</c> uses for its bare hit area);</item>
/// <item>the colour is a theme token, so a scope's scheme can restyle it - the half a manifest already had.</item>
/// </list>
/// </summary>
internal static class KernelSectionHeaderTests
{
    private const string Scope = "section-header-lane";
    private const string Title = "Section";

    /// <summary>The divider a scheme installs, as the document's own hex writes it.</summary>
    private static readonly Color ProbeDivider = new(0x21 / 255f, 0x92 / 255f, 0x4A / 255f, 1f);

    private static int failures;

    public static int RunAll()
    {
        failures = 0;
        Run("The divider is painted by default, in the theme's Divider token", VerifyDividerIsPaintedByDefault);
        Run("Chrome=none paints nothing while the title stays", VerifyChromeNonePaintsNothing);
        Run("A Chrome value this kind does not implement is refused at creation", VerifyChromeIsFailClosed);
        Run("A scope's scheme moves the divider colour", VerifySchemeMovesTheDivider);
        return failures;
    }

    // --- lanes -----------------------------------------------------------------------------------

    /// <summary>
    /// The shipped look, pinned before anything changes: five surface calls, all of them the one-pixel band at
    /// the element's bottom edge, all of them the theme's own <c>Divider</c> token.
    /// </summary>
    private static void VerifyDividerIsPaintedByDefault()
    {
        UiTheme theme = UiTheme.DarkGold;
        ClearBoxes();
        using UiHost host = NewHost(Page(""), theme);
        UiLayoutSnapshot snapshot = Arrange(host, 200f);

        Rect head = snapshot.RectById["head"];
        List<Rect> painted = RecordedSolidRects();
        Check(painted.Count > 0, "the default header paints its divider (" + painted.Count + " surface call(s))");
        Check(AllInside(painted, head), "and every call lands inside the element's own rect");
        Check(AnyRecordedBoxColor(theme.Divider), "in the theme's Divider token rather than a literal colour");
    }

    /// <summary>
    /// The switch. The assertion is the absence of a DRAWING, not a different colour: nothing at all is painted
    /// in the element's rect, while the title it exists for is still on the wire.
    /// </summary>
    private static void VerifyChromeNonePaintsNothing()
    {
        UiTheme theme = UiTheme.DarkGold;
        ClearBoxes();
        using UiHost host = NewHost(Page(" Chrome=\"none\""), theme);
        UiLayoutSnapshot snapshot = Arrange(host, 200f);

        Rect head = snapshot.RectById["head"];
        int painted = RecordedSolidRects().Count;
        Check(painted == 0, "Chrome=none paints nothing in the element's rect (" + painted + " surface call(s))");
        Check(AnyLabelInside(head), "while the title is still drawn: the switch removes the chrome, not the content");
        Check(!AnyRecordedBoxColor(theme.Divider), "and no Divider-coloured call sneaks in by another path");
    }

    /// <summary>
    /// Fail-closed, the A2/A3 shape: <c>Chrome</c> is vocabulary this kind implements exactly one value of, so a
    /// value it would ignore is refused where the element, the attribute and the path are still known.
    /// </summary>
    private static void VerifyChromeIsFailClosed()
    {
        ExpectRefused("Chrome=\"panel\"", "panel");
        ExpectRefused("Chrome=\"true\"", "true");
    }

    /// <summary>
    /// The theming half a manifest already had, pinned so the fix cannot remove it: the line's colour is the
    /// scope's <c>Divider</c> token, so a scheme can restyle it (transparent included) without any new attribute.
    /// </summary>
    private static void VerifySchemeMovesTheDivider()
    {
        UiTheme theme = UiTheme.DarkGold;
        ClearBoxes();
        using UiHost host = NewHost(Page(" Scheme=\"quiet\"", QuietScheme), theme);
        Arrange(host, 200f);

        Check(AnyRecordedBoxColorNear(ProbeDivider),
            "the scope's scheme moved the divider colour off the baseline token");
        Check(!AnyRecordedBoxColor(theme.Divider),
            "so the line is themed through the one token, not painted from the injected theme");
    }

    // --- page and helpers ------------------------------------------------------------------------

    private static string Page(string headerAttributes, string styleSection = "") =>
        "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
        + styleSection
        + "<Widget Id=\"head\" Kind=\"section/header\" Title=\"" + Title + "\" Height=\"24\""
        + headerAttributes + " />"
        + "</UiPage>";

    private const string QuietScheme =
        "<Styles Schema=\"1\">"
        + "<Scheme Name=\"quiet\">"
        + "<Color Token=\"Divider\" Value=\"#21924A\"/>"
        + "</Scheme>"
        + "</Styles>";

    private static UiHost NewHost(string xml, UiTheme theme)
    {
        return new UiHost(
            Scope, UiLayoutManifest.Parse(xml), new UiBindings(), theme, new StubMetrics(), new StubTranslation());
    }

    private static UiLayoutSnapshot Arrange(UiHost host, float width)
    {
        host.DrawFrame(new Rect(0f, 0f, width, 40f));
        return host.MeasureAndArrange(new Vector2(width, 40f));
    }

    private static string? CreationFailure(string xml)
    {
        try
        {
            using var host = NewHost(xml, UiTheme.DarkGold);
            return null;
        }
        catch (Exception ex)
        {
            return ex.GetType().Name + ": " + ex.Message;
        }
    }

    /// <summary>
    /// A refused value has to say which one is accepted: "invalid attribute" alone leaves the author guessing
    /// whether they misspelled the name or the value.
    /// </summary>
    private static void ExpectRefused(string what, string authored)
    {
        string? failure = CreationFailure(Page(" Chrome=\"" + authored + "\""));
        Check(failure != null
                && failure.IndexOf("Chrome", StringComparison.Ordinal) >= 0
                && failure.IndexOf("none", StringComparison.OrdinalIgnoreCase) >= 0,
            what + " is refused at creation, naming the value the kind implements: " + (failure ?? "NO FAILURE"));
    }

    private static bool AllInside(List<Rect> rects, Rect bounds)
    {
        for (int i = 0; i < rects.Count; i++)
        {
            Rect rect = rects[i];
            if (rect.y < bounds.y - 0.001f || rect.yMax > bounds.yMax + 0.001f
                || rect.x < bounds.x - 0.001f || rect.xMax > bounds.xMax + 0.001f)
            {
                return false;
            }
        }

        return true;
    }

    private static void ClearBoxes()
    {
        MethodInfo? clear = typeof(Verse.Widgets).GetMethod("ClearDrawBoxSolidCalls", BindingFlags.Public | BindingFlags.Static);
        if (clear == null) throw new Exception("Verse stub is missing ClearDrawBoxSolidCalls");
        clear.Invoke(null, null);
    }

    private static List<Rect> RecordedSolidRects()
    {
        var rects = (IList?)StubField("DrawBoxSolidRects") ?? throw new Exception("Verse stub is missing DrawBoxSolidRects");
        var found = new List<Rect>();
        for (int i = 0; i < rects.Count; i++)
        {
            if (rects[i] is Rect rect) found.Add(rect);
        }

        return found;
    }

    private static bool AnyRecordedBoxColor(Color wanted)
    {
        return AnyRecordedBoxColorNear(wanted, 0.0001f);
    }

    private static bool AnyRecordedBoxColorNear(Color wanted, float tolerance = 0.01f)
    {
        var colors = (IList?)StubField("DrawBoxSolidColors") ?? throw new Exception("Verse stub is missing DrawBoxSolidColors");
        for (int i = 0; i < colors.Count; i++)
        {
            if (colors[i] is Color color
                && Math.Abs(color.r - wanted.r) <= tolerance
                && Math.Abs(color.g - wanted.g) <= tolerance
                && Math.Abs(color.b - wanted.b) <= tolerance
                && Math.Abs(color.a - wanted.a) <= tolerance)
            {
                return true;
            }
        }

        return false;
    }

    private static bool AnyLabelInside(Rect bounds)
    {
        var rects = (IList?)StubField("LabelRects") ?? throw new Exception("Verse stub is missing LabelRects");
        for (int i = 0; i < rects.Count; i++)
        {
            if (rects[i] is Rect rect
                && rect.x < bounds.xMax && rect.xMax > bounds.x
                && rect.y < bounds.yMax && rect.yMax > bounds.y)
            {
                return true;
            }
        }

        return false;
    }

    private static object StubField(string name)
    {
        FieldInfo? field = typeof(Verse.Widgets).GetField(name, BindingFlags.Public | BindingFlags.Static);
        if (field == null) throw new Exception("Verse stub is missing the recording hook " + name);
        return field.GetValue(null)!;
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

    private sealed class StubMetrics : ITextMetrics
    {
        public float MeasureText(string text, UiFont font, float width)
        {
            return string.IsNullOrEmpty(text) ? 0f : StubTextWidth.Of(text, font);
        }

        public float MeasureWidth(string text, UiFont font)
        {
            return string.IsNullOrEmpty(text) ? 0f : StubTextWidth.Of(text, font);
        }
    }

    private sealed class StubTranslation : IUiTranslation
    {
        public string Translate(string key) => "[" + key + "]";

        public int TranslationRevision => 0;
    }
}
