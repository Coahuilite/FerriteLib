using System;

namespace FerriteLib.UiKit.Kernel;
// Platform facts and native effects only. Activation/range transitions live in UiInteractionSession.
public sealed partial class UiControls
{
    public UiInteractionTarget InteractionTarget(UiWidgetContext ctx, string binding) => InteractionTarget(ctx, binding, "", null);
    public UiInteractionTarget InteractionTarget(UiWidgetContext ctx, string binding, string part, object? itemKey = null)
    {
        UiNode node = ctx.Node;
        return new UiInteractionTarget(node.Id.Key, binding + "\u001f" + node.InteractionScope, part, itemKey, node.InteractionScope);
    }

    public UiInteractionResult Activation(UiLayoutRect rect, UiWidgetContext ctx, string binding = "activation") => Activation(rect, ctx, binding, "", null, true);
    public UiInteractionResult Activation(UiLayoutRect rect, UiWidgetContext ctx, string binding, string part, object? itemKey = null, bool writable = true)
    {

        return PointerInteraction(rect, ctx.Session, ctx.Node, InteractionTarget(ctx, binding, part, itemKey), CanReceivePointerPress(rect, ctx), !IsInputDisabled(ctx.Node) && writable, false, default, 0, ctx);
    }

    public UiInteractionResult Range(UiLayoutRect rect, UiWidgetContext ctx, string binding, float value, float min, float max, bool writable = true)
    {
        return PointerInteraction(rect, ctx, true, new UiRange(min, max), value, writable, binding);
    }

    public UiInteractionResult Point(UiLayoutRect hit, UiLayoutRect plot, UiWidgetContext ctx, string binding, object key, float x, float y, bool writable = true)
    {
        UiNode node = ctx.Node;
        var geometry = new UiHitGeometry(hit.X, hit.Y, hit.Width, hit.Height, plot.X, plot.Width, plot.Y, plot.Height, true);
        return PointerInteraction(hit, ctx.Session, node, InteractionTarget(ctx, binding, "chart-point", key),
            CanReceivePointer(ctx), !IsInputDisabled(node) && writable, false, default, x, ctx, true, y, geometry);
    }

    internal void EndPointCapture(UiWidgetContext ctx, UiInteractionEnd reason)
    {
        UiNode node = ctx.Node;
        UiInteractionTarget? owner = ctx.Session.Interactions.CaptureOwner;
        if (owner?.Node != node.Id.Key || owner?.Part != "chart-point") return;
        ctx.Session.Interactions.End(reason);
        bool ownNative = ReferenceEquals(ctx.Session.OwnedHotControlOwner, node) || (node.Id.IsNone && ctx.Session.OwnedHotControlOwner == null);
        if (ownNative && ctx.Session.OwnedHotControl.HasValue)
            ctx.Session.ReleaseHotControl(ctx.Session.OwnedHotControl.Value);
    }

    private UiInteractionResult PointerInteraction(UiLayoutRect rect, UiWidgetContext ctx, bool range, UiRange limits, float value, bool writable, string binding)
    {
        UiSession session = ctx.Session;
        UiNode node = ctx.Node;
        return PointerInteraction(rect, session, node, InteractionTarget(ctx, binding), CanReceivePointerPress(rect, ctx), !IsInputDisabled(node) && writable, range, limits, value, ctx);
    }

