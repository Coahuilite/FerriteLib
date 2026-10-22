using System;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel.Widgets;
/// <summary>Default range wrapper: shared behavior, measured label/track, typed request binding.</summary>
internal sealed class SliderWidget : IUiWidget, IUiMeasureParticipant, IUiLabelledWidget, IUiPointerLayout, IUiRetainedDisplay
{
    public const string Kind = "input/slider";
    private UiElementSpec spec = UiElementSpec.Empty;
    private readonly UiRetainedPaint retained = new();
    bool IUiRetainedDisplay.PaintLast(Rect rect, UiWidgetContext ctx) => retained.Paint(rect, ctx);
    UiMeasurement? IUiRetainedDisplay.MeasureLast(UiWidgetContext ctx) => retained.Measure(ctx);
    string IUiWidget.Kind => Kind;

    public static void Register()
    {
        UiWidgetRegistry.Register(UiWidgetRegistry.CoreScope, Kind, () => new SliderWidget(), AtomVocabulary.Schema(AtomRoles.ToneAndEmphasis, "Bind", "Min", "Max", "Label", "LabelKey", "Height"), new[] { "Label", "LabelKey" });
    }

    public void Configure(UiElementSpec spec)
    {
        this.spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public void Validate(IUiBindings bindings, string elementPath)
    {
        string key = AtomVocabulary.ReadBindKey(spec);
        if (string.IsNullOrEmpty(key))
            throw new InvalidOperationException("SliderWidget at '" + elementPath + "' requires Id or Bind.");
        bindings.ValidateValue<float>(key, elementPath);
        _ = Limits();
    }

    private UiRange Limits() => new UiRange(AtomVocabulary.ReadFloat(spec, "Min", 0), AtomVocabulary.ReadFloat(spec, "Max", 1));
    private string Label(UiWidgetContext ctx) => AtomVocabulary.ResolveText(spec, ctx, "Label", "LabelKey");
    private UiSliderLayout Layout(UiWidgetContext ctx, float width, string? capturedLabel = null) => UiSliderLayout.Measure(capturedLabel ?? Label(ctx), width, AtomVocabulary.TextFont(ctx), UiElementProperties.DensityOf(ctx.Theme), ctx.Metrics, ctx.Node?.SharedLabelWidth ?? 0);
    public float MeasureLabel(UiWidgetContext ctx) => Label(ctx).Length == 0 ? 0 : ctx.Metrics.MeasureWidth(Label(ctx), AtomVocabulary.TextFont(ctx)) + ctx.Theme.Geometry.Spacing;
    public UiMeasurement CaptureMeasurement(UiWidgetContext ctx)
    {
        string label = Label(ctx);
        return new UiMeasurement(AtomVocabulary.LabelMeasureInputs(ctx, label), () => AtomVocabulary.ReadHeight(spec,
            UiSliderLayout.Measure(label, ctx.ViewWidth, AtomVocabulary.TextFont(ctx), UiElementProperties.DensityOf(ctx.Theme), ctx.Metrics, ctx.Node?.SharedLabelWidth ?? 0).Height));
    }

    public float Measure(UiWidgetContext ctx) => CaptureMeasurement(ctx).Calculate();
    public System.Collections.Generic.IEnumerable<UiPointerRegion> PointerRegions(UiWidgetContext ctx, UiLayoutRect bounds)
    {
        Rect track = TrackRect(new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height), ctx, Layout(ctx, bounds.Width));
        yield return new UiPointerRegion(new UiLayoutRect(track.x, track.y, track.width, track.height));
    }

    private static Rect TrackRect(Rect rect, UiWidgetContext ctx, UiSliderLayout layout)
    {
        float gap = ctx.Theme.Geometry.Gap;
        Rect track = rect;
        if (layout.LabelWidth > 0)
        {
            if (layout.Stacked)
            {
                float trackY = rect.y + layout.LabelHeight + gap;
                float trackHeight = Math.Max(1, rect.height - layout.LabelHeight - gap);
                track = new Rect(rect.x, trackY, rect.width, trackHeight);
            }
            else
            {
                float trackX = rect.x + layout.LabelWidth + gap;
                float trackWidth = Math.Max(1, rect.width - layout.LabelWidth - gap);
                track = new Rect(trackX, rect.y, trackWidth, rect.height);
            }
        }

        return track;
    }

    public void Draw(Rect rect, UiWidgetContext ctx)
    {
        if (rect.width <= 1 || rect.height <= 1)
            return;
        string key = AtomVocabulary.ReadBindKey(spec);
        UiRange range = Limits();
        float current = ctx.Bindings.TryGet(key, out float bound) ? bound : range.Min;
        string retainedLabel=Label(ctx);
        UiSliderLayout layout = Layout(ctx, rect.width,retainedLabel);
        Rect track = TrackRect(rect, ctx, layout);
        bool? writable = AtomVocabulary.WritableOf(ctx, key);
        UiInteractionResult interaction = UiNative.Range(track, ctx, key, range.Clamp(current), range.Min, range.Max, writable != false);
        // Only an actual selection makes a request. Repaint and out-of-range model values do not repair the model.
        UiInteractionRequests.Range(UiNative.InteractionTarget(ctx, key), interaction, value => ctx.Bindings.Set(key, value));
        if (!ctx.Session.IsActive)
            return;
        UiResolvedStyle style = AtomVocabulary.ResolveRole(spec, ctx, writable);
        if (layout.LabelWidth > 0)
            UiThemeDraw.Label(new Rect(rect.x, rect.y, layout.LabelWidth, layout.Stacked ? layout.LabelHeight : rect.height), Label(ctx), ctx.Theme, style.Text, AtomVocabulary.TextFont(ctx), TextAnchor.MiddleLeft);
        UiNative.PaintRange(track, ctx.Theme, range.Clamp(current), range.Min, range.Max);
        retained.Capture(ctx, (r,c) =>
        {
            UiSliderLayout lastLayout=Layout(c,r.width,retainedLabel);
            if(lastLayout.LabelWidth>0) UiThemeDraw.Label(new Rect(r.x,r.y,lastLayout.LabelWidth,lastLayout.Stacked?lastLayout.LabelHeight:r.height),retainedLabel,c.Theme,style.Text,AtomVocabulary.TextFont(c),TextAnchor.MiddleLeft);
            UiNative.PaintRange(TrackRect(r,c,lastLayout),c.Theme,range.Clamp(current),range.Min,range.Max);
        });
    }
}
