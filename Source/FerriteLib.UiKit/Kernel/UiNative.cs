using System;
using System.Globalization;
using UnityEngine;
using Verse;
using VerseWidgets = Verse.Widgets;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// Thin wrappers around native IMGUI/Verse controls. All transient state is read/written through
/// <see cref="UiSession"/>; this type has no process-global registries or deferred dispatch.
/// </summary>
public static class UiNative
{
    // Test seams, same pattern as the old UiInteract but scoped to the greenfield kernel.
    internal static bool DebugMousePositionEnabled;
    internal static Vector2 DebugMousePosition;
    internal static bool DebugClick;
    internal static bool DebugMouseDown;
    internal static bool DebugMouseDrag;
    internal static bool DebugMouseUp;
    internal static float DebugWheelNotches;
    internal static bool DebugEnter;
    internal static bool DebugFocusLost;
    internal static bool DebugLayoutEvent;
    internal static int DebugHotControl;
    internal static int DebugControlIdCounter;
    internal static Func<Rect, float, float, float, float>? SliderOverride;
    internal static Func<Rect, string, string>? TextFieldOverride;
    internal static Func<Rect, bool>? ButtonOverride;

    /// <summary>
    /// Allocates a stable native IMGUI control id for an element. This is the id used with
    /// <see cref="GUIUtility.hotControl"/> so drag interactions follow real IMGUI capture semantics.
    /// </summary>
    public static int GetControlId(string elementId)
    {
        if (DebugMousePositionEnabled)
        {
            // Keep ids stable across frames in the stub path; real GUIUtility ids are stable for
            // the same control sequence, but a per-call counter would break capture continuity.
            return (elementId ?? "").GetHashCode();
        }

        return GUIUtility.GetControlID((elementId ?? "").GetHashCode(), FocusType.Passive);
    }

    /// <summary>True when <paramref name="controlId"/> currently owns the native hot control.</summary>
    public static bool HasHotControl(int controlId)
    {
        if (DebugMousePositionEnabled) return DebugHotControl == controlId;
        return GUIUtility.hotControl == controlId;
    }

    /// <summary>Captures the native hot control for <paramref name="controlId"/>.</summary>
    public static void CaptureHotControl(int controlId)
    {
        if (DebugMousePositionEnabled)
        {
            DebugHotControl = controlId;
            return;
        }

        GUIUtility.hotControl = controlId;
    }

    /// <summary>Releases the native hot control when <paramref name="controlId"/> owns it.</summary>
    public static void ReleaseHotControl(int controlId)
    {
        if (DebugMousePositionEnabled)
        {
            if (DebugHotControl == controlId) DebugHotControl = 0;
            return;
        }

        if (GUIUtility.hotControl == controlId) GUIUtility.hotControl = 0;
    }

    /// <summary>
    /// The raw invisible button, without hit-stack arbitration. A caller that holds a context must use
    /// <see cref="Button(Rect, UiWidgetContext)"/>. This overload has no session, and a static primitive
    /// cannot reach one without a process-wide mutable static - which the promotion gate forbids - so it
    /// keeps the pre-stack behaviour: a covering popup does not stop it. <c>docs/api-tiers.md</c> states
    /// that contract and the one-line migration for consumers.
    /// </summary>
    public static bool Button(Rect rect)
    {
        if (ButtonOverride != null) return ButtonOverride(rect);
        return VerseWidgets.ButtonInvisible(rect);
    }

