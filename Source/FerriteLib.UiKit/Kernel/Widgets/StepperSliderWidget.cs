using System;
using System.Globalization;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel.Widgets;

/// <summary>
/// Greenfield stepper-slider composite. Uses the session-owned <see cref="UiNative"/> primitives
/// and writes through the typed <see cref="IUiBindings"/>; no neutral command bridge.
/// The typed value-binding key is the <c>Bind</c> attribute when present, otherwise the element Id.
/// </summary>
public sealed class StepperSliderWidget : IUiWidget
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
        return ReadHeight();
    }

    public void Draw(Rect rect, UiWidgetContext ctx)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;

        float min = ReadFloat("Min", 0f);
        float max = ReadFloat("Max", 1f);
        float step = Math.Max(0.0001f, ReadFloat("Step", 1f));
        string format = Read("Format");
        if (format.Length == 0) format = "0.##";
        bool live = ReadBool("Live", true);
        float buttonWidth = Math.Max(1f, ReadFloat("ButtonWidth", DefaultButtonWidth));
        float fieldWidth = Math.Max(1f, ReadFloat("FieldWidth", DefaultFieldWidth));

        string bindKey = ReadBindKey();
        float current = ctx.Bindings.TryGet(bindKey, out float bound) ? bound : min;
        string elementId = bindKey;

        float x = rect.x;
        float y = rect.y;
        float height = rect.height;
        string label = ReadLabel(ctx);
        if (label.Length > 0)
        {
            // Through the theme's text outlet, so this label is covered by the fit audit like every
            // other string the kernel draws (its private copy was one of the audit's blind spots).
            float labelWidth = Math.Max(1f, ctx.Metrics.MeasureWidth(label, ctx.Theme.DefaultFont) + LabelPad);
            UiThemeDraw.Label(new Rect(x, y, labelWidth, height), label, ctx.Theme, ctx.Theme.TextPrimary);
            x += labelWidth + Gap;
        }

        Rect minusRect = new(x, y, buttonWidth, height);
        x += buttonWidth + Gap;

        float remainingWidth = Math.Max(1f, rect.xMax - x - fieldWidth - Gap * 2f - buttonWidth);
        Rect sliderRect = new(x, y, remainingWidth, height);
        x += remainingWidth + Gap;

        Rect fieldRect = new(x, y, fieldWidth, height);
        x += fieldWidth + Gap;

        Rect plusRect = new(x, y, buttonWidth, height);

        DrawButton(minusRect, "−", ctx.Theme);
        DrawButton(plusRect, "+", ctx.Theme);

        float sliderValue = UiNative.Slider(sliderRect, elementId, ctx.Session, current, min, max, out bool sliderChanged);
        if (sliderChanged)
        {
            ctx.Bindings.Set(elementId, sliderValue);
        }

        UiNative.NumberField(fieldRect, elementId, ctx.Session, sliderValue, min, max, format, out bool committed);
        if (committed && (live || !ctx.Session.GetOrCreateValueState(elementId).Focused))
        {
            ctx.Bindings.Set(elementId, ctx.Session.GetOrCreateValueState(elementId).FloatValue);
        }

        if (UiNative.Button(minusRect, ctx))
        {
            float next = UiNative.ClampValue(current - step, min, max);
            ctx.Bindings.Set(elementId, next);
        }

        if (UiNative.Button(plusRect, ctx))
        {
            float next = UiNative.ClampValue(current + step, min, max);
            ctx.Bindings.Set(elementId, next);
        }
    }

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
