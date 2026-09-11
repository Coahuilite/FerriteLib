using System;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel.Widgets;

/// <summary>
/// Core leaf atom <c>input/number-field</c>: one float, edited as text, committed under a rule.
/// <para>
/// <b>Why this earns a kind</b> — per-element interaction state that no attribute can carry: focus,
/// the in-progress edit buffer, and the commit rule (a parse writes through the typed binding and
/// clamps into the declared window; unparseable text is kept in the buffer and never written; Enter or
/// losing focus restores the formatted value). That state lives in the session's value bag, which is
/// why the atom and <c>input/slider</c> sharing one binding key see one value instead of two.
/// </para>
/// </summary>
public sealed class NumberFieldWidget : IUiWidget
{
    public const string Kind = "input/number-field";

    private const float DefaultHeight = 28f;
    private const float LabelPad = 4f;
    private const float Gap = 6f;
    private const string DefaultFormat = "0.##";

    private UiElementSpec spec = UiElementSpec.Empty;

    string IUiWidget.Kind => Kind;

    public static void Register()
    {
        UiWidgetRegistry.Register(
            UiWidgetRegistry.CoreScope,
            Kind,
            () => new NumberFieldWidget(),
            AtomVocabulary.Schema("Bind", "Min", "Max", "Format", "Live", "Label", "LabelKey", "Height"),
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
        return AtomVocabulary.ReadHeight(spec, DefaultHeight);
    }

    public void Draw(Rect rect, UiWidgetContext ctx)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;

        string bindKey = AtomVocabulary.ReadBindKey(spec);
        float min = AtomVocabulary.ReadFloat(spec, "Min", 0f);
        float max = AtomVocabulary.ReadFloat(spec, "Max", 1f);
        string format = AtomVocabulary.Read(spec, "Format");
        if (format.Length == 0) format = DefaultFormat;
        bool live = AtomVocabulary.ReadBool(spec, "Live", true);
        float current = ctx.Bindings.TryGet(bindKey, out float bound) ? bound : min;

        Rect field = rect;
        string label = AtomVocabulary.ResolveText(spec, ctx, "Label", "LabelKey");
        if (label.Length > 0)
        {
            float labelWidth = LabelBandWidth(label, ctx);
            UiThemeDraw.Label(
                new Rect(rect.x, rect.y, labelWidth, rect.height),
                label,
                ctx.Theme,
                ctx.Theme.TextPrimary,
                ctx.Theme.DefaultFont,
                TextAnchor.MiddleLeft);
            float fieldX = rect.x + labelWidth + Gap;
            field = new Rect(fieldX, rect.y, Math.Max(1f, rect.xMax - fieldX), rect.height);
        }

        UiNative.NumberField(field, bindKey, ctx.Session, current, min, max, format, out bool committed);
        if (!committed) return;

        // Live=false defers the write while the control holds focus: an editor that writes every
        // keystroke cannot be typed into, so the deferred form is the one the consumer asked for.
        // Read the session value, not the returned display string — the committed value is the
        // parsed and clamped one, and the field returns whatever text is currently in the buffer.
        UiValueState state = ctx.Session.GetOrCreateValueState(bindKey);
        if (live || !state.Focused)
        {
            ctx.Bindings.Set(bindKey, state.FloatValue);
        }
    }

    /// <summary>Label band measured from the glyph model, the same geometry rule the slider atom owns.</summary>
    private static float LabelBandWidth(string label, UiWidgetContext ctx)
    {
        return Math.Max(1f, ctx.Metrics.MeasureWidth(label, ctx.Theme.DefaultFont) + LabelPad);
    }
}