    /// <summary>
    /// The invisible button with popup arbitration: when the pointer sits inside a popup layer, this element
    /// does not take the click. Ordinary overlapping controls still follow native IMGUI event consumption.
    /// Every library widget and consumer control that holds a context belongs here: one rule, and no
    /// per-element yield branch anywhere.
    /// <para>
    /// The rule is geometric and it has no owner exemption. A dropdown trigger keeps its toggle-to-close
    /// because the trigger's own rect is not the menu's rect: a click outside the published menu hits the
    /// trigger, and a click inside it belongs to the option row under the pointer - including when the menu
    /// was clamped or bounded over its own trigger, which is the case the owner-id exemption used to hand to
    /// the trigger and lose the selection (F09/D1, retired in FL-IC1).
    /// </para>
    /// </summary>
    public static bool Button(Rect rect, UiWidgetContext ctx)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        UiNode element = ctx.Node ?? ctx.Session.ActiveNode;
#if FER_DEV
        string eventBefore = UiDiagnosticHub.ActiveSubscription?.Geometry == null ? "" : DiagnosticEventName();
#endif

        // A disabled element does not capture the pointer at all - no hot control, no consumed event, no
        // click for whatever is underneath to miss. The check is here rather than in the widget for the
        // same reason the yield check is: one rule for every kind, including kinds this library has never
        // seen, instead of a per-widget branch each of them can forget.
        if (IsInputDisabled(element))
        {
#if FER_DEV
            UiDevGeometryProbe.NoteInput(ctx, element, rect, "disabled", eventBefore);
#endif
            return false;
        }

        if (ctx.Session.IsPointerOverHigherLayer(element, WindowPointer(ctx)))
        {
#if FER_DEV
            UiDevGeometryProbe.NoteInput(ctx, element, rect, "covered", eventBefore);
#endif
            return false;
        }

#if FER_DEV
        bool fired = Button(rect);
        UiDevGeometryProbe.NoteInput(ctx, element, rect, fired ? "hit" : "miss", eventBefore);
        return fired;
#else
        return Button(rect);
#endif
    }

    /// <summary>
    /// The receive-eligibility answer for a control that takes the pointer itself instead of handing a press
    /// to a native control: a new interaction must belong to a window the backend lets receive input, a valid
    /// viewport, and an interactive node, and a popup covering that point outranks it. Four questions,
    /// answered by the same code <see cref="Button(Rect,UiWidgetContext)"/> consults, plus the effective clip
    /// a raw-pointer control cannot delegate to a native group:
    /// <list type="bullet">
    /// <item>the element (or an ancestor) is not disabled;</item>
    /// <item>the pointer is not inside any popup layer - the menu, not the thing under it, takes the press;</item>
    /// <item>the pointer is inside <paramref name="rect"/> by the native hover answer, so the window, the
    /// enclosing scroll views and the obscuring stack all get to say no the way they do for every native
    /// control;</item>
    /// <item>the pointer is inside the clip the engine published for this element.</item>
    /// </list>
    /// <para>
    /// This answers a NEW press only. Continuing an existing drag is a question of capture ownership, not of
    /// where the pointer happens to be this frame, and a caller that gates its release with this method
    /// re-creates the failure this rule exists to prevent: a held drag ends on its release, not on a hover.
    /// </para>
    /// </summary>
    public static bool CanReceivePointerPress(Rect rect, UiWidgetContext ctx)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        UiSession session = ctx.Session;
        UiNode element = ctx.Node ?? session.ActiveNode;
        Vector2 windowPoint = WindowPointer(ctx);

        if (IsInputDisabled(element)) return false;
        if (session.IsPointerOverHigherLayer(element, windowPoint)) return false;
        if (!IsMouseOver(rect)) return false;
        return session.IsPointInEffectiveClip(windowPoint);
    }

    /// <summary>
    /// The event pointer in Host window space, lifted by the origin the engine published for the element being
    /// drawn. A control that holds a context needs this to compare its own draw-space geometry with the
    /// window-space hit stack; a caller with no context is already in window space and keeps
    /// <see cref="PointerPosition()"/>.
    /// </summary>
    public static Vector2 WindowPointer(UiWidgetContext ctx)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        return PointerPositionIn(ctx);
    }

#if FER_DEV
    // Observation only: the original phase is retained by the host's capture before controls consume it.
    internal static string DiagnosticEventName()
    {
        if (DebugMousePositionEnabled)
        {
            if (DebugMouseDown) return "MouseDown";
            if (DebugMouseUp) return "MouseUp";
        }
        return Event.current == null ? "none" : Event.current.type.ToString();
    }
