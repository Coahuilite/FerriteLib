using System;
using System.Collections.Generic;

using UnityEngine;

using FerriteLib.UiKit.Kernel;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// Text-fit audit lane. Layout can promise a rect but not that a string fits it, so the audit is the
/// only machinery that turns "this label might be clipped" into an addressable finding. These checks
/// prove the wiring and the reporting policy through the real drawing outlet
/// (<see cref="UiThemeDraw.Label"/>): that the audit is silent until enabled, that the vertical axis is
/// the one that fires for wrapped text, that a paragraph whose band holds its wrapped lines is NOT
/// reported AND that one whose band does not is, that the width axis only applies to declared-single-line
/// labels, and that findings are deduplicated, scoped, and bounded.
///
/// Two kinds of assertion live here, and the difference matters when reading a green run. The width-axis
/// and wiring ones are PLUMBING: they hold under any ruler that reports a number larger than the rect. The
/// wrapped-text pair is the only WRAPPING evidence, and it needs both halves - the silent case and the
/// overflowing negative control - because a ruler that cannot count lines passes the silent case by
/// construction. This lane carried a constant height ruler until 2026-09-24 and its paragraph fixture
/// was sized from that ruler's return value; the sizes below are derived from the claim instead (§14.31).
///
/// Absolute pixel truth still belongs to the real font engine; the numbers here are the harness convention,
/// calibrated from in-game ui.text.overflow need values.
/// </summary>
internal static class KernelTextAuditTests
{
    // Only the declared-single-line draws use this band, and those consult the WIDTH axis - the audit returns
    // before it looks at height, and skips rects of height <= 1 outright. This is therefore not a text
    // measurement and must not be read as one; it used to be 16f, which happened to be the dead height
    // ruler's constant (T27). Nothing here depends on its value beyond being > 1.
    private const float SingleLineBand = 16f;

    private static int failures;

    public static int RunAll()
    {
        failures = 0;
        Run("Enable flag gates the measuring cost", VerifyDisabledStaysQuiet);
        Run("Single-line label reports width need against the rect", VerifyWidthFinding);
        Run("Repeated frames deduplicate", VerifyDeduplication);
        Run("Element scope attributes the finding", VerifyElementScope);
        Run("An unscoped finding carries length and rect width", VerifyUnscopedDiscriminators);
        Run("Wrapped text taller than its band reports height", VerifyHeightFinding);
        Run("Paragraph given room for its lines is never reported", VerifyWrappedParagraphIsSilent);
        Run("A paragraph the band cannot hold IS reported", VerifyWrappedParagraphOverflowIsReported);
        Run("The lane's fixture arithmetic still matches the ruler", VerifyFixtureArithmeticMatchesTheRuler);
        Run("The two harness width implementations agree", VerifyStubWidthModelMatchesTheSharedOne);
        Run("Fitting text reports nothing", VerifyFittingTextIsSilent);
        Run("Finding budget bounds the audit", VerifySaturation);
        Run("Detaching unbinds the sink", VerifyDetach);
        return failures;
    }

    private static void VerifyDisabledStaysQuiet()
    {
        var reports = new List<UiOverflowReport>();
        UiFitAudit.Attach(new ModelMetrics(), reports.Add);
        UiFitAudit.Reset();

        Draw(new Rect(0f, 0f, 100f, 8f), "too tall for eight pixels", UiFont.Tiny);
        Check(reports.Count == 0, "Attached but not enabled: the audit must not report");

        UiFitAudit.Enabled = true;
        Draw(new Rect(0f, 0f, 100f, 8f), "too tall for eight pixels", UiFont.Tiny);
        Check(reports.Count == 1, "Enabling is what turns measurement on");

        UiFitAudit.Enabled = false;
        Draw(new Rect(0f, 0f, 100f, 8f), "different overflowing text", UiFont.Tiny);
        Check(reports.Count == 1, "Disabling stops the per-frame text-generation cost");
        Stop();
    }

    private static void VerifyWidthFinding()
    {
        List<UiOverflowReport> reports = Start();

        // A single-line label: 4 Latin units at Tiny (12px em, half-width advance) = 24px of need.
        Draw(new Rect(0f, 0f, 10f, SingleLineBand), "abcd", UiFont.Tiny, singleLine: true);

        Check(reports.Count == 1, "One width finding for an overflowing single-line label");
        if (reports.Count != 1)
        {
            Stop();
            return;
        }

        UiOverflowReport report = reports[0];
        Check(report.Axis == UiOverflowAxis.Width, "Failing axis is Width");
        Check(Near(24f, report.Needed), "Needed width comes from the injected metrics (got " + report.Needed + ")");
        Check(Near(10f, report.Available), "Available width is the rect handed to the label");
        Check(report.ElementPath == "(unscoped)", "Unscoped drawing is attributed as unscoped, got " + report.ElementPath);
        Stop();
    }

