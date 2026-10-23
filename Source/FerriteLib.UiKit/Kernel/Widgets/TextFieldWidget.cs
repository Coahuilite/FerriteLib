using System;

namespace FerriteLib.UiKit.Kernel.Widgets;
/// <summary>
/// Single-line or basic plain-text multiline native editing, with one instance-local neutral draft.
/// Selection, clipboard and character composition belong to Verse/Unity, not to this wrapper.
/// </summary>
internal sealed class TextFieldWidget : IUiWidget, IUiMeasureParticipant, IUiLabelledWidget, IUiPointerLayout, IUiRetainedDisplay
{
    public const string Kind = "input/text-field";

    private UiElementSpec spec = UiElementSpec.Empty;
    private readonly UiRetainedPaint retained = new();
    bool IUiRetainedDisplay.PaintLast(UiLayoutRect rect, UiWidgetContext ctx) => retained.Paint(rect, ctx);
    UiMeasurement? IUiRetainedDisplay.MeasureLast(UiWidgetContext ctx) => retained.Measure(ctx);

    string IUiWidget.Kind => Kind;

    public static void Register()
    {
        UiWidgetRegistry.Register(
            UiWidgetRegistry.CoreScope,
            Kind,
            () => new TextFieldWidget(),
            // The label set is the text this kind paints as its own label. The placeholder is NOT in it:
            // it is a hint painted inside the field only while the field is empty, never the element's
            // declared width (B8's rule is "the label set equals what the kind paints *as its label*",
            // not "every string it can paint").
            AtomVocabulary.Schema(
                AtomRoles.ToneAndEmphasis,
                "Bind", "Live", "Multiline", "Placeholder", "PlaceholderKey", "Label", "LabelKey", "Height"),
            new[] { "Label", "LabelKey" });
    }

