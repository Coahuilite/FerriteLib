using System;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel.Widgets;
/// <summary>
/// Native numeric text editing over the common draft/model observation and finite numeric codec.
/// Invalid or conflicting drafts remain local; accepted requests are not assumed business acceptance.
/// </summary>
public sealed class NumberFieldWidget : IUiWidget, IUiMeasureParticipant, IUiLabelledWidget, IUiPointerLayout
{
    public const string Kind = "input/number-field";

    private const string DefaultFormat = "0.##";

    private UiElementSpec spec = UiElementSpec.Empty;

    string IUiWidget.Kind => Kind;

    public static void Register()
    {
        UiWidgetRegistry.Register(
            UiWidgetRegistry.CoreScope,
            Kind,
            () => new NumberFieldWidget(),
            AtomVocabulary.Schema(AtomRoles.ToneAndEmphasis, "Bind", "Min", "Max", "Format", "Live", "Label", "LabelKey", "Height"),
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
                $"NumberFieldWidget at '{elementPath}' requires a non-empty Id or Bind to use as its typed binding key.");
        }

        bindings.ValidateValue<float>(bindKey, elementPath);
    }

    public UiMeasurement CaptureMeasurement(UiWidgetContext ctx)
    {
        string label = AtomVocabulary.ResolveText(spec, ctx, "Label", "LabelKey");
        return new UiMeasurement(AtomVocabulary.LabelMeasureInputs(ctx, label), () => AtomVocabulary.ReadHeight(spec,
            UiSliderLayout.Measure(label, ctx.ViewWidth, AtomVocabulary.TextFont(ctx), UiElementProperties.DensityOf(ctx.Theme), ctx.Metrics, ctx.Node?.SharedLabelWidth ?? 0).Height));
    }

    public float Measure(UiWidgetContext ctx) => CaptureMeasurement(ctx).Calculate();

    public System.Collections.Generic.IEnumerable<UiPointerRegion> PointerRegions(UiWidgetContext ctx, UiLayoutRect bounds)
    {
        ResolveLabelBand(new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height), ctx, out Rect field);
        yield return new UiPointerRegion(new UiLayoutRect(field.x, field.y, field.width, field.height));
    }

    public void Draw(Rect rect, UiWidgetContext ctx)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;

        string bindKey = AtomVocabulary.ReadBindKey(spec);
        float min = AtomVocabulary.ReadFloat(spec, "Min", 0f);
        float max = AtomVocabulary.ReadFloat(spec, "Max", 1f);
        string format = AtomVocabulary.Read(spec, "Format");
        if (format.Length == 0) format = DefaultFormat;
        bool live = AtomVocabulary.ReadBool(spec, "Live", false);
        float current = ctx.Bindings.TryGet(bindKey, out float bound) ? bound : min;

        Rect field = rect;
        ResolveLabelBand(rect, ctx, out field);
        bool? writable = AtomVocabulary.WritableOf(ctx, bindKey);
        UiResolvedStyle style = AtomVocabulary.ResolveRole(spec, ctx, writable);
        var numeric = new UiNumericEdit(min, max, format);
        UiNative.Edit(field, ctx, bindKey, current, numeric.Format, numeric.Parse,
            live, false, value => ctx.Bindings.Set(bindKey, value), () => ctx.Bindings.Get<float>(bindKey));
        if (!ctx.Session.IsActive) return;
        UiValueState state = ctx.Session.GetOrCreateValueState((ctx.Node ?? ctx.Session.ActiveNode).Id, bindKey);
        if (numeric.Parse(state.EditText, out float parsed)) state.FloatValue = parsed;
        AtomVocabulary.PaintEditFeedback(field, ctx, UiNative.Editor<float>(ctx, bindKey, numeric.Format, numeric.Parse));
        DrawLabel(rect, ctx, style);
    }

    /// <summary>
    /// The label band: the string's own glyph advance plus the theme's spacing, measured with the font
    /// the label is drawn in. Same geometry rule the slider atom owns; the field keeps what it leaves.
    /// </summary>
    public float MeasureLabel(UiWidgetContext ctx)
    {
        string label = AtomVocabulary.ResolveText(spec, ctx, "Label", "LabelKey");
        return label.Length == 0 ? 0 : ctx.Metrics.MeasureWidth(label, AtomVocabulary.TextFont(ctx)) + ctx.Theme.Geometry.Spacing;
    }

    private UiSliderLayout LabelLayout(UiWidgetContext ctx, float width) => UiSliderLayout.Measure(AtomVocabulary.ResolveText(spec, ctx, "Label", "LabelKey"), width, AtomVocabulary.TextFont(ctx), UiElementProperties.DensityOf(ctx.Theme), ctx.Metrics, ctx.Node?.SharedLabelWidth ?? 0);
    private void ResolveLabelBand(Rect rect, UiWidgetContext ctx, out Rect field)
    {
        UiSliderLayout layout = LabelLayout(ctx, rect.width);
        if (layout.LabelWidth <= 0)
        {
            field = rect;
            return;
        }

        float gap = ctx.Theme.Geometry.Gap;
        field = layout.Stacked ? new Rect(rect.x, rect.y + layout.LabelHeight + gap, rect.width, Math.Max(1, rect.height - layout.LabelHeight - gap)) : new Rect(rect.x + layout.LabelWidth + gap, rect.y, Math.Max(1, rect.width - layout.LabelWidth - gap), rect.height);
    }

    private void DrawLabel(Rect rect, UiWidgetContext ctx, UiResolvedStyle style)
    {
        string label = AtomVocabulary.ResolveText(spec, ctx, "Label", "LabelKey");
        if (label.Length == 0)
            return;
        UiSliderLayout layout = LabelLayout(ctx, rect.width);
        UiThemeDraw.Label(new Rect(rect.x, rect.y, layout.LabelWidth, layout.Stacked ? layout.LabelHeight : rect.height), label, ctx.Theme, style.Text, AtomVocabulary.TextFont(ctx), TextAnchor.MiddleLeft);
    }
}