    private static void VerifyDeduplication()
    {
        List<UiOverflowReport> reports = Start();

        for (int frame = 0; frame < 4; frame++)
        {
            Draw(new Rect(0f, 0f, 10f, SingleLineBand), "same text every frame", UiFont.Tiny, singleLine: true);
        }

        Check(reports.Count == 1, "A page redrawn 60 times per second reports one finding, not 60");
        Stop();
    }

    private static void VerifyElementScope()
    {
        List<UiOverflowReport> reports = Start();

        UiFitAudit.BeginElement("page-root/body-row/nav-column/nav");
        Draw(new Rect(0f, 0f, 10f, SingleLineBand), "scoped overflow", UiFont.Tiny, singleLine: true);
        UiFitAudit.EndElement();
        Draw(new Rect(0f, 0f, 10f, SingleLineBand), "unscoped overflow", UiFont.Tiny, singleLine: true);

        Check(reports.Count == 2, "Scoped and unscoped findings are distinct");
        Check(reports.Count == 2 && reports[0].ElementPath == "page-root/body-row/nav-column/nav",
            "Finding carries the element path the layout pass announced");
        Check(reports.Count == 2 && reports[1].ElementPath == "(unscoped)",
            "EndElement clears the scope so stray text is not misattributed");
        Stop();
    }

    /// <summary>
    /// A finding outside any element scope cannot say where it came from — two windows can be on screen at
    /// once — and the caller that logs it may not be allowed to put UI text into the record. The two
    /// non-content discriminators are what makes that branch decidable anyway: the length separates the
    /// candidates and the rect width says which box it was drawn into. This lane holds both, and holds that
    /// they are the values of the string and rect that actually overflowed rather than incidental numbers.
    /// </summary>
    private static void VerifyUnscopedDiscriminators()
    {
        List<UiOverflowReport> reports = Start();

        // Two labels of different lengths in the same rect: one field must separate them.
        string longLabel = "a close affordance label that does not fit";
        Draw(new Rect(0f, 0f, 12f, SingleLineBand), longLabel, UiFont.Tiny, singleLine: true);
        Draw(new Rect(0f, 0f, 12f, SingleLineBand), "short", UiFont.Tiny, singleLine: true);

        Check(reports.Count == 2, "Both unscoped findings are reported (got " + reports.Count + ")");
        if (reports.Count != 2)
        {
            Stop();
            return;
        }

        Check(reports[0].ElementPath == "(unscoped)" && reports[1].ElementPath == "(unscoped)",
            "Neither finding claims an element it cannot know");
        Check(reports[0].TextLength == longLabel.Length && reports[0].TextLength != reports[1].TextLength,
            "text_len separates the two candidates without carrying either string ("
            + reports[0].TextLength + " vs " + reports[1].TextLength + ")");
        Check(Near(12f, reports[0].RectWidth) && Near(12f, reports[1].RectWidth),
            "rect_width names the box the label was actually drawn into");
        Check(reports[0].Axis == UiOverflowAxis.Width,
            "and the width axis is the one a declared-single-line label reports");
        Stop();
    }

    private static void VerifyHeightFinding()
    {
        List<UiOverflowReport> reports = Start();

        // PLUMBING, not wrapping evidence: "ab" in a 100px band is ONE line, so what this proves is that a
        // ruler reporting need > available is routed to the Height axis with the band as Available. It stays
        // green under a wrap-aware ruler for the same one-line reason (measured 2026-09-24, T27). The
        // wrapping evidence is the paragraph pair below.
        Draw(new Rect(0f, 0f, 100f, 10f), "ab", UiFont.Tiny);

        Check(reports.Count == 1, "One height finding");
        Check(reports.Count == 1 && reports[0].Axis == UiOverflowAxis.Height, "Failing axis is Height");
        Check(reports.Count == 1 && Near(10f, reports[0].Available), "Available height is the band handed over");
        Stop();
    }

