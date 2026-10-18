using System;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel;

// Platform facts and native effects only. Activation/range transitions live in UiInteractionSession.
public static partial class UiNative
{
    public static UiInteractionTarget InteractionTarget(UiWidgetContext ctx, string binding)
    {
        UiNode node = ctx.Node ?? ctx.Session.ActiveNode;
        return new UiInteractionTarget(node.Id.Key, binding + "\u001f" + node.InteractionScope);
    }
    public static UiInteractionResult Activation(Rect rect, UiWidgetContext ctx, string binding = "activation")
    {
        // Existing harness seam represents an already-established logical activation, not native pointer timing.
        if (ButtonOverride != null)
        {
#if FER_DEV
            string before = DiagnosticEventName();
#endif
            UiNode node = ctx.Node ?? ctx.Session.ActiveNode;
            bool enabled = !IsInputDisabled(node);
            bool covered = ctx.Session.IsPointerOverHigherLayer(node, WindowPointer(ctx));
            bool established = enabled && !covered && ButtonOverride(rect);
            UiInteractionResult result = ctx.Session.Interactions.Activate(InteractionTarget(ctx, binding), new UiInputEligibility(established));
#if FER_DEV
            UiDevGeometryProbe.NoteInput(ctx, node, rect, !enabled ? "disabled" : covered ? "covered" : established ? "hit" : "miss", before);
#endif
            return result;
        }
        return PointerInteraction(rect, ctx, false, default, 0, true, binding);
    }

    public static UiInteractionResult Range(Rect rect, UiWidgetContext ctx, string binding, float value, float min, float max, bool writable = true)
    {
        if (SliderOverride != null && writable && !IsInputDisabled(ctx.Node ?? ctx.Session.ActiveNode) && !ctx.Session.IsPointerOverHigherLayer(ctx.Node ?? ctx.Session.ActiveNode, WindowPointer(ctx)))
            return ctx.Session.Interactions.Select(InteractionTarget(ctx, binding), new UiRange(min, max), SliderOverride(rect, value, min, max),
                new UiInputEligibility(!IsInputDisabled(ctx.Node ?? ctx.Session.ActiveNode), writable,
                    !ctx.Session.IsPointerOverHigherLayer(ctx.Node ?? ctx.Session.ActiveNode, WindowPointer(ctx))));
        return PointerInteraction(rect, ctx, true, new UiRange(min, max), value, writable, binding);
    }

    private static UiInteractionResult PointerInteraction(Rect rect, UiWidgetContext ctx, bool range, UiRange limits, float value, bool writable, string binding)
    {
        UiSession session = ctx.Session;
        UiNode node = ctx.Node ?? session.ActiveNode;
        return PointerInteraction(rect, session, node, InteractionTarget(ctx, binding),
            CanReceivePointerPress(rect, ctx), !IsInputDisabled(node) && writable, range, limits, value, ctx);
    }

    private static UiInteractionResult PointerInteraction(Rect rect, UiSession session, UiNode node, UiInteractionTarget target,
        bool canReceive, bool canOperate, bool range, UiRange limits, float value, UiWidgetContext? ctx = null)
    {
#if FER_DEV
        string eventBefore = DiagnosticEventName();
#endif
        int allocatedControl = GetControlId(target.Node + "\u001f" + target.TargetScope);
        bool owns = session.Interactions.CaptureOwner?.Equals(target) == true;
        int control = owns && session.OwnedHotControl.HasValue ? session.OwnedHotControl.Value : allocatedControl;
        bool hadNodeCapture = session.Interactions.CaptureOwner?.Node == target.Node;
        UiPointerPhase phase = PointerPhase();
        if (owns && !HasHotControl(control)) phase = UiPointerPhase.Lost;
        var point = PointerPosition();
        var input = new UiPointerSample(phase, point.x, point.y, Event.current?.type == EventType.Used);
        int nativeCapture = DebugMousePositionEnabled ? DebugHotControl : GUIUtility.hotControl;
        var eligibility = new UiInputEligibility(canOperate && session.PointerInputAllowed && session.IsInputGeometryReliable, true, canReceive && (nativeCapture == 0 || owns));
        float thumb = Math.Min(12, rect.width / 2);
        var geometry = range ? new UiHitGeometry(rect.x, rect.y, rect.width, rect.height, rect.x + thumb / 2, rect.width - thumb)
            : new UiHitGeometry(rect.x, rect.y, rect.width, rect.height);
        UiInteractionResult result;
        try
        {
            result = range
                ? session.Interactions.PointerRange(target, input, geometry, eligibility, limits, value)
                : session.Interactions.PointerActivation(target, input, geometry, eligibility);
        }
        catch
        {
            // Invalid geometry ends this operation in the core; apply that release even when no result is returned.
            if (hadNodeCapture && !session.Interactions.CaptureOwner.HasValue && session.OwnedHotControl.HasValue)
                session.ReleaseHotControl(session.OwnedHotControl.Value);
            throw;
        }
        bool captured = session.Interactions.CaptureOwner?.Equals(target) == true;
        if (captured)
        {
            if (!session.IsHotControlOwned(control)) session.CaptureHotControlForNode(control, node);
            session.NoteHotControlOwnerDrawn(control);
        }
        else if (hadNodeCapture && session.OwnedHotControl.HasValue) session.ReleaseHotControl(session.OwnedHotControl.Value);
        if (result.Consume) ConsumePointerEvent();
        if (result.Consume || result.Request) session.NoteInteractionTarget(node);
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
    private static UiPointerPhase PointerPhase()
    {
        if (IsPointerDown()) return UiPointerPhase.Press;
        if (IsPointerDragging()) return UiPointerPhase.Drag;
        if (IsPointerUp()) return UiPointerPhase.Release;
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
