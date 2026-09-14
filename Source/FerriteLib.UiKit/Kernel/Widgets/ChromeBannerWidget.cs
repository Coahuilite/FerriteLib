using System;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel.Widgets;

/// <summary>
/// Greenfield chrome banner: a theme-aware text band whose height follows the text it was given. A
/// manifest <c>Height</c> attribute still wins outright (the layout pass consults it before Measure), so
/// when one is present this widget's own calculation is only the fallback.
/// <para>
/// Rebuilt over the <c>text/wrapped</c> atom (0.4.x leaf set): the wrapped-height rule lives in
/// <see cref="WrappedTextWidget.MeasureBand"/> and this kind only feeds it its own font, its own
/// leading and its own floor. The kind string, the attribute schema, the label set and the painted
/// result are unchanged, which is what lets an existing manifest keep loading against the same
/// vocabulary while the duplicated arithmetic disappears. Its leading follows the theme's density
/// (the band floor is its own); its type size stays the one this kind is named for, so a density font
/// change deliberately does not move this band — that pin is the composite's look, not an oversight.
/// </para>
/// </summary>
public sealed class ChromeBannerWidget : IUiWidget
{
    public const string Kind = "chrome/banner";

    private const float DefaultHeight = 22f;

    /// <summary>
    /// This kind's own type size. Measure and Draw read the one name, so the band the layout reserves
    /// and the band that is painted cannot drift apart — which is the risk the deleted copy of the
    /// wrapped-height rule used to carry.
    /// </summary>
    private const UiFont BandFont = UiFont.Tiny;

    private UiElementSpec spec = UiElementSpec.Empty;

    string IUiWidget.Kind => Kind;

    public static void Register()
    {
        UiWidgetRegistry.Register(
            UiWidgetRegistry.CoreScope,
            Kind,
            () => new ChromeBannerWidget(),
            new[] { "Id", "Kind", "Bind", "Text", "TextKey", "Height", "Tab", "Hidden" },
            new[] { "Text", "TextKey" });
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
        // The atom's band at this kind's font and leading, floored by this kind's own default band.
        return Math.Max(
            ReadHeight(),
            WrappedTextWidget.MeasureBand(ctx, ResolveText(ctx), BandFont, ctx.Theme.Geometry.Padding));
    }

    public void Draw(Rect rect, UiWidgetContext ctx)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;

        string text = ResolveText(ctx);
        if (text.Length == 0) return;

        UiThemeDraw.Label(rect, text, ctx.Theme, ctx.Theme.TextSecondary, BandFont, TextAnchor.MiddleLeft);
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
