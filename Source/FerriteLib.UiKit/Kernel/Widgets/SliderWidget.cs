using System;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel.Widgets;

/// <summary>Default range wrapper: shared behavior, measured label/track, typed request binding.</summary>
public sealed class SliderWidget : IUiWidget
{
    public const string Kind = "input/slider";
    private UiElementSpec spec = UiElementSpec.Empty;
    string IUiWidget.Kind => Kind;
    public static void Register()
    {
        UiWidgetRegistry.Register(UiWidgetRegistry.CoreScope, Kind, () => new SliderWidget(),
            AtomVocabulary.Schema(AtomRoles.ToneAndEmphasis, "Bind", "Min", "Max", "Label", "LabelKey", "Height"), new[] { "Label", "LabelKey" });
    }
    public void Configure(UiElementSpec spec) { this.spec = spec ?? throw new ArgumentNullException(nameof(spec)); }
    public void Validate(IUiBindings bindings, string elementPath)
    {
        string key = AtomVocabulary.ReadBindKey(spec);
        if (string.IsNullOrEmpty(key)) throw new InvalidOperationException("SliderWidget at '" + elementPath + "' requires Id or Bind.");
        bindings.ValidateValue<float>(key, elementPath);
        _ = Limits();
    }
    private UiRange Limits() => new UiRange(AtomVocabulary.ReadFloat(spec, "Min", 0), AtomVocabulary.ReadFloat(spec, "Max", 1));
    private string Label(UiWidgetContext ctx) => AtomVocabulary.ResolveText(spec, ctx, "Label", "LabelKey");
    private UiSliderLayout Layout(UiWidgetContext ctx, float width)
        => UiSliderLayout.Measure(Label(ctx), width, AtomVocabulary.TextFont(ctx), UiElementProperties.DensityOf(ctx.Theme), ctx.Metrics);
    public float Measure(UiWidgetContext ctx) => AtomVocabulary.ReadHeight(spec, Layout(ctx, ctx.ViewWidth).Height);
    public void Draw(Rect rect, UiWidgetContext ctx)
    {
        if (rect.width <= 1 || rect.height <= 1) return;
        string key = AtomVocabulary.ReadBindKey(spec);
        UiRange range = Limits();
        float current = ctx.Bindings.TryGet(key, out float bound) ? bound : range.Min;
        UiSliderLayout layout = Layout(ctx, rect.width);
        float gap = ctx.Theme.Geometry.Gap;
        Rect track = layout.LabelWidth == 0 ? rect : layout.Stacked
            ? new Rect(rect.x, rect.y + layout.LabelHeight + gap, rect.width, Math.Max(1, rect.height - layout.LabelHeight - gap))
            : new Rect(rect.x + layout.LabelWidth + gap, rect.y, Math.Max(1, rect.width - layout.LabelWidth - gap), rect.height);
        bool? writable = AtomVocabulary.WritableOf(ctx, key);
        UiInteractionResult interaction = UiNative.Range(track, ctx, key, range.Clamp(current), range.Min, range.Max, writable != false);
        // Only an actual selection makes a request. Repaint and out-of-range model values do not repair the model.
        UiInteractionRequests.Range(UiNative.InteractionTarget(ctx, key), interaction, value => ctx.Bindings.Set(key, value));
        if (!ctx.Session.IsActive) return;
        UiResolvedStyle style = AtomVocabulary.ResolveRole(spec, ctx, writable);
        if (layout.LabelWidth > 0)
            UiThemeDraw.Label(new Rect(rect.x, rect.y, layout.LabelWidth, layout.Stacked ? layout.LabelHeight : rect.height),
                Label(ctx), ctx.Theme, style.Text, AtomVocabulary.TextFont(ctx), TextAnchor.MiddleLeft);
        UiNative.PaintRange(track, ctx.Theme, range.Clamp(current), range.Min, range.Max);
    }
}
