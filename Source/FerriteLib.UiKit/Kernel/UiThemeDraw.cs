using System;



namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// Theme-aware, pure drawing helpers for greenfield widgets. These helpers only paint; input
/// authority remains with native IMGUI/Verse controls. Every public helper restores the IMGUI
/// and Verse text state it temporarily uses.
/// </summary>
public static class UiThemeDraw
{
    /// <param name="singleLine">
    /// True for labels that must stay on one line (badges, dropdown display text). It is a rendering
    /// promise, not only an audit axis: the wrap state is turned off around the draw, so an over-wide
    /// single-line label is clipped horizontally instead of wrapping out of its row. The fitting audit
    /// then checks width instead of height, because for those labels wrapping is not an available answer.
    /// </param>
    public static void Label(IUiDrawing drawing, UiLayoutRect rect,string text,UiTheme theme,UiColour? color=null,UiFont? font=null,UiTextAnchor anchor=UiTextAnchor.MiddleLeft,bool singleLine=false){UiFont resolved=font??theme.Styles.Font;UiFitAudit.Check(rect,text,resolved,singleLine);drawing.Text(rect,text,resolved,color??theme[UiPaletteRole.Foreground],anchor,!singleLine);}

    /// <summary>
    /// Paints one surface treatment: the fill plus its hairline edge. This is the pair-shaped entry a
    /// table answer feeds, so a caller never spells a fill and a border separately — which is how the
    /// same two tokens used to end up re-derived per widget.
    /// </summary>
    public static void Surface(IUiDrawing drawing,UiLayoutRect rect, UiSurfaceStyle style, float hairline = 1f)
    {
        if (rect.Width <= 0f || rect.Height <= 0f) return;

        float edge = Math.Max(1f, hairline);
        {
            Solid(drawing,rect, style.Fill);
            Solid(drawing,new UiLayoutRect(rect.X, rect.Y, rect.Width, edge), style.Border);
            Solid(drawing,new UiLayoutRect(rect.X, rect.YMax - edge, rect.Width, edge), style.Border);
            Solid(drawing,new UiLayoutRect(rect.X, rect.Y, edge, rect.Height), style.Border);
            Solid(drawing,new UiLayoutRect(rect.XMax - edge, rect.Y, edge, rect.Height), style.Border);
        }
    }

    /// <summary>Paints an explicit fill with an explicit edge colour, or the theme's shared border.</summary>
    public static void Surface(IUiDrawing drawing,UiLayoutRect rect, UiTheme theme, UiColour fill, UiColour? border = null)
    {
        Surface(drawing,rect, new UiSurfaceStyle(fill, border ?? theme[UiPaletteRole.Border]), theme.Geometry.Hairline);
    }

    public static void Panel(IUiDrawing drawing,UiLayoutRect rect, UiTheme theme)
    {
        Surface(drawing,rect, theme.PanelSurface, theme.Geometry.Hairline);
    }

    public static void Base(IUiDrawing drawing,UiLayoutRect rect, UiTheme theme)
    {
        Surface(drawing,rect, theme.BaseSurface, theme.Geometry.Hairline);
    }

    /// <summary>Paints the uninterrupted workspace plane behind a multi-column layout.</summary>
    public static void Workspace(IUiDrawing drawing,UiLayoutRect rect, UiTheme theme)
    {
        if (rect.Width <= 0f || rect.Height <= 0f) return;
        {
            Solid(drawing,rect, theme[UiPaletteRole.WorkspaceSurface]);
        }
    }

    /// <summary>Descriptive alias for callers that treat the workspace as a background plane.</summary>
    public static void BackgroundPlane(IUiDrawing drawing,UiLayoutRect rect, UiTheme theme)
    {
        Workspace(drawing,rect, theme);
    }

    /// <summary>Paints a flat section band and its lower rule, both at the theme's density.</summary>
    public static void SectionBand(IUiDrawing drawing,UiLayoutRect rect, UiTheme theme, UiColour? fill = null, UiColour? rule = null)
    {
        if (rect.Width <= 0f || rect.Height <= 0f) return;
        {
            float edge = Math.Max(1f, theme.Geometry.Hairline);
            Solid(drawing,rect, fill ?? theme[UiPaletteRole.SectionSurface]);
            Solid(drawing,new UiLayoutRect(rect.X, rect.YMax - edge, rect.Width, edge), rule ?? theme[UiPaletteRole.Divider]);
        }
    }

