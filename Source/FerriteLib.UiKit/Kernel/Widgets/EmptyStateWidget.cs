using System;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel.Widgets;

/// <summary>
/// Greenfield empty state placeholder.
/// <para>
/// Rebuilt over the <c>text/wrapped</c> atom (0.4.x leaf set): the wrapped-height rule is the atom's
/// <see cref="WrappedTextWidget.MeasureBand"/>, and this kind contributes only its own font, its own
/// leading and its own floor. The kind string, the attribute schema, the label set and the painted
/// result stay as they were.
/// </para>
/// </summary>
public sealed class EmptyStateWidget : IUiWidget
{
    public const string Kind = "state/empty";

    private const float DefaultHeight = 48f;
    private const float VerticalPadding = 12f;

    /// <summary>This kind's own type size, read by both Measure and Draw so they cannot drift apart.</summary>
    private const UiFont BandFont = UiFont.Small;

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
        // The atom's band at this kind's font and leading, floored by this kind's own default band.
        return Math.Max(
            ReadHeight(),
            WrappedTextWidget.MeasureBand(ctx, ResolveText(ctx), BandFont, VerticalPadding));
    }

    public void Draw(Rect rect, UiWidgetContext ctx)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;

        UiThemeDraw.Label(rect, ResolveText(ctx), ctx.Theme, ctx.Theme.TextSecondary, BandFont, TextAnchor.MiddleCenter);
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
