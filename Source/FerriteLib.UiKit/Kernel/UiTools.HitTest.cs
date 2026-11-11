using System;
using System.Collections.Generic;
using System.Globalization;

namespace FerriteLib.UiKit.Kernel;

public sealed partial class UiToolConnection
{
    public const int CoordinateHitTestVersion = 1;

    /// <summary>Reads the topmost declared pointer owner at a logical Host-window point from the completed draw.
    /// Returns detached observation data, never an operation address. Does not advance input or selection.</summary>
    public UiHostObservation HitTest(UiLayoutPoint windowPoint)
    {
        RequireThread();
        if (IsClosed || !host.TryGetTarget(out UiHost? owner) || !owner.Session.IsActive)
            throw new ObjectDisposedException(nameof(UiToolConnection));
        if (float.IsNaN(windowPoint.X) || float.IsInfinity(windowPoint.X)
            || float.IsNaN(windowPoint.Y) || float.IsInfinity(windowPoint.Y))
            throw new ArgumentOutOfRangeException(nameof(windowPoint));
        return owner.ReadPointerHit(windowPoint);
    }
}

public sealed partial class UiHost
{
    internal UiHostObservation ReadPointerHit(UiLayoutPoint point)
    {
        UiObservationStatus status = engine.ObservationLayoutStatus(CreateContext(0), lastLayoutRevision != theme.LayoutRevision, out string reason);
        var nodes = new List<UiObservedNode>();
        if (!DisplayRequired) { status = UiObservationStatus.Stale; reason = "host-not-displayed"; }
        if (status == UiObservationStatus.Sampled)
        {
            status = session.ReadPointerHit(point, out UiNode? node, out UiLayoutRect bounds, out UiLayoutRect? clip, out bool popup, out reason);
            if (node != null)
            {
                string Rect(UiLayoutRect r) => string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3}", r.X, r.Y, r.Width, r.Height);
                var facts = new List<UiObservedFact> {
                    new("mount-lifetime", node.MountLifetime.ToString(CultureInfo.InvariantCulture), source: "session/node-incarnation"),
                    new("interaction-scope", node.InteractionScope, source: "owner"),
                    new("pointer-region", Rect(bounds), source: "completed-draw/window-logical"),
                    new("clip", clip.HasValue ? Rect(clip.Value) : "", source: "completed-draw/window-logical"),
                    new("popup", popup.ToString(), source: "session/hit-layers"),
                    new("pointer-allowed", session.PointerInputAllowed.ToString(), source: "session/current-input"),
                    new("input-disabled", UiControls.IsInputDisabled(node).ToString(), source: "session/current-node-qualification"),
                    new("capture-owner", session.Interactions.CaptureOwner?.Node ?? "", source: "session/interaction"),
                    new("native-capture-owner", session.OwnedHotControlOwner?.Id.Key ?? "", source: "session/owned-hot-control")
                };
                int depth = 0;
                for (UiNode? parent = node.Parent; parent != null; parent = parent.Parent) depth++;
                nodes.Add(new UiObservedNode(node.Id.Key, node.Path, node.Kind, node.Parent?.Id.Key, depth, facts.AsReadOnly()));
            }
        }
        return new UiHostObservation(session.Identity, source, session.Frame, session.DefinitionRevision,
            engine.ArrangedDefinitionRevision, DateTime.UtcNow, status, reason, false, nodes.AsReadOnly());
    }
}

public sealed partial class UiSession
{
    private bool pointerReadCompleted;
    private int pointerReadFrame, pointerReadDefinition, pointerReadContent;
    private long pointerReadMount;
    internal void InvalidatePointerRead() => pointerReadCompleted = false;
    internal void CompletePointerRead()
    {
        if (!IsActive || hitPassOpen || !IsInputGeometryReliable) return;
        pointerReadFrame = Frame;
        pointerReadDefinition = DefinitionRevision;
        pointerReadContent = ContentRevision;
        pointerReadMount = nextMountLifetime;
        pointerReadCompleted = true;
    }

    internal UiObservationStatus ReadPointerHit(UiLayoutPoint point, out UiNode? node,
        out UiLayoutRect bounds, out UiLayoutRect? clip, out bool popup, out string reason)
    {
        node = null; bounds = default; clip = null; popup = false;
        reason = IsBudgetMeasuring ? "budget-measurement-in-progress" : "await-completed-current-draw";
        if (IsBudgetMeasuring) return UiObservationStatus.Stale;
        if (!pointerReadCompleted || hitPassOpen || !IsInputGeometryReliable || pointerReadFrame != Frame
            || pointerReadDefinition != DefinitionRevision || pointerReadContent != ContentRevision
            || pointerReadMount != nextMountLifetime) return UiObservationStatus.Stale;

        int index = TopmostPopupIndex(hitLayers, point);
        if (index >= 0)
        {
            UiHitLayer layer = hitLayers[index];
            node = layer.Element; bounds = layer.Rect; popup = true;
        }
        else
        {
            index = UiPointerLayers.TopmostIndex(contentInputLayers, point.X, point.Y);
            if (index >= 0)
            {
                UiPointerLayer layer = contentInputLayers[index];
                node = FindObservationNode(layer.Owner); bounds = layer.Bounds; clip = layer.Clip;
            }
        }
        if (index < 0) { reason = "no-declared-pointer-region"; return UiObservationStatus.NotFound; }
        if (node == null || !ReferenceEquals(FindObservationNode(node.Id.Key), node))
        { node = null; reason = "pointer-owner-retired"; return UiObservationStatus.Stale; }
        reason = "topmost-declared-pointer-owner; not-an-operation-address";
        return UiObservationStatus.Sampled;
    }
}