    /// <summary>Paints a section band with an optional general-purpose title.</summary>
    public static void SectionHeader(IUiDrawing drawing,UiLayoutRect rect, string title, UiTheme theme, UiColour? color = null, UiFont? font = null)
    {
        SectionBand(drawing,rect, theme);
        if (!string.IsNullOrEmpty(title))
        {
            Label(drawing,rect, title, theme, color ?? theme[UiPaletteRole.Foreground], font ?? UiFont.Small);
        }
    }

    /// <summary>Paints the narrow edge marker used for active or focused content.</summary>
    public static void AccentRail(IUiDrawing drawing,UiLayoutRect rect, UiTheme theme, bool active = true, float width = 2f, UiColour? color = null)
    {
        if (!active || rect.Width <= 0f || rect.Height <= 0f) return;
        {
            Solid(drawing,new UiLayoutRect(rect.X, rect.Y, UiMath.Clamp(width, 1f, rect.Width), rect.Height), color ?? theme[UiPaletteRole.Accent]);
        }
    }

    /// <summary>Alias emphasizing that the rail marks keyboard or mouse focus.</summary>
    public static void FocusRail(IUiDrawing drawing,UiLayoutRect rect, UiTheme theme, bool focused = true, float width = 2f)
    {
        AccentRail(drawing,rect, theme, focused, width);
    }

    // --- the selector field: one neutral shape, three outlets (SA1.1) ------------------------------

    /// <summary>
    /// The width of the left accent rail the selector look reserves. A SHAPE constant, not a token: the
    /// rail's colour stays a token argument (<see cref="AccentRail"/>'s own default), so restyling can
    /// never move the rect, and the width is fixed because the rail is part of the look's identity -
    /// an element drawn 3px here and an element drawn 1px here would be two different looks wearing one
    /// name. Callers reserve this width; they do not re-invent it.
    /// </summary>
    public const float SelectorAccentWidth = 3f;

    /// <summary>
    /// The width of the right slot the arrow area reserves: the value text ends before it, so the arrow
    /// is an independent region rather than ink overlapping the text it explains.
    /// </summary>
    public const float SelectorArrowZoneWidth = 18f;

    /// <summary>The arrow's base width in px; the glyph is drawn procedurally, so no font or texture is involved.</summary>
    public const float SelectorArrowWidth = 10f;

    /// <summary>The arrow's height in px.</summary>
    public const float SelectorArrowHeight = 6f;

    /// <summary>The arrow slot inside a selector field: the <see cref="SelectorArrowZoneWidth"/> at the field's right edge.</summary>
    public static UiLayoutRect SelectorArrowZone(UiLayoutRect field)
    {
        float width = Math.Min(SelectorArrowZoneWidth, Math.Max(0f, field.Width));
        return new UiLayoutRect(field.XMax - width, field.Y, width, field.Height);
    }

    /// <summary>
    /// The value-text outlet of a selector field: everything between the reserved accent rail and the
    /// reserved arrow slot, further inset by the theme's own padding. Measure and draw both reserve
    /// through these outlets, so a consumer composite that adopts the selector look cannot drift from
    /// the core kind that registers it.
    /// </summary>
    public static UiLayoutRect SelectorTextOutlet(UiLayoutRect field, UiTheme theme)
    {
        float padding = theme.Geometry.Padding;
        float x = field.X + Math.Min(SelectorAccentWidth, field.Width) + padding;
        float right = SelectorArrowZone(field).X - padding;
        return new UiLayoutRect(x, field.Y, Math.Max(0f, right - x), field.Height);
    }

    /// <summary>
    /// Paints the selector's down arrow centred in <paramref name="zone"/>: a filled triangle stacked
    /// from unit-height solids, point at the bottom. Procedural on purpose - the library ships no
    /// textures and the game's font carries no guaranteed glyph for an arrow - and every hairline goes
    /// through <see cref="Solid"/>, so this adds no second backend outlet. The colour is the caller's
    /// token (default: the theme's primary ink); the shape is these constants.
    /// </summary>
    public static void SelectorArrow(IUiDrawing drawing,UiLayoutRect zone, UiTheme theme, UiColour? color = null)
    {
        if (zone.Width <= 0f || zone.Height <= 0f) return;
        UiColour ink = color ?? theme[UiPaletteRole.Foreground];

        float baseWidth = Math.Min(SelectorArrowWidth, Math.Max(1f, zone.Width));
        float height = Math.Min(SelectorArrowHeight, Math.Max(1f, zone.Height));
        float centreX = zone.X + zone.Width * 0.5f;
        float top = zone.Y + (zone.Height - height) * 0.5f;
        int rows = UiMath.CeilToInt(height);
        for (int row = 0; row < rows; row++)
        {
            // Linear taper from the base to the point; the last row is the tip.
            float step = rows <= 1 ? 0f : (float)row / (rows - 1);
            float rowWidth = Math.Max(1f, baseWidth * (1f - step));
            Solid(drawing,new UiLayoutRect(centreX - rowWidth * 0.5f, top + row, rowWidth, 1f), ink);
        }
    }