    private UiInteractionResult PointerInteraction(UiLayoutRect rect, UiSession session, UiNode node, UiInteractionTarget target, bool canReceive, bool canOperate, bool range, UiRange limits, float value, UiWidgetContext? ctx = null,
        bool pointOperation = false, float currentY = 0, UiHitGeometry? pointGeometry = null)
    {
#if FER_DEV
        string eventBefore = DiagnosticEventName();
#endif
        UiControlKey allocatedControl = GetControlId(target.Node + "\u001f" + target.TargetScope + "\u001f" + target.Part + "\u001f" + target.GetHashCode());
        bool owns = session.Interactions.CaptureOwner?.Equals(target) == true;
        bool sameNativeOwner = ReferenceEquals(session.OwnedHotControlOwner, node) || (node.Id.IsNone && session.OwnedHotControlOwner == null);
        UiControlKey control = owns && sameNativeOwner && session.OwnedHotControl.HasValue ? session.OwnedHotControl.Value : allocatedControl;
        bool hadNodeCapture = session.Interactions.CaptureOwner?.Node == target.Node;
        UiPointerPhase phase = PointerPhase();
        if (owns && (!sameNativeOwner || !HasHotControl(control)))
            phase = UiPointerPhase.Lost;
        var point = PointerPosition();
        var input = new UiPointerSample(phase, point.X, point.Y, Input.Consumed);
        bool captureAvailable = Execution.CaptureAvailable;
        var eligibility = new UiInputEligibility(canOperate && session.PointerInputAllowed && session.IsInputGeometryReliable, true, canReceive && (captureAvailable || owns));
        float thumb = Math.Min(12, rect.Width / 2);
        var geometry = pointGeometry ?? (range ? new UiHitGeometry(rect.X, rect.Y, rect.Width, rect.Height, rect.X + thumb / 2, rect.Width - thumb) : new UiHitGeometry(rect.X, rect.Y, rect.Width, rect.Height));
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
    public UiInteractionResult PopupOption(UiLayoutRect rect, string elementId, UiWidgetContext ctx, object? key, bool selectable = true)
    {
        UiSession session = ctx.Session;
        UiNode node = ctx.Node;
        var target = InteractionTarget(ctx, elementId, "popup-option", key);
        UiInteractionResult result = PopupPointer(rect, elementId, session, target, true, selectable, node);
        session.NotePopupOptionCapture(target, result.Captured);
        if (result.Request)
            Trace?.Invoke("option fired id=" + elementId + " row=" + Describe(rect));
        return result;
    }

    private UiInteractionResult PopupPointer(UiLayoutRect rect, string elementId, UiSession session, UiInteractionTarget target, bool option, bool selectable, UiNode ownerNode)
    {
        UiNode node = ownerNode;
        bool enabled = selectable && !IsInputDisabled(node) && (!option || session.IsPopupOpen(elementId, node));
        return PointerInteraction(rect, session, node, target, enabled && IsMouseOver(rect), enabled, false, default, 0);
    }

    private UiPointerPhase PointerPhase()
    {
        if (IsPointerDown())
            return UiPointerPhase.Press;
        if (IsPointerDragging())
            return UiPointerPhase.Drag;
        if (IsPointerUp())
            return UiPointerPhase.Release;
        // Used preserves the original release in rawType. It ends ownership, but remains consumed.
        UiFrame current = Input;
        if (current.RawPhase == UiFramePhase.MouseUp && current.Button == 0)
            return UiPointerPhase.Release;
        return UiPointerPhase.None;
    }

    /// <summary>Only paints a range; never calls a native composite interactive slider.</summary>
    public void PaintRange(UiLayoutRect rect, UiTheme theme, float value, float min, float max)
    {
        float fraction = max == min ? 0 : UiMath.Clamp01((value - min) / (max - min));
        float thumb = Math.Min(12, rect.Width / 2);
        float inset = thumb / 2;
        UiThemeDraw.Surface(Drawing,new UiLayoutRect(rect.X + inset, rect.Y + rect.Height / 2 - 1, Math.Max(1, rect.Width - inset * 2), 2), theme.RaisedSurface, theme.Geometry.Hairline);
        UiThemeDraw.Surface(Drawing,new UiLayoutRect(rect.X + fraction * (rect.Width - thumb), rect.Y + Math.Max(0, (rect.Height - 16) / 2), thumb, Math.Min(16, rect.Height)), theme.SelectedSurface, theme.Geometry.Hairline);
    }
}


