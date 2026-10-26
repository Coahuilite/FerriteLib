using System;
using System.Collections.Generic;

namespace FerriteLib.UiKit.Kernel;

internal sealed partial class UiLayoutEngine
{
    private readonly struct LayoutStamp
    {
        private readonly float width, shared, padding, spacing, gap, row, hairline;
        private readonly UiFont font;
        internal LayoutStamp(UiWidgetContext ctx)
        {
            width = ctx.ViewWidth; shared = ctx.Node.SharedLabelWidth; font = ctx.Theme.DefaultFont;
            var g = ctx.Theme.Geometry; padding = g.Padding; spacing = g.Spacing;
            gap = g.Gap; row = g.RowHeight; hairline = g.Hairline;
        }
        internal bool Matches(UiWidgetContext ctx) => width == ctx.ViewWidth && shared == ctx.Node.SharedLabelWidth
            && font == ctx.Theme.DefaultFont && padding == ctx.Theme.Geometry.Padding && spacing == ctx.Theme.Geometry.Spacing
            && gap == ctx.Theme.Geometry.Gap && row == ctx.Theme.Geometry.RowHeight && hairline == ctx.Theme.Geometry.Hairline;
    }
    private sealed class StackRecipe
    {
        internal UiNode Node = null!;
        internal UiLayoutFrame Frame;
        internal IReadOnlyList<ChildSlot> Slots = null!;
        internal IReadOnlyList<UiLayoutChild> Children = null!;
        internal UiMeasureInputs Inputs = null!;
        internal UiLayoutReuse Reuse = null!;
        internal UiNodeId Scope;
        internal string Key = "";
    }
    private Dictionary<UiNodeId, StackRecipe> stackRecipes = new(), candidateStackRecipes = new();
    private readonly Dictionary<UiNodeId, int> entryIndex = new();
    private Dictionary<string, int> namedIndex = new(StringComparer.Ordinal), scrollIndex = new(StringComparer.Ordinal), contentIndex = new(StringComparer.Ordinal);
    private readonly List<int> rootEntries = new();
    private readonly HashSet<string> visibleDemand = new(StringComparer.Ordinal);
    private readonly HashSet<UiNode> invalidatedInputs = new();
    private IReadOnlyList<UiElementSpec>? pipelineRoots;
    private UiElementSpec[] rootDefinitions = Array.Empty<UiElementSpec>();
    private IUiBindingAccess? pipelineBindings;
    private ITextMetrics? pipelineMetrics;
    private UiTheme? pipelineTheme;
    private IUiTranslation? pipelineTranslation;
    private int pipelinePropertyRevision;
    private bool hasLegacyMeasure, hasCustomDemand, hasReferenceHeight, hasDiagnosticCallbacks, notificationsCommitted, layoutInputChanged;
    private bool incrementalValidation, conservativeValidation, validationRequiresFull;
    // This memo lives only within one synchronization: a conservative fallback must not replay a getter
    // already captured at the same constraints. It is never a cross-pass legacy purity assumption.
    private readonly Dictionary<UiNodeId, List<LeafMeasure>> validatedLeaves = new();

