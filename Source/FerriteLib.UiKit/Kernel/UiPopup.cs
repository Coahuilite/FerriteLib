using System;
using System.Collections.Generic;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// The single geometry-and-input primitive for dropdown-shaped popups. Every surface that opens a
/// session popup over content must draw its option list through <see cref="DrawOptionList"/>: it is
/// the only place that pushes a popup layer into the owned hit stack
/// (<see cref="UiSession.PushHitLayer"/>), which is what makes anything under the popup yield the click,
/// and the only place that applies the viewport rules - the clamp/flip and the bounded, scrollable height
/// (<see cref="RectFor"/>, <see cref="RowsOf"/>). A second copy of either rule is exactly how the
/// consumer-side composite dropdowns shipped without them and let a covered trigger steal the click an
/// option row was about to make, and how a long menu's tail became paint no pointer can reach.
/// </summary>
public static class UiPopup
{
    /// <summary>Height of one option row; popup geometry and the fitting audit share this constant.</summary>
    public const float OptionHeight = 24f;


    /// <summary>
    /// How many option rows one wheel notch moves a bounded menu. A notch is not a row: at one row per notch a
    /// forty-option menu costs the player forty notches, and the industry default for a wheel step is three
    /// lines. It is a library default, not a consumer's number, and it is a constant rather than a parameter so
    /// the geometry, the hit test and the scroll cannot disagree about how fast the same rect moves.
    /// </summary>
    public const float WheelRowsPerNotch = 3f;

    /// <summary>
    /// The whole rows a menu of <paramref name="optionCount"/> options shows inside <paramref name="viewport"/>:
    /// every option it carries when they all fit, otherwise as many as fit the finite viewport, and never zero -
    /// a menu that shows no row cannot be chosen from at all. A viewport the host never published (zero height)
    /// bounds nothing, which is the same escape hatch <see cref="RectFor"/> keeps.
    /// <para>
    /// This is the single answer behind the height, the row loop and the scroll limit, so a long menu's last
    /// option is reachable by scrolling instead of being painted below the window where no pointer can arrive.
    /// </para>
    /// </summary>
    public static int VisibleRowCount(int optionCount, Rect viewport)
    {
        if (optionCount <= 0) return 0;
        if (viewport.height <= 0f) return optionCount;

        int fits = (int)(viewport.height / OptionHeight);
        if (fits < 1) fits = 1;
        return fits < optionCount ? fits : optionCount;
    }

    /// <summary>
    /// Popup rect in Host window space: below the anchor when it fits, above it when it does not, and never
    /// beyond the viewport. A popup running off the window edge cannot be clicked at all, so the flip is a
    /// correctness rule, not cosmetics. A viewport the host never published (zero height) keeps the plain
    /// below-the-anchor placement.
    /// <para>
    /// <b>The height is bounded, and it is bounded to whole rows.</b> It used to be
    /// <c>optionCount * OptionHeight</c> whatever the viewport said, which painted a long menu straight through
    /// the bottom of the window: the options past the edge had no rect a pointer could reach, and the clamp
    /// branch could only move the starting point, not the overflow. Bounding to <see cref="VisibleRowCount"/>
    /// keeps the one invariant the arbitration depends on - every point inside the layer is inside exactly one
    /// row - because the height stays a multiple of <see cref="OptionHeight"/>.
    /// </para>
    /// </summary>
    public static Rect RectFor(Rect anchor, int optionCount, Rect viewport)
    {
        float height = VisibleRowCount(optionCount, viewport) * OptionHeight;
        float y = anchor.yMax;
        float x = anchor.x;
        if (viewport.height <= 0f) return new Rect(x, y, anchor.width, height);

        if (y + height > viewport.yMax && anchor.y - height >= viewport.y)
        {
            y = anchor.y - height;
        }
        else if (y + height > viewport.yMax)
        {
            // Neither side has the room. The height is already bounded to the viewport, so the menu fits the
            // window entirely: pin it to the TOP. That keeps the first options - the ones a player looks for
            // first - inside the rect, and it leaves the bottom of a low trigger below the menu where a
            // close-click can still land. The old branch clamped the bottom edge instead, which was only ever
            // reachable because the height could exceed the viewport: with a bounded menu it would park the
            // whole trigger under the last rows for no reason.
            y = viewport.y;
        }

        if (x + anchor.width > viewport.xMax)
        {
            x = Math.Max(viewport.x, viewport.xMax - anchor.width);
        }

        return new Rect(x, y, anchor.width, height);
    }

    /// <summary>
    /// Draws the popup panel plus one hit-tested row for every option the bounded menu shows, publishes the
    /// covered rect for the next frame's yield check, and invokes <paramref name="onSelected"/> with the chosen
    /// value after closing the popup. Option rows are single-line surfaces: a wrapping option means a too-narrow
    /// popup, which the fitting audit must see on the width axis rather than absorb silently.
    /// <para>
    /// Returns the <b>hovered</b> option's value, or an empty string when no visible row is hovered, so the
    /// caller can publish an option-level fact - the dropdown's <c>HoverHelpKey</c> identity - without
    /// re-deriving this helper's row geometry. The hover test is the raw rect form on purpose: the popup is the
    /// topmost layer and these rows are its own, which is the same reason <see cref="UiNative.DropdownOptionRow"/>
    /// is the context-free hit. Nothing is consumed by the test, so the row's click below is unaffected.
    /// </para>
    /// <para>
    /// A row that the scroll has moved out of the menu is not drawn, not hovered and not hit-tested: the answer
    /// it would report belongs to a rect that is not on screen. Scrolling is this list's own rect's business
    /// (<see cref="RowsOf"/>), and the wheel that drives it is routed by
    /// <see cref="UiNative.TakePopupWheelIfCovering"/>.
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
        PopupWindow window = RowsOf(popupRect, ctx, options.Count);