    private static void VerifyWrappedParagraphIsSilent()
    {
        List<UiOverflowReport> reports = Start();

        // The guard against the obvious wrong design: Verse wraps labels, so a long paragraph's unwrapped
        // width is enormous by construction, and a band that HOLDS ITS WRAPPED LINES is not a defect. The
        // band is therefore derived from that claim - the wrapped line count and the calibrated line height,
        // written out in WrappedHeight - and deliberately not from the ruler: a fixture that asked the ruler
        // how tall to be could never fail. It used to draw into a 40px band and SingleLineBand, sizes copied from
        // the world of a ruler that answered one line for every string, which is why it was green for a
        // reason unrelated to its claim (measured 2026-09-24, T27; §14.31).
        const float width = 100f;
        string paragraph = new string('x', 400);
        Draw(new Rect(0f, 0f, width, WrappedHeight(paragraph, UiFont.Tiny, width) + 1f), paragraph, UiFont.Tiny);
        Draw(new Rect(0f, 0f, width, WrappedHeight(paragraph, UiFont.Small, width) + 1f), paragraph, UiFont.Small);

        Check(reports.Count == 0, "A paragraph whose band holds its wrapped lines is not a finding");
        Stop();
    }

    /// <summary>
    /// NEGATIVE CONTROL for the fixture above, and the only reason this lane can see a dead ruler at all: a
    /// ruler that answers one line's height no matter how long the string is fits any band of this size, so
    /// without this case a constant ruler would pass the silent half forever. Both halves are needed - the
    /// silent one states the claim, this one states that measuring still happens.
    /// </summary>
    private static void VerifyWrappedParagraphOverflowIsReported()
    {
        const float width = 100f;
        string paragraph = new string('x', 400);
        List<UiOverflowReport> reports = Start();

        Draw(new Rect(0f, 0f, width, WrappedHeight(paragraph, UiFont.Small, width) * 0.5f), paragraph, UiFont.Small);

        Check(reports.Count == 1, "A band half the paragraph's wrapped height is reported (got " + reports.Count + ")");
        Check(reports.Count == 1 && reports[0].Axis == UiOverflowAxis.Height, "and it is the Height axis");
        Stop();
    }

    /// <summary>
    /// DIAGNOSTIC - it sizes nothing. The fixtures above derive their bands from <see cref="WrappedHeight"/>,
    /// so if a future edit changes the ruler's height model without changing that arithmetic (or the
    /// reverse), the fixtures would quietly start asserting something other than their claim. This makes that
    /// drift noisy instead of silent. It is the counterpart of the negative control: that one catches a ruler
    /// that cannot fail, this one catches a ruler that changed without its fixtures noticing.
    /// </summary>
    private static void VerifyFixtureArithmeticMatchesTheRuler()
    {
        var ruler = new ModelMetrics();
        string paragraph = new string('x', 400);

        float derived = WrappedHeight(paragraph, UiFont.Small, 100f);
        float measured = ruler.MeasureText(paragraph, UiFont.Small, 100f);

        Check(Math.Abs(derived - measured) < 0.01f,
            "the fixture arithmetic and the ruler still agree (" + derived.ToString("0.##") + " vs "
            + measured.ToString("0.##") + ")");
    }

    private static void VerifyFittingTextIsSilent()
    {
        List<UiOverflowReport> reports = Start();

        Draw(new Rect(0f, 0f, 200f, 40f), "fits", UiFont.Small);
        Draw(new Rect(0f, 0f, 200f, 40f), "fits", UiFont.Small, singleLine: true);
        Draw(new Rect(0f, 0f, 200f, 40f), "", UiFont.Small);
        Draw(new Rect(0f, 0f, 0f, 40f), "zero width", UiFont.Small);

        Check(reports.Count == 0, "Fitting, empty, and degenerate rects produce no findings");
        Stop();
    }

    private static void VerifySaturation()
    {
        List<UiOverflowReport> reports = Start();

        for (int i = 0; i < UiFitAudit.MaxReports + 12; i++)
        {
            Draw(new Rect(0f, 0f, 4f, 4f), "distinct text " + i.ToString(), UiFont.Medium);
        }

        Check(reports.Count == UiFitAudit.MaxReports,
            "Audit stops at the finding budget (got " + reports.Count + ")");
        Check(UiFitAudit.Saturated, "Saturated flag is observable for the host");
        Stop();
    }

    private static void VerifyDetach()
    {
        List<UiOverflowReport> reports = Start();
        Draw(new Rect(0f, 0f, 4f, 4f), "before detach", UiFont.Medium);
        Check(reports.Count == 1, "Audit reports while attached");

        UiFitAudit.Detach();
        UiFitAudit.Enabled = true;
        Draw(new Rect(0f, 0f, 4f, 4f), "after detach", UiFont.Medium);
        Check(reports.Count == 1, "A detached audit has no sink to report to, even if re-enabled");

        UiFitAudit.Detach();
        Check(!UiFitAudit.Enabled, "Detach also clears the enable flag so a disposed host leaves nothing armed");
    }

