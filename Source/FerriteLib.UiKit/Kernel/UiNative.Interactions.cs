using System;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel;
// Platform facts and native effects only. Activation/range transitions live in UiInteractionSession.
public static partial class UiNative
{
    public static UiInteractionTarget InteractionTarget(UiWidgetContext ctx, string binding) => InteractionTarget(ctx, binding, "", null);
    public static UiInteractionTarget InteractionTarget(UiWidgetContext ctx, string binding, string part, object? itemKey = null)
    {
        UiNode node = ctx.Node ?? ctx.Session.ActiveNode;
        return new UiInteractionTarget(node.Id.Key, binding + "\u001f" + node.InteractionScope, part, itemKey, node.InteractionScope);
    }

    public static UiInteractionResult Activation(Rect rect, UiWidgetContext ctx, string binding = "activation") => Activation(rect, ctx, binding, "", null, true);
    public static UiInteractionResult Activation(Rect rect, UiWidgetContext ctx, string binding, string part, object? itemKey = null, bool writable = true)
    {
        // Existing harness seam represents an already-established logical activation, not native pointer timing.
        if (ButtonOverride != null)
        {
#if FER_DEV
            string before = DiagnosticEventName();
#endif
            UiNode node = ctx.Node ?? ctx.Session.ActiveNode;
            bool enabled = !IsInputDisabled(node) && ctx.Session.PointerInputAllowed && ctx.Session.IsInputGeometryReliable;
            bool covered = ctx.Session.IsPointerOverHigherLayer(node, WindowPointer(ctx));
            bool established = enabled && writable && !covered && ButtonOverride(rect);
            UiInteractionResult result = ctx.Session.Interactions.Activate(InteractionTarget(ctx, binding, part, itemKey), new UiInputEligibility(established));
#if FER_DEV
            UiDevGeometryProbe.NoteInput(ctx, node, rect, !enabled ? "disabled" : covered ? "covered" : established ? "hit" : "miss", before);
#endif
            return result;
        }

        return PointerInteraction(rect, ctx.Session, ctx.Node ?? ctx.Session.ActiveNode, InteractionTarget(ctx, binding, part, itemKey), CanReceivePointerPress(rect, ctx), !IsInputDisabled(ctx.Node ?? ctx.Session.ActiveNode) && writable, false, default, 0, ctx);
    }

    public static UiInteractionResult Range(Rect rect, UiWidgetContext ctx, string binding, float value, float min, float max, bool writable = true)
    {
        if (SliderOverride != null && writable && !IsInputDisabled(ctx.Node ?? ctx.Session.ActiveNode) && !ctx.Session.IsPointerOverHigherLayer(ctx.Node ?? ctx.Session.ActiveNode, WindowPointer(ctx)))
        {
            // Historical native-value test seam: returning current means no selected sample.
            // Production pointer results already distinguish capture-only from a new drag sample.
            float selected = SliderOverride(rect, value, min, max);
            if (selected == value) return default;
            return ctx.Session.Interactions.Select(InteractionTarget(ctx, binding), new UiRange(min, max), selected, new UiInputEligibility(!IsInputDisabled(ctx.Node ?? ctx.Session.ActiveNode), writable, !ctx.Session.IsPointerOverHigherLayer(ctx.Node ?? ctx.Session.ActiveNode, WindowPointer(ctx))));
        }
        return PointerInteraction(rect, ctx, true, new UiRange(min, max), value, writable, binding);
    }

    public static UiInteractionResult Point(Rect hit, Rect plot, UiWidgetContext ctx, string binding, object key, float x, float y, bool writable = true)
    {
        UiNode node = ctx.Node ?? ctx.Session.ActiveNode;
        var geometry = new UiHitGeometry(hit.x, hit.y, hit.width, hit.height, plot.x, plot.width, plot.y, plot.height, true);
        return PointerInteraction(hit, ctx.Session, node, InteractionTarget(ctx, binding, "chart-point", key),
            CanReceivePointer(ctx), !IsInputDisabled(node) && writable, false, default, x, ctx, true, y, geometry);
    }