    /// <summary>
    /// The COMPLETE selector field - the shared entry (SA1.1): plane and hairline through <see cref="Surface"/>,
    /// the accent rail at <see cref="SelectorAccentWidth"/>, the arrow zone with its own divider hairline, and
    /// the value text inside <see cref="SelectorTextOutlet"/> - composed in this one order. The core kind's
    /// selector look and a consumer's own dropdown-shaped composite both call THIS method, so the two cannot
    /// diverge by re-spelling the sequence; a caller that resolves the status ladder itself still gets the
    /// same colours, because every colour here comes from the passed <paramref name="style"/> or the theme's
    /// accent. Nothing reserves a slot for a help button.
    /// </summary>
    /// <param name="font">
    /// The text font; null takes <see cref="UiFont.Small"/>, the size the core dropdown field has always
    /// drawn its value text at.
    /// </param>
    public static void SelectorField(IUiDrawing drawing,UiLayoutRect field, string display, UiTheme theme, UiResolvedStyle style, UiFont? font = null)
    {
        if (field.Width <= 1f || field.Height <= 1f) return;

        Surface(drawing,field, style.Surface, theme.Geometry.Hairline);

        UiLayoutRect arrow = SelectorArrowZone(field);
        if (arrow.Width > 0f)
        {
            Solid(
drawing,                new UiLayoutRect(arrow.X - theme.Geometry.Hairline, field.Y, theme.Geometry.Hairline, field.Height),
                style.Surface.Border);
            SelectorArrow(drawing,arrow, theme, style.Text);
        }

        AccentRail(drawing,field, theme, width: SelectorAccentWidth);

        UiLayoutRect outlet = SelectorTextOutlet(field, theme);
        if (outlet.Width > 1f)
        {
            Label(drawing,outlet, display, theme, style.Text, font ?? UiFont.Small, UiTextAnchor.MiddleLeft, singleLine: true);
        }
    }

    /// <param name="writable">
    /// False when the element's data side says its value cannot be written: the treatment is then the
    /// disabled one whatever tone was authored, because state beats author — the one written precedence
    /// rule for style. Null means the caller did not ask, and the tone decides alone.
    /// </param>
    /// <summary>Paints a compact rectangular status treatment without owning interaction.</summary>
    public static void StatusTreatment(IUiDrawing drawing,UiLayoutRect rect, UiTheme theme, UiStatusTone tone = UiStatusTone.Neutral, bool? writable = null)
    {
        if (rect.Width <= 0f || rect.Height <= 0f) return;
        Surface(drawing,rect, theme.Styles.Resolve(tone, UiEmphasis.Normal, writable).Surface, theme.Geometry.Hairline);
    }

    /// <summary>
    /// The stable paint of an element the tree recovered after it threw: an alarm-toned band carrying
    /// the layout path it was supposed to occupy. Deliberately generic — the library owns no product
    /// vocabulary and no translation keys, so it reports the slot's identity rather than a sentence
    /// nobody declared. The diagnostic text lives in the session, not here.
    /// </summary>
    public static void RecoveryBand(IUiDrawing drawing,UiLayoutRect rect, UiTheme theme, string elementPath)
    {
        if (rect.Width <= 1f || rect.Height <= 1f) return;

        Surface(drawing,rect, theme, theme[UiPaletteRole.DangerSurface], theme[UiPaletteRole.Border]);
        Label(
drawing,            new UiLayoutRect(rect.X + 6f, rect.Y, Math.Max(1f, rect.Width - 12f), rect.Height),
            elementPath ?? "",
            theme,
            theme[UiPaletteRole.DangerForeground],
            UiFont.Tiny,
            singleLine: true);
    }

