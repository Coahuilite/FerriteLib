using System;
using System.Collections.Generic;
using System.Globalization;

namespace FerriteLib.UiKit.Kernel;

internal sealed partial class UiLayoutEngine
{
    private sealed class LeafMeasure
    {
        internal readonly UiElementSpec Spec;
        internal readonly ITextMetrics Metrics;
        internal readonly UiMeasureInputs Context, Inputs, Sources;
        internal readonly UiMeasureInputs? Environment;
        internal readonly float Height;
        internal readonly int Pass;
        internal LeafMeasure(UiElementSpec spec, ITextMetrics metrics, UiMeasureInputs context, UiMeasureInputs sources,
            UiMeasureInputs inputs, UiMeasureInputs? environment, float height, int pass)
        { Spec = spec; Metrics = metrics; Context = context; Sources = sources; Inputs = inputs; Environment = environment; Height = height; Pass = pass; }
    }
    private sealed class LayoutScope
    {
        internal readonly Dictionary<UiNodeId, List<LeafMeasure>> Leaves = new();
        internal readonly Dictionary<string, UiLayoutReuse> Plans = new(StringComparer.Ordinal);
        internal readonly Dictionary<string, UiNodeId> PlanNodes = new(StringComparer.Ordinal);
        internal LayoutScope Copy()
        {
            var copy = new LayoutScope();
            foreach (var pair in Leaves) copy.Leaves.Add(pair.Key, new List<LeafMeasure>(pair.Value));
            foreach (var pair in Plans) copy.Plans.Add(pair.Key, pair.Value);
            foreach (var pair in PlanNodes) copy.PlanNodes.Add(pair.Key, pair.Value);
            return copy;
        }
    }
    private Dictionary<UiNodeId, LayoutScope> layoutScopes = new();
    internal void ReleaseLayoutCaches()
    {
        layoutScopes.Clear(); textMetricsCache = null; activeLayoutScope = null;
        cachedSnapshot = null; lastEntries.Clear(); widgetInstances.Clear(); repeatStates.Clear();
        materializedRepeats.Clear(); keyNodes.Clear(); committedRevisions.Clear();
        visibleInputs.Clear(); widthInputs.Clear(); tabInputs.Clear(); autoWidths.Clear(); labelContributions.Clear();
    }
    private UiNodeId activeUpdateScope;
    private LayoutScope? activeLayoutScope;
    private bool layoutRequested, forceMeasurement;
    private int layoutPass;
    private bool pendingInputValidation;
    internal void RequestInputValidation() { pendingInputValidation = true; }
    private UiTextMetricsCache? textMetricsCache;
    private int cachedHostLayoutRevision = -1;

