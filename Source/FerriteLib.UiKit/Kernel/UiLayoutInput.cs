using System;
using System.Collections.Generic;

namespace FerriteLib.UiKit.Kernel;

/// <summary>Explicit structural-container constraints and their non-committing measurement companion.
/// Null constraint fields retain XML/automatic behavior.</summary>
public sealed class UiLayoutInput
{
    public const int BudgetMeasurementVersion = 1;

    /// <summary>Measures current constraints without publishing layout or changing scroll. The optional
    /// reader may inspect provisional nodes synchronously; only the detached snapshot may escape.</summary>
    public static UiLayoutSnapshot MeasureBudget(UiHost host, UiLayoutPoint available, Action<UiLayoutSnapshot>? readPrepared = null)
        => (host ?? throw new ArgumentNullException(nameof(host))).MeasureBudget(available, readPrepared);

    public UiLayoutInput(float? height = null, bool? narrow = null)
    {
        if (height.HasValue && (float.IsNaN(height.Value) || float.IsInfinity(height.Value) || height.Value < 0)) throw new ArgumentOutOfRangeException(nameof(height));
        Height = height; Narrow = narrow;
    }
    public float? Height { get; }
    public bool? Narrow { get; }
}

public sealed partial class UiHost
{
    private readonly Dictionary<string, UiLayoutInput> layoutInputs = new(StringComparer.Ordinal);
    private void PruneLayoutInputs(UiLayoutManifest previous, UiLayoutManifest next)
    {
        string? Kind(UiLayoutManifest document, string id)
        {
            string? kind = null;
            void Find(UiElementSpec spec) { if (spec.Id == id) kind = spec.Kind; foreach (UiElementSpec child in spec.Children) Find(child); }
            foreach (UiElementSpec root in document.Roots) Find(root);
            return kind;
        }
        foreach (string id in new List<string>(layoutInputs.Keys))
            if (Kind(next, id) == null || Kind(previous, id) != Kind(next, id)) layoutInputs.Remove(id);
    }
    /// <summary>Changes a height budget or responsive variant without replacing the definition or Session.</summary>
    public void SetLayoutInput(string elementId, UiLayoutInput? input)
    {
        RequireOutsideBudget();
        RequireToolThread();
        if (!session.IsActive) throw new ObjectDisposedException(nameof(UiHost));
        UiElementSpec? found = null;
        void Find(UiElementSpec spec) { if (spec.Id == elementId && elementId.Length > 0) found = spec; foreach (UiElementSpec child in spec.Children) Find(child); }
        foreach (UiElementSpec root in manifest.Roots) Find(root);
        if (found == null || !UiLayoutEngine.AcceptsLayoutInput(found)) throw new ArgumentException("Layout input requires a declared structural container ID in this Host.", nameof(elementId));
        if (input?.Narrow.HasValue == true && !found.TryGetAttribute("Breakpoint", out _)) throw new ArgumentException("A Narrow input selects an existing declared responsive variant; declare its Breakpoint first.", nameof(input));
        if (input == null)
        { if (!layoutInputs.Remove(elementId)) return; }
        else
        {
            if (layoutInputs.TryGetValue(elementId, out UiLayoutInput? prior) && prior.Height == input.Height && prior.Narrow == input.Narrow) return;
            layoutInputs[elementId] = input;
        }
        // The established full transaction is the deliberate conservative fallback for changed constraints.
        session.BumpContentRevision();
        engine.RequestInputValidation();
    }
    /// <summary>Completes/cancels only the named existing edit through its wrapper; never implies persistence.</summary>
    public bool CompleteEdit(string elementId, bool cancel = false)
    {
        RequireToolThread();
        if (!session.IsActive || !DisplayRequired) return false;
        return engine.CompleteEdit(CreateContext(0), elementId, cancel);
    }
    /// <summary>Ends only this mounted subtree's UI lifetime, without submitting edits or changing business state.</summary>
    public void EndElementLifetime(string elementId)
    {
        RequireToolThread();
        if (!session.IsActive) throw new ObjectDisposedException(nameof(UiHost));
        UiNode root = Element(elementId);
        try { session.EndSubtreeLifetime(root); }
        finally
        {
            engine.ReleaseLayoutCaches();
            if (session.IsActive) session.BumpContentRevision();
        }
    }
}

public sealed partial class UiSession
{
    internal void EndSubtreeLifetime(UiNode root)
    {
        var keep = new HashSet<UiNodeId>();
        var retiring = new List<UiNode>();
        foreach (UiNode node in nodes.Values)
        {
            bool belongs = false;
            for (UiNode? ancestor = node; ancestor != null; ancestor = ancestor.Parent)
                if (ReferenceEquals(ancestor, root)) { belongs = true; break; }
            if (belongs) retiring.Add(node); else keep.Add(node.Id);
        }
        var failures = new List<Exception>();
        foreach (UiNode node in retiring)
        {
            if (!IsActive) break;
            Interactions.EndNode(node.Id.Key, UiInteractionEnd.Removed);
            try { SuspendFailedInput(node); }
            catch (Exception error) when (UiObservationBoundary.IsOrdinary(error)) { failures.Add(error); }
        }
        try { if (IsActive) PruneNodesExcept(keep); }
        catch (Exception error) when (UiObservationBoundary.IsOrdinary(error)) { failures.Add(error); }
        finally
        {
            var ended = new HashSet<UiNode>(retiring);
            DropLayersOf(ended, hitLayers);
            DropLayersOf(ended, dispatchLayers);
            if (activeEditNode != null && ended.Contains(activeEditNode)) { activeEditNode = null; activeEditState = null; }
            if (lastInteractionNode != null && ended.Contains(lastInteractionNode)) lastInteractionNode = null;
            if (cancelTargetNode != null && ended.Contains(cancelTargetNode)) cancelTargetNode = null;
        }
        if (failures.Count > 0) throw new AggregateException("Local UI lifetime ended; one or more owner releases failed.", failures);
    }
}

internal sealed partial class UiLayoutEngine
{
    internal static bool AcceptsLayoutInput(UiElementSpec spec) => GetContainerKind(spec).Length > 0;
    private readonly IReadOnlyDictionary<string, UiLayoutInput> dynamicLayoutInputs;
    private UiLayoutInput? DynamicInput(UiElementSpec spec) => dynamicLayoutInputs.TryGetValue(spec.Id, out UiLayoutInput? input) ? input : null;
    internal bool CompleteEdit(UiWidgetContext context, string elementId, bool cancel)
    {
        foreach (PlacedEntry entry in lastEntries)
        {
            if (entry.Spec.Id != elementId || entry.Node.EditingState?.Editor is not IUiEditState) continue;
            UiToolOperation operation = cancel ? UiToolOperation.CancelEdit : UiToolOperation.CommitEdit;
            var target = new UiToolTarget("owned-edit", context.Session.Identity, context.Session.DefinitionRevision,
                entry.Node.MountLifetime, entry.Id.Key, entry.Node.InteractionScope, new UiSemanticAction(operation, "editor"));
            return ExecuteTool(context, target, target.Action, new UiToolValue(), true, out _).Length == 0;
        }
        return false;
    }
}
