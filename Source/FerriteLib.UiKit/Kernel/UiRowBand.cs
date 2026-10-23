using System;
using FerriteLib.UiKit.Kernel.Widgets;

namespace FerriteLib.UiKit.Kernel;
/// <summary>
/// One composed tree-row band, as a reusable PUBLIC primitive (R4-B). <c>container/tree</c> is its first
/// driver, and a consumer's own composite calls the same two members per row, so there is exactly one
/// implementation of the band's measure, its painted order and its per-part hit.
/// <para>
/// <b>What it owns.</b> The indent one level resolves to, the marker's and the box's geometry, the picture's
/// slot and its letterbox fit, the label that takes the remainder, and the rule that a part which took the
/// click is not offered to the band body as well. The row's selection, expansion and checkbox answers are
/// <see cref = "UiTreeRow"/>'s own, re-read on every call - this primitive holds no state at all.
/// </para>
/// <para>
/// <b>Clip.</b> Everything it paints is clamped into <paramref name = "band"/>: the label is given the band's
/// remainder, the picture is fitted inside its own slot. The session's effective clip is the engine's, applied
/// on top by whoever draws the container.
/// </para>
/// </summary>
public static class UiRowBand
{
    /// <summary>
    /// One row's band height: the caller's row height, raised to the picture's own height plus the theme's
    /// padding when the row carries an image. Measure and draw both come through here, so the height a page
    /// reserves and the height a band occupies cannot disagree.
    /// </summary>
    public static float Measure(UiTreeRow row, UiTheme theme, float rowHeight)
    {
        if (theme == null)
            throw new ArgumentNullException(nameof(theme));
        float band = rowHeight;
        if (!row.Image.IsEmpty)
        {
            float natural = row.ImageSize.Y > 0f ? row.ImageSize.Y : row.Image.Height;
            if (natural > 0f)
            {
                band = Math.Max(band, natural + theme.Geometry.Padding * 2f);
            }
        }

        return Math.Max(1f, band);
    }

    /// <summary>Exact operation regions for a custom row wrapper's IUiPointerLayout capability.</summary>
    public static System.Collections.Generic.IEnumerable<UiPointerRegion> PointerRegions(UiTreeRow row, UiLayoutRect band,
        UiTheme theme, float rowHeight, UiRowBandActions actions, UiRowBandLayout layout = default)
    {
        Parts parts = ResolveParts(row, band, theme, rowHeight, layout);
        if (row.Expandable && actions.Disclosure != null) yield return Region(parts.Marker);
        if (row.Checkable && actions.Checkbox != null) yield return Region(parts.Box);
        if (actions.Body != null) yield return Region(band);
    }
    private static UiPointerRegion Region(UiLayoutRect rect) => new(new UiLayoutRect(rect.X, rect.Y, rect.Width, rect.Height));
    private readonly struct Parts
    {
        public Parts(UiLayoutRect marker, UiLayoutRect box, float contentX, float labelRight, float side)
        {
            Marker = marker;
            Box = box;
            ContentX = contentX;
            LabelRight = labelRight;
            Side = side;
        }
        public UiLayoutRect Marker { get; }
        public UiLayoutRect Box { get; }
        public float ContentX { get; }
        public float LabelRight { get; }
        public float Side { get; }
    }
    private static Parts ResolveParts(UiTreeRow row, UiLayoutRect band, UiTheme theme, float rowHeight, UiRowBandLayout layout)
    {
        float pad = theme.Geometry.Padding;
        float partSide = layout.ControlSize.HasValue ? Math.Max(1f, Math.Min(band.Height, layout.ControlSize.Value)) : Math.Max(6f, Math.Min(band.Height, rowHeight) - pad * 2f);
        float indentStep = layout.IndentStep ?? theme.Geometry.Spacing;
        float x = band.X + pad + Math.Max(0f, row.Depth * indentStep);
        float labelRight = band.XMax;
        var marker = new UiLayoutRect(x, band.Y + (band.Height - partSide) * 0.5f, partSide, partSide);
        if (row.Expandable)
            x = marker.XMax + pad;
        float boxX = layout.CheckboxRightInset.HasValue ? Math.Max(band.X, band.XMax - Math.Max(0f, layout.CheckboxRightInset.Value) - partSide) : x;
        var box = new UiLayoutRect(boxX, band.Y + (band.Height - partSide) * 0.5f, partSide, partSide);
        if (row.Checkable)
        {
            if (layout.CheckboxRightInset.HasValue)
                labelRight = Math.Max(x, box.X - pad);
            else
                x = box.XMax + pad;
        }

        return new Parts(marker, box, x, labelRight, partSide);
    }

