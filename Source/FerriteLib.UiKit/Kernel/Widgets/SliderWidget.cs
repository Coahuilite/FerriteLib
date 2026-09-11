using System;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel.Widgets;

/// <summary>
/// Core leaf atom <c>input/slider</c>: a bare slider bound to one float, with an optional measured
/// label band.
/// <para>
/// <b>Why this earns a kind</b> — the geometry rule over the label band, plus the value contract.
/// The band is measured from the real glyph advance of the resolved label (the reshape US→FL round 3
/// forced on <c>input/stepper-slider</c>, here as the atom's own rule), and the track is exactly what
/// the label leaves; a fixed label column is the defect that produced that item. The value half is
/// what the manifest cannot express either: a typed float binding key, the clamp window, and the
/// session-owned value state the native control keeps its drag continuity in — the same state
/// <c>input/number-field</c> reads, so the two atoms on one binding cannot disagree.
/// </para>
/// </summary>
public sealed class SliderWidget : IUiWidget
{
    public const string Kind = "input/slider";


    private UiElementSpec spec = UiElementSpec.Empty;

    string IUiWidget.Kind => Kind;

    public static void Register()
    {
        UiWidgetRegistry.Register(
            UiWidgetRegistry.CoreScope,
            Kind,
            () => new SliderWidget(),
            AtomVocabulary.Schema(AtomRoles.ToneAndEmphasis, "Bind", "Min", "Max", "Label", "LabelKey", "Height"),
            new[] { "Label", "LabelKey" });
    }

    public void Configure(UiElementSpec spec)
    {
        this.spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public void Validate(IUiBindings bindings, string elementPath)
    {
        string bindKey = AtomVocabulary.ReadBindKey(spec);
        if (string.IsNullOrEmpty(bindKey))
        {
            throw new InvalidOperationException(
                $"SliderWidget at '{elementPath}' requires a non-empty Id or Bind to use as its typed binding key.");
        }

        bindings.ValidateValue<float>(bindKey, elementPath);
    }

    public float Measure(UiWidgetContext ctx)
    {
        return AtomVocabulary.ReadHeight(spec, ctx.Theme.Geometry.RowHeight);
    }

    public void Draw(Rect rect, UiWidgetContext ctx)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;

        string bindKey = AtomVocabulary.ReadBindKey(spec);
        float min = AtomVocabulary.ReadFloat(spec, "Min", 0f);
        float max = AtomVocabulary.ReadFloat(spec, "Max", 1f);
        float current = ctx.Bindings.TryGet(bindKey, out float bound) ? bound : min;

        Rect track = rect;
        ResolveLabelBand(rect, ctx, out track);
        UiResolvedStyle style = AtomVocabulary.ResolveRole(spec, ctx, AtomVocabulary.WritableOf(ctx, bindKey));
        DrawLabel(rect, ctx, style);

        // The binding's value is clamped into the declared window before the native control sees it:
        // a host value outside [Min, Max] would otherwise arrive as the control's starting point and
        // be written back only on the next change.
        float value = UiNative.Slider(
            track, bindKey, ctx.Session, UiNative.ClampValue(current, min, max), min, max, out bool changed);
        if (changed)
        {
            ctx.Bindings.Set(bindKey, value);
        }
    }

    /// <summary>
    /// The label band: the string's own glyph advance plus the theme's spacing, measured with the font
    /// the label is drawn in, so draw and measure can never resolve two sizes. The track keeps what the
    /// band leaves — a constant column is exactly what round 3 removed.
    /// </summary>
    private void ResolveLabelBand(Rect rect, UiWidgetContext ctx, out Rect track)
    {
        track = rect;
        string label = AtomVocabulary.ResolveText(spec, ctx, "Label", "LabelKey");
        if (label.Length == 0) return;

        float trackX = rect.x + BandWidth(label, ctx) + ctx.Theme.Geometry.Gap;
        track = new Rect(trackX, rect.y, Math.Max(1f, rect.xMax - trackX), rect.height);
    }

    /// <summary>Paints the measured label band in the role's text colour, before the track is drawn.</summary>
    private void DrawLabel(Rect rect, UiWidgetContext ctx, UiResolvedStyle style)
    {
        string label = AtomVocabulary.ResolveText(spec, ctx, "Label", "LabelKey");
        if (label.Length == 0) return;

        UiThemeDraw.Label(
            new Rect(rect.x, rect.y, BandWidth(label, ctx), rect.height),
            label,
            ctx.Theme,
            style.Text,
            AtomVocabulary.TextFont(ctx),
            TextAnchor.MiddleLeft);
    }

    /// <summary>One label width, shared by the measure and the paint so the band cannot disagree with itself.</summary>
    private static float BandWidth(string label, UiWidgetContext ctx)
    {
        return Math.Max(1f, ctx.Metrics.MeasureWidth(label, AtomVocabulary.TextFont(ctx)) + ctx.Theme.Geometry.Spacing);
    }
}
