using System;
using System.Globalization;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel.Widgets;
/// <summary>
/// Greenfield stepper-slider composite. Uses the session-owned <see cref = "UiNative"/> primitives
/// and writes through the typed <see cref = "IUiBindings"/>; no neutral command bridge.
/// The typed value-binding key is the <c>Bind</c> attribute when present, otherwise the element Id.
/// </summary>
public sealed class StepperSliderWidget : IUiWidget, IUiLabelledWidget
{
    public const string Kind = "input/stepper-slider";

    private const float DefaultHeight = 28f;
    // The label band is measured, never hard-coded: the 80f constant this replaces was the
    // library-side specimen of the N1 complaint (US->FL round 3), and its removal is the accepted
    // reshape — the same text-natural seam Width="Auto" uses, proving itself in the library's own
    // widget. The pad keeps glyphs off the band edge; it is not a size floor.
    private const float LabelPad = 4f;
    private const float DefaultButtonWidth = 24f;
    private const float DefaultFieldWidth = 64f;
    private const float Gap = 6f;

    private UiElementSpec spec = UiElementSpec.Empty;

    public string KindName => Kind;

    string IUiWidget.Kind => Kind;

    public static void Register()
    {
        UiWidgetRegistry.Register(
            UiWidgetRegistry.CoreScope,
            Kind,
            () => new StepperSliderWidget(),
            new[] { "Id", "Kind", "Bind", "Min", "Max", "Step", "Format", "Live", "ButtonWidth", "FieldWidth", "Height", "Label", "LabelKey", "Tab", "Hidden" },
            new[] { "Label", "LabelKey" });
    }

