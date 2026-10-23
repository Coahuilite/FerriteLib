using System;
using System.Collections.Generic;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// The single geometry-and-input primitive for dropdown-shaped popups. Every surface that opens a
/// session popup over content must draw its option list through <see cref="DrawChoices{T}"/>: it is
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
    /// The floor of one row is the one place the bound is not absolute: a viewport shorter than
    /// <see cref="OptionHeight"/> still gets a single row, so that menu stands one row proud of the window edge.
    /// It is written down rather than smoothed over because the alternative - a zero-height menu - is the same
    /// unreachable-option defect this slice exists to fix, one row smaller. The shortest real host this line
    /// ships into is far above that, and it is a playtest question, not a product blocker.
    /// </para>
    /// <para>
    /// This is the single answer behind the height, the row loop and the scroll limit, so a long menu's last
    /// option is reachable by scrolling instead of being painted below the window where no pointer can arrive.
    /// </para>
    /// </summary>
    public static int VisibleRowCount(int optionCount, UiLayoutRect viewport)
    {
        int fits = (int)(viewport.Height / OptionHeight);
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
    public static UiLayoutRect RectFor(UiLayoutRect anchor, int optionCount, UiLayoutRect viewport)
    {
        float height = VisibleRowCount(optionCount, viewport) * OptionHeight;
        float y = anchor.YMax;
        float x = anchor.X;
        if (viewport.Height <= 0f) return new UiLayoutRect(x, y, anchor.Width, height);

        if (y + height > viewport.YMax && anchor.Y - height >= viewport.Y)
        {
            y = anchor.Y - height;
        }
        else if (y + height > viewport.YMax)
        {
            // Neither side has the room. The height is already bounded to the viewport, so the menu fits the
            // window entirely: pin it to the TOP. That keeps the first options - the ones a player looks for
            // first - inside the rect, and it leaves the bottom of a low trigger below the menu where a
            // close-click can still land. The old branch clamped the bottom edge instead, which was only ever
            // reachable because the height could exceed the viewport: with a bounded menu it would park the
            // whole trigger under the last rows for no reason.
            y = viewport.Y;
        }

        if (x + anchor.Width > viewport.XMax)
        {
            x = Math.Max(viewport.X, viewport.XMax - anchor.Width);
        }

        return new UiLayoutRect(x, y, anchor.Width, height);
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
    /// topmost layer and these rows are its own. <see cref="ctx.Controls.PopupOption"/> applies the shared
    /// capture protocol with that row's stable value identity. Hover consumes nothing.
    /// </para>
    /// <para>
    /// A row that the scroll has moved out of the menu is not drawn, not hovered and not hit-tested: the answer
    /// it would report belongs to a rect that is not on screen. Scrolling is this list's own rect's business
    /// (<see cref="RowsOf"/>), and the wheel that drives it is routed by
    /// <see cref="ctx.Controls.TakePopupWheelIfCovering"/>.
    /// </para>
    /// </summary>
    /// <summary>Draw one typed choice source; return the hovered source index, or -1 outside visible rows.</summary>
    public static int DrawChoices<T>(UiLayoutRect popupRect,string elementId,UiWidgetContext ctx,
        IReadOnlyList<UiChoice<T>> options,T current,Action<T> onSelected,bool selectable=true)
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
            UiLayoutRect rowRect = RowRect(popupRect, slot);
            if (ctx.Controls.IsWindowMouseOver(rowRect))
            {
                hovered = index;
            }

            DrawRow(ctx.Drawing,rowRect, UiChoiceSource.Label(options[index],ctx.Translation), EqualityComparer<T>.Default.Equals(options[index].Value,current), ctx.Theme);
            UiInteractionResult input = ctx.Controls.PopupOption(rowRect, elementId, ctx, options[index].Value, selectable);
            if (input.Request)
            {
                ctx.Session.ClosePopup();
                UiInteractionTarget target = ctx.Controls.InteractionTarget(ctx, elementId, "popup-option", options[index].Value);
                UiValueBehavior.Request(target, UiValueBehavior.Selection(input, options[index].Value), onSelected);
                return hovered;
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
    private static PopupWindow RowsOf(UiLayoutRect popupRect, UiWidgetContext ctx, int optionCount)
    {
        int visible = (int)(popupRect.Height / OptionHeight);
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
    private static void PushPopupLayer(UiLayoutRect popupRect, string elementId, UiWidgetContext ctx)
    {
        ctx.Session.PushHitLayer(ctx.Node, popupRect, isPopup: true, popupOwnerId: elementId);
        if (ctx.Controls.Trace != null)
        {
            // The published rect and the scroll that picked which rows live inside it, in one line: a real-game
            // log has to be able to say which option was reachable where, not only that a menu was open.
            ctx.Controls.Trace("publish id=" + elementId + " rect=" + UiControls.Describe(popupRect)
                + " firstRow=" + ctx.Session.OpenPopupScrollRows);
        }

        UiThemeDraw.Panel(ctx.Drawing,popupRect, ctx.Theme);
    }

    /// <summary>
    /// One option row's rect: the row band the bounded popup reserved for <paramref name="slot"/>, the slot
    /// WITHIN the visible window (not the index into the caller's option list - the window's first row is what
    /// turns one into the other, and only rows inside the menu's own rect may be painted or hit).
    /// </summary>
    private static UiLayoutRect RowRect(UiLayoutRect popupRect, int slot)
    {
        return new UiLayoutRect(popupRect.X, popupRect.Y + slot * OptionHeight, popupRect.Width, OptionHeight);
    }

    /// <summary>
    /// One option row's paint: the theme table's Active entry when it is the current value, else Neutral, with
    /// the label inset by the theme's padding. The trigger field and the status outlets read the same table
    /// (this row used to re-derive the mapping verbatim, which is the fourth copy the one-table rule exists to
    /// delete), and both option lists paint through here.
    /// </summary>
    private static void DrawRow(IUiDrawing drawing,UiLayoutRect rowRect, string label, bool selected, UiTheme theme)
    {
        UiResolvedStyle style = theme.Styles.Resolve(selected ? UiStatusTone.Active : UiStatusTone.Neutral);
        float padding = theme.Geometry.Padding;
        UiThemeDraw.Surface(drawing,rowRect, style.Surface, theme.Geometry.Hairline);
        UiThemeDraw.Label(
drawing,            new UiLayoutRect(rowRect.X + padding, rowRect.Y, rowRect.Width - padding * 2f, rowRect.Height),
            label,
            theme,
            style.Text,
            theme.Styles.Font,
            UiTextAnchor.MiddleLeft,
            singleLine: true);
    }
}
