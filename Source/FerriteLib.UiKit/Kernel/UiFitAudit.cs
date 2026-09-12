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
        TextLength = (text ?? "").Length;
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

    /// <summary>
    /// Length of the overflowing text in UTF-16 code units, and the second of the two non-content
    /// discriminators this record carries (the other is <see cref="RectWidth"/>).
    /// <para>
    /// <b>Why a length instead of the text.</b> A finding outside any element scope is attributed to
    /// <c>"(unscoped)"</c>, and a caller that must not put UI text into a log — the prudent default for
    /// a record another mod's diagnostic pipeline may ship — is then left unable to say which of several
    /// on-screen strings produced it. Length plus rect width is enough to pin that down in practice: a
    /// twenty-four unit literal, a thirteen unit translation and a twenty-six unit literal are three
    /// different findings, and the width axis only fires for labels their owner declared single-line.
    /// Neither field is content, so adding them does not change what a record may safely carry.
    /// </para>
    /// </summary>
    public int TextLength { get; }
}

/// <summary>
/// One distinct appearance fallback: an authored role value that is not in the vocabulary, sampled from
/// the real draw pass. It exists because the value's own layer cannot report it usefully — the theme's
/// resolved-value table sees enumerated keys only, so the authored text and the element it was written
/// on are known here and nowhere else.
/// </summary>
public readonly struct UiStyleFallbackReport
{
    public UiStyleFallbackReport(string elementPath, string kind, string attribute, string authored, string resolved)
    {
        ElementPath = elementPath;
        Kind = kind;
        Attribute = attribute;
        Authored = authored;
        Resolved = resolved;
    }

    /// <summary>Element path the declaration was found on, so the author knows where to fix it.</summary>
    public string ElementPath { get; }

    /// <summary>The kind whose schema accepted the attribute.</summary>
    public string Kind { get; }

    /// <summary>The attribute name: Tone or Emphasis.</summary>
    public string Attribute { get; }

    /// <summary>The value as authored, exactly as the manifest spelled it.</summary>
    public string Authored { get; }

    /// <summary>The declared value it resolved to instead.</summary>
    public string Resolved { get; }

    /// <summary>The one line a host logs when it wants this finding in a log rather than in a sink.</summary>
    public string Diagnostic =>
        ElementPath + ": " + Kind + " " + Attribute + "=\"" + Authored + "\" is not a declared value; fell back to " + Resolved + ".";
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
/// <para>
/// The appearance half — an authored role value outside the vocabulary — lives on this same surface and
/// carries the same lifecycle deliberately: it is bounded by <see cref="MaxReports"/>, forgotten by
/// <see cref="Reset"/>, and has its sink and last diagnostic dropped by <see cref="Detach"/>, so a host
/// that opens windows for hours accumulates nothing across them. It is not gated by
/// <see cref="Enabled"/> (resolving a name costs one comparison, and fail-soft must not mean silent) and
/// it is not a monotonic global: enable, reset and detach are the whole of its life.
/// </para>
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

    // Appearance fallbacks are keyed like the text findings — element, kind, attribute, authored value —
    // so a page drawn at 60 fps records each typo once, not 60 times.
    private static readonly HashSet<string> StyleFallbacks = new(StringComparer.Ordinal);

    private static ITextMetrics? metrics;
    private static Action<UiOverflowReport>? sink;
    private static Action<UiStyleFallbackReport>? styleSink;
    private static string? lastStyleFallback;
    private static string currentPath = string.Empty;

    /// <summary>Master switch. Off means the audit does not measure anything at all.</summary>
    public static bool Enabled { get; set; }

    /// <summary>True once the distinct-finding budget is spent; the audit then stops measuring.</summary>
    public static bool Saturated => Reported.Count >= MaxReports;

    /// <summary>Number of distinct findings collected since the last <see cref="Reset"/>.</summary>
    public static int ReportedCount => Reported.Count;

    /// <summary>
    /// Distinct appearance fallbacks recorded since the last <see cref="Reset"/>. Unlike the text half
    /// this count is live whether or not the audit is enabled: resolving an authored name costs one
    /// comparison, so "fail-soft must not mean silent" does not depend on a host opting in. It stops
    /// growing at <see cref="MaxReports"/>, which is how a caller can tell the bound was reached.
    /// </summary>
    public static int StyleFallbackCount => StyleFallbacks.Count;

    /// <summary>
    /// The diagnostic of the most recent appearance fallback, or null when nothing has fallen back.
    /// Cumulative like <see cref="StyleFallbackCount"/>: it is a report to read, not frame state.
    /// </summary>
    public static string? LastStyleFallbackDiagnostic => lastStyleFallback;

    /// <summary>Binds the measuring implementation and the reporting callback. Does not enable the audit.</summary>
    public static void Attach(ITextMetrics textMetrics, Action<UiOverflowReport> reportSink)
    {
        metrics = textMetrics ?? throw new ArgumentNullException(nameof(textMetrics));
        sink = reportSink ?? throw new ArgumentNullException(nameof(reportSink));
    }

    /// <summary>
    /// Binds the appearance-fallback sink. Separate from <see cref="Attach"/> on purpose: the text half
    /// costs a measurement per label and is opt-in, while an appearance fallback is already known by the
    /// time it happens, so a host may want this half without the measuring one. Both sinks are dropped by
    /// <see cref="Detach"/>.
    /// </summary>
    public static void AttachStyleFallback(Action<UiStyleFallbackReport> styleReportSink)
    {
        styleSink = styleReportSink ?? throw new ArgumentNullException(nameof(styleReportSink));
    }

    /// <summary>Drops the bindings and disables the audit, so a disposed host cannot leave a dangling sink.</summary>
    public static void Detach()
    {
        Enabled = false;
        metrics = null;
        sink = null;
        styleSink = null;
        lastStyleFallback = null;
        currentPath = string.Empty;
    }

    /// <summary>Forgets collected findings. A new window session should re-report defects it can still reproduce.</summary>
    public static void Reset()
    {
        Reported.Clear();
        StyleFallbacks.Clear();
        lastStyleFallback = null;
        currentPath = string.Empty;
    }

    /// <summary>
    /// Marks the element whose draw pass is about to run. The layout pass calls it per arranged entry, and
    /// the window shell calls it for the text it draws outside the page tree — its chrome and the notice
    /// band, under <c>&lt;windowType&gt;/chrome</c> and <c>&lt;windowType&gt;/notice</c> — because those
    /// labels have no element identity of their own and an <c>"(unscoped)"</c> finding cannot say which of
    /// two open windows produced it.
    /// </summary>
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

    /// <summary>
    /// Records one appearance fallback: an authored role value outside the vocabulary, resolved to the
    /// default row. Called by the atom vocabulary as it resolves a role, which is the only layer that
    /// still knows the authored text and the element it belongs to. The counter and the last diagnostic
    /// move on every occurrence; the sink fires once per distinct finding, bounded by
    /// <see cref="MaxReports"/> so a pathological page cannot grow this without limit.
    /// </summary>
    internal static void ReportStyleFallback(string elementPath, string kind, string attribute, string authored, string resolved)
    {
        var report = new UiStyleFallbackReport(
            string.IsNullOrEmpty(elementPath) ? "(unscoped)" : elementPath,
            kind ?? "",
            attribute ?? "",
            authored ?? "",
            resolved ?? "");

        lastStyleFallback = report.Diagnostic;

        string key = report.ElementPath + "|" + report.Kind + "|" + report.Attribute + "|" + report.Authored;
        if (StyleFallbacks.Count >= MaxReports) return;
        if (!StyleFallbacks.Add(key)) return;

        styleSink?.Invoke(report);
    }
}
