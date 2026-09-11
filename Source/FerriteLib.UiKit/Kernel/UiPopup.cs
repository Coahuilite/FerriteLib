using System;
using System.Collections.Generic;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// The single geometry-and-input primitive for dropdown-shaped popups. Every surface that opens a
/// session popup over content must draw its option list through <see cref="DrawOptionList"/>: it is
/// the only place that publishes the covered rect (<see cref="UiSession.SetPopupRect"/>), which is
/// what lets <see cref="UiNative"/> make covered triggers yield, and the only place that applies the
/// viewport clamp/flip rule (<see cref="RectFor"/>). A second copy of either rule is exactly how the
/// consumer-side composite dropdowns shipped without them and let a covered trigger steal the click
/// an option row was about to make.
/// </summary>
public static class UiPopup
{
    /// <summary>Height of one option row; popup geometry and the fitting audit share this constant.</summary>
    public const float OptionHeight = 24f;


    /// <summary>
    /// Popup rect in Host window space: below the anchor when it fits, above it when it does not, and
    /// never beyond the viewport. A popup running off the window edge cannot be clicked at all, so the
    /// flip is a correctness rule, not cosmetics. A viewport the host never published (zero height)
    /// keeps the plain below-the-anchor placement.
    /// </summary>
    public static Rect RectFor(Rect anchor, int optionCount, Rect viewport)
    {
        float height = optionCount * OptionHeight;
        float y = anchor.yMax;
        float x = anchor.x;
        if (viewport.height <= 0f) return new Rect(x, y, anchor.width, height);

        if (y + height > viewport.yMax && anchor.y - height >= viewport.y)
        {
            y = anchor.y - height;
        }
        else if (y + height > viewport.yMax)
        {
            y = Math.Max(viewport.y, viewport.yMax - height);
        }

        if (x + anchor.width > viewport.xMax)
        {
            x = Math.Max(viewport.x, viewport.xMax - anchor.width);
        }

        return new Rect(x, y, anchor.width, height);
    }

    /// <summary>
    /// Draws the popup panel plus one hit-tested row per option, publishes the covered rect for the
    /// next frame's yield check, and invokes <paramref name="onSelected"/> with the chosen value after
    /// closing the popup. Option rows are single-line surfaces: a wrapping option means a too-narrow
    /// popup, which the fitting audit must see on the width axis rather than absorb silently.
    /// </summary>
    public static void DrawOptionList(
        Rect popupRect,
        string elementId,
        UiWidgetContext ctx,
        IReadOnlyList<KeyValuePair<string, string>> options,
        string current,
        Action<string> onSelected)
    {
        if (string.IsNullOrEmpty(elementId)) throw new ArgumentException("Popup element id is required.", nameof(elementId));
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        if (options == null) throw new ArgumentNullException(nameof(options));
        if (onSelected == null) throw new ArgumentNullException(nameof(onSelected));

        // Published for the *next* frame's content pass: a click on a popup row is delivered in a
        // later frame than the one that drew the row, and only the triggers below can yield to it.
        ctx.Session.SetPopupRect(popupRect);
        if (UiNative.Trace != null)
        {
            UiNative.Trace("publish id=" + elementId + " rect=" + UiNative.Describe(popupRect));
        }
        UiThemeDraw.Panel(popupRect, ctx.Theme);

        for (int i = 0; i < options.Count; i++)
        {
            Rect rowRect = new(popupRect.x, popupRect.y + i * OptionHeight, popupRect.width, OptionHeight);
            bool selected = string.Equals(options[i].Value, current, StringComparison.Ordinal);
            // The same table the trigger field and the status outlets read: this row used to re-derive
            // the mapping verbatim, which is the fourth copy the one-table rule exists to delete.
            UiResolvedStyle style = ctx.Theme.Styles.Resolve(selected ? UiStatusTone.Active : UiStatusTone.Neutral);
            float padding = ctx.Theme.Geometry.Padding;
            UiThemeDraw.Surface(rowRect, style.Surface, ctx.Theme.Geometry.Hairline);
            UiThemeDraw.Label(
                new Rect(rowRect.x + padding, rowRect.y, rowRect.width - padding * 2f, rowRect.height),
                options[i].Key,
                ctx.Theme,
                style.Text,
                UiFont.Small,
                TextAnchor.MiddleLeft,
                singleLine: true);

            if (UiNative.DropdownOptionRow(rowRect, elementId, ctx.Session))
            {
                UiNative.ConsumePointerEvent();
                ctx.Session.ClosePopup();
                onSelected(options[i].Value);
            }
        }
    }
}
