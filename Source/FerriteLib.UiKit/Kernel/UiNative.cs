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
    /// The invisible button with topmost-first dispatch: when the pointer sits inside a hit layer that
    /// belongs to another element - a popup above this content, an overlapping neighbour painted later -
    /// this element does not take the click. Every library widget and every consumer control that holds a
    /// context belongs here: one rule, and no per-element yield branch anywhere.
    /// </summary>
    public static bool Button(Rect rect, UiWidgetContext ctx)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        UiNode element = ctx.Node ?? ctx.Session.ActiveNode;
        if (ctx.Session.IsPointerOverHigherLayer(element, PointerPositionIn(ctx))) return false;
        return Button(rect);
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

        // The hit stack decides: a popup drawn over this trigger is a layer above it, so the trigger must
        // not take the click - unless the popup is its own, which the same layer comparison handles.
        bool covered = ctx != null && session.IsPointerOverHigherLayer(
            ctx.Node ?? session.ActiveNode, PointerPositionIn(ctx));
        if (Trace != null && session.OpenPopupId != null)
        {
            Vector2 local = PointerPosition();
            Vector2 window = PointerPositionIn(ctx);
            Trace("trigger id=" + elementId
                + " owner=" + session.OpenPopupId
                + " event=" + (Event.current != null ? Event.current.type.ToString() : "none")
                + " pointerLocal=" + Describe(local)
                + " pointerWindow=" + Describe(window)
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

    public static float Slider(Rect rect, string elementId, UiSession session, float value, float min, float max, out bool changed)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));
        UiValueState state = session.GetOrCreateValueState(elementId);
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

    public static string TextField(Rect rect, string text)
    {
        return NativeTextField(rect, text);
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