    /// <summary>
    /// Draws one band into <paramref name = "band"/>: indent, disclosure marker, checkbox, image and label, in
    /// that order, each part present only when the row asks for it. Every part's click is reported to
    /// <paramref name = "actions"/> with the row's stable <see cref = "UiTreeRow.Key"/> - the body, the marker and
    /// the box are separate targets, and one click is delivered once.
    /// </summary>
    /// <param name = "style">
    /// The resolved style of the element the band belongs to. A selected row resolves the Active tone from
    /// <paramref name = "ctx"/>'s theme instead, which is the same tone a chosen mode-row cell and a chosen
    /// dropdown option use.
    /// </param>
    /// <param name = "layout">
    /// The two placement inputs a caller may override: the per-level indent step and the checkbox's
    /// right-alignment inset. The default (<see cref = "UiRowBandLayout"/>'s own) is the library's behaviour:
    /// the theme's spacing per level and the box immediately after the indent.
    /// </param>
    public static void Draw(UiTreeRow row, UiLayoutRect band, UiWidgetContext ctx, float rowHeight, UiResolvedStyle style, UiRowBandActions actions, UiRowBandLayout layout = default)
        => DrawResolved(row,band,ctx,rowHeight,style,actions,null,layout);
    internal static string? DrawResolved(UiTreeRow row, UiLayoutRect band, UiWidgetContext ctx, float rowHeight, UiResolvedStyle style, UiRowBandActions actions, string? text, UiRowBandLayout layout = default)
    {
        if (ctx == null)
            throw new ArgumentNullException(nameof(ctx));
        if (band.Width <= 1f || band.Height <= 1f)
            return null;
        UiTheme theme = ctx.Theme;
        Parts parts = ResolveParts(row, band, theme, rowHeight, layout);
        UiLayoutRect marker = parts.Marker;
        UiLayoutRect box = parts.Box;
        // Geometry feeds the shared behavior before paint. Only the actual part owner is pressed;
        // raw MouseDown, hover, or a sibling's capture cannot manufacture a pressed row.
        UiInteractionResult disclosure = row.Expandable ? ctx.Controls.Activation(marker, ctx, actions.DisclosureTarget, "disclosure", row.Key, actions.Disclosure != null && actions.DisclosureEnabled) : default;
        bool overDisclosure = row.Expandable && actions.Disclosure != null && ctx.Controls.IsMouseOver(marker, ctx);
        bool ownsCheck = ctx.Session.Interactions.CaptureOwner?.Equals(ctx.Controls.InteractionTarget(ctx, actions.CheckboxTarget, "check", row.Key)) == true;
        UiInteractionResult check = row.Checkable && !disclosure.Request ? ctx.Controls.Activation(box, ctx, actions.CheckboxTarget, "check", row.Key, actions.Checkbox != null && actions.CheckboxEnabled && (!overDisclosure || ownsCheck)) : default;
        bool overCheck = row.Checkable && actions.Checkbox != null && ctx.Controls.IsMouseOver(box, ctx);
        bool ownsBody = ctx.Session.Interactions.CaptureOwner?.Equals(ctx.Controls.InteractionTarget(ctx, actions.BodyTarget, "body", row.Key)) == true;
        UiInteractionResult body = !disclosure.Request && !check.Request && actions.Body != null ? ctx.Controls.Activation(band, ctx, actions.BodyTarget, "body", row.Key, actions.BodyEnabled && ((!overDisclosure && !overCheck) || ownsBody)) : default;
        if (actions.Disclosure != null && disclosure.Request)
        {
            UiValueBehavior.Request(ctx.Controls.InteractionTarget(ctx, actions.DisclosureTarget, "disclosure", row.Key), UiValueBehavior.Expansion(disclosure, !row.Expanded, row.Expandable), _ => actions.Disclosure(row.Key));
        }
        else if (actions.Checkbox != null && check.Request)
        {
            UiValueBehavior.Request(ctx.Controls.InteractionTarget(ctx, actions.CheckboxTarget, "check", row.Key), UiValueBehavior.Boolean(check, !row.Checked), _ => actions.Checkbox(row.Key));
        }
        else if (actions.Body != null)
        {
            UiInteractionRequests.Activation(ctx.Controls.InteractionTarget(ctx, actions.BodyTarget, "body", row.Key), body, () => actions.Body(row.Key));
        }

        if (!ctx.Session.IsActive)
            return null;
        return Paint(row,band,ctx,rowHeight,style,disclosure.Captured || check.Captured || body.Captured,ctx.Controls.IsMouseOver(band,ctx),layout,text);
    }
    internal static string Paint(UiTreeRow row,UiLayoutRect band,UiWidgetContext ctx,float rowHeight,UiResolvedStyle style,bool armed=false,bool hovered=false,UiRowBandLayout layout=default,string? capturedText=null)
    {
        UiTheme theme=ctx.Theme;
        float pad=theme.Geometry.Padding;
        Parts parts=ResolveParts(row,band,theme,rowHeight,layout);
        float partSide=parts.Side,x=parts.ContentX,labelRight=parts.LabelRight;
        UiLayoutRect marker=parts.Marker,box=parts.Box;
        if (armed)
            UiThemeDraw.Solid(ctx.Drawing,band, theme.SelectedSurface.Fill);
        else if (hovered)
            UiThemeDraw.Solid(ctx.Drawing,band, theme.HoverSurface.Fill);
        UiResolvedStyle ink = style;
        if (row.Selected)
        {
            ink = theme.Styles.Resolve(UiStatusTone.Active);
            UiThemeDraw.Surface(ctx.Drawing,band, ink.Surface, theme.Geometry.Hairline);
        }

        if (row.Expandable)
        {
            UiThemeDraw.Surface(ctx.Drawing,marker, row.Expanded ? ink.Surface : new UiSurfaceStyle(theme[UiPaletteRole.WindowSurface], ink.Text), theme.Geometry.Hairline);
            if (row.Expanded)
            {
                float inset = Math.Max(2f, partSide * 0.25f);
                UiThemeDraw.Solid(ctx.Drawing,new UiLayoutRect(marker.X + inset, marker.Y + inset, Math.Max(1f, partSide - inset * 2f), Math.Max(1f, partSide - inset * 2f)), ink.Text);
            }
        }

        if (row.Checkable)
        {
            UiThemeDraw.Surface(ctx.Drawing,box, row.Checked ? ink.Surface : new UiSurfaceStyle(theme[UiPaletteRole.WindowSurface], ink.Text), theme.Geometry.Hairline);
            if (row.Checked)
            {
                float inset = Math.Max(2f, partSide * 0.25f);
                UiThemeDraw.Solid(ctx.Drawing,new UiLayoutRect(box.X + inset, box.Y + inset, Math.Max(1f, partSide - inset * 2f), Math.Max(1f, partSide - inset * 2f)), ink.Text);
            }
        }

        if (!row.Image.IsEmpty)
        {
            float naturalWidth = row.ImageSize.X > 0f ? row.ImageSize.X : row.Image.Width;
            float slotWidth = Math.Max(1f, Math.Min(naturalWidth, labelRight - x - pad));
            var slot = new UiLayoutRect(x, band.Y + pad, slotWidth, Math.Max(1f, band.Height - pad * 2f));
            UiThemeDraw.Image(ctx.Drawing,slot, row.Image, ink.Text);
            x = slot.XMax + pad;
        }

        float labelWidth = labelRight - x;
        if (labelWidth > 1f)
        {
            string paintedText=capturedText ?? ReadText(row,ctx);
            UiThemeDraw.Label(ctx.Drawing,new UiLayoutRect(x, band.Y, labelWidth, band.Height), paintedText, theme, ink.Text, AtomVocabulary.TextFont(ctx), UiTextAnchor.MiddleLeft, singleLine: true);
            return paintedText;
        }
        return "";
    }

