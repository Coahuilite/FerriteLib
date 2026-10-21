using System;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel.Widgets;
/// <summary>
/// Single-line or basic plain-text multiline native editing, with one instance-local neutral draft.
/// Selection, clipboard and character composition belong to Verse/Unity, not to this wrapper.
/// </summary>
public sealed class TextFieldWidget : IUiWidget, IUiMeasureParticipant, IUiLabelledWidget, IUiPointerLayout
{
    public const string Kind = "input/text-field";

    private UiElementSpec spec = UiElementSpec.Empty;

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

    public void Validate(IUiBindings bindings, string elementPath)
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
            UiNode node = ctx.Node ?? ctx.Session.ActiveNode;
            if (node.ValueStates.TryGetValue(binding, out UiValueState? state) && state.Editor is IUiEditState edit && (edit.Focused || edit.Modified || edit.Conflict)) text = edit.Draft;
        }
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
        ResolveLabelBand(new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height), ctx, out Rect field);
        yield return new UiPointerRegion(new UiLayoutRect(field.x, field.y, field.width, field.height));
    }

    public void Draw(Rect rect, UiWidgetContext ctx)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;

        string bindKey = AtomVocabulary.ReadBindKey(spec);
        bool live = AtomVocabulary.ReadBool(spec, "Live", false);
        bool multiline = AtomVocabulary.ReadBool(spec, "Multiline", false);
        string current = ctx.Bindings.TryGet(bindKey, out string bound) ? bound ?? "" : "";

        Rect field = rect;
        ResolveLabelBand(rect, ctx, out field);
        bool? writable = AtomVocabulary.WritableOf(ctx, bindKey);
        UiResolvedStyle style = AtomVocabulary.ResolveRole(spec, ctx, writable);

        // One funnel call carries the whole interaction: the disabled refusal, focus, the draft, and the
        // commit rule. The returned string is what the field is actually showing, so the placeholder test
        // below reads the truth rather than the model.
        UiNative.Edit(field, ctx, bindKey, current, value => value, Parse, live, multiline,
            value => ctx.Bindings.Set(bindKey, value), () => ctx.Bindings.Get<string>(bindKey));
        if (!ctx.Session.IsActive) return;
        DrawLabel(rect, ctx, style);
        UiEditSession<string> editor = UiNative.Editor<string>(ctx, bindKey, value => value, Parse);
        AtomVocabulary.PaintEditFeedback(field, ctx, editor);
        DrawPlaceholder(field, editor.Draft, ctx, writable);
    }

    private static bool Parse(string text, out string value) { value = text; return true; }

    /// <summary>
    /// The hint painted inside an empty, unfocused field. Deliberately <c>TextSecondary</c>: a placeholder
    /// is information whose absence is not an error, so it takes the secondary ink rather than the disabled
    /// token, which belongs to a control that cannot be used. A read-only field paints none at all - a
    /// hint that invites typing into something that refuses writes is a lie the lane pins from both sides.
    /// </summary>
    private void DrawPlaceholder(Rect field, string typed, UiWidgetContext ctx, bool? writable)
    {
        if (writable == false) return;
        if (typed.Length != 0) return;
        if (ctx.Session.GetOrCreateValueState((ctx.Node ?? ctx.Session.ActiveNode).Id, AtomVocabulary.ReadBindKey(spec)).Focused) return;

        string placeholder = AtomVocabulary.ResolveText(spec, ctx, "Placeholder", "PlaceholderKey");
        if (placeholder.Length == 0) return;

        float inset = ctx.Theme.Geometry.Spacing;
        UiThemeDraw.Label(
            new Rect(field.x + inset, field.y, Math.Max(1f, field.width - inset * 2f), field.height),
            placeholder,
            ctx.Theme,
            ctx.Theme[UiPaletteRole.SecondaryForeground],
            AtomVocabulary.TextFont(ctx),
            TextAnchor.MiddleLeft);
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
