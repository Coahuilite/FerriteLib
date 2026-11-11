using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;

namespace FerriteLib.UiKit.Kernel;

public sealed partial class UiHost
{
    private int budgetDrawDepth, budgetArrangeDepth;

    internal UiLayoutSnapshot MeasureBudget(UiLayoutPoint available, Action<UiLayoutSnapshot>? readPrepared)
    {
        RequireToolThread();
        if (!session.IsActive) throw new ObjectDisposedException(nameof(UiHost));
        if (session.IsBudgetMeasuring || budgetDrawDepth != 0 || budgetArrangeDepth != 0 || pendingDocumentCommit)
            throw new InvalidOperationException("Measure budgets outside Draw and document staging; nested budget measurement is not supported.");
        if (float.IsNaN(available.X) || float.IsInfinity(available.X) || available.X <= 0
            || float.IsNaN(available.Y) || float.IsInfinity(available.Y) || available.Y <= 0)
            throw new ArgumentOutOfRangeException(nameof(available), "Budget constraints must be finite and positive.");

        // The existing binding reader/owner supplies current values, not a previously frozen display.
        // No host presentation enrollment, notification commit or owner advancement occurs here.
        UiBindings owner = UiBindings.OwnerOf(bindings);
        using IDisposable current = owner.UsePresentation(null);
        using UiBindings.DisplayScope pure = owner.DisplayOnly();
        using IDisposable pureRead = owner.Reactive.Pure();
        IDisposable prepared = session.BeginBudgetMeasurement();
        // A private calculation workspace, not another Host, Session or business/node tree. It uses
        // the same measurement implementation and original session nodes; no caches are promoted.
        Exception? failure = null;
        UiLayoutSnapshot? result = null;
        try
        {
            var calculation = new UiLayoutEngine(source, new UiStyleResolver(theme, styleResolver.Document), manifest.Templates, layoutInputs);
            result = calculation.CalculateBudget(CreateContext(available.X), available, manifest.Roots);
            if (pure.Fault != null) throw pure.Fault;
            readPrepared?.Invoke(result);
            if (!session.IsActive) throw new ObjectDisposedException(nameof(UiHost));
            if (pure.Fault != null) throw pure.Fault;
        }
        catch (Exception error) { failure = error; }
        try { prepared.Dispose(); }
        catch (Exception cleanup) when (failure != null && UiObservationBoundary.IsOrdinary(cleanup))
        { throw new AggregateException("Budget measurement and provisional cleanup both failed.", failure, cleanup); }
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
        return result!;
    }

    private void RequireOutsideBudget()
    {
        if (session.IsBudgetMeasuring)
            throw new InvalidOperationException("Budget measurement cannot publish, advance or change owner layout inputs.");
    }
}

internal sealed partial class UiLayoutEngine
{
    // Shared by the existing committed full path and the explicitly non-committing calculation.
    private MeasuredBox MeasureRoots(UiWidgetContext ctx, IReadOnlyList<UiElementSpec> roots, float width, float height)
    {
        var box = new MeasuredBox { Width = width };
        float y = 0;
        for (int i = 0; i < roots.Count; i++)
        {
            UiElementSpec root = roots[i];
            if (IsHidden(root, ctx, narrow: false, parent: null, declaredIndex: i))
            {
                RecordDeclaredKeys(root, ctx.Session.ActiveNode, ctx.Bindings);
                continue;
            }
            UiNode node = ctx.Session.GetOrCreateElementNode(UiNodeId.Root(root, i), root, i);
            MeasuredBox child = OffsetBox(MeasureElement(ctx, root, width, Math.Max(0, height - y), node), 0, y);
            box.Entries.AddRange(child.Entries);
            y += child.Height;
        }
        box.Height = Math.Max(1, y);
        return box;
    }