    private void RetainPipeline(UiWidgetContext ctx, IReadOnlyList<UiElementSpec> roots)
    {
        pipelineRoots = roots; rootDefinitions = new UiElementSpec[roots.Count];
        for (int i = 0; i < roots.Count; i++) rootDefinitions[i] = roots[i];
        pipelineBindings = ctx.Bindings; pipelineMetrics = textMetricsCache!.Source;
        pipelineTheme = ctx.Theme; pipelineTranslation = ctx.Translation;
        pipelinePropertyRevision = ctx.Theme.PropertyRevision;
        entryIndex.Clear(); rootEntries.Clear(); visibleDemand.Clear();
        namedIndex = new(StringComparer.Ordinal); scrollIndex = new(StringComparer.Ordinal); contentIndex = new(StringComparer.Ordinal);
        hasLegacyMeasure = false; hasCustomDemand = false; hasReferenceHeight = false; hasDiagnosticCallbacks = false;
        for (int i = 0; i < lastEntries.Count; i++)
        {
            PlacedEntry entry = lastEntries[i]; entryIndex.Add(entry.Id, i);
            if (entry.Node.Parent == null) rootEntries.Add(i);
            if (entry.Spec.Id.Length > 0) namedIndex[entry.Spec.Id] = i;
            if (entry.ContainerKind == "Scroll")
            { scrollIndex[ScrollKey(entry)] = i; if (entry.ContentRect.HasValue) contentIndex[ScrollKey(entry)] = i; }
            hasReferenceHeight |= UiPlacement.IsMatchContentHeight(entry.Spec);
            if (entry.Widget == null) continue;
            hasLegacyMeasure |= entry.Widget is not IUiMeasureParticipant && !FixedLeaf(entry.Spec);
            hasCustomDemand |= entry.Widget is IUiPresentationDemand;
            hasDiagnosticCallbacks |= entry.Widget is IUiLayoutDiagnosticSource;
            string key = ReadDeclaredBindKey(entry.Spec); if (key.Length > 0) visibleDemand.Add(key);
            foreach (var attribute in entry.Spec.Attributes)
                if (UiDefinitionCompiler.BindingRole(scope, entry.Spec, attribute.Key) != UiBindingRole.None)
                    visibleDemand.Add(attribute.Value);
        }
        stackRecipes = candidateStackRecipes;
    }
    private static bool FixedLeaf(UiElementSpec spec) => spec.TryGetNumber("Height", out _);
    private void NotePipelineNotification(string key, IReadOnlyList<UiNode> owners)
    {
        if (visibleInputs.ContainsKey(key) || widthInputs.ContainsKey(key) || tabInputs.ContainsKey(key)) layoutInputChanged = true;
        foreach (UiNode node in owners)
        {
            invalidatedInputs.Add(node);
            if (!entryIndex.TryGetValue(node.Id, out int index) || lastEntries[index].IsContainer) layoutInputChanged = true;
        }
    }
    private void RecordStack(UiNode node, string phase, UiLayoutFrame frame, IReadOnlyList<ChildSlot> slots,
        IReadOnlyList<UiLayoutChild> children, UiMeasureInputs inputs, UiLayoutReuse reuse, string key)
    {
        if (phase != "Stack") return;
        candidateStackRecipes[node.Id] = new StackRecipe { Node = node, Frame = frame, Slots = slots, Children = children,
            Inputs = inputs, Reuse = reuse, Scope = activeUpdateScope, Key = key };
    }
    private bool TryIncremental(UiWidgetContext ctx, UiLayoutPoint available, IReadOnlyList<UiElementSpec> roots, bool externalContentChanged, out UiLayoutSnapshot snapshot)
    {
        snapshot = null!;
        float width = Normalize(available.X, 1f), height = Normalize(available.Y, 1f);
        if (!ReferenceEquals(roots, pipelineRoots) || roots.Count != rootDefinitions.Length) return false;
        for (int i = 0; i < roots.Count; i++) if (!ReferenceEquals(roots[i], rootDefinitions[i])) return false;
        if (cachedSnapshot == null || textMetricsCache == null || externalContentChanged
            || cachedAvailable.X != width || cachedAvailable.Y != height
            || cachedDefinitionRevision != DefinitionRevision(ctx) || cachedTranslationRevision != ctx.Translation.TranslationRevision
            || cachedHostLayoutRevision != ctx.Theme.LayoutRevision || pipelinePropertyRevision != ctx.Theme.PropertyRevision
            || !ReferenceEquals(pipelineBindings, ctx.Bindings) || !ReferenceEquals(pipelineMetrics, ctx.Metrics)
            || !ReferenceEquals(pipelineTheme, ctx.Theme) || !ReferenceEquals(pipelineTranslation, ctx.Translation)
            || hasCustomDemand || hasDiagnosticCallbacks || hasLegacyMeasure && pendingInputValidation) return false;
        if (hasReferenceHeight && (pendingInputValidation || ctx.Session.HasDirtyNodes)) return false;
        if (pendingInputValidation && (visibleInputs.Count != 0 || widthInputs.Count != 0 || tabInputs.Count != 0 || repeatStates.Count != 0)) return false;

        foreach (UiNode node in ctx.Session.DirtyNodes)
            if (!entryIndex.TryGetValue(node.Id, out int index) || lastEntries[index].IsContainer) return false;
        UiBindings registry = UiBindings.OwnerOf(ctx.Bindings);
        bool ordinaryBoundary = !ReferenceEquals(registry, ctx.Bindings) || registry.HasOrdinaryReads;
        if (ordinaryBoundary)
        {
            CommitNotifications(ctx); notificationsCommitted = true;
            if (pendingInputValidation || ctx.Session.HasDirtyNodes || cachedContentRevision != ctx.Session.ContentRevision
                || invalidatedInputs.Count > 0) return false;
        }
        // Only already-visible, statically declared tracked demand is advanced here. Ordinary getters
        // are not read; dynamic custom demand keeps its original full traversal and call conditions.
        UiBindings demandOwner = UiBindings.OwnerOf(ctx.Bindings);
        foreach (string key in visibleDemand) demandOwner.PreparePresentation(key);
        if (!notificationsCommitted) CommitNotifications(ctx); notificationsCommitted = true;
        layoutRequested = pendingInputValidation || ctx.Session.HasDirtyNodes || cachedContentRevision != ctx.Session.ContentRevision;
        forceMeasurement = false;
        if (layoutInputChanged || hasLegacyMeasure && layoutRequested || repeatStates.Count > 0 && layoutRequested) return false;
        // These roles can change a peer's width or a scrollbar reservation without changing an outer band.
        // Their unknown/legacy calculation conditions are retained on the complete path.
        if ((autoWidths.Count > 0 || labelContributions.Count > 0) && layoutRequested) return false;
        foreach (RoleInput role in autoWidths.Values) if (!RoleEnvironmentCurrent(role, ctx)) return false;
        foreach (RoleInput role in labelContributions.Values) if (!RoleEnvironmentCurrent(role, ctx)) return false;
        foreach (PlacedEntry entry in lastEntries) if (ctx.Session.IsTripped(entry.Node)) return false;

        UiTextMetricsCache committedText = textMetricsCache;
        UiSession.ArrangementJournal journal = ctx.Session.BeginIncrementalArrangement();
        var staged = new Dictionary<UiNodeId, List<LeafMeasure>>();
        var heights = new Dictionary<int, float>();
        var changes = new Dictionary<int, PlacedEntry>();
        var plans = new Dictionary<UiNodeId, StackRecipe>();
        bool committed = false;
        try
        {
            textMetricsCache = committedText.Copy();
            UiWidgetContext baseContext = ctx.WithMetrics(textMetricsCache);
            var validationScope = new LayoutScope();
            incrementalValidation = true; conservativeValidation = ordinaryBoundary; validationRequiresFull = false;
            for (int index = 0; index < lastEntries.Count; index++)
            {
                PlacedEntry entry = lastEntries[index];
                UiWidgetContext local = PipelineContext(entry, baseContext, journal);
                if (!entry.LayoutInputs.Matches(local)) return false;
                if (entry.Widget == null || FixedLeaf(entry.Spec) || entry.HasContentHeightReference) continue;
                if (!layoutScopes.TryGetValue(entry.Node.UpdateScopeRoot, out LayoutScope? ownerScope)
                    || !ownerScope.Leaves.TryGetValue(entry.Id, out List<LeafMeasure>? records)) return false;
                validationScope.Leaves.Clear();
                validationScope.Leaves.Add(entry.Id, records);
                LayoutScope? previousScope = activeLayoutScope; activeLayoutScope = validationScope;
                UiNode previousNode = ctx.Session.EnterNode(entry.Node);
                float measured;
                try { measured = ResolveHeight(entry.Spec, entry.Widget, local, entry.MeasureWidth, entry.Node, null); }
                finally { ctx.Session.ExitNode(previousNode); activeLayoutScope = previousScope; }
                if (validationRequiresFull) return false;
                List<LeafMeasure> nextRecords = validationScope.Leaves[entry.Id];
                if (!ReferenceEquals(nextRecords, records))
                { staged[entry.Id] = nextRecords; validatedLeaves[entry.Id] = nextRecords; }
                if (measured != entry.Rect.Height) heights[index] = measured;
            }
            // A retained MatchContent allocation is valid on idle, but any newly validated result can
            // change its independent reference. Do not infer that dependency from an outer rectangle.
            if (hasReferenceHeight && (staged.Count != 0 || heights.Count != 0 || invalidatedInputs.Count != 0)) return false;
            if (heights.Count > 0 && !ReflowStableStacks(heights, changes, plans)) return false;
            EntryPages next = changes.Count == 0 ? lastEntries : lastEntries.Replace(changes);
            foreach (var pair in changes)
            {
                PlacedEntry entry = pair.Value; journal.Touch(entry.Node);
                entry.Node.Rect = entry.Rect; entry.Node.ContentRect = entry.ContentRect;
            }
            float totalHeight = 0;
            foreach (int root in rootEntries) totalHeight += next[root].Rect.Height;
            UiLayoutSnapshot result = changes.Count == 0 ? cachedSnapshot
                : new UiLayoutSnapshot(new UiLayoutPoint(width, Math.Max(1, totalHeight)), new GeometryView(namedIndex, next),
                    cachedSnapshot.VisibleIds, new GeometryView(scrollIndex, next), new GeometryView(contentIndex, next, true),
                    cachedDefinitionRevision, cachedSnapshot.Diagnostics);
            foreach (int scroll in contentIndex.Values)
            { PlacedEntry entry = next[scroll]; ctx.Session.SetScrollPosition(entry.Node, ClampScroll(ctx.Session.GetScrollPosition(entry.Node), entry.ContentRect!.Value, entry.Rect)); }
            ApplyScrollTarget(ctx, next);
            // No author callback remains after this publication boundary.
            foreach (var pair in staged) layoutScopes[lastEntries[entryIndex[pair.Key]].Node.UpdateScopeRoot].Leaves[pair.Key] = pair.Value;
            foreach (var pair in plans)
            {
                StackRecipe recipe = pair.Value; stackRecipes[pair.Key] = recipe;
                layoutScopes[recipe.Scope].Plans[recipe.Key] = recipe.Reuse;
            }
            lastEntries = next; cachedSnapshot = result;
            cachedContentRevision = ctx.Session.ContentRevision;
            observationLayoutFrame = ctx.Session.Frame; observationLayoutAtUtc = DateTime.UtcNow;
            pendingInputValidation = false; ctx.Session.ClearDirtyNodes();
            ctx.Session.LayoutWork.GeometryEntries += changes.Count;
            ctx.Session.LayoutWork.PublishedEntries += changes.Count;
            if (staged.Count == 0 && changes.Count == 0) { ctx.Session.LayoutWork.Reused++; ctx.Session.LayoutWork.Last = "validated-retained"; }
            else { ctx.Session.LayoutWork.SparseCandidates++; ctx.Session.LayoutWork.Last = "stable-candidate"; }
            journal.Commit(); committed = true; snapshot = result; return true;
        }
        finally
        {
            activeLayoutScope = null; incrementalValidation = false;
            if (!committed) { journal.Rollback(); textMetricsCache = committedText; }
        }
    }
    private bool RoleEnvironmentCurrent(RoleInput input, UiWidgetContext ctx)
    {
        if (!ReferenceEquals(input.Metrics, ctx.Metrics) || !ReferenceEquals(input.Translation, ctx.Translation)
            || input.TranslationRevision != ctx.Translation.TranslationRevision) return false;
        UiMeasureInputs? environment = (ctx.Metrics as IUiTextMetricsEnvironment)?.CaptureEnvironment(input.Font);
        return environment == null ? input.Environment == null : environment.Equals(input.Environment);
    }
    private void ReleasePipeline()
    {
        stackRecipes.Clear(); candidateStackRecipes.Clear(); entryIndex.Clear();
        namedIndex = new(StringComparer.Ordinal); scrollIndex = new(StringComparer.Ordinal); contentIndex = new(StringComparer.Ordinal);
        rootEntries.Clear(); visibleDemand.Clear(); validatedLeaves.Clear(); invalidatedInputs.Clear();
        pipelineRoots = null; rootDefinitions = Array.Empty<UiElementSpec>();
        pipelineBindings = null; pipelineMetrics = null; pipelineTheme = null; pipelineTranslation = null;
    }
    private UiWidgetContext PipelineContext(PlacedEntry entry, UiWidgetContext ctx, UiSession.ArrangementJournal journal)
    {
        if (styleResolver != null && entry.StyleChain != null && entry.StyleChain.Count > 0)
            styleResolver.ThemeFor(entry.StyleChain, entry.Node.Path);
        UiTheme host = styleResolver?.Baseline ?? ctx.Theme;
        UiFont fallback = UiWidgetRegistry.GetFallbackFont(ctx.Source, entry.Node.Kind);
        if (!entry.Node.Properties.TryCurrentResolution(entry.Node, host, fallback, out UiTheme theme))
        { journal.Touch(entry.Node); theme = entry.Node.Properties.Apply(entry.Node, host, fallback); }
        return ctx.WithStyleChain(entry.StyleChain).WithTheme(theme).WithViewWidth(entry.MeasureWidth).WithNode(entry.Node);
    }
}