    private static List<UiOverflowReport> Start()
    {
        var reports = new List<UiOverflowReport>();
        UiFitAudit.Attach(new ModelMetrics(), reports.Add);
        UiFitAudit.Reset();
        UiFitAudit.Enabled = true;
        return reports;
    }

    private static void Stop()
    {
        UiFitAudit.Detach();
    }

    private static void Check(bool condition, string name)
    {
        if (condition)
        {
            Console.WriteLine("  ok: " + name);
            return;
        }

        failures++;
        Console.Error.WriteLine("  FAIL: " + name);
    }

    private static void Draw(Rect rect, string text, UiFont font, bool singleLine = false)
    {
        UiThemeDraw.Label(rect, text, UiTheme.DarkGold, null, font, TextAnchor.MiddleLeft, singleLine);
    }

    private static bool Near(float expected, float actual)
    {
        return Math.Abs(expected - actual) <= 0.001f;
    }

    /// <summary>
    /// The half-width convention exists TWICE: this project's <see cref="StubTextWidth"/>, and the inline
    /// model the Verse stub's <c>Text.CalcSize</c> carries. The second one is what
    /// <see cref="VerseFerriteTextMetrics"/> walks, so it is what a production host measures with - and until
    /// now nothing compared them, which means an edit to either would have been a silent divergence between
    /// what these lanes measure and what a game-side host measures. Same strings, same fonts, both paths, one
    /// number. (The exact glyph advances exist only in the game, so this holds the two HARNESS models
    /// together; it says nothing about the real font.)
    /// </summary>
    private static void VerifyStubWidthModelMatchesTheSharedOne()
    {
        var production = new VerseFerriteTextMetrics();
        string[] samples = { "abcd", "中文字符", "mixed 中文 text", "\u2212+", new string('x', 40) };
        UiFont[] fonts = { UiFont.Tiny, UiFont.Small, UiFont.Medium };

        foreach (UiFont font in fonts)
        {
            foreach (string sample in samples)
            {
                float shared = StubTextWidth.Of(sample, font);
                float stub = production.MeasureWidth(sample, font);
                Check(Near(shared, stub),
                    "the shared width model and the Verse stub agree for " + font + " on "
                    + sample.Length + " char(s) (" + shared.ToString("0.###") + " vs " + stub.ToString("0.###") + ")");
            }
        }
    }

    /// <summary>
    /// The wrapped height a fixture's CLAIM implies, computed in the lane: the half-width advance divided by
    /// the width, rounded up, times the calibrated one-line height. What must never be copied from the
    /// instrument under test is how TALL it thinks the text is, because then the fixture could not fail;
    /// <see cref="VerifyFixtureArithmeticMatchesTheRuler"/> keeps that copy and the ruler from drifting apart
    /// in silence.
    ///
    /// The ADVANCE comes from this project's half-width model, <see cref="StubTextWidth"/> - and that model
    /// has a second implementation, the inline one inside the Verse stub's <c>Text.CalcSize</c>, which is the
    /// one the consumer's production path walks. Two implementations of one convention is exactly the drift
    /// this batch is about, so <see cref="VerifyStubWidthModelMatchesTheSharedOne"/> compares them rather
    /// than trusting that they were written the same way.
    /// </summary>
    private static float WrappedHeight(string text, UiFont font, float width)
    {
        float advance = StubTextWidth.Of(text ?? "", font);
        int lines = Math.Max(1, (int)Math.Ceiling(advance / Math.Max(1f, width)));
        return lines * CalibratedLineHeight(font);
    }

    /// <summary>
    /// The calibrated one-line heights, the claim's other half: from in-game <c>ui.text.overflow</c> need
    /// values (Tiny 18.0, Small 21.33333, Medium 30.0). <b>Large is not measured</b> and borrows Small's
    /// until someone calibrates it.
    /// </summary>
    private static float CalibratedLineHeight(UiFont font) => font switch
    {
        UiFont.Tiny => 18f,
        UiFont.Medium => 30f,
        _ => 21.33333f
    };

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
    /// The harness text convention in BOTH axes: a line is <see cref="CalibratedLineHeight"/> tall, a CJK
    /// ideograph or full-width punctuation is one em and anything else half an em - so the height a string
    /// needs depends on the text, the font AND the width, never on the font alone. The font-constant version
    /// this replaces could not tell a wrapped paragraph from a single line, which made every height assertion
    /// in this lane a statement about the rect (measured 2026-09-24, T27).
    /// </summary>
    private sealed class ModelMetrics : ITextMetrics
    {
        public float MeasureText(string text, UiFont font, float width)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            return WrappedHeight(text, font, width);
        }

        public float MeasureWidth(string text, UiFont font) => StubTextWidth.Of(text, font);
    }
}