    private MeasuredBox MeasureElement(UiWidgetContext ctx, UiElementSpec spec, float width, float availableHeight,
        UiNode node, float contentHeightReference = NoContentHeightReference)
    {
        LayoutScope? parent = activeLayoutScope;
        UiNodeId parentId = activeUpdateScope;
        bool declaresScope = spec.TryGetBoolean("UpdateScope", out bool enabled) && enabled;
        string kind = GetContainerKind(spec);
        bool compositionRoot = kind == "Section" || kind == "Surface" || kind == "Scroll" || kind == "Clip" || kind == "Repeat";
        if (parent == null || declaresScope || compositionRoot)
        {
            activeUpdateScope = node.Id;
            if (!layoutScopes.TryGetValue(node.Id, out activeLayoutScope))
                layoutScopes.Add(node.Id, activeLayoutScope = new LayoutScope());
        }
        node.SetUpdateScope(activeUpdateScope);
        try
        {
            // Natural flow without Fill does not consume a parent's remaining height. A position change
            // therefore cannot turn into a measurement constraint change for an otherwise identical region.
            if (kind.Length > 0 && !ConsumesAvailableHeight(spec)) availableHeight = 0;
            return MeasureElementCore(ctx, spec, width, availableHeight, node, contentHeightReference);
        }
        finally { activeLayoutScope = parent; activeUpdateScope = parentId; }
    }
    private bool ConsumesAvailableHeight(UiElementSpec spec)
    {
        if (IsFill(spec)) return true;
        foreach (UiElementSpec child in spec.Children) if (ConsumesAvailableHeight(child)) return true;
        return GetContainerKind(spec) == "Repeat" && spec.TryGetAttribute("Template", out string name)
            && templates.TryGetValue(name, out UiElementSpec? template) && ConsumesAvailableHeight(template);
    }
    private static UiMeasureInputs MeasureContext(UiWidgetContext ctx) => new(numbers: new[]
    {
        ctx.ViewWidth, (float)ctx.Theme.DefaultFont, ctx.Theme.Geometry.Padding, ctx.Theme.Geometry.Spacing,
        ctx.Theme.Geometry.Gap, ctx.Theme.Geometry.RowHeight, ctx.Theme.Geometry.Hairline, ctx.Node?.SharedLabelWidth ?? 0
    });
    private static UiMeasureInputs MeasureSources(UiElementSpec spec, IUiBindingReader bindings)
    {
        string[] keys = { ReadDeclaredBindKey(spec), ReadAttribute(spec, ActionBindAttribute), ReadAttribute(spec, OptionsBindAttribute),
            ReadAttribute(spec, WidthKeyAttribute), ReadAttribute(spec, VisibleKeyAttribute), ReadAttribute(spec, "SelectedKey"),
            ReadAttribute(spec, TabAttribute).Length > 0 ? UiBindings.ActiveTabKey : "" };
        var values = new List<string>();
        foreach (string key in keys) if (key.Length > 0) { values.Add(key); values.Add(bindings.GetRevision(key).ToString(CultureInfo.InvariantCulture)); }
        return new UiMeasureInputs(values.ToArray());
    }
    private float MeasureWidgetWithReuse(UiElementSpec spec, IUiWidget widget, UiWidgetContext ctx, UiNode node)
    {
        LayoutScope scope = activeLayoutScope!;
        if (!scope.Leaves.TryGetValue(node.Id, out List<LeafMeasure>? records))
            scope.Leaves.Add(node.Id, records = new List<LeafMeasure>());
        UiMeasureInputs context = MeasureContext(ctx);
        LeafMeasure? prior = records.FindLast(value => ReferenceEquals(value.Spec, spec) && ReferenceEquals(value.Metrics, textMetricsCache!.Source) && context.Equals(value.Context));
        bool enrolled = widget is IUiMeasureParticipant;
        bool contextEqual = prior != null;
        UiMeasureInputs sources = MeasureSources(spec, ctx.Bindings);
        if (prior == null && enrolled) prior = records.FindLast(value => ReferenceEquals(value.Spec, spec) && ReferenceEquals(value.Metrics, textMetricsCache!.Source));
        if (prior != null && !ctx.Session.IsTripped(node) && !enrolled && prior.Pass == layoutPass) return prior.Height;
        UiMeasureInputs? environment = (ctx.Metrics as IUiTextMetricsEnvironment)?.CaptureEnvironment(ctx.Theme.DefaultFont);
        bool environmentEqual = prior != null && (!prior.Inputs.UsesTextMetrics || environment != null && environment.Equals(prior.Environment));
        bool validation = ctx.Session.IsTripped(node) || pendingInputValidation || forceMeasurement || node.IsDirty || prior == null || !contextEqual || !environmentEqual || enrolled && !sources.Equals(prior.Sources);
        if (prior != null && !validation && (enrolled || !layoutRequested)) return prior.Height;
        // An old metrics implementation keeps its original idle-snapshot condition, not a new promise
        // that its unknown mutable environment is cacheable at a subsequent synchronization point.
        if (contextEqual && prior != null && !ctx.Session.IsTripped(node) && !layoutRequested && !forceMeasurement && !node.IsDirty && (!enrolled || sources.Equals(prior.Sources)) && prior.Environment == null && environment == null) return prior.Height;

        UiMeasurement? request = enrolled ? ((IUiMeasureParticipant)widget).CaptureMeasurement(ctx) : null;
        if (request == null && enrolled) throw new InvalidOperationException("CaptureMeasurement returned null at " + node.Path + ".");
        UiMeasureInputs inputs = request?.Inputs ?? new UiMeasureInputs(usesTextMetrics: true);
        if (prior != null && !ctx.Session.IsTripped(node) && enrolled && UiMeasureValidity.CanReuse(prior.Inputs, inputs, prior.Environment, environment))
        {
            if (contextEqual) records.Remove(prior);
            records.Add(new LeafMeasure(spec, textMetricsCache!.Source, context, sources, inputs, environment, prior.Height, layoutPass));
            if (records.Count > 4) records.RemoveAt(0);
            return prior.Height;
        }
        node.LayoutMeasureCount++;
        float height = request == null ? widget.Measure(ctx) : request.Calculate();
        if (float.IsNaN(height) || float.IsInfinity(height)) throw new InvalidOperationException("Measurement must be finite at " + node.Path + ".");
        // Tripped/failing work is never promoted to a reusable result. The session guard owns recovery.
        if (!ctx.Session.IsTripped(node))
        {
            if (contextEqual && prior != null) records.Remove(prior);
            records.Add(new LeafMeasure(spec, textMetricsCache!.Source, context, sources, inputs, environment, height, layoutPass));
            if (records.Count > 4) records.RemoveAt(0);
        }
        return height;
    }
    private static bool SameDiagnostics(IReadOnlyList<UiLayoutDiagnostic> before, IReadOnlyList<UiLayoutDiagnostic> after)
    {
        if (before.Count != after.Count) return false;
        for (int i = 0; i < before.Count; i++) if (before[i].Path != after[i].Path || before[i].Code != after[i].Code || before[i].Message != after[i].Message) return false;
        return true;
    }
    private static bool SameGeometry(IReadOnlyList<PlacedEntry> before, IReadOnlyList<PlacedEntry> after)
    {
        if (before.Count != after.Count) return false;
        for (int i = 0; i < before.Count; i++)
        {
            PlacedEntry a = before[i], b = after[i];
            if (!a.Id.Equals(b.Id) || !SameRect(a.Rect, b.Rect) || !SameRect(a.ContentRect, b.ContentRect) || a.SubtreeCount != b.SubtreeCount) return false;
        }
        return true;
    }