    internal static void EndPointCapture(UiWidgetContext ctx, UiInteractionEnd reason)
    {
        UiNode node = ctx.Node ?? ctx.Session.ActiveNode;
        UiInteractionTarget? owner = ctx.Session.Interactions.CaptureOwner;
        if (owner?.Node != node.Id.Key || owner?.Part != "chart-point") return;
        ctx.Session.Interactions.End(reason);
        bool ownNative = ReferenceEquals(ctx.Session.OwnedHotControlOwner, node) || (node.Id.IsNone && ctx.Session.OwnedHotControlOwner == null);
        if (ownNative && ctx.Session.OwnedHotControl.HasValue)
            ctx.Session.ReleaseHotControl(ctx.Session.OwnedHotControl.Value);
    }

    private static UiInteractionResult PointerInteraction(Rect rect, UiWidgetContext ctx, bool range, UiRange limits, float value, bool writable, string binding)
    {
        UiSession session = ctx.Session;
        UiNode node = ctx.Node ?? session.ActiveNode;
        return PointerInteraction(rect, session, node, InteractionTarget(ctx, binding), CanReceivePointerPress(rect, ctx), !IsInputDisabled(node) && writable, range, limits, value, ctx);
    }

    private static UiInteractionResult PointerInteraction(Rect rect, UiSession session, UiNode node, UiInteractionTarget target, bool canReceive, bool canOperate, bool range, UiRange limits, float value, UiWidgetContext? ctx = null,
        bool pointOperation = false, float currentY = 0, UiHitGeometry? pointGeometry = null)
    {
#if FER_DEV
        string eventBefore = DiagnosticEventName();
#endif
        int allocatedControl = GetControlId(target.Node + "\u001f" + target.TargetScope + "\u001f" + target.Part + "\u001f" + target.GetHashCode());
        bool owns = session.Interactions.CaptureOwner?.Equals(target) == true;
        bool sameNativeOwner = ReferenceEquals(session.OwnedHotControlOwner, node) || (node.Id.IsNone && session.OwnedHotControlOwner == null);
        int control = owns && sameNativeOwner && session.OwnedHotControl.HasValue ? session.OwnedHotControl.Value : allocatedControl;
        bool hadNodeCapture = session.Interactions.CaptureOwner?.Node == target.Node;
        UiPointerPhase phase = PointerPhase();
        if (owns && (!sameNativeOwner || !HasHotControl(control)))
            phase = UiPointerPhase.Lost;
        var point = PointerPosition();
        var input = new UiPointerSample(phase, point.x, point.y, Event.current?.type == EventType.Used);
        int nativeCapture = DebugMousePositionEnabled ? DebugHotControl : GUIUtility.hotControl;
        var eligibility = new UiInputEligibility(canOperate && session.PointerInputAllowed && session.IsInputGeometryReliable, true, canReceive && (nativeCapture == 0 || owns));
        float thumb = Math.Min(12, rect.width / 2);
        var geometry = pointGeometry ?? (range ? new UiHitGeometry(rect.x, rect.y, rect.width, rect.height, rect.x + thumb / 2, rect.width - thumb) : new UiHitGeometry(rect.x, rect.y, rect.width, rect.height));
        UiInteractionResult result;
        try
        {
            if (pointOperation) result = session.Interactions.PointerPoint(target, input, geometry, eligibility, value, currentY);
            else if (range) result = session.Interactions.PointerRange(target, input, geometry, eligibility, limits, value);
            else result = session.Interactions.PointerActivation(target, input, geometry, eligibility);
        }
        catch
        {
            // Invalid geometry ends this operation in the core; apply that release even when no result is returned.
            if (hadNodeCapture && sameNativeOwner && !session.Interactions.CaptureOwner.HasValue && session.OwnedHotControl.HasValue)
                session.ReleaseHotControl(session.OwnedHotControl.Value);
            throw;
        }

        bool captured = session.Interactions.CaptureOwner?.Equals(target) == true;
        if (captured)
        {
            if (!session.IsHotControlOwned(control))
                session.CaptureHotControlForNode(control, node);
            session.NoteHotControlOwnerDrawn(control);
        }
        else if (hadNodeCapture && sameNativeOwner && !session.Interactions.CaptureOwner.HasValue && session.OwnedHotControl.HasValue)
            session.ReleaseHotControl(session.OwnedHotControl.Value);
        if (result.Consume)
            ConsumePointerEvent();
        if (result.Consume || result.Request)
            session.NoteInteractionTarget(node);
        if (result.Ended != UiInteractionEnd.None)
            Trace?.Invoke("interaction end node=" + target.Node + " part=" + target.Part + " reason=" + result.Ended);
#if FER_DEV
        if (ctx != null)
        {
            bool covered = session.IsPointerOverHigherLayer(node, WindowPointer(ctx));
            string verdict = !canOperate ? "disabled" : covered ? "covered" : result.Consume || result.Request ? "hit" : "miss";
            UiDevGeometryProbe.NoteInput(ctx, node, rect, verdict, eventBefore);
        }
#endif
        return result;
    }

