using System;

namespace FerriteLib.UiKit.Kernel;

/// <summary>One selected node's last actual draw fact. It is not a stored screenshot rectangle.</summary>
public sealed class UiToolSelection
{
    internal UiToolSelection(string node, string status, int frame, UiLayoutRect? window, UiLayoutRect? clip)
    { Node = node; Status = status; Frame = frame; Window = window; Clip = clip; }
    public string Node { get; }
    public string Status { get; }
    public int Frame { get; }
    public UiLayoutRect? Window { get; }
    public UiLayoutRect? Clip { get; }
}

internal sealed class UiToolSelectionState
{
    internal UiToolSelectionState(UiNode node) { Key = node.Id.Key; Mount = node.MountLifetime; }
    internal readonly string Key;
    internal readonly long Mount;
    internal string Status = "await-current-draw";
    internal int Frame;
    internal int Definition = -1;
    internal UiLayoutRect? Window, Clip;
    internal UiToolSelection Snapshot() => new(Key, Status, Frame, Window, Clip);
}

public sealed partial class UiSession
{
    internal UiToolSelectionState? ToolSelection;
    internal void BeginToolSelectionPass()
    {
        UiToolSelectionState? selected = ToolSelection;
        if (selected == null) return;
        UiNode? node = FindObservationNode(selected.Key);
        selected.Status = node == null || node.MountLifetime != selected.Mount ? "removed-or-replaced" : "not-drawn-in-current-pass";
        selected.Frame = Frame;
        selected.Window = selected.Clip = null;
    }
}

public sealed partial class UiToolConnection
{
    public UiToolSelection? SelectNode(string? nodeKey)
    {
        RequireThread();
        if (IsClosed || !host.TryGetTarget(out UiHost? owner) || !owner.Session.IsActive) throw new ObjectDisposedException(nameof(UiToolConnection));
        if (string.IsNullOrEmpty(nodeKey)) { owner.Session.ToolSelection = null; return null; }
        UiNode node = owner.Session.FindObservationNode(nodeKey!) ?? throw new ArgumentException("Node no longer exists; read the target again.", nameof(nodeKey));
        owner.Session.ToolSelection = new UiToolSelectionState(node);
        return owner.Session.ToolSelection.Snapshot();
    }
    public UiToolSelection? ReadSelection()
    {
        RequireThread();
        if (IsClosed || !host.TryGetTarget(out UiHost? owner) || !owner.Session.IsActive) return null;
        UiToolSelectionState? selected = owner.Session.ToolSelection;
        if (selected == null) return null;
        UiNode? node = owner.Session.FindObservationNode(selected.Key);
        if (node == null || node.MountLifetime != selected.Mount)
            return new UiToolSelection(selected.Key, "removed-or-replaced", owner.Session.Frame, null, null);
        if (selected.Definition >= 0 && selected.Definition != owner.Session.DefinitionRevision)
            return new UiToolSelection(selected.Key, "definition-changed-await-current-draw", owner.Session.Frame, null, null);
        return selected.Snapshot();
    }
}

internal sealed partial class UiLayoutEngine
{
    private static void OutlineSelected(PlacedEntry entry, UiWidgetContext ctx, UiLayoutRect drawRect)
    {
        UiToolSelectionState? selected = ctx.Session.ToolSelection;
        if (selected == null || selected.Key != entry.Id.Key || selected.Mount != entry.Node.MountLifetime) return;
        UiLayoutRect window = ctx.ToWindowRect(drawRect);
        UiLayoutRect clip = IntersectClip(ctx.Session.CurrentClip, window);
        selected.Frame = ctx.Session.Frame;
        selected.Definition = ctx.Session.DefinitionRevision;
        selected.Clip = ctx.Session.CurrentClip;
        selected.Window = null;
        if (clip.Width <= 0 || clip.Height <= 0) { selected.Status = "outside-current-clip"; return; }
        selected.Window = window;
        selected.Status = clip.Width < window.Width || clip.Height < window.Height ? "drawn-partly-clipped" : "drawn";
        // Same original walk and native clipping as the control. No capture, second renderer or hit layer.
        UiColour ink = ctx.Theme[UiPaletteRole.Accent];
        float line = Math.Max(1, ctx.Theme.Geometry.Hairline * 2);
        UiThemeDraw.Solid(ctx.Drawing, new UiLayoutRect(drawRect.X, drawRect.Y, drawRect.Width, line), ink);
        UiThemeDraw.Solid(ctx.Drawing, new UiLayoutRect(drawRect.X, drawRect.YMax - line, drawRect.Width, line), ink);
        UiThemeDraw.Solid(ctx.Drawing, new UiLayoutRect(drawRect.X, drawRect.Y, line, drawRect.Height), ink);
        UiThemeDraw.Solid(ctx.Drawing, new UiLayoutRect(drawRect.XMax - line, drawRect.Y, line, drawRect.Height), ink);
    }
}