    private static string ReadText(UiTreeRow row, UiWidgetContext ctx)
    {
        return row.TextKey.Length > 0 ? ctx.Translation.Translate(row.TextKey) : row.Text;
    }
}

/// <summary>
/// The two placement inputs <see cref = "UiRowBand.Draw"/> lets a caller override (R4-B). The default value -
/// <c>default(UiRowBandLayout)</c> - is the library's own behaviour, so a caller that has no convention of its
/// own passes nothing and gets exactly what <c>container/tree</c> gets: the theme's spacing per level, and the
/// checkbox immediately after the indent.
/// <para>
/// Both are caller SUPPLIED rather than caller COMPUTED: the band's internals stay the one place that decides
/// where each part goes and how big it is, and these two answers are inputs to that decision.
/// </para>
/// </summary>
public readonly struct UiRowBandLayout
{
    public UiRowBandLayout(float? indentStep = null, float? checkboxRightInset = null, float? controlSize = null)
    {
        IndentStep = indentStep;
        CheckboxRightInset = checkboxRightInset;
        ControlSize = controlSize;
    }

    /// <summary>
    /// The distance one level of <see cref = "UiTreeRow.Depth"/> indents by. Null keeps the theme's
    /// <c>Geometry.Spacing</c> — the library's own step — and zero is a real answer: a flat list states it.
    /// </summary>
    public float? IndentStep { get; }
    /// <summary>
    /// When set, the checkbox is right-aligned: its far edge sits this far inside <c>band.xMax</c>, and the
    /// label's room ends before it. Null keeps the box immediately after the indent, which is what every
    /// existing page has. A negative value is read as zero (clamped into the band, never refused at runtime).
    /// </summary>
    public float? CheckboxRightInset { get; }
    /// <summary>Optional visual side for disclosure and checkbox; null keeps the theme-derived size.</summary>
    public float? ControlSize { get; }
}

