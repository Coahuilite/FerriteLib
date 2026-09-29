using System;
using System.Collections.Generic;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// The single geometry-and-input primitive for dropdown-shaped popups. Every surface that opens a
/// session popup over content must draw its option list through <see cref="DrawOptionList"/>: it is
/// the only place that pushes a popup layer into the owned hit stack
/// (<see cref="UiSession.PushHitLayer"/>), which is what makes anything under the popup yield the click,
/// and the only place that applies the viewport clamp/flip rule (<see cref="RectFor"/>). A second copy
/// of either rule is exactly how the consumer-side composite dropdowns shipped without them and let a
/// covered trigger steal the click an option row was about to make.
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
    /// <para>
    /// Returns the <b>hovered</b> option's value, or an empty string when no row is hovered, so the caller can
    /// publish an option-level fact — the dropdown's <c>HoverHelpKey</c> identity — without re-deriving this
    /// helper's row geometry. The hover test is the raw rect form on purpose: the popup is the topmost layer
    /// and these rows are its own, which is the same reason <see cref="UiNative.DropdownOptionRow"/> is the
    /// context-free hit. Nothing is consumed by the test, so the row's click below is unaffected.
    /// </para>
    /// </summary>
    public static string DrawOptionList(
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

        PushPopupLayer(popupRect, elementId, ctx);

        string hovered = "";
        for (int i = 0; i < options.Count; i++)
        {
            Rect rowRect = RowRect(popupRect, i);
            if (UiNative.IsMouseOver(rowRect))
            {
                hovered = options[i].Value;
            }

            DrawRow(rowRect, options[i].Key, string.Equals(options[i].Value, current, StringComparison.Ordinal), ctx.Theme);
            if (UiNative.DropdownOptionRow(rowRect, elementId, ctx.Session))
            {
                UiNative.ConsumePointerEvent();
                ctx.Session.ClosePopup();
                onSelected(options[i].Value);
            }
        }

        return hovered;
    }

    /// <summary>
    /// The typed sibling of <see cref="DrawOptionList"/> (R4-A, 0.7.x): the same panel, the same hit layer, the
    /// same row geometry and the same row hit, for options whose values are not strings. Selection and hover
    /// compare the boxed values with <c>object.Equals</c> - value equality for an enum or a boxed primitive, and
    /// REFERENCE equality for a reference-typed choice that does not override <c>Equals</c> (bind such a type
    /// only if its instances are the ones the page hands back, as the demo's lane does) - and
    /// <paramref name="onSelected"/> receives the chosen option's real instance back: no label is parsed and no
    /// value is spelled, which is the whole reason this overload exists beside the string one.
    /// <para>
    /// Returns the hovered row's INDEX, or -1 when no row is hovered, so the caller can publish that row's
    /// option-level help identity without re-deriving this helper's row geometry.
    /// </para>
    /// </summary>
    public static int DrawChoiceList(
        Rect popupRect,
        string elementId,
        UiWidgetContext ctx,
        IReadOnlyList<UiChoice<object?>> options,
        object? current,
        Action<object?> onSelected)
    {
        if (string.IsNullOrEmpty(elementId)) throw new ArgumentException("Popup element id is required.", nameof(elementId));
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        if (options == null) throw new ArgumentNullException(nameof(options));
        if (onSelected == null) throw new ArgumentNullException(nameof(onSelected));

        PushPopupLayer(popupRect, elementId, ctx);

        int hovered = -1;
        for (int i = 0; i < options.Count; i++)
        {
            Rect rowRect = RowRect(popupRect, i);
            if (UiNative.IsMouseOver(rowRect))
            {
                hovered = i;
            }

            DrawRow(rowRect, options[i].Text, Equals(options[i].Value, current), ctx.Theme);
            if (UiNative.DropdownOptionRow(rowRect, elementId, ctx.Session))
            {
                UiNative.ConsumePointerEvent();
                ctx.Session.ClosePopup();
                onSelected(options[i].Value);
            }
        }

        return hovered;
    }

    /// <summary>
    /// The popup as a hit layer: the stack is what makes a covered element yield, uniformly for every
    /// primitive, and the layer carries the owning dropdown's id so the owner's own trigger keeps its
    /// toggle-to-close behaviour while every SIBLING control of the same composite element yields. Element
    /// identity alone cannot express that: a composite draws several dropdowns under one UiNode. Both option
    /// lists go through here, so the rule stays in one place.
    /// </summary>
    private static void PushPopupLayer(Rect popupRect, string elementId, UiWidgetContext ctx)
    {
        ctx.Session.PushHitLayer(ctx.Node ?? ctx.Session.ActiveNode, popupRect, isPopup: true, popupOwnerId: elementId);
        if (UiNative.Trace != null)
        {
            UiNative.Trace("publish id=" + elementId + " rect=" + UiNative.Describe(popupRect));
        }

        UiThemeDraw.Panel(popupRect, ctx.Theme);
    }

    /// <summary>One option row's rect: the row band the popup geometry reserved for index <paramref name="index"/>.</summary>
    private static Rect RowRect(Rect popupRect, int index)
    {
        return new Rect(popupRect.x, popupRect.y + index * OptionHeight, popupRect.width, OptionHeight);
    }

    /// <summary>
    /// One option row's paint: the theme table's Active entry when it is the current value, else Neutral, with
    /// the label inset by the theme's padding. The trigger field and the status outlets read the same table
    /// (this row used to re-derive the mapping verbatim, which is the fourth copy the one-table rule exists to
    /// delete), and both option lists paint through here.
    /// </summary>
    private static void DrawRow(Rect rowRect, string label, bool selected, UiTheme theme)
    {
        UiResolvedStyle style = theme.Styles.Resolve(selected ? UiStatusTone.Active : UiStatusTone.Neutral);
        float padding = theme.Geometry.Padding;
        UiThemeDraw.Surface(rowRect, style.Surface, theme.Geometry.Hairline);
        UiThemeDraw.Label(
            new Rect(rowRect.x + padding, rowRect.y, rowRect.width - padding * 2f, rowRect.height),
            label,
            theme,
            style.Text,
            UiFont.Small,
            TextAnchor.MiddleLeft,
            singleLine: true);
    }
}
