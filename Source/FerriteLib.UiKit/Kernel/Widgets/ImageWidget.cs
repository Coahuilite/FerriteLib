using System;

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
/// <see cref="Texture"/> (including a supplier RenderTexture) through the ordinary value binding (<c>BindReadOnly</c> or <c>BindValue</c>), which
/// is the same generic base every other value uses. A null texture is not an error - the element paints its
/// own empty slot in the resolved style, so "there is no picture here" is visible and distinguishable from an
/// element that was hidden and therefore not drawn at all.
/// </para>
/// <para>
/// <b>Pointer.</b> It takes none: no button, no hit test, no capture, so an icon on top of a row never steals
/// the row's click. A caller that wants an interactive picture puts it inside a kind that owns the hit.
/// </para>
/// </summary>
internal sealed class ImageWidget : IUiWidget, IUiMeasureParticipant, IUiDescriptiveWidget
{
    internal const string Kind = "display/image";

    private UiElementSpec spec = UiElementSpec.Empty;

    string IUiWidget.Kind => Kind;

    internal static void Register()
    {
UiWidgetRegistry.Define(new UiWidgetDefinition(
            UiWidgetRegistry.CoreScope,
            Kind,
            () => new ImageWidget(),
            AtomVocabulary.Schema(AtomRoles.ToneAndEmphasis, "Bind", "Height", "Description", "DescriptionKey", "Decorative", "Fit", "ImageAlignX", "ImageAlignY", "AspectHeight")));
        // Deliberately no label set: the kind paints no text, so Width="Auto" has nothing honest to measure.
    }

    public void Configure(UiElementSpec spec)
    {
        this.spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public void Validate(IUiBindingReader bindings, string elementPath)
    {
        string key = ReadBindKey();
        if (key.Length == 0)
        {
            throw new InvalidOperationException(
                "ImageWidget at '" + elementPath + "' requires a Bind naming the value binding that holds its "
                + "texture; an image with no source draws only its empty slot.");
        }

        UiTextureBinding.Validate(bindings, key, elementPath);
        ReadOptions();
    }

    public UiMeasurement CaptureMeasurement(UiWidgetContext ctx)
    {
        if (spec.TryGetAttribute("Height", out string declaredHeight) && declaredHeight.Trim().Length > 0)
            return new UiMeasurement(new UiMeasureInputs(), () => AtomVocabulary.ReadFloat(spec, "Height", ctx.Theme.Geometry.RowHeight));
        UiImage texture = ReadTexture(ctx);
        float width = texture.IsEmpty ? 0 : texture.Width, height = texture.IsEmpty ? 0 : texture.Height;
        return new UiMeasurement(new UiMeasureInputs(numbers: new[] { width, height, AtomVocabulary.ReadBool(spec, "AspectHeight", false) ? ctx.ViewWidth : 0, ctx.Theme.Geometry.Padding, ctx.Theme.Geometry.RowHeight }), () =>
        {
            if (spec.TryGetAttribute("Height", out string raw) && raw.Trim().Length > 0) return AtomVocabulary.ReadFloat(spec, "Height", ctx.Theme.Geometry.RowHeight);
            if (!texture.IsEmpty && AtomVocabulary.ReadBool(spec, "AspectHeight", false))
            {
                float aspect = UiImageLayout.AspectHeight(ctx.ViewWidth, width, height);
                return aspect > 0 ? aspect : ctx.Theme.Geometry.RowHeight;
            }
            return height > 0 ? height + ctx.Theme.Geometry.Padding * 2 : ctx.Theme.Geometry.RowHeight;
        });
    }

    public float Measure(UiWidgetContext ctx) => CaptureMeasurement(ctx).Calculate();

    public void Draw(UiLayoutRect rect, UiWidgetContext ctx)
    {
        if (rect.Width <= 1f || rect.Height <= 1f) return;

        UiResolvedStyle style = AtomVocabulary.ResolveRole(spec, ctx, writable: null);
        if (UiThemeDraw.Image(ctx.Drawing,rect, ReadTexture(ctx), style.Text, ReadOptions()))
        {
            return;
        }

        // The empty slot, in the element's own resolved style: the same plane and hairline the checkbox box
        // and the tree's marker use, so a missing picture reads as a placeholder rather than as a broken glyph.
        UiThemeDraw.Surface(ctx.Drawing,rect, style.Surface, ctx.Theme.Geometry.Hairline);
    }

    public UiDescription Describe(UiWidgetContext ctx)
    {
        UiDescriptionRole role = AtomVocabulary.ReadBool(spec, "Decorative", false) ? UiDescriptionRole.Decoration : UiDescriptionRole.Image;
        string name = AtomVocabulary.ResolveText(spec, ctx, "Description", "DescriptionKey");
        try
        {
            if (!UiTextureBinding.TryRead(ctx.Bindings, ReadBindKey(), out UiImage texture))
                return new UiDescription(role, name: name, unknownReason: "MissingBinding");
            return new UiDescription(role, name: name, available: !texture.IsEmpty);
        }
        catch (InvalidOperationException ex) when (ex is not UiReactiveFault)
        {
            return new UiDescription(role, name: name, unknownReason: "BindingReadUnavailable");
        }
    }

    private string ReadBindKey()
    {
        return AtomVocabulary.ReadBindKey(spec);
    }

    private UiImageOptions ReadOptions()
    {
        spec.TryGetAttribute("Fit", out string fit);
        spec.TryGetAttribute("ImageAlignX", out string x);
        spec.TryGetAttribute("ImageAlignY", out string y);
        return UiImageOptions.Parse(fit, x, y);
    }

    /// <summary>
    /// The bound texture, read under the collection kinds' fail-soft contract: an absent or mistyped binding
    /// answers null and records one report, so a page with no picture source paints its empty slot instead of
    /// tripping the slot. <c>Validate</c> still refuses the authoring mistake at creation time.
    /// </summary>
    private UiImage ReadTexture(UiWidgetContext ctx)
    {
        try
        {
            if (UiTextureBinding.TryRead(ctx.Bindings, ReadBindKey(), out UiImage texture)) return texture;
        }
        catch (InvalidOperationException ex) when (ex is not UiReactiveFault) { }
        AtomVocabulary.ReportUnresolved(ctx, Kind, ReadBindKey(), "no texture");
        return default;
    }
}