    public void Configure(UiElementSpec spec)
    {
        this.spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public void Validate(IUiBindings bindings, string elementPath)
    {
        string bindKey = ReadBindKey();
        if (string.IsNullOrEmpty(bindKey))
        {
            throw new InvalidOperationException(
                $"StepperSliderWidget at '{elementPath}' requires a non-empty Id or Bind to use as its typed binding key.");
        }

        bindings.ValidateValue<float>(bindKey, elementPath);
    }

    public float Measure(UiWidgetContext ctx)
    {
        float height = ReadHeight();
        return spec.TryGetAttribute("Height", out _) ? height : Math.Max(height, LabelLayout(ctx, ctx.ViewWidth).Height);
    }

    public void Draw(Rect rect, UiWidgetContext ctx)
    {
        if (rect.width <= 1f || rect.height <= 1f)
            return;
        float min = ReadFloat("Min", 0f);
        float max = ReadFloat("Max", 1f);
        float step = Math.Max(0.0001f, ReadFloat("Step", 1f));
        string format = Read("Format");
        if (format.Length == 0)
            format = "0.##";
        bool live = ReadBool("Live", false);
        float buttonWidth = Math.Max(1f, ReadFloat("ButtonWidth", DefaultButtonWidth));
        float fieldWidth = Math.Max(1f, ReadFloat("FieldWidth", DefaultFieldWidth));
        string bindKey = ReadBindKey();
        float current = ctx.Bindings.TryGet(bindKey, out float bound) ? bound : min;
        var numeric = new UiNumericEdit(min, max, format);
        UiEditSession<float> editor = UiNative.Editor<float>(ctx, bindKey, numeric.Format, numeric.Parse);
        editor.Observe(current);
        float x = rect.x;
        float y = rect.y;
        float height = rect.height;
        string label = ReadLabel(ctx);
        UiSliderLayout labelLayout = LabelLayout(ctx, rect.width);
        if (labelLayout.LabelWidth > 0)
        {
            if (labelLayout.Stacked)
            {
                y += labelLayout.LabelHeight + ctx.Theme.Geometry.Gap;
                height = Math.Max(1, rect.yMax - y);
            }
            else
                x += labelLayout.LabelWidth + Gap;
        }

        Rect minusRect = new(x, y, buttonWidth, height);
        x += buttonWidth + Gap;
        float remainingWidth = Math.Max(1f, rect.xMax - x - fieldWidth - Gap * 2f - buttonWidth);
        Rect sliderRect = new(x, y, remainingWidth, height);
        x += remainingWidth + Gap;
        Rect fieldRect = new(x, y, fieldWidth, height);
        x += fieldWidth + Gap;
        Rect plusRect = new(x, y, buttonWidth, height);
        bool writable = ctx.Bindings.IsWritable(bindKey);
        UiInteractionResult minus = UiNative.Activation(minusRect, ctx, bindKey, "decrement", writable: writable);
        UiInteractionResult plus = UiNative.Activation(plusRect, ctx, bindKey, "increment", writable: writable);
        UiInteractionResult range = UiNative.Range(sliderRect, ctx, bindKey, current, min, max, writable);
        bool takeover = false;
        void Set(float value) => ctx.Bindings.Set(bindKey, value);
        float ReadModel() => ctx.Bindings.Get<float>(bindKey);
        if (minus.Request || plus.Request)
        {
            UiInteractionResult operation = minus.Request ? minus : plus;
            string part = minus.Request ? "decrement" : "increment";
            UiInteractionRequests.Activation(UiNative.InteractionTarget(ctx, bindKey, part), operation, () => numeric.Step(editor, ReadModel(), minus.Request ? -step : step, Set, ReadModel));
            takeover = true;
        }
        else if (range.Request && range.Value != numeric.Clamp(current))
        {
            UiInteractionRequests.Range(UiNative.InteractionTarget(ctx, bindKey), range, value => editor.TakeOver(value, Set, ReadModel));
            takeover = true;
        }

        if (!ctx.Session.IsActive)
            return;
        if (!takeover)
            UiNative.Edit(fieldRect, ctx, bindKey, ReadModel(), numeric.Format, numeric.Parse, live, false, Set, ReadModel);
        if (!ctx.Session.IsActive)
            return;
        UiValueState state = ctx.Session.GetOrCreateValueState((ctx.Node ?? ctx.Session.ActiveNode).Id, bindKey);
        state.EditText = editor.Draft;
        state.Focused = editor.Focused;
        if (numeric.Parse(editor.Draft, out float parsed))
            state.FloatValue = parsed;
        // Only presentation follows established requests; a label failure cannot swallow an operation.
        if (label.Length > 0)
        {
            float labelWidth = labelLayout.LabelWidth;
            UiThemeDraw.Label(new Rect(rect.x, rect.y, labelWidth, labelLayout.Stacked ? labelLayout.LabelHeight : height), label, ctx.Theme, ctx.Theme.TextPrimary, AtomVocabulary.TextFont(ctx));
        }

        DrawButton(minusRect, "−", ctx.Theme);
        DrawButton(plusRect, "+", ctx.Theme);
        UiNative.PaintRange(sliderRect, ctx.Theme, ReadModel(), min, max);
        AtomVocabulary.PaintEditFeedback(fieldRect, ctx, editor);
    }

    public float MeasureLabel(UiWidgetContext ctx)
    {
        string label = ReadLabel(ctx);
        return label.Length == 0 ? 0 : ctx.Metrics.MeasureWidth(label, AtomVocabulary.TextFont(ctx)) + ctx.Theme.Geometry.Spacing;
    }

    private UiSliderLayout LabelLayout(UiWidgetContext ctx, float width) => UiSliderLayout.Measure(ReadLabel(ctx), width, AtomVocabulary.TextFont(ctx), UiElementProperties.DensityOf(ctx.Theme), ctx.Metrics, ctx.Node?.SharedLabelWidth ?? 0, Math.Max(1, ReadFloat("ButtonWidth", DefaultButtonWidth)) * 2 + Math.Max(1, ReadFloat("FieldWidth", DefaultFieldWidth)) + Gap * 3 + ctx.Theme.Geometry.RowHeight * 2);

    private string ReadBindKey()
    {
        if (spec.TryGetAttribute("Bind", out string bindKey) && bindKey.Length > 0)
        {
            return bindKey;
        }

        return spec.Id;
    }

    private string ReadLabel(UiWidgetContext ctx)
    {
        if (spec.TryGetAttribute("LabelKey", out string key) && key.Length > 0)
        {
            return ctx.Translation.Translate(key);
        }

        return spec.TryGetAttribute("Label", out string label) ? label : "";
    }

    private static void DrawButton(Rect rect, string text, UiTheme theme)
    {
        UiThemeDraw.Solid(rect, theme.Raised);
        UiThemeDraw.Label(rect, text, theme, theme.TextPrimary, anchor: TextAnchor.MiddleCenter);
    }


    private float ReadHeight()
    {
        if (spec.TryGetAttribute("Height", out string raw)
            && float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float height)
            && height > 0f)
        {
            return height;
        }

        return DefaultHeight;
    }

    private string Read(string name)
    {
        return spec.TryGetAttribute(name, out string value) ? value : "";
    }

    private float ReadFloat(string name, float fallback)
    {
        return spec.TryGetAttribute(name, out string raw)
            && float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
            ? value
            : fallback;
    }

    private bool ReadBool(string name, bool fallback)
    {
        if (!spec.TryGetAttribute(name, out string raw)) return fallback;
        return string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "1", StringComparison.Ordinal);
    }
}