    private static bool SameRect(UiLayoutRect a, UiLayoutRect b) => a.X == b.X && a.Y == b.Y && a.Width == b.Width && a.Height == b.Height;
    private static bool SameRect(UiLayoutRect? a, UiLayoutRect? b) => a.HasValue == b.HasValue && (!a.HasValue || SameRect(a.Value, b!.Value));

    private static void AppendDefinition(List<string> into, UiElementSpec spec)
    {
        into.Add(spec.Kind); into.Add(spec.Id);
        var names = new List<string>(spec.Attributes.Keys); names.Sort(StringComparer.Ordinal);
        into.Add(names.Count.ToString(CultureInfo.InvariantCulture));
        foreach (string name in names) { into.Add(name); into.Add(spec.Attributes[name]); }
    }

    private UiLayoutPlan ReusePlan(UiNode node, UiElementSpec spec, string phase, UiLayoutFrame frame, IReadOnlyList<ChildSlot> visible,
        IReadOnlyList<UiLayoutChild> children, UiLayoutMeasure measure, Func<UiLayoutMeasure, UiLayoutPlan> calculate)
    {
        var text = new List<string> { phase };
        AppendDefinition(text, spec);
        var values = new List<float> { frame.Width, frame.AvailableHeight, frame.Left, frame.Top, frame.Right, frame.Bottom,
            frame.Gap, frame.Height.Fixed ?? -1, frame.Height.Fill ? 1 : 0 };
        for (int i = 0; i < children.Count; i++)
        {
            text.Add(node.Id.Child(visible[i].Spec, visible[i].DeclaredIndex).Key);
            AppendDefinition(text, visible[i].Spec);
            UiLayoutChild child = children[i];
            values.AddRange(new[] { child.Width, child.Fill ? 1 : 0, child.AlignX, child.AlignY,
                child.OffsetX.Pixels, child.OffsetX.Fraction, child.OffsetY.Pixels, child.OffsetY.Fraction, child.MatchContentHeight ? 1 : 0 });
        }
        string key = node.Id.Key + ":" + phase + ":" + frame.Width.ToString("R", CultureInfo.InvariantCulture);
        LayoutScope scope = activeLayoutScope!;
        scope.Plans.TryGetValue(key, out UiLayoutReuse? prior);
        UiLayoutReuse next = UiLayoutReuse.Resolve(prior, new UiMeasureInputs(text.ToArray(), values.ToArray()), measure, calculate, out bool reused);
        if (!reused) node.LayoutArrangeCount++;
        scope.Plans[key] = next;
        scope.PlanNodes[key] = node.Id;
        var variants = new List<string>();
        string prefix = node.Id.Key + ":" + phase + ":";
        foreach (string candidate in scope.Plans.Keys) if (scope.PlanNodes[candidate].Equals(node.Id) && candidate.StartsWith(prefix, StringComparison.Ordinal)) variants.Add(candidate);
        while (variants.Count > 4)
        {
            string discard = variants[0] == key ? variants[1] : variants[0];
            scope.Plans.Remove(discard); scope.PlanNodes.Remove(discard); variants.Remove(discard);
        }
        return next.Plan;
    }
}
