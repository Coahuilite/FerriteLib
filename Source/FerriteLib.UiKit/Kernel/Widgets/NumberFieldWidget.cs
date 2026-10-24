using System;

namespace FerriteLib.UiKit.Kernel.Widgets;
/// <summary>
/// Native numeric text editing over the common draft/model observation and finite numeric codec.
/// Invalid or conflicting drafts remain local; accepted requests are not assumed business acceptance.
/// </summary>
internal sealed class NumberFieldWidget : IUiWidget, IUiMeasureParticipant, IUiLabelledWidget, IUiPointerLayout, IUiRetainedDisplay
{
    public const string Kind = "input/number-field";

    private const string DefaultFormat = "0.##";

    private UiElementSpec spec = UiElementSpec.Empty;
    private readonly UiRetainedPaint retained = new();
    bool IUiRetainedDisplay.PaintLast(UiLayoutRect rect, UiWidgetContext ctx) => retained.Paint(rect, ctx);
    UiMeasurement? IUiRetainedDisplay.MeasureLast(UiWidgetContext ctx) => retained.Measure(ctx);

    string IUiWidget.Kind => Kind;

    public static void Register()
    {
UiWidgetRegistry.Define(new UiWidgetDefinition(
            UiWidgetRegistry.CoreScope,
            Kind,
            () => new NumberFieldWidget(),
            AtomVocabulary.Schema(AtomRoles.ToneAndEmphasis, "Bind", "Min", "Max", "Format", "Live", "Label", "LabelKey", "Height"),
            new[] { "Label", "LabelKey" }));
    }

    public void Configure(UiElementSpec spec)
    {
        this.spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public void Validate(IUiBindingAccess bindings, string elementPath)
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
        ResolveLabelBand(new UiLayoutRect(bounds.X, bounds.Y, bounds.Width, bounds.Height), ctx, out UiLayoutRect field);
        yield return new UiPointerRegion(new UiLayoutRect(field.X, field.Y, field.Width, field.Height));
    }

    public void Draw(UiLayoutRect rect, UiWidgetContext ctx)
    {
        if (rect.Width <= 1f || rect.Height <= 1f) return;

        string bindKey = AtomVocabulary.ReadBindKey(spec);
        float min = AtomVocabulary.ReadFloat(spec, "Min", 0f);
        float max = AtomVocabulary.ReadFloat(spec, "Max", 1f);
        string format = AtomVocabulary.Read(spec, "Format");
        if (format.Length == 0) format = DefaultFormat;
        bool live = AtomVocabulary.ReadBool(spec, "Live", false);
        float current = ctx.Bindings.TryGet(bindKey, out float bound) ? bound : min;

        UiLayoutRect field = rect;
        ResolveLabelBand(rect, ctx, out field);
        bool? writable = AtomVocabulary.WritableOf(ctx, bindKey);
        UiResolvedStyle style = AtomVocabulary.ResolveRole(spec, ctx, writable);
        var numeric = new UiNumericEdit(min, max, format);
        ctx.Controls.Edit(field, ctx, bindKey, current, numeric.Format, numeric.Parse,
            live, false, value => ctx.Bindings.Set(bindKey, value), () => ctx.Bindings.Get<float>(bindKey));
        if (!ctx.Session.IsActive) return;
        UiControlState state = ctx.Session.GetControlState(ctx.Node.Id, bindKey);
        UiEditSession<float> retainedEditor = ctx.Controls.Editor<float>(ctx,bindKey,numeric.Format,numeric.Parse);
        AtomVocabulary.PaintEditFeedback(field, ctx, retainedEditor);
        string retainedLabel=AtomVocabulary.ResolveText(spec,ctx,"Label","LabelKey");
        DrawLabel(rect, ctx, style,retainedLabel);
        retained.Capture(ctx, (r,c) =>
        {
            ResolveLabelBand(r,c,out UiLayoutRect slot,retainedLabel);
            UiRetainedPaint.Field(slot,c,retainedEditor.Draft);
            DrawLabel(r,c,style,retainedLabel);
        });
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

    private UiSliderLayout LabelLayout(UiWidgetContext ctx, float width, string? capturedLabel = null) => UiSliderLayout.Measure(capturedLabel ?? AtomVocabulary.ResolveText(spec, ctx, "Label", "LabelKey"), width, AtomVocabulary.TextFont(ctx), UiElementProperties.DensityOf(ctx.Theme), ctx.Metrics, ctx.Node?.SharedLabelWidth ?? 0);
    private void ResolveLabelBand(UiLayoutRect rect, UiWidgetContext ctx, out UiLayoutRect field, string? capturedLabel = null)
    {
        UiSliderLayout layout = LabelLayout(ctx, rect.Width, capturedLabel);
        if (layout.LabelWidth <= 0)
        {
            field = rect;
            return;
        }

        float gap = ctx.Theme.Geometry.Gap;
        field = layout.Stacked ? new UiLayoutRect(rect.X, rect.Y + layout.LabelHeight + gap, rect.Width, Math.Max(1, rect.Height - layout.LabelHeight - gap)) : new UiLayoutRect(rect.X + layout.LabelWidth + gap, rect.Y, Math.Max(1, rect.Width - layout.LabelWidth - gap), rect.Height);
    }

    private void DrawLabel(UiLayoutRect rect, UiWidgetContext ctx, UiResolvedStyle style, string? capturedLabel = null)
    {
        string label = capturedLabel ?? AtomVocabulary.ResolveText(spec, ctx, "Label", "LabelKey");
        if (label.Length == 0)
            return;
        UiSliderLayout layout = LabelLayout(ctx, rect.Width, capturedLabel);
        UiThemeDraw.Label(ctx.Drawing,new UiLayoutRect(rect.X, rect.Y, layout.LabelWidth, layout.Stacked ? layout.LabelHeight : rect.Height), label, ctx.Theme, style.Text, AtomVocabulary.TextFont(ctx), UiTextAnchor.MiddleLeft);
    }
}
