using System;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel.Widgets;

/// <summary>
/// Empty-state status text sharing native width measurement and effective environment typography.
/// </summary>
public sealed class EmptyStateWidget : IUiWidget, IUiDescriptiveWidget
{
    public const string Kind = "state/empty";

    private const float DefaultHeight = 48f;

    /// <summary>This kind's undeclared fallback type size, read by both Measure and Draw so they cannot drift apart.</summary>
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
            WrappedTextWidget.MeasureBand(ctx, Describe(ctx).Text, AtomVocabulary.TextFont(ctx, BandFont), ctx.Theme.Geometry.Padding * 2f));
    }

    public UiDescription Describe(UiWidgetContext ctx) => new UiDescription(UiDescriptionRole.Status, text: ResolveText(ctx));

    public void Draw(Rect rect, UiWidgetContext ctx)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;

        UiThemeDraw.Label(rect, Describe(ctx).Text, ctx.Theme, ctx.Theme.TextSecondary, AtomVocabulary.TextFont(ctx, BandFont), TextAnchor.MiddleCenter);
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
