using System;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel.Widgets;

/// <summary>Greenfield section header: a themed title with divider.</summary>
public sealed class SectionHeaderWidget : IUiWidget
{
    public const string Kind = "section/header";

    private const float DefaultHeight = 24f;

    private UiElementSpec spec = UiElementSpec.Empty;

    string IUiWidget.Kind => Kind;

    public static void Register()
    {
        UiWidgetRegistry.Register(
            UiWidgetRegistry.CoreScope,
            Kind,
            () => new SectionHeaderWidget(),
            new[] { "Id", "Kind", "Title", "TitleKey", "Height", "Tab", "Hidden" },
            new[] { "Title", "TitleKey" });
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
        return ReadHeight();
    }

    public void Draw(Rect rect, UiWidgetContext ctx)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;

        string title = spec.TryGetAttribute("TitleKey", out string key) && key.Length > 0
            ? ctx.Translation.Translate(key)
            : spec.TryGetAttribute("Title", out string literal) ? literal : "";

        UiThemeDraw.Label(rect, title, ctx.Theme, ctx.Theme.TextPrimary, UiFont.Small, TextAnchor.MiddleLeft);
        UiThemeDraw.Surface(
            new Rect(rect.x, rect.yMax - 1f, rect.width, 1f),
            ctx.Theme,
            ctx.Theme.Divider,
            ctx.Theme.Divider);
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