    public void Configure(UiElementSpec spec)
    {
        this.spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public void Validate(IUiBindingReader bindings, string elementPath)
    {
        string bindKey = AtomVocabulary.ReadBindKey(spec);
        if (string.IsNullOrEmpty(bindKey))
        {
            throw new InvalidOperationException(
                $"TextFieldWidget at '{elementPath}' requires a non-empty Id or Bind to use as its typed binding key.");
        }

        bindings.ValidateValue<string>(bindKey, elementPath);
    }

    public UiMeasurement CaptureMeasurement(UiWidgetContext ctx)
    {
        float fixedHeight = AtomVocabulary.ReadHeight(spec, 0);
        if (fixedHeight > 0) return new UiMeasurement(new UiMeasureInputs(numbers: new[] { fixedHeight }), () => fixedHeight);
        string label = AtomVocabulary.ResolveText(spec, ctx, "Label", "LabelKey");
        bool multiline = AtomVocabulary.ReadBool(spec, "Multiline", false);
        string text = "";
        if (multiline)
        {
            string binding = AtomVocabulary.ReadBindKey(spec);
            text = ctx.Bindings.Get<string>(binding) ?? "";
            UiNode node = ctx.Node;
            if (node.ValueStates.TryGetValue(binding, out UiValueState? state) && state.Editor is IUiEditState edit && (edit.Focused || edit.Modified || edit.Conflict)) text = edit.Draft;
        }
        retained.SetMeasure(c=>CaptureText(c,text,label,multiline));
        return CaptureText(ctx,text,label,multiline);
    }
    private UiMeasurement CaptureText(UiWidgetContext ctx,string text,string label,bool multiline)
    {
        return new UiMeasurement(new UiMeasureInputs(new[] { label, text }, new[] { ctx.ViewWidth, (float)AtomVocabulary.TextFont(ctx), ctx.Theme.Geometry.Spacing, ctx.Theme.Geometry.Gap, ctx.Theme.Geometry.RowHeight, ctx.Node?.SharedLabelWidth ?? 0 }, multiline || label.Length > 0), () =>
        {
            float declared = AtomVocabulary.ReadHeight(spec, 0);
            if (declared > 0) return declared;
            UiSliderLayout layout = UiSliderLayout.Measure(label, ctx.ViewWidth, AtomVocabulary.TextFont(ctx), UiElementProperties.DensityOf(ctx.Theme), ctx.Metrics, ctx.Node?.SharedLabelWidth ?? 0);
            if (!multiline) return layout.Height;
            float fieldWidth = layout.Stacked || layout.LabelWidth <= 0 ? ctx.ViewWidth : Math.Max(1, ctx.ViewWidth - layout.LabelWidth - ctx.Theme.Geometry.Gap);
            return (layout.Stacked ? layout.LabelHeight + ctx.Theme.Geometry.Gap : 0) + Math.Max(ctx.Theme.Geometry.RowHeight, ctx.Metrics.MeasureText(text, AtomVocabulary.TextFont(ctx), fieldWidth) + ctx.Theme.Geometry.Spacing * 2);
        });
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
        bool live = AtomVocabulary.ReadBool(spec, "Live", false);
        bool multiline = AtomVocabulary.ReadBool(spec, "Multiline", false);
        string current = ctx.Bindings.TryGet(bindKey, out string bound) ? bound ?? "" : "";

        UiLayoutRect field = rect;
        ResolveLabelBand(rect, ctx, out field);
        bool? writable = AtomVocabulary.WritableOf(ctx, bindKey);
        UiResolvedStyle style = AtomVocabulary.ResolveRole(spec, ctx, writable);

        // One funnel call carries the whole interaction: the disabled refusal, focus, the draft, and the
        // commit rule. The returned string is what the field is actually showing, so the placeholder test
        // below reads the truth rather than the model.
        ctx.Controls.Edit(field, ctx, bindKey, current, value => value, Parse, live, multiline,
            value => ctx.Bindings.Set(bindKey, value), () => ctx.Bindings.Get<string>(bindKey));
        if (!ctx.Session.IsActive) return;
        string retainedLabel=AtomVocabulary.ResolveText(spec,ctx,"Label","LabelKey");
        DrawLabel(rect, ctx, style,retainedLabel);
        UiEditSession<string> editor = ctx.Controls.Editor<string>(ctx, bindKey, value => value, Parse);
        AtomVocabulary.PaintEditFeedback(field, ctx, editor);
        DrawPlaceholder(field, editor.Draft, ctx, writable);
        // Native input may have changed the reliable draft after this pass's measurement.
        retained.SetMeasure(c => CaptureText(c,editor.Draft,retainedLabel,multiline));
        retained.Capture(ctx, (r,c) =>
        {
            ResolveLabelBand(r,c,out UiLayoutRect slot,retainedLabel);
            UiRetainedPaint.Field(slot,c,editor.Draft);
            DrawLabel(r,c,style,retainedLabel);
        });
    }

    private static bool Parse(string text, out string value) { value = text; return true; }

    /// <summary>
    /// The hint painted inside an empty, unfocused field. Deliberately <c>TextSecondary</c>: a placeholder
    /// is information whose absence is not an error, so it takes the secondary ink rather than the disabled
    /// token, which belongs to a control that cannot be used. A read-only field paints none at all - a
    /// hint that invites typing into something that refuses writes is a lie the lane pins from both sides.
    /// </summary>
    private void DrawPlaceholder(UiLayoutRect field, string typed, UiWidgetContext ctx, bool? writable)
    {
        if (writable == false) return;
        if (typed.Length != 0) return;
        if (ctx.Session.GetOrCreateValueState(ctx.Node.Id, AtomVocabulary.ReadBindKey(spec)).Focused) return;

        string placeholder = AtomVocabulary.ResolveText(spec, ctx, "Placeholder", "PlaceholderKey");
        if (placeholder.Length == 0) return;

        float inset = ctx.Theme.Geometry.Spacing;
        UiThemeDraw.Label(
ctx.Drawing,            new UiLayoutRect(field.X + inset, field.Y, Math.Max(1f, field.Width - inset * 2f), field.Height),
            placeholder,
            ctx.Theme,
            ctx.Theme[UiPaletteRole.SecondaryForeground],
            AtomVocabulary.TextFont(ctx),
            UiTextAnchor.MiddleLeft);
    }

    /// <summary>
    /// The label band: the label's own glyph advance plus the theme's spacing, measured with the font the
    /// label is drawn in. The same geometry rule the slider and the number field own; this kind keeps what
    /// the band leaves, so a labelled text field and a labelled number field line up on one page.
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