/// <summary>
/// The per-part targets one composed row band reports through (R4-B). A null part is not a target at all:
/// its control is still painted (a row can show its state without being togglable), and the click falls
/// through to the body. Each callback receives the row's stable <see cref = "UiTreeRow.Key"/> - never an index
/// and never a part name to parse, so a consumer dispatches to its own typed action with its own data.
/// </summary>
public readonly struct UiRowBandActions
{
    public UiRowBandActions(Action<string>? body = null, Action<string>? disclosure = null, Action<string>? checkbox = null) : this(body, disclosure, checkbox, "row-band", "row-band", "row-band")
    {
    }

    public UiRowBandActions(Action<string>? body, Action<string>? disclosure, Action<string>? checkbox, string bodyTarget, string disclosureTarget, string checkboxTarget)
        : this(body, disclosure, checkbox, bodyTarget, disclosureTarget, checkboxTarget, true, true, true)
    {
    }

    public UiRowBandActions(Action<string>? body, Action<string>? disclosure, Action<string>? checkbox,
        string bodyTarget, string disclosureTarget, string checkboxTarget,
        bool bodyEnabled, bool disclosureEnabled, bool checkboxEnabled)
    {
        BodyEnabled = bodyEnabled;
        DisclosureEnabled = disclosureEnabled;
        CheckboxEnabled = checkboxEnabled;
        Body = body;
        Disclosure = disclosure;
        Checkbox = checkbox;
        BodyTarget = bodyTarget ?? throw new ArgumentNullException(nameof(bodyTarget));
        DisclosureTarget = disclosureTarget ?? throw new ArgumentNullException(nameof(disclosureTarget));
        CheckboxTarget = checkboxTarget ?? throw new ArgumentNullException(nameof(checkboxTarget));
    }

    /// <summary>The row's body, or null when the band is not itself a target.</summary>
    public Action<string>? Body { get; }
    public bool BodyEnabled { get; }
    public bool DisclosureEnabled { get; }
    public bool CheckboxEnabled { get; }
    /// <summary>The disclosure marker, or null when expanding is the consumer's business elsewhere.</summary>
    public Action<string>? Disclosure { get; }
    /// <summary>The checkbox, or null when the row shows its checked state without offering to change it.</summary>
    public Action<string>? Checkbox { get; }
    /// <summary>Replaceable business targets, independent of the stable row and part identities.</summary>
    public string BodyTarget { get; }
    public string DisclosureTarget { get; }
    public string CheckboxTarget { get; }
}


