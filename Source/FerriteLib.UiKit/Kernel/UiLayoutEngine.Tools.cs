using System;
using System.Collections.Generic;

namespace FerriteLib.UiKit.Kernel;
internal sealed partial class UiLayoutEngine
{
    internal IReadOnlyList<UiToolCapability> ReadToolTargets(UiWidgetContext context, UiHostObservation observation, UiObservationBudget budget, out bool limited)
    {
        limited = observation.Truncated;
        var result = new List<UiToolCapability>();
        if (observation.Status != UiObservationStatus.Sampled) return result.AsReadOnly();
        var selected = new HashSet<string>(StringComparer.Ordinal);
        foreach (UiObservedNode node in observation.Nodes) selected.Add(node.Key);
        foreach (PlacedEntry entry in lastEntries)
        {
            if (!selected.Contains(entry.Id.Key) || entry.Widget is not IUiSemanticWidget semantic) continue;
            if (entry.Id.Key.Length > budget.Text || entry.Node.InteractionScope.Length > budget.Text) { limited = true; continue; }
            UiWidgetContext local = context.WithViewWidth(entry.MeasureWidth).WithNode(entry.Node);
            using IDisposable read = UiBindings.OwnerOf(local.Bindings).DisplayOnly();
            IReadOnlyList<UiSemanticAction> actions = semantic.ReadActions(local, budget.Items);
            int count = Math.Min(actions.Count, budget.Items);
            limited |= actions.Count >= budget.Items;
            for (int i = 0; i < count && result.Count < 512; i++)
            {
                UiSemanticAction action = actions[i];
                if (action.Part.Length > budget.Text || action.Item.Length > budget.Text) { limited = true; continue; }
                var described = new UiSemanticAction(action.Operation, action.Part, action.Item, budget.Limit(action.Name)) { Choice = action.Choice };
                limited |= action.Name.Length > budget.Text;
                long lifetime = entry.Node.ToolItemLifetime(described, mint: true);
                if (lifetime == 0) { limited = true; continue; }
                result.Add(new UiToolCapability(new UiToolTarget(Guid.NewGuid().ToString("N"), context.Session.Identity, context.Session.DefinitionRevision, entry.Node.MountLifetime,
                    entry.Id.Key, entry.Node.InteractionScope, described, lifetime), described));
            }
            limited |= result.Count == 512;
        }
        return result.AsReadOnly();
    }
    internal void RefreshToolItems(UiWidgetContext context, HashSet<string> interested)
    {
        foreach (PlacedEntry entry in lastEntries)
        {
            if (!interested.Remove(entry.Id.Key) || entry.Widget is not IUiSemanticWidget semantic) continue;
            UiWidgetContext local = context.WithViewWidth(entry.MeasureWidth).WithNode(entry.Node);
            try
            {
                using IDisposable read = UiBindings.OwnerOf(local.Bindings).DisplayOnly();
                entry.Node.ReconcileToolItems(semantic.ReadActions(local, 128));
            }
            catch (Exception error) when (UiObservationBoundary.IsOrdinary(error)) { entry.Node.ClearToolItems(); }
        }
        foreach (string key in interested) context.Session.FindObservationNode(key)?.ClearToolItems();
    }
    internal string ExecuteTool(UiWidgetContext context, UiToolTarget target, UiSemanticAction ownerAction, UiToolValue value, bool displayRequired, out bool attempted)
    {
        attempted = false;
        if (!context.Session.IsActive) return "host-closed";
        if (target.Host != context.Session.Identity) return "foreign-session";
        UiNode? node = context.Session.FindObservationNode(target.Node);
        if (node == null || node.MountLifetime != target.Mount) return "mount-closed-or-replaced";
        if (target.Definition != context.Session.DefinitionRevision) return "definition-changed; capture-again";
        if (node.InteractionScope != target.Scope) return "logical-target-changed";
        if (!displayRequired || !context.Session.PointerInputAllowed) return "host-input-ineligible";
        if (context.Session.OpenPopupId != null || context.Session.Interactions.CaptureOwner.HasValue) return "modal-or-capture-active";
        PlacedEntry? found = null;
        foreach (PlacedEntry entry in lastEntries) if (ReferenceEquals(entry.Node, node)) { found = entry; break; }
        if (found == null || !node.IsArranged) return "target-hidden-or-unarranged";
        if (found.Widget is not IUiSemanticWidget semantic) return "unsupported-wrapper";
        // Re-read current owner qualification, including ancestors; never accept client-supplied flags.
        for (UiNode? ancestor = node; ancestor != null; ancestor = ancestor.Parent)
        {
            foreach (PlacedEntry entry in lastEntries)
            {
                if (!ReferenceEquals(entry.Node, ancestor)) continue;
                UiWidgetContext local = ContextForEntry(entry, context, default);
                if ((entry.Spec.TryGetBoolean("Hidden", out bool hidden) && hidden)
                    || (entry.Spec.TryGetBoolean("Visible", out bool visible) && !visible)) return "target-hidden";
                string visibility = ReadAttribute(entry.Spec, "VisibleBind");
                if (visibility.Length > 0 && local.Bindings.TryGetBool(visibility, out bool shown) && !shown) return "target-hidden";
                string tab = ReadAttribute(entry.Spec, "Tab");
                if (tab.Length > 0 && (!local.Bindings.TryGet(UiBindings.ActiveTabBind, out string current) || !string.Equals(current, tab, StringComparison.OrdinalIgnoreCase))) return "target-tab-hidden";
                ancestor.IsDisabled = ResolveDisabled(entry.Spec, local);
                break;
            }
        }
        if (UiControls.IsInputDisabled(node)) return "owner-disabled-or-unavailable";
        UiWidgetContext execution = ContextForEntry(found, context, default);
        UiSemanticAction? currentAction = null;
        using (UiBindings.OwnerOf(execution.Bindings).DisplayOnly())
            foreach (UiSemanticAction action in semantic.ReadActions(execution, 128))
                if (ownerAction.Same(action)) { currentAction = action; break; }
        if (currentAction == null) return "part-or-item-no-longer-available";
        if (target.ItemLifetime != 0 && node.ToolItemLifetime(currentAction, mint: false) != target.ItemLifetime) return "part-or-item-lifetime-closed";
        if (!context.Session.IsActive) return "host-closed";
        attempted = true;
        context.Session.NoteInteractionTarget(node);
        UiNode previous = context.Session.EnterNode(node);
        try { return semantic.ExecuteAction(execution, currentAction, value) ? "" : "owner-rejected-operation"; }
        catch (UiRequestFailure failure) { context.Session.RecordRequestFailure(node, failure); throw; }
        finally { context.Session.ExitNode(previous); }
    }
}
