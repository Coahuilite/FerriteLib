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
/// the one that fires for wrapped text, that a paragraph which was given room for its wrapped lines is
/// NOT reported, that the width axis only applies to declared-single-line labels, and that findings are
/// deduplicated, scoped, and bounded. Absolute pixel truth still belongs to the real font engine.
/// </summary>
internal static class KernelTextAuditTests
{
    // Stub heights in ModelMetrics: Tiny 16, Small 24, Medium 32 (independent of the string).
    private const float TinyBand = 16f;

    private static int failures;

    public static int RunAll()
    {
        failures = 0;
        Run("Enable flag gates the measuring cost", VerifyDisabledStaysQuiet);
        Run("Single-line label reports width need against the rect", VerifyWidthFinding);
        Run("Repeated frames deduplicate", VerifyDeduplication);
        Run("Element scope attributes the finding", VerifyElementScope);
        Run("Wrapped text taller than its band reports height", VerifyHeightFinding);
        Run("Paragraph given room for its lines is never reported", VerifyWrappedParagraphIsSilent);
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
        Draw(new Rect(0f, 0f, 10f, TinyBand), "abcd", UiFont.Tiny, singleLine: true);

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
            Draw(new Rect(0f, 0f, 10f, TinyBand), "same text every frame", UiFont.Tiny, singleLine: true);
        }

        Check(reports.Count == 1, "A page redrawn 60 times per second reports one finding, not 60");
        Stop();
    }

    private static void VerifyElementScope()
    {
        List<UiOverflowReport> reports = Start();

        UiFitAudit.BeginElement("page-root/body-row/nav-column/nav");
        Draw(new Rect(0f, 0f, 10f, TinyBand), "scoped overflow", UiFont.Tiny, singleLine: true);
        UiFitAudit.EndElement();
        Draw(new Rect(0f, 0f, 10f, TinyBand), "unscoped overflow", UiFont.Tiny, singleLine: true);

        Check(reports.Count == 2, "Scoped and unscoped findings are distinct");
        Check(reports.Count == 2 && reports[0].ElementPath == "page-root/body-row/nav-column/nav",
            "Finding carries the element path the layout pass announced");
        Check(reports.Count == 2 && reports[1].ElementPath == "(unscoped)",
            "EndElement clears the scope so stray text is not misattributed");
        Stop();
    }

    private static void VerifyHeightFinding()
    {
        List<UiOverflowReport> reports = Start();

        // Wrapping label given an 10px band: the wrapped text still needs a full 16px line, so the tail
        // of the string is never drawn. This is the defect class that a width check would miss entirely.
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
        // width is enormous by construction. As long as the band holds its wrapped lines, this is not a
        // defect and must never be reported.
        string paragraph = new string('x', 400);
        Draw(new Rect(0f, 0f, 100f, TinyBand), paragraph, UiFont.Tiny);
        Draw(new Rect(0f, 0f, 100f, 40f), paragraph, UiFont.Small);

        Check(reports.Count == 0, "A paragraph that was given room for its lines is not a finding");
        Stop();
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

    /// <summary>Font-constant heights (the lane convention) plus the shared half-width model.</summary>
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
}
