using System;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel.Widgets;

/// <summary>
/// Greenfield chrome banner: a theme-aware text band whose height follows the text it was given. A
/// manifest <c>Height</c> attribute still wins outright (the layout pass consults it before Measure), so
/// when one is present this widget's own calculation is only the fallback.
/// </summary>
public sealed class ChromeBannerWidget : IUiWidget
{
    public const string Kind = "chrome/banner";

    private const float DefaultHeight = 22f;
    private const float VerticalPadding = 6f;

    private UiElementSpec spec = UiElementSpec.Empty;

    string IUiWidget.Kind => Kind;

    public static void Register()
    {
        UiWidgetRegistry.Register(
            UiWidgetRegistry.CoreScope,
            Kind,
            () => new ChromeBannerWidget(),
            new[] { "Id", "Kind", "Bind", "Text", "TextKey", "Height", "Tab", "Hidden" });
    }

    public void Configure(UiElementSpec spec)
    {
        this.spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public void Validate(IUiBindings bindings, string elementPath)
    {
        if (spec.TryGetAttribute("Bind", out string bindKey) && bindKey.Length > 0)
        {
            bindings.ValidateValue<string>(bindKey, elementPath);
        }
    }

    public float Measure(UiWidgetContext ctx)
    {
        return BandFor(ctx, ResolveText(ctx));
    }

    public void Draw(Rect rect, UiWidgetContext ctx)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;

        string text = ResolveText(ctx);
        if (text.Length == 0) return;

        UiThemeDraw.Label(rect, text, ctx.Theme, ctx.Theme.TextSecondary, UiFont.Tiny, TextAnchor.MiddleLeft);
    }

    /// <summary>
    /// One resolution path for the banner text, shared by Measure and Draw: the engine allocates the band
    /// from Measure and then hands that rect to Draw, so the two must agree on the string or the wrapped
    /// tail is cut off. Banner copy is user-facing prose (status and routing warnings), so it is resolved
    /// through the translation seam before measuring.
    /// </summary>
    private string ResolveText(UiWidgetContext ctx)
    {
        if (spec.TryGetAttribute("Bind", out string bindKey) && bindKey.Length > 0)
        {
            ctx.Bindings.TryGet(bindKey, out string bound);
            return bound ?? "";
        }

        if (spec.TryGetAttribute("TextKey", out string key) && key.Length > 0)
        {
            return ctx.Translation.Translate(key);
        }

        return spec.TryGetAttribute("Text", out string literal) ? literal : "";
    }

    private float BandFor(UiWidgetContext ctx, string text)
    {
        float minimum = ReadHeight();
        if (text.Length == 0) return minimum;

        float lines = Math.Max(1f, ctx.Metrics.MeasureText(text, UiFont.Tiny, Math.Max(1f, ctx.ViewWidth)));
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
