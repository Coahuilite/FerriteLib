using System;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel.Widgets;

/// <summary>
/// Core leaf atom <c>text/wrapped</c>: one string, wrapped into the width the tree arranged for it.
/// <para>
/// <b>Why this earns a kind</b> — the measure contract over its own content. The manifest can declare
/// a band height, but nothing in it can say "this element is exactly as tall as its bound string needs
/// when wrapped at its arranged width". <see cref="Measure"/> is that contract, and it is the capability
/// both <c>chrome/banner</c> and <c>state/empty</c> re-implement today (the two kinds whose only shared
/// justification is a wrapped string whose height follows the wrap).
/// </para>
/// <para>
/// Two decisions are deliberately not attributes. There is no font attribute: the theme's
/// <see cref="UiTheme.DefaultFont"/> is what the engine's <c>Width="Auto"</c> seam measures a declared
/// label set with, so a per-element font would make the Auto width and the drawn text disagree. And
/// there is no single-line switch: wrapping is what this atom is, and a label that must not wrap is a
/// different element. A declared <c>Height</c> still wins outright — the layout pass resolves it before
/// Measure on every widget — which is the author's explicit override of this contract, not a second
/// contract.
/// </para>
/// </summary>
public sealed class WrappedTextWidget : IUiWidget
{
    public const string Kind = "text/wrapped";

    /// <summary>Band kept when the string is empty: a leaf that collapses would move its siblings.</summary>
    private const float EmptyBandHeight = 20f;

    /// <summary>Vertical half-leading above and below the wrapped block, inside the measured band.</summary>
    private const float VerticalPadding = 6f;

    private UiElementSpec spec = UiElementSpec.Empty;

    string IUiWidget.Kind => Kind;

    public static void Register()
    {
        UiWidgetRegistry.Register(
            UiWidgetRegistry.CoreScope,
            Kind,
            () => new WrappedTextWidget(),
            AtomVocabulary.Schema("Bind", "Text", "TextKey", "Height"),
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
        float band = MeasureBand(ctx, ResolveText(ctx), ctx.Theme.DefaultFont, VerticalPadding * 2f);
        return band > 0f ? band : EmptyBandHeight;
    }

    /// <summary>
    /// The atom's band contract, and the only copy of it: the height a string needs when wrapped at
    /// the width this context arranged, plus the caller's own vertical leading. The atom measures
    /// through this function, and so do the composites rebuilt over the atom — <c>chrome/banner</c>
    /// and <c>state/empty</c>, the two the roadmap called "a missing leaf atom named twice". Their
    /// font and their leading are parameters here rather than this kind's constants for exactly that
    /// reason: once this seam exists, a second copy of "wrap the string, reserve the height" is the
    /// defect, not a style choice.
    /// <para>
    /// Returns 0 for an empty string on purpose. The floor belongs to the caller's named band — this
    /// atom's own, the banner's 22 or the empty state's 48 — so a caller that wants a floor applies
    /// its own instead of inheriting one from here.
    /// </para>
    /// </summary>
    internal static float MeasureBand(UiWidgetContext ctx, string text, UiFont font, float verticalLead)
    {
        if (string.IsNullOrEmpty(text)) return 0f;

        float wrapped = ctx.Metrics.MeasureText(text, font, Math.Max(1f, ctx.ViewWidth));
        return Math.Max(1f, wrapped) + verticalLead;
    }

    public void Draw(Rect rect, UiWidgetContext ctx)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;

        string text = ResolveText(ctx);
        if (text.Length == 0) return;

        // Upper-left anchored and inset by the same padding Measure added, so the rect handed to the
        // single text outlet is the rect the wrapped height was computed for. The fit audit then
        // reports a real defect only, never this atom's own padding.
        float pad = Math.Min(VerticalPadding, Math.Max(0f, (rect.height - 1f) * 0.5f));
        UiThemeDraw.Label(
            new Rect(rect.x, rect.y + pad, rect.width, Math.Max(1f, rect.height - pad * 2f)),
            text,
            ctx.Theme,
            ctx.Theme.TextPrimary,
            ctx.Theme.DefaultFont,
            TextAnchor.UpperLeft);
    }

    private string ResolveText(UiWidgetContext ctx)
    {
        if (spec.TryGetAttribute("Bind", out string bindKey) && bindKey.Length > 0)
        {
            ctx.Bindings.TryGet(bindKey, out string bound);
            return bound ?? "";
        }

        return AtomVocabulary.ResolveText(spec, ctx, "Text", "TextKey");
    }
}
