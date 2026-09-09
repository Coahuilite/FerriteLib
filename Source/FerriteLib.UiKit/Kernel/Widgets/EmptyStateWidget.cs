using System;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel.Widgets;

/// <summary>Greenfield empty state placeholder.</summary>
public sealed class EmptyStateWidget : IUiWidget
{
    public const string Kind = "state/empty";

    private const float DefaultHeight = 48f;
    private const float VerticalPadding = 12f;

    private UiElementSpec spec = UiElementSpec.Empty;

    string IUiWidget.Kind => Kind;

    public static void Register()
    {
        UiWidgetRegistry.Register(
            UiWidgetRegistry.CoreScope,
            Kind,
            () => new EmptyStateWidget(),
            new[] { "Id", "Kind", "Text", "TextKey", "Height", "Tab", "Hidden" },
            new[] { "Text", "TextKey" });
    }

    public void Configure(UiElementSpec spec)
    {
        this.spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public void Validate(IUiBindings bindings, string elementPath)
    {
        // No required binding.
    }

    public float Measure(UiWidgetContext ctx)
    {
        return BandFor(ctx, ResolveText(ctx));
    }

    public void Draw(Rect rect, UiWidgetContext ctx)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;

        UiThemeDraw.Label(rect, ResolveText(ctx), ctx.Theme, ctx.Theme.TextSecondary, UiFont.Small, TextAnchor.MiddleCenter);
    }

    /// <summary>Shared by Measure and Draw so the allocated band always matches the drawn string.</summary>
    private string ResolveText(UiWidgetContext ctx)
    {
        if (spec.TryGetAttribute("TextKey", out string key) && key.Length > 0)
        {
            return ctx.Translation.Translate(key);
        }

        return spec.TryGetAttribute("Text", out string literal) ? literal : "";
    }

    /// <summary>
    /// Empty-state copy is a sentence, not a word: it gets the height its wrapped lines need, with the
    /// declared or default height as the floor.
    /// </summary>
    private float BandFor(UiWidgetContext ctx, string text)
    {
        float minimum = ReadHeight();
        if (text.Length == 0) return minimum;

        float lines = Math.Max(1f, ctx.Metrics.MeasureText(text, UiFont.Small, Math.Max(1f, ctx.ViewWidth)));
        return Math.Max(minimum, lines + VerticalPadding);
    }

    private float ReadHeight()
    {
        if (spec.TryGetAttribute("Height", out string raw)
            && float.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float height)
            && height > 0f)
        {
            return height;
        }

        return DefaultHeight;
    }
}
