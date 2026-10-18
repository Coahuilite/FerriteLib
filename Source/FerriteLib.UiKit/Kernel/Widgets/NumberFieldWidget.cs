using System;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel.Widgets;

/// <summary>
/// Native numeric text editing over the common draft/model observation and finite numeric codec.
/// Invalid or conflicting drafts remain local; accepted requests are not assumed business acceptance.
/// </summary>
public sealed class NumberFieldWidget : IUiWidget
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
        UiValueState state = ctx.Session.GetOrCreateValueState(bindKey);
        if (numeric.Parse(state.EditText, out float parsed)) state.FloatValue = parsed;
        AtomVocabulary.PaintEditFeedback(field, ctx, UiNative.Editor<float>(ctx, bindKey, numeric.Format, numeric.Parse));
        DrawLabel(rect, ctx, style);
    }

    /// <summary>
    /// The label band: the string's own glyph advance plus the theme's spacing, measured with the font
    /// the label is drawn in. Same geometry rule the slider atom owns; the field keeps what it leaves.
    /// </summary>
    private void ResolveLabelBand(Rect rect, UiWidgetContext ctx, out Rect field)
    {
        field = rect;
        string label = AtomVocabulary.ResolveText(spec, ctx, "Label", "LabelKey");
        if (label.Length == 0) return;

        float fieldX = rect.x + BandWidth(label, ctx) + ctx.Theme.Geometry.Gap;
        field = new Rect(fieldX, rect.y, Math.Max(1f, rect.xMax - fieldX), rect.height);
    }

    /// <summary>Paints the measured label band in the role's text colour, before the field is drawn.</summary>
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
