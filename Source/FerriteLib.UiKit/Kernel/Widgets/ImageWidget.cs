using System;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel.Widgets;

/// <summary>
/// Core kind <c>display/image</c>: one bound texture, drawn through the library's only image outlet.
/// <para>
/// <b>Why this earns a kind</b> - a measure contract over its own content plus a geometry rule. The height a
/// page must reserve for a picture is the texture's own, and where the picture lands inside its rect is the
/// letterbox fit (<see cref="UiThemeDraw.FitImage"/>); the manifest can state neither, and no existing kind
/// carries a texture at all (glyph text is not an image).
/// </para>
/// <para>
/// <b>What it does not do.</b> It loads nothing, caches nothing and resolves no path: the consumer binds a
/// <see cref="Texture2D"/> through the ordinary value binding (<c>BindReadOnly</c> or <c>BindValue</c>), which
/// is the same generic base every other value uses. A null texture is not an error - the element paints its
/// own empty slot in the resolved style, so "there is no picture here" is visible and distinguishable from an
/// element that was hidden and therefore not drawn at all.
/// </para>
/// <para>
/// <b>Pointer.</b> It takes none: no button, no hit test, no capture, so an icon on top of a row never steals
/// the row's click. A caller that wants an interactive picture puts it inside a kind that owns the hit.
/// </para>
/// </summary>
internal sealed class ImageWidget : IUiWidget
{
    internal const string Kind = "display/image";

    private UiElementSpec spec = UiElementSpec.Empty;

    string IUiWidget.Kind => Kind;

    internal static void Register()
    {
        UiWidgetRegistry.Register(
            UiWidgetRegistry.CoreScope,
            Kind,
            () => new ImageWidget(),
            AtomVocabulary.Schema(AtomRoles.ToneAndEmphasis, "Bind", "Height"));
        // Deliberately no label set: the kind paints no text, so Width="Auto" has nothing honest to measure.
    }

    public void Configure(UiElementSpec spec)
    {
        this.spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public void Validate(IUiBindings bindings, string elementPath)
    {
        string key = ReadBindKey();
        if (key.Length == 0)
        {
            throw new InvalidOperationException(
                "ImageWidget at '" + elementPath + "' requires a Bind naming the value binding that holds its "
                + "texture; an image with no source draws only its empty slot.");
        }

        bindings.ValidateValue<Texture2D>(key, elementPath);
    }

    public float Measure(UiWidgetContext ctx)
    {
        // The declared height wins when a page sets one; otherwise the bound texture's own height plus the
        // theme's padding is the band, so the row a picture sits in is measured rather than assumed. A missing
        // texture keeps one density-derived row, which is the same band an empty slot paints into.
        if (spec.TryGetAttribute("Height", out string raw) && raw.Trim().Length > 0)
        {
            return AtomVocabulary.ReadFloat(spec, "Height", ctx.Theme.Geometry.RowHeight);
        }

        Texture2D? texture = ReadTexture(ctx);
        float natural = texture == null ? 0f : texture.height;
        return natural > 0f ? natural + ctx.Theme.Geometry.Padding * 2f : ctx.Theme.Geometry.RowHeight;
    }

    public void Draw(Rect rect, UiWidgetContext ctx)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;

        UiResolvedStyle style = AtomVocabulary.ResolveRole(spec, ctx, writable: null);
        if (UiThemeDraw.Image(rect, ReadTexture(ctx), style.Text))
        {
            return;
        }

        // The empty slot, in the element's own resolved style: the same plane and hairline the checkbox box
        // and the tree's marker use, so a missing picture reads as a placeholder rather than as a broken glyph.
        UiThemeDraw.Surface(rect, style.Surface, ctx.Theme.Geometry.Hairline);
    }

    private string ReadBindKey()
    {
        return AtomVocabulary.ReadBindKey(spec);
    }

    /// <summary>
    /// The bound texture, read under the collection kinds' fail-soft contract: an absent or mistyped binding
    /// answers null and records one report, so a page with no picture source paints its empty slot instead of
    /// tripping the slot. <c>Validate</c> still refuses the authoring mistake at creation time.
    /// </summary>
    private Texture2D? ReadTexture(UiWidgetContext ctx)
    {
        return AtomVocabulary.ReadOr<Texture2D?>(ctx, Kind, ReadBindKey(), null, "no texture");
    }
}
