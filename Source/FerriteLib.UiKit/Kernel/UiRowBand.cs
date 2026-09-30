using System;
using FerriteLib.UiKit.Kernel.Widgets;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// One composed tree-row band, as a reusable PUBLIC primitive (R4-B). <c>container/tree</c> is its first
/// driver, and a consumer's own composite calls the same two members per row, so there is exactly one
/// implementation of the band's measure, its painted order and its per-part hit.
/// <para>
/// <b>What it owns.</b> The indent one level resolves to, the marker's and the box's geometry, the picture's
/// slot and its letterbox fit, the label that takes the remainder, and the rule that a part which took the
/// click is not offered to the band body as well. The row's selection, expansion and checkbox answers are
/// <see cref="UiTreeRow"/>'s own, re-read on every call - this primitive holds no state at all.
/// </para>
/// <para>
/// <b>Clip.</b> Everything it paints is clamped into <paramref name="band"/>: the label is given the band's
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
        if (theme == null) throw new ArgumentNullException(nameof(theme));
        float band = rowHeight;
        if (row.Image != null)
        {
            float natural = row.ImageSize.y > 0f ? row.ImageSize.y : row.Image.height;
            if (natural > 0f)
            {
                band = Math.Max(band, natural + theme.Geometry.Padding * 2f);
            }
        }

        return Math.Max(1f, band);
    }

    /// <summary>
    /// Draws one band into <paramref name="band"/>: indent, disclosure marker, checkbox, image and label, in
    /// that order, each part present only when the row asks for it. Every part's click is reported to
    /// <paramref name="actions"/> with the row's stable <see cref="UiTreeRow.Key"/> - the body, the marker and
    /// the box are separate targets, and one click is delivered once.
    /// </summary>
    /// <param name="style">
    /// The resolved style of the element the band belongs to. A selected row resolves the Active tone from
    /// <paramref name="ctx"/>'s theme instead, which is the same tone a chosen mode-row cell and a chosen
    /// dropdown option use.
    /// </param>
    /// <param name="layout">
    /// The two placement inputs a caller may override: the per-level indent step and the checkbox's
    /// right-alignment inset. The default (<see cref="UiRowBandLayout"/>'s own) is the library's behaviour:
    /// the theme's spacing per level and the box immediately after the indent.
    /// </param>
    public static void Draw(
        UiTreeRow row,
        Rect band,
        UiWidgetContext ctx,
        float rowHeight,
        UiResolvedStyle style,
        UiRowBandActions actions,
        UiRowBandLayout layout = default)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        if (band.width <= 1f || band.height <= 1f) return;

        UiTheme theme = ctx.Theme;
        float pad = theme.Geometry.Padding;

        UiResolvedStyle ink = style;
        if (row.Selected)
        {
            // The row's own selection answer paints the resolved Active tone, and everything the row paints -
            // marker, box, label - takes its ink from that same answer.
            ink = theme.Styles.Resolve(UiStatusTone.Active);
            UiThemeDraw.Surface(band, ink.Surface, theme.Geometry.Hairline);
        }

        // A part is sized by the ROW's own height, not by the band: a row made taller by its picture keeps the
        // same marker and box it would have had, so an icon never inflates the controls beside it.
        float partSide = layout.ControlSize.HasValue
            ? Math.Max(1f, Math.Min(band.height, layout.ControlSize.Value))
            : Math.Max(6f, Math.Min(band.height, rowHeight) - pad * 2f);
        float indentStep = layout.IndentStep ?? theme.Geometry.Spacing;
        float x = band.x + pad + Math.Max(0f, row.Depth * indentStep);

        // The label's room. A right-aligned box is placed from the band's far edge and takes its width out of
        // this, so nothing the row draws can run under it.
        float labelRight = band.xMax;
        bool partFired = false;
        if (row.Expandable)
        {
            var marker = new Rect(x, band.y + (band.height - partSide) * 0.5f, partSide, partSide);
            if (row.Expanded)
            {
                // Filled and outlined both come from the resolved answer, so a tone move carries the marker
                // with the label instead of leaving a second colour literal here.
                UiThemeDraw.Surface(marker, ink.Surface, theme.Geometry.Hairline);
                float inset = Math.Max(2f, partSide * 0.25f);
                UiThemeDraw.Solid(
                    new Rect(marker.x + inset, marker.y + inset, Math.Max(1f, partSide - inset * 2f), Math.Max(1f, partSide - inset * 2f)),
                    ink.Text);
            }
            else
            {
                UiThemeDraw.Surface(marker, new UiSurfaceStyle(theme.Base, ink.Text), theme.Geometry.Hairline);
            }

            x = marker.xMax + pad;
            if (actions.Disclosure != null && UiNative.Button(marker, ctx))
            {
                actions.Disclosure(row.Key);
                partFired = true;
            }
        }

        if (row.Checkable)
        {
            // Right-aligned when the caller asked for it - the box's own rect, its own hit, the same per-part
            // contract as the left placement - and otherwise exactly where it has always been: after the indent.
            float boxX = layout.CheckboxRightInset.HasValue
                ? Math.Max(band.x, band.xMax - Math.Max(0f, layout.CheckboxRightInset.Value) - partSide)
                : x;
            var box = new Rect(boxX, band.y + (band.height - partSide) * 0.5f, partSide, partSide);
            UiThemeDraw.Surface(box, row.Checked ? ink.Surface : new UiSurfaceStyle(theme.Base, ink.Text), theme.Geometry.Hairline);
            if (row.Checked)
            {
                float inset = Math.Max(2f, partSide * 0.25f);
                UiThemeDraw.Solid(
                    new Rect(box.x + inset, box.y + inset, Math.Max(1f, partSide - inset * 2f), Math.Max(1f, partSide - inset * 2f)),
                    ink.Text);
            }

            if (layout.CheckboxRightInset.HasValue)
            {
                labelRight = Math.Max(x, box.x - pad);
            }
            else
            {
                x = box.xMax + pad;
            }

            if (actions.Checkbox != null && UiNative.Button(box, ctx))
            {
                actions.Checkbox(row.Key);
                partFired = true;
            }
        }

        if (row.Image != null)
        {
            // The consumer's own picture, in its own slot: the explicit size when one was stated, else the
            // texture's natural width, and the band's inner height. The outlet letterboxes inside that slot, so
            // the drawn rect can never leave it.
            float naturalWidth = row.ImageSize.x > 0f ? row.ImageSize.x : row.Image.width;
            float slotWidth = Math.Max(1f, Math.Min(naturalWidth, labelRight - x - pad));
            var slot = new Rect(x, band.y + pad, slotWidth, Math.Max(1f, band.height - pad * 2f));
            UiThemeDraw.Image(slot, row.Image, ink.Text);
            x = slot.xMax + pad;
        }

        float labelWidth = labelRight - x;
        if (labelWidth > 1f)
        {
            UiThemeDraw.Label(
                new Rect(x, band.y, labelWidth, band.height),
                ReadText(row, ctx),
                theme,
                ink.Text,
                AtomVocabulary.TextFont(ctx),
                TextAnchor.MiddleLeft,
                singleLine: true);
        }

        // One band, one body target: the funnel owns whether the element may take it, and a click a part
        // already took is not offered to the band as well.
        if (!partFired && actions.Body != null && UiNative.Button(band, ctx))
        {
            actions.Body(row.Key);
        }
    }

    private static string ReadText(UiTreeRow row, UiWidgetContext ctx)
    {
        return row.TextKey.Length > 0 ? ctx.Translation.Translate(row.TextKey) : row.Text;
    }
}

/// <summary>
/// The two placement inputs <see cref="UiRowBand.Draw"/> lets a caller override (R4-B). The default value -
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
    /// The distance one level of <see cref="UiTreeRow.Depth"/> indents by. Null keeps the theme's
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
/// through to the body. Each callback receives the row's stable <see cref="UiTreeRow.Key"/> - never an index
/// and never a part name to parse, so a consumer dispatches to its own typed action with its own data.
/// </summary>
public readonly struct UiRowBandActions
{
    public UiRowBandActions(
        Action<string>? body = null,
        Action<string>? disclosure = null,
        Action<string>? checkbox = null)
    {
        Body = body;
        Disclosure = disclosure;
        Checkbox = checkbox;
    }

    /// <summary>The row's body, or null when the band is not itself a target.</summary>
    public Action<string>? Body { get; }

    /// <summary>The disclosure marker, or null when expanding is the consumer's business elsewhere.</summary>
    public Action<string>? Disclosure { get; }

    /// <summary>The checkbox, or null when the row shows its checked state without offering to change it.</summary>
    public Action<string>? Checkbox { get; }
}