    /// <summary>Popup rows are already in window space and belong to the open menu, not its covered trigger.</summary>
    public static UiInteractionResult PopupOption(Rect rect, string elementId, UiWidgetContext ctx, object? key, bool selectable = true)
    {
        UiSession session = ctx.Session;
        UiNode node = ctx.Node ?? session.ActiveNode;
        var target = InteractionTarget(ctx, elementId, "popup-option", key);
        UiInteractionResult result = PopupPointer(rect, elementId, session, target, true, selectable, node);
        session.NotePopupOptionCapture(target, result.Captured);
        if (result.Request)
            Trace?.Invoke("option fired id=" + elementId + " row=" + Describe(rect));
        return result;
    }

    private static UiInteractionResult PopupPointer(Rect rect, string elementId, UiSession session, UiInteractionTarget target, bool option, bool selectable = true, UiNode? ownerNode = null)
    {
        UiNode node = ownerNode ?? session.ActiveNode;
        bool enabled = selectable && !IsInputDisabled(node) && (!option || session.IsPopupOpen(elementId, node));
        if (ButtonOverride != null)
            return session.Interactions.Activate(target, new UiInputEligibility(enabled && ButtonOverride(rect)));
        return PointerInteraction(rect, session, node, target, enabled && IsMouseOver(rect), enabled, false, default, 0);
    }

    private static UiPointerPhase PointerPhase()
    {
        if (IsPointerDown())
            return UiPointerPhase.Press;
        if (IsPointerDragging())
            return UiPointerPhase.Drag;
        if (IsPointerUp())
            return UiPointerPhase.Release;
        // Used preserves the original release in rawType. It ends ownership, but remains consumed.
        Event? current = Event.current;
        if (!DebugMousePositionEnabled && current?.rawType == EventType.MouseUp && current.button == 0)
            return UiPointerPhase.Release;
        return UiPointerPhase.None;
    }

    /// <summary>Only paints a range; never calls a native composite interactive slider.</summary>
    public static void PaintRange(Rect rect, UiTheme theme, float value, float min, float max)
    {
        float fraction = max == min ? 0 : Mathf.Clamp01((value - min) / (max - min));
        float thumb = Math.Min(12, rect.width / 2);
        float inset = thumb / 2;
        UiThemeDraw.Surface(new Rect(rect.x + inset, rect.y + rect.height / 2 - 1, Math.Max(1, rect.width - inset * 2), 2), theme.RaisedSurface, theme.Geometry.Hairline);
        UiThemeDraw.Surface(new Rect(rect.x + fraction * (rect.width - thumb), rect.y + Math.Max(0, (rect.height - 16) / 2), thumb, Math.Min(16, rect.height)), theme.SelectedSurface, theme.Geometry.Hairline);
    }
}