        string hovered = "";
        for (int slot = 0; slot < window.RowCount; slot++)
        {
            int index = window.FirstRow + slot;
            Rect rowRect = RowRect(popupRect, slot);
            if (UiNative.IsMouseOver(rowRect))
            {
                hovered = options[index].Value;
            }

            DrawRow(rowRect, options[index].Key,
                string.Equals(options[index].Value, current, StringComparison.Ordinal), ctx.Theme);
            if (UiNative.DropdownOptionRow(rowRect, elementId, ctx.Session))
            {
                UiNative.ConsumePointerEvent();
                ctx.Session.ClosePopup();
                onSelected(options[index].Value);
            }
        }

        return hovered;
    }

    /// <summary>
    /// The typed sibling of <see cref="DrawOptionList"/> (R4-A, 0.7.x): the same panel, the same hit layer, the
    /// same bounded row window and the same row hit, for options whose values are not strings. Selection and
    /// hover compare the boxed values with <c>object.Equals</c> - value equality for an enum or a boxed
    /// primitive, and REFERENCE equality for a reference-typed choice that does not override <c>Equals</c> (bind
    /// such a type only if its instances are the ones the page hands back, as the demo's lane does) - and
    /// <paramref name="onSelected"/> receives the chosen option's real instance back: no label is parsed and no
    /// value is spelled, which is the whole reason this overload exists beside the string one.
    /// <para>
    /// Returns the hovered row's INDEX, or -1 when no visible row is hovered, so the caller can publish that
    /// row's option-level help identity without re-deriving this helper's row geometry. The index is the index
    /// into the CALLER'S list, not the slot in the window: a scrolled menu still reports the option it carries,
    /// and the two list forms stay one contract rather than two habits.
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
        PopupWindow window = RowsOf(popupRect, ctx, options.Count);

        int hovered = -1;
        for (int slot = 0; slot < window.RowCount; slot++)
        {
            int index = window.FirstRow + slot;
            Rect rowRect = RowRect(popupRect, slot);
            if (UiNative.IsMouseOver(rowRect))
            {
                hovered = index;
            }

            DrawRow(rowRect, options[index].Text, Equals(options[index].Value, current), ctx.Theme);
            if (UiNative.DropdownOptionRow(rowRect, elementId, ctx.Session))
            {
                UiNative.ConsumePointerEvent();
                ctx.Session.ClosePopup();
                onSelected(options[index].Value);
            }
        }

        return hovered;
    }

    /// <summary>
    /// One frame's slice of the option list: the row the bounded rect starts with, and how many whole rows it
    /// shows. Computed once per list, before any row is painted, hovered or hit-tested, so geometry, hit and
    /// scroll read one finite viewport instead of three opinions.
    /// <para>
    /// The window publishes its own limit to the session (<see cref="UiSession.NotePopupScrollExtent"/>) and
    /// re-reads the clamped result, which is what makes a wheel notch that ran past the end of the list, or a
    /// list that got shorter under an already-scrolled menu, impossible to leave the pointer clicking at paint
    /// that is not there.
    /// </para>
    /// </summary>
    private static PopupWindow RowsOf(Rect popupRect, UiWidgetContext ctx, int optionCount)
    {
        int visible = (int)(popupRect.height / OptionHeight);
        if (visible < 1) visible = 1;
        if (visible > optionCount) visible = optionCount;

        ctx.Session.NotePopupScrollExtent(optionCount - visible);
        return new PopupWindow(ctx.Session.OpenPopupScrollRows, visible);
    }

    /// <summary>The rows one option list draws and hit-tests in one pass of one bounded menu.</summary>
    private readonly struct PopupWindow
    {
        public PopupWindow(int firstRow, int rowCount)
        {
            FirstRow = firstRow;
            RowCount = rowCount;
        }

        public int FirstRow { get; }

        public int RowCount { get; }
    }

    /// <summary>
    /// The popup as a hit layer: the stack is what makes a covered element yield, uniformly for every
    /// primitive, and the layer carries the owning dropdown's id for the two questions that DO need an owner -
    /// whose menu a wheel notch belongs to (<see cref="UiSession.OpenPopupCoversPointer"/>) and which dropdown a
    /// diagnostic should blame. It is not an input exemption: coverage is the rect, and the owner yields to its
    /// own menu where that menu covers it, exactly like any other element under it. Element identity alone
    /// cannot express any of this: a composite draws several dropdowns under one UiNode. Both option lists go
    /// through here, so the rule stays in one place.
    /// </summary>
    private static void PushPopupLayer(Rect popupRect, string elementId, UiWidgetContext ctx)
    {
        ctx.Session.PushHitLayer(ctx.Node ?? ctx.Session.ActiveNode, popupRect, isPopup: true, popupOwnerId: elementId);
        if (UiNative.Trace != null)
        {
            // The published rect and the scroll that picked which rows live inside it, in one line: a real-game
            // log has to be able to say which option was reachable where, not only that a menu was open.
            UiNative.Trace("publish id=" + elementId + " rect=" + UiNative.Describe(popupRect)
                + " firstRow=" + ctx.Session.OpenPopupScrollRows);
        }

        UiThemeDraw.Panel(popupRect, ctx.Theme);
    }

    /// <summary>
    /// One option row's rect: the row band the bounded popup reserved for <paramref name="slot"/>, the slot
    /// WITHIN the visible window (not the index into the caller's option list - the window's first row is what
    /// turns one into the other, and only rows inside the menu's own rect may be painted or hit).
    /// </summary>
    private static Rect RowRect(Rect popupRect, int slot)
    {
        return new Rect(popupRect.x, popupRect.y + slot * OptionHeight, popupRect.width, OptionHeight);
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
