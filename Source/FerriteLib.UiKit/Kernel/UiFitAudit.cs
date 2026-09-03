using System;
using System.Collections.Generic;

using UnityEngine;

namespace FerriteLib.UiKit.Kernel;

/// <summary>Which dimension of a label rect the text failed to fit into.</summary>
public enum UiOverflowAxis
{
    /// <summary>The label declared itself single-line but needs more width than the rect has.</summary>
    Width,

    /// <summary>The text wrapped into more lines than the band has height for; the tail is never drawn.</summary>
    Height
}

/// <summary>One distinct text-fitting failure, sampled from the real draw pass.</summary>
public readonly struct UiOverflowReport
{
    public UiOverflowReport(
        string elementPath,
        string text,
        UiFont font,
        UiOverflowAxis axis,
        float needed,
        float available,
        float rectWidth = 0f)
    {
        ElementPath = elementPath;
        Text = text;
        Font = font;
        Axis = axis;
        Needed = needed;
        Available = available;
        RectWidth = rectWidth;
    }

    public string ElementPath { get; }

    public string Text { get; }

    public UiFont Font { get; }

    public UiOverflowAxis Axis { get; }

    public float Needed { get; }

    public float Available { get; }

    /// <summary>
    /// Width of the rect the text was laid out in. A height finding is only explainable next to the
    /// width it wrapped at: need/have alone cannot tell a band that is too short from a column that is
    /// too narrow. Diagnostic only — the shipped usdiag record does not carry it.
    /// </summary>
    public float RectWidth { get; }
}

/// <summary>
/// Opt-in text-fitting audit for the draw pass.
///
/// Layout can only promise a rect; it cannot know that a translated string grew past it. Rather than
/// ask every widget to check its own strings (which is how a second, half-hearted convention starts),
/// the audit sits on the single text outlet <see cref="UiThemeDraw.Label"/> and compares measured need
/// against the rect actually handed to it. The layout pass announces the owning element through
/// <see cref="BeginElement"/> so each finding is addressable by element path.
///
/// Disabled by default: every measurement costs a real text-generation pass, so the host enables it
/// only while diagnosing. Once <see cref="MaxReports"/> distinct findings are collected the audit goes
/// quiet on its own, which bounds both the log and the per-frame cost of a pathological page.
/// </summary>
public static class UiFitAudit
{
    /// <summary>Distinct findings collected before the audit stops measuring. Bounds per-frame cost.</summary>
    public const int MaxReports = 48;

    // Absorbs sub-pixel disagreement between the measuring pass and the drawing pass.
    private const float Tolerance = 1.5f;

    // Findings are keyed by text so a page drawn at 60 fps reports each defect once, not 60 times.
    private const int TextKeyBudget = 40;

    private static readonly HashSet<string> Reported = new(StringComparer.Ordinal);

    private static ITextMetrics? metrics;
    private static Action<UiOverflowReport>? sink;
    private static string currentPath = string.Empty;

    /// <summary>Master switch. Off means the audit does not measure anything at all.</summary>
    public static bool Enabled { get; set; }

    /// <summary>True once the distinct-finding budget is spent; the audit then stops measuring.</summary>
    public static bool Saturated => Reported.Count >= MaxReports;

    /// <summary>Number of distinct findings collected since the last <see cref="Reset"/>.</summary>
    public static int ReportedCount => Reported.Count;

    /// <summary>Binds the measuring implementation and the reporting callback. Does not enable the audit.</summary>
    public static void Attach(ITextMetrics textMetrics, Action<UiOverflowReport> reportSink)
    {
        metrics = textMetrics ?? throw new ArgumentNullException(nameof(textMetrics));
        sink = reportSink ?? throw new ArgumentNullException(nameof(reportSink));
    }

    /// <summary>Drops the binding and disables the audit, so a disposed host cannot leave a dangling sink.</summary>
    public static void Detach()
    {
        Enabled = false;
        metrics = null;
        sink = null;
        currentPath = string.Empty;
    }

    /// <summary>Forgets collected findings. A new window session should re-report defects it can still reproduce.</summary>
    public static void Reset()
    {
        Reported.Clear();
        currentPath = string.Empty;
    }

    /// <summary>Marks the element whose draw pass is about to run. Called by the layout pass only.</summary>
    public static void BeginElement(string elementPath)
    {
        if (!Enabled) return;
        currentPath = elementPath ?? string.Empty;
    }

    /// <summary>Clears the element scope so text drawn outside the page is attributed to no element.</summary>
    public static void EndElement()
    {
        if (!Enabled) return;
        currentPath = string.Empty;
    }

    /// <summary>
    /// Compares one label's measured need against its rect.
    ///
    /// The default check is vertical, and that ordering is the substance of the audit: Verse's label
    /// drawing wraps text into the rect, so an ordinary paragraph's unwrapped width always exceeds its
    /// box. A width-first check would therefore flag every multi-line string as a defect while hiding the
    /// one that actually cuts text away — wrapped lines that no longer fit the band. Height need versus
    /// band height is the signal for real clipping. The width axis is reserved for labels whose owner
    /// declared them single-line, where wrapping is not an available answer and truncation is silent.
    /// </summary>
    public static void Check(Rect rect, string text, UiFont font, bool singleLine)
    {
        if (!Enabled) return;

        ITextMetrics? textMetrics = metrics;
        if (textMetrics == null || Saturated) return;
        if (string.IsNullOrEmpty(text) || rect.width <= 1f || rect.height <= 1f) return;

        if (singleLine)
        {
            float neededWidth = textMetrics.MeasureWidth(text, font);
            if (neededWidth > rect.width + Tolerance)
            {
                Report(rect, text, font, UiOverflowAxis.Width, neededWidth, rect.width);
            }

            return;
        }

        float neededHeight = textMetrics.MeasureText(text, font, rect.width);
        if (neededHeight > rect.height + Tolerance)
        {
            Report(rect, text, font, UiOverflowAxis.Height, neededHeight, rect.height);
        }
    }

    private static void Report(
        Rect rect,
        string text,
        UiFont font,
        UiOverflowAxis axis,
        float needed,
        float available)
    {
        string key = currentPath + "|" + (int)axis + "|" + (int)font + "|" + Shorten(text);
        if (!Reported.Add(key)) return;

        sink?.Invoke(new UiOverflowReport(
            currentPath.Length == 0 ? "(unscoped)" : currentPath,
            text,
            font,
            axis,
            needed,
            available,
            rect.width));
    }

    private static string Shorten(string text)
    {
        return text.Length <= TextKeyBudget ? text : text.Substring(0, TextKeyBudget);
    }
}