    /// <summary>Marks retained display without replacing it or introducing another palette.</summary>
    internal static void FailureMarker(IUiDrawing drawing,UiLayoutRect rect, UiTheme theme, string identity)
    {
        float band = Math.Min(3f, rect.Height);
        Solid(drawing,new UiLayoutRect(rect.X, rect.YMax - band, rect.Width, band), theme[UiPaletteRole.DangerForeground]);
        Label(drawing,new UiLayoutRect(rect.XMax - Math.Min(20, rect.Width), rect.Y, Math.Min(20, rect.Width), Math.Min(20, rect.Height)),
            "!", theme, theme[UiPaletteRole.DangerForeground], UiFont.Tiny, UiTextAnchor.MiddleRight, singleLine: true);
    }

    /// <param name="writable">
    /// False paints the badge as disabled whatever tone was authored, the same state-beats-author rule
    /// <see cref="StatusTreatment"/> documents.
    /// </param>
    /// <summary>Paints a status treatment and centers its short, reusable badge label.</summary>
    public static void StatusBadge(IUiDrawing drawing,UiLayoutRect rect, string text, UiTheme theme, UiStatusTone tone = UiStatusTone.Neutral, UiFont? font = null, bool? writable = null)
    {
        // A badge is compact surface text, which is the emphasis whose neutral label is secondary; the
        // plane and the text come from one query, so a badge and a field can no longer disagree.
        UiResolvedStyle style = theme.Styles.Resolve(tone, UiEmphasis.Muted, writable);
        Surface(drawing,rect, style.Surface, theme.Geometry.Hairline);
        Label(drawing,rect, text, theme, style.Text, font ?? UiFont.Tiny, UiTextAnchor.MiddleCenter, singleLine: true);
    }

    /// <summary>
    /// Paints one axis-aligned solid rect. This is the only fill primitive in the library: widgets that
    /// need a rule, a glyph cell or a data point call this instead of reaching for
    /// <c>Verse.Widgets.DrawBoxSolid</c>, which is what keeps the backend-contact allowlist short enough
    /// to gate (see the harness containment lane).
    /// </summary>
    public static void Solid(IUiDrawing drawing, UiLayoutRect rect,UiColour color){if(rect.Width>0&&rect.Height>0)drawing.Fill(rect,color);}

    /// <summary>
    /// The letterbox fit, and the library's one answer to "where does a picture go inside this rect": the
    /// largest rect with <paramref name="naturalSize"/>'s aspect ratio that fits inside
    /// <paramref name="slot"/>, centred on both axes. Deterministic and total - a non-positive natural size
    /// or slot answers an empty rect, which every caller reads as "nothing to fit" rather than as a
    /// zero-sized draw.
    /// </summary>
    public static UiLayoutRect FitImage(UiLayoutRect slot, UiLayoutPoint naturalSize)
    {
        UiLayoutRect fitted = UiImageLayout.Arrange(new UiLayoutRect(slot.X, slot.Y, slot.Width, slot.Height), naturalSize.X, naturalSize.Y);
        return new UiLayoutRect(fitted.X, fitted.Y, fitted.Width, fitted.Height);
    }

    /// <summary>
    /// One texture, letterboxed into <paramref name="rect"/> (see <see cref="FitImage"/>) and tinted through
    /// <c>GUI.color</c> exactly as <see cref="Label"/> tints a string - so an icon's ink comes from the same
    /// resolved style the row's text does, never from a colour literal here. Returns false when there is no
    /// texture or it has no usable size: nothing is painted in that case and the CALLER decides what it means
    /// (the image kind paints its empty slot, a hidden element is not drawn at all). It consumes no input, and
    /// a null texture is an answer rather than a fault.
    /// </summary>
    public static bool Image(IUiDrawing drawing, UiLayoutRect rect,UiImage image,UiColour tint)=>Image(drawing,rect,image,tint,default);

    /// <summary>Draws a supplier-owned texture with explicit fit and inner alignment. Cover clips to the slot.</summary>
    public static bool Image(IUiDrawing drawing,UiLayoutRect rect,UiImage image,UiColour tint,UiImageOptions options){if(image.IsEmpty)return false;UiLayoutRect fitted=UiImageLayout.Arrange(rect,image.Width,image.Height,options);if(fitted.Width<=1||fitted.Height<=1)return false;if(options.Fit==UiImageFit.Cover)drawing.Clip(rect,()=>drawing.Image(new UiLayoutRect(fitted.X-rect.X,fitted.Y-rect.Y,fitted.Width,fitted.Height),image,tint));else drawing.Image(fitted,image,tint);return true;}
}