#endif

    /// <summary>
    /// True when the element being drawn is disabled and must not take input. The engine publishes the
    /// disabled state on the element's node once per draw (<see cref="UiNode.IsDisabled"/>), and the walk
    /// up the parent chain is what makes a widget's own sub-node answer like the element that owns it:
    /// <see cref="UiWidgetContext.Child"/> mints a sub-node under the element, so the two are one control
    /// for input purposes.
    /// </summary>
    private static bool IsInputDisabled(UiNode? node)
    {
        for (UiNode? cursor = node; cursor != null; cursor = cursor.Parent)
        {
            if (cursor.IsDisabled) return true;
        }

        return false;
    }

    /// <summary>
    /// True when the element an interactive primitive was handed is disabled. The slider and the number
    /// field take a session and a state key rather than a context, so the node is resolved through the one
    /// bridge a page owns - the declared Id - and a key that names no arranged element answers false: that
    /// is the documented arbitrary-state-key usage of those parameters, and it must behave exactly as it
    /// did before this guard existed.
    /// </summary>
    private static bool IsElementDisabled(UiSession session, string elementId)
    {
        return session.GetNodeByElementId(elementId) is UiNode node && IsInputDisabled(node);
    }

    /// <summary>
    /// Draws/tests a dropdown trigger using the session-owned popup model. A click on the trigger
    /// opens the popup for <paramref name="elementId"/> anchored at <paramref name="rect"/>, or
    /// closes it when the same element already owns the open popup. The anchor is stored exactly
    /// as given (used by callers that already operate in window space).
    /// </summary>
    public static bool DropdownButton(Rect rect, string elementId, UiSession session)
    {
        return DropdownButtonCore(rect, elementId, session, rect, null);
    }

    /// <summary>
    /// Draws/tests a dropdown trigger like <see cref="DropdownButton(Rect,string,UiSession)"/> but
    /// converts the draw-local trigger rect into the Host's final usable window space before
    /// storing it as the popup anchor. Triggers inside scrolled/grouped containers draw in
    /// content-local coordinates; the popup is drawn and hit-tested later in the same OnGUI pass
    /// outside those scopes, so anchor, draw and hit-test must share window space.
    /// </summary>
    public static bool DropdownButton(Rect rect, string elementId, UiWidgetContext ctx)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        return DropdownButtonCore(rect, elementId, ctx.Session, ctx.ToWindowRect(rect), ctx);
    }

    private static bool DropdownButtonCore(Rect rect, string elementId, UiSession session, Rect anchor, UiWidgetContext? ctx)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));
        if (string.IsNullOrEmpty(elementId)) throw new ArgumentException("Dropdown element id is required.", nameof(elementId));

        // A disabled trigger neither opens nor closes its popup: the dropdown is one of the pointer
        // captures this guard exists for, and the context-carrying entry points answer it before any of
        // the routing below runs. Checked against the element being drawn, so it holds for a trigger that
        // is drawn by hand through the session overload as well.
        if (IsInputDisabled(ctx?.Node ?? session.ActiveNode)) return false;

        // Keep the open popup attached to this trigger, and release it when the trigger has left the window.
        // The anchor handed to this call is THIS frame's window-space rect (the context overload computes it
        // fresh at the call site) while the open-time store would otherwise freeze the menu where it was
        // opened. A no-op unless this element owns the open popup; a disabled trigger never reaches here, so
        // its popup is released by the pass-boundary reconcile instead of being refreshed.
        session.NotePopupOwnerDrawn(elementId, anchor);

        // The hit stack decides, and it decides geometrically: a popup layer whose rect contains the pointer
        // is a layer above the trigger, so the trigger must not take the click - the option row under that
        // pointer takes it. The trigger keeps its toggle-to-close without an identity exemption, because the
        // menu's rect is not the trigger's rect: a click on the part of the trigger the menu does not cover
        // is covered by nothing and still reaches this branch's other side: the option menu outranks the
        // trigger bar beneath it, and only where the menu is not.
        bool covered = session.IsPointerOverHigherLayer(ctx?.Node ?? session.ActiveNode, PointerPositionIn(ctx));
        if (Trace != null && session.OpenPopupId != null)
        {
            Vector2 local = PointerPosition();
            Vector2 window = PointerPositionIn(ctx);
            Trace("trigger id=" + elementId
                + " owner=" + session.OpenPopupId
                + " ownPopup=" + (string.Equals(session.OpenPopupId, elementId, StringComparison.Ordinal) ? "true" : "false")
                + " event=" + (Event.current != null ? Event.current.type.ToString() : "none")
                + " pointerLocal=" + Describe(local)
                + " pointerWindow=" + Describe(window)
                + " anchor=" + Describe(anchor)
                + " yields=" + (covered ? "true" : "false"));
        }

        if (covered) return false;
        if (ctx != null ? !Button(rect, ctx) : !Button(rect)) return false;

        if (session.IsPopupOpen(elementId))
        {
            session.ClosePopup();
        }
        else
        {
            session.OpenPopup(elementId, anchor);
        }

        return true;
    }

    /// <summary>
    /// Hit-tests one dropdown option row. Rows are only interactive while the same session owns a
    /// popup for <paramref name="elementId"/>; this keeps popup interaction session-local.
    /// </summary>
    public static bool DropdownOptionRow(Rect rowRect, string elementId, UiSession session)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));
        if (string.IsNullOrEmpty(elementId)) throw new ArgumentException("Dropdown element id is required.", nameof(elementId));

        bool fired = session.IsPopupOpen(elementId) && Button(rowRect);
        if (Trace != null && fired)
        {
            Trace("option fired id=" + elementId
                + " event=" + (Event.current != null ? Event.current.type.ToString() : "none")
                + " row=" + Describe(rowRect));
        }

        return fired;
    }

    /// <summary>
    /// True when a popup covers the pointer for a primitive that owns no popup of its own. Such a primitive
    /// must not take the press at all: the same refusal the context-carrying button applies, extended to the
    /// pointer-capturing primitives so a covered slider or field cannot steal the click an option row is about
    /// to make. The pointer is lifted into window space by the origin the engine published for the element
    /// being drawn - the same conversion the funnel uses.
    /// </summary>
    private static bool IsCoveredByPopup(UiSession session)
    {
        // No element identity: a value-state-keyed primitive owns no popup, so ANY covering popup covers it.
        return session.IsPointerOverHigherLayer(session.ActiveNode, PointerPosition() + session.CurrentWindowOrigin);
    }

    public static float Slider(Rect rect, string elementId, UiSession session, float value, float min, float max, out bool changed)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));
        UiValueState state = session.GetOrCreateValueState(elementId);
        if (IsCoveredByPopup(session))
        {
            // Covered: mirror the disabled branch's contract exactly - the native control is never reached,
            // no drag can start and nothing takes the hot control from the option row under the popup.
            state.FloatValue = ClampValue(value, min, max);
            if (!state.Focused)
            {
                state.EditText = FormatValue(state.FloatValue, "0.##");
            }

            changed = false;
            return state.FloatValue;
        }

        if (IsElementDisabled(session, elementId))
        {
            // Disabled: the native control is never reached, so no drag can start and nothing can take the
            // hot control away from what sits under the pointer - the funnel owns that rule for every kind,
            // exactly as Button does. The state keeps the value the caller handed in, so re-enabling resumes
            // on the model rather than on a drag that never happened.
            state.FloatValue = ClampValue(value, min, max);
            if (!state.Focused)
            {
                state.EditText = FormatValue(state.FloatValue, "0.##");
            }

            changed = false;
            return state.FloatValue;
        }

        float newValue = NativeHorizontalSlider(rect, value, min, max);
        newValue = ClampValue(newValue, min, max);
        changed = Math.Abs(newValue - value) > 0.0001f;
        state.FloatValue = newValue;
        if (changed || !state.Focused)
        {
            state.EditText = FormatValue(newValue, "0.##");
        }

        return newValue;
    }

    /// <summary>
    /// The raw text field, carrying no element identity: there is no session and no key to resolve, so it
    /// cannot consult the element's disabled state, cannot hold a draft and cannot report a commit. A caller
    /// that wants any of those — which is every page-model kind — reaches the field through
    /// <see cref="TextField(Rect, string, UiSession, string, out bool)"/>, its identity-bearing sibling. This
    /// form stays for a caller that genuinely has no element to name, and the gap it used to be is closed by
    /// that overload rather than by widening this one.
    /// </summary>
    public static string TextField(Rect rect, string text)
    {
        return NativeTextField(rect, text);
    }

    /// <summary>
    /// The identity-bearing text field: the string sibling of <see cref="NumberField"/>, and the form every
    /// page-model kind uses. Same funnel contract as the numeric one — a disabled element never reaches the
    /// native control and never focuses; a focused field keeps its draft in the session while the model holds
    /// the committed value; and the disabled refusal happens before the native control so no click is
    /// consumed.
    /// <para>
    /// The commit rule is stated once here so its two halves cannot drift: <paramref name="committed"/> is
    /// true when the buffer changed this frame, <b>or</b> when focus just left with a buffer that differs
    /// from the model. The first half is a live editor's keystroke; the second is a deferred editor's only
    /// chance to write, because the frame that leaves focus is otherwise indistinguishable from a frame that
    /// typed nothing. A caller that ignores the second half would leave the draft in the session and never
    /// move the model.
    /// </para>
    /// </summary>
    public static string TextField(
        Rect rect,
        string elementId,
        UiSession session,
        string value,
        out bool committed)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));
        UiValueState state = session.GetOrCreateValueState(elementId);
        string model = value ?? "";
        if (IsCoveredByPopup(session))
        {
            // Covered: the field must not consume the click the option row underneath it is about to make.
            // Same shape as the disabled refusal - no focus, no native control, no commit.
            state.Focused = false;
            state.EditText = model;
            committed = false;
            return model;
        }

        if (IsElementDisabled(session, elementId))
        {
            // Disabled: the field never takes focus, never reaches the native control and never commits, so
            // no click is consumed and no text is edited. The draft is dropped back onto the model so a
            // re-enabled field resumes there instead of on a dead edit.
            state.Focused = false;
            state.EditText = model;
            committed = false;
            return model;
        }

        if (!state.Focused)
        {
            state.EditText = model;
        }

        if (IsMouseDownOver(rect))
        {
            state.Focused = true;
        }

        string displayText = state.Focused ? state.EditText : model;
        string text = NativeTextField(rect, displayText) ?? "";
        bool changed = !string.Equals(text, displayText, StringComparison.Ordinal);
        state.EditText = text;

        bool left = state.Focused && (IsEnterPressed() || IsFocusLost(rect));
        if (left) state.Focused = false;

        committed = changed || (left && !string.Equals(state.EditText, model, StringComparison.Ordinal));
        if (committed) return state.EditText;
        return state.Focused ? state.EditText : model;
    }

    public static string NumberField(
        Rect rect,
        string elementId,
        UiSession session,
        float value,
        float min,
        float max,
        string format,
        out bool committed)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));
        UiValueState state = session.GetOrCreateValueState(elementId);
        if (IsCoveredByPopup(session))
        {
            // Covered: mirror the disabled refusal - no focus, no parse, no native control, no commit, so the
            // click survives for the option row the popup is drawing over this field.
            state.Focused = false;
            state.FloatValue = ClampValue(value, min, max);
            state.EditText = FormatValue(state.FloatValue, format);
            committed = false;
            return state.EditText;
        }

        if (IsElementDisabled(session, elementId))
        {
            // Disabled: the field never takes focus, never parses and never commits, and the native control
            // is never reached - so no click is consumed and no text is edited. The buffer is reset to the
            // model's value so a re-enabled field resumes there instead of on a dead edit.
            state.Focused = false;
            state.FloatValue = ClampValue(value, min, max);
            state.EditText = FormatValue(state.FloatValue, format);
            committed = false;
            return state.EditText;
        }

        if (!state.Focused)
        {
            state.FloatValue = value;
            state.EditText = FormatValue(value, format);
        }

        if (IsMouseDownOver(rect))
        {
            state.Focused = true;
        }

        string displayText = state.Focused ? state.EditText : FormatValue(state.FloatValue, format);
        string text = NativeTextField(rect, displayText);
        float previousValue = state.FloatValue;
        committed = false;

        bool parsed = TryParseNumber(text, out float parsedValue);
        if (parsed)
        {
            parsedValue = ClampValue(parsedValue, min, max);
            bool textChanged = !string.Equals(text, displayText, StringComparison.Ordinal);
            bool valueChanged = Math.Abs(parsedValue - previousValue) > 0.0001f;
            state.FloatValue = parsedValue;
            state.EditText = text;
            committed = textChanged || valueChanged;
        }
        else
        {
            state.EditText = text;
        }

        if (state.Focused && (IsEnterPressed() || IsFocusLost(rect)))
        {
            state.EditText = FormatValue(state.FloatValue, format);
            state.Focused = false;
        }

        return state.Focused ? state.EditText : FormatValue(state.FloatValue, format);
    }

    public static Vector2 PointerPosition()
    {
        if (DebugMousePositionEnabled) return DebugMousePosition;
        Event? current = Event.current;
        return current != null ? current.mousePosition : default;
    }

    public static bool IsPointerDown()
    {
        if (DebugMousePositionEnabled) return DebugMouseDown;
        Event? current = Event.current;
        return current != null && current.type == EventType.MouseDown && current.button == 0;
    }

    public static bool IsPointerDragging()
    {
        if (DebugMousePositionEnabled) return DebugMouseDrag;
        Event? current = Event.current;
        return current != null && current.type == EventType.MouseDrag && current.button == 0;
    }

    /// <summary>
    /// The event pointer translated into Host window space through the caller's own origin, so it is
    /// comparable with the hit stack's window-space rects. Callers without a context (already window
    /// space) keep the raw position.
    /// </summary>
    private static Vector2 PointerPositionIn(UiWidgetContext? ctx)
    {
        Vector2 point = PointerPosition();
        if (ctx == null) return point;
        return ctx.ToWindowRect(new Rect(point.x, point.y, 0f, 0f)).position;
    }

    public static bool IsPointerUp()
    {
        if (DebugMousePositionEnabled) return DebugMouseUp;
        Event? current = Event.current;
        return current != null && current.type == EventType.MouseUp && current.button == 0;
    }

    /// <summary>
    /// The wheel movement of the current event in NOTCHES, or 0 when this event is not a wheel event. The
    /// library's one read of <c>Event.delta</c>: a control that scrolls asks here instead of touching the
    /// event, the same reason a control that clicks asks <see cref="IsPointerDown"/>.
    /// <para>
    /// The sign is the library's convention, stated once: a POSITIVE value asks for the LATER part of the
    /// content, and it is <c>-delta.y</c> because IMGUI reports a downward notch as a negative delta. Whether a
    /// real device's wheel reaches the menu at all, and with what weight, is in-game behaviour the stub cannot
    /// show: the harness pumps a wheel event with a chosen delta and proves the routing; the short playtest
    /// judges the feel. A notch is not a row either - see <see cref="UiPopup.WheelRowsPerNotch"/>.
    /// </para>
    /// </summary>
    public static float PointerWheelNotches()
    {
        if (DebugMousePositionEnabled) return DebugWheelNotches;
        Event? current = Event.current;
        if (current == null || current.type != EventType.ScrollWheel) return 0f;
        return -current.delta.y;
    }

    /// <summary>
    /// The wheel half of the popup-priority rule: while the session has a menu open and the pointer sits inside
    /// the rect that menu published, the MENU takes the wheel, and the scroll container or chart underneath
    /// never sees the event: a layer covering the point outranks what is under it.
    /// <para>
    /// It is read at the engine's pass boundary, immediately after the pass opens: that is the one moment the
    /// dispatch stack holds the previous pass's complete paint order, menu layer included, while no control has
    /// consumed anything yet. So the geometry is the same rect every click decision in this frame reads, and
    /// consuming here is what stops a native scroll view from taking the notch first.
    /// </para>
    /// </summary>
    internal static void TakePopupWheelIfCovering(UiSession session)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));

        float notches = PointerWheelNotches();
        if (notches == 0f || session.OpenPopupId == null) return;
        if (!session.OpenPopupCoversPointer(PointerPosition())) return;

        int rows = (int)Math.Round(notches * UiPopup.WheelRowsPerNotch, MidpointRounding.AwayFromZero);
        if (rows == 0) rows = notches > 0f ? 1 : -1;

        if (session.ScrollPopupByRows(rows))
        {
            // Exactly one consumer for one event: the menu moved, so nothing below it may also move.
            ConsumePointerEvent();
        }
    }

    /// <summary>
    /// Optional diagnostic sink for the input-routing decisions this type makes. The library stays
    /// neutral: it reports caller-agnostic facts (element ids, rects, event types) and never logs
    /// them itself; a consumer wires the hook to its own log when it needs in-game truth about click
    /// routing that no stub harness can reproduce.
    /// </summary>
    public static Action<string>? Trace;

    private static string Describe(Vector2 point)
    {
        return point.x.ToString("F1", CultureInfo.InvariantCulture) + "," + point.y.ToString("F1", CultureInfo.InvariantCulture);
    }

    internal static string Describe(Rect rect)
    {
        return rect.x.ToString("F1", CultureInfo.InvariantCulture) + "," + rect.y.ToString("F1", CultureInfo.InvariantCulture)
            + "," + rect.width.ToString("F1", CultureInfo.InvariantCulture) + "," + rect.height.ToString("F1", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Marks the current IMGUI event handled, so a control drawn later in the same pass cannot also
    /// react to it. No-op in the harness, where there is no live event.
    /// </summary>
    public static void ConsumePointerEvent()
    {
        Event? current = Event.current;
        if (current != null) current.Use();
    }

    internal static bool TryParseNumber(string text, out float value)
    {
        if (text != null && text.Trim().Length > 0
            && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        value = 0f;
        return false;
    }

    public static float ClampValue(float value, float min, float max)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) return min;
        if (max < min) return min;
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    internal static string FormatValue(float value, string format)
    {
        if (string.IsNullOrEmpty(format)) format = "0.##";
        return value.ToString(format, CultureInfo.InvariantCulture);
    }

    public static bool IsMouseDownOver(Rect rect)
    {
        if (DebugMousePositionEnabled) return DebugMouseDown && IsMouseOver(rect);
        Event? current = Event.current;
        return current != null && current.type == EventType.MouseDown && current.button == 0 && IsMouseOver(rect);
    }

    public static bool IsMouseDragOver(Rect rect)
    {
        if (DebugMousePositionEnabled) return DebugMouseDrag && IsMouseOver(rect);
        Event? current = Event.current;
        return current != null && current.type == EventType.MouseDrag && current.button == 0 && IsMouseOver(rect);
    }

    public static bool IsMouseUpOver(Rect rect)
    {
        if (DebugMousePositionEnabled) return DebugMouseUp && IsMouseOver(rect);
        Event? current = Event.current;
        return current != null && current.type == EventType.MouseUp && current.button == 0 && IsMouseOver(rect);
    }

    /// <summary>
    /// The library's one hover primitive, and the only place in the assembly that asks the backend
    /// whether the pointer is inside a rect. Public because a consumer that reads
    /// <c>UnityEngine.Mouse.IsOver</c> instead gets correct in-game behaviour and harness-invisible
    /// behaviour at the same time: this form honours the <c>DebugMousePositionEnabled</c> seam, so a
    /// hover-driven control is drivable by the kernel-host lane. (US→FL round 1, P1.)
    /// </summary>
    public static bool IsMouseOver(Rect rect)
    {
        if (DebugMousePositionEnabled)
        {
            return DebugMousePosition.x >= rect.x
                && DebugMousePosition.x <= rect.xMax
                && DebugMousePosition.y >= rect.y
                && DebugMousePosition.y <= rect.yMax;
        }

        return Mouse.IsOver(rect);
    }

    /// <summary>
    /// The hover counterpart of <see cref="Button(Rect, UiWidgetContext)"/>: true when the pointer is inside
    /// the rect and this element is not covered by a higher layer. It consumes nothing, which is the point: a
    /// kind can ask "is this part of me hovered" and publish the answer without stealing the click the way
    /// <c>Button</c> does.
    /// <para>
    /// The disabled rule is deliberately NOT applied here, and that is a correction rather than an omission.
    /// Disabled-ness refuses <b>input</b> — it lives in <see cref="Button(Rect, UiWidgetContext)"/> and in the
    /// session-plus-key primitives, where a disabled element must take no drag, edit or focus. Hover here also
    /// drives <b>inspection</b>: the engine claims a hovered element's declared <c>HelpKey</c>, and a control
    /// that is unavailable is exactly when a player needs to be told why. The consumer evidence is its own
    /// unavailable-state help entries (a row whose action is disabled still claims the help that explains it),
    /// and vanilla shows tooltips on disabled controls for the same reason. The higher-layer rule is the one
    /// that does belong: an element under another element's open popup is not hovered by anyone's reading.
    /// </para>
    /// </summary>
    public static bool IsMouseOver(Rect rect, UiWidgetContext ctx)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        UiNode element = ctx.Node ?? ctx.Session.ActiveNode;
        if (ctx.Session.IsPointerOverHigherLayer(element, PointerPositionIn(ctx))) return false;
        return IsMouseOver(rect);
    }

    public static bool IsEnterPressed()
    {
        if (DebugEnter) return true;
        Event? current = Event.current;
        return current != null
            && current.type == EventType.KeyDown
            && (current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter);
    }

    /// <summary>
    /// True during IMGUI's layout pass, when controls are registered but no input event is being
    /// dispatched. Consumers gate frame-transient work (scroll-position seeding, overlay bookkeeping)
    /// on it so it runs once per pass instead of once per event. Behind the <c>DebugLayoutEvent</c>
    /// seam, so a harness can drive either pass without a live IMGUI loop. (US→FL round 1, P6.)
    /// </summary>
    public static bool IsLayoutEvent()
    {
        if (DebugLayoutEvent) return true;
        Event? current = Event.current;
        return current != null && current.type == EventType.Layout;
    }

    public static bool IsFocusLost(Rect rect)
    {
        if (DebugFocusLost) return true;
        Event? current = Event.current;
        return current != null
            && current.type == EventType.MouseDown
            && current.button == 0
            && !IsMouseOver(rect);
    }

    private static float NativeHorizontalSlider(Rect rect, float value, float min, float max)
    {
        if (SliderOverride != null) return SliderOverride(rect, value, min, max);
        return VerseWidgets.HorizontalSlider(rect, value, min, max, middleAlignment: true);
    }

    private static string NativeTextField(Rect rect, string text)
    {
        if (TextFieldOverride != null) return TextFieldOverride(rect, text);
        return VerseWidgets.TextField(rect, text) ?? "";
    }
}