    internal UiLayoutSnapshot CalculateBudget(UiWidgetContext ctx, UiLayoutPoint available, IReadOnlyList<UiElementSpec> roots)
    {
        layoutRequested = forceMeasurement = true;
        layoutPass++;
        textMetricsCache = new UiTextMetricsCache(ctx.Metrics);
        ctx = ctx.WithMetrics(textMetricsCache);
        ctx.Session.BeginArrange();
        MeasuredBox box = MeasureRoots(ctx, roots, available.X, available.Y);
        var rects = new Dictionary<string, UiLayoutRect>(StringComparer.Ordinal);
        var visible = new List<string>();
        var viewports = new Dictionary<string, UiLayoutRect>(StringComparer.Ordinal);
        var contents = new Dictionary<string, UiLayoutRect>(StringComparer.Ordinal);
        foreach (PlacedEntry entry in box.Entries)
        {
            if (entry.Spec.Id.Length != 0) rects[entry.Spec.Id] = entry.Rect;
            visible.Add(entry.Id.Path);
            if (entry.ContainerKind != "Scroll") continue;
            string key = ScrollKey(entry);
            viewports[key] = entry.Rect;
            if (entry.ContentRect.HasValue) contents[key] = entry.ContentRect.Value;
        }
        // Detached maps only. No node geometry publication, prune/retirement, scroll targeting/clamp,
        // input layers, retained pipeline, notification acknowledgement or last-successful snapshot.
        return new UiLayoutSnapshot(new UiLayoutPoint(available.X, box.Height), rects, visible, viewports,
            contents, ctx.Session.DefinitionRevision, Array.AsReadOnly(layoutDiagnostics.ToArray()));
    }
}

public sealed partial class UiSession
{
    internal bool IsBudgetMeasuring { get; private set; }

    internal IDisposable BeginBudgetMeasurement()
    {
        if (IsBudgetMeasuring || hitPassOpen) throw new InvalidOperationException("Budget measurement requires an idle layout owner.");
        var priorNodes = new Dictionary<UiNodeId, UiNode>(nodes);
        var restores = new List<Action>();
        foreach (UiNode node in nodes.Values) restores.Add(node.CaptureBudgetPreparation());
        var priorDirty = new HashSet<UiNode>(dirtyNodes);
        UiNode priorActive = activeNode;
        long priorMount = nextMountLifetime;
        IsBudgetMeasuring = true;
        return new UiReactiveScope(() =>
        {
            try
            {
                // Explicit owner Close wins. Never resurrect a closed Session or its resources.
                if (!IsActive) return;
                List<Exception>? failures = null;
                foreach (var pair in new List<KeyValuePair<UiNodeId, UiNode>>(nodes))
                {
                    if (!priorNodes.ContainsKey(pair.Key))
                        try { pair.Value.ReleaseState(); }
                        catch (Exception error) when (UiObservationBoundary.IsOrdinary(error)) { (failures ??= new()).Add(error); }
                    if (!IsActive) return;
                }
                nodes.Clear();
                foreach (var pair in priorNodes) nodes.Add(pair.Key, pair.Value);
                foreach (Action restore in restores)
                {
                    try { restore(); }
                    catch (Exception error) when (UiObservationBoundary.IsOrdinary(error)) { (failures ??= new()).Add(error); }
                    if (!IsActive) return;
                }
                dirtyNodes.Clear();
                foreach (UiNode node in priorDirty) dirtyNodes.Add(node);
                activeNode = priorActive;
                // Provisional references cannot escape the synchronous reader and were released above.
                // No observation/operation address is minted by this path.
                nextMountLifetime = priorMount;
                if (failures != null) throw new AggregateException("Budget preparation discarded; one or more local-state cleanup callbacks failed.", failures);
            }
            finally { IsBudgetMeasuring = false; }
        });
    }
}

public sealed partial class UiNode
{
    internal Action CaptureBudgetPreparation()
    {
        Action restore = CaptureArrangement();
        bool dirty = IsDirty;
        int measures = LayoutMeasureCount, arrangements = LayoutArrangeCount;
        var stateKeys = new HashSet<string>(customState.Keys, StringComparer.Ordinal);
        var slotKeys = new HashSet<string>(slots.Keys, StringComparer.Ordinal);
        return () =>
        {
            if (!session.IsActive) return;
            // Pure measurement may read existing local inputs, but cannot mutate their objects.
            // Discard only newly prepared slots; there is no generic business-state clone/rollback.
            List<Exception>? failures = null;
            foreach (string key in new List<string>(customState.Keys))
                if (!stateKeys.Contains(key))
                {
                    customState.Remove(key);
                    if (customRelease.TryGetValue(key, out Action? release))
                    {
                        customRelease.Remove(key);
                        try { release(); }
                        catch (Exception error) when (UiObservationBoundary.IsOrdinary(error)) { (failures ??= new()).Add(error); }
                        if (!session.IsActive) return;
                    }
                }
            foreach (string key in new List<string>(slots.Keys)) if (!slotKeys.Contains(key)) slots.Remove(key);
            restore();
            IsDirty = dirty;
            LayoutMeasureCount = measures;
            LayoutArrangeCount = arrangements;
            if (failures != null) throw new AggregateException("Provisional node state cleanup failed at '" + Path + "'.", failures);
        };
    }
}
