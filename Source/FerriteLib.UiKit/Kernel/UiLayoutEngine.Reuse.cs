using System;
using System.Collections.Generic;
using System.Globalization;

namespace FerriteLib.UiKit.Kernel;

internal sealed partial class UiLayoutEngine
{
    private readonly struct SourceRevision
    {
        internal readonly string Key;
        internal readonly int Revision;
        internal SourceRevision(string key, int revision) { Key = key; Revision = revision; }
    }
    private readonly struct SourceSnapshot : IEquatable<SourceSnapshot>
    {
        internal readonly SourceRevision[] Entries;
        internal SourceSnapshot(SourceRevision[] entries) { Entries = entries; }
        public bool Equals(SourceSnapshot other)
        {
            if (ReferenceEquals(Entries, other.Entries)) return true;
            if (Entries.Length != other.Entries.Length) return false;
            for (int i = 0; i < Entries.Length; i++)
                if (!string.Equals(Entries[i].Key, other.Entries[i].Key, StringComparison.Ordinal)
                    || Entries[i].Revision != other.Entries[i].Revision) return false;
            return true;
        }
    }
    private readonly struct MeasureContextStamp
    {
        private readonly float width, font, size, padding, spacing, gap, row, hairline, label;
        private readonly bool shared;
        internal MeasureContextStamp(UiWidgetContext ctx, bool shared)
        {
            width = ctx.ViewWidth; font = (float)ctx.Theme.DefaultFont; size = UiTextFonts.For(ctx).Font.Size ?? 0;
            padding = ctx.Theme.Geometry.Padding; spacing = ctx.Theme.Geometry.Spacing; gap = ctx.Theme.Geometry.Gap;
            row = ctx.Theme.Geometry.RowHeight; hairline = ctx.Theme.Geometry.Hairline;
            this.shared = shared; label = shared ? ctx.Node?.SharedLabelWidth ?? 0 : 0;
            // Preserve the detached-input constructor's validation, after all values have been read.
            if (!Finite(width) || !Finite(font) || !Finite(size) || !Finite(padding) || !Finite(spacing)
                || !Finite(gap) || !Finite(row) || !Finite(hairline) || !Finite(label))
                throw new ArgumentException("Measurement inputs must be finite.");
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        internal bool Equals(MeasureContextStamp other) => width == other.width && font == other.font && size == other.size
            && padding == other.padding && spacing == other.spacing && gap == other.gap && row == other.row
            && hairline == other.hairline && label == other.label && shared == other.shared;
    }
    private sealed class LeafMeasure
    {
        internal readonly UiElementSpec Spec;
        internal readonly ITextMetrics Metrics;
        internal readonly MeasureContextStamp Context;
        internal readonly UiMeasureInputs Inputs;
        internal readonly SourceSnapshot Sources;
        internal readonly UiMeasureInputs? Environment;
        internal readonly float Height;
        internal readonly int Pass;
        internal LeafMeasure(UiElementSpec spec, ITextMetrics metrics, MeasureContextStamp context, SourceSnapshot sources,
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
        ReleasePipeline();
        layoutScopes.Clear(); textMetricsCache = null; activeLayoutScope = null;
        cachedSnapshot = null; lastEntries = new PageTable<PlacedEntry>(Array.Empty<PlacedEntry>()); widgetInstances.Clear(); repeatStates.Clear();
        materializedRepeats.Clear(); keyNodes.Clear(); committedRevisions.Clear(); candidateRevisions.Clear();
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
        catch (Exception error) when (ctx.Session.IsBudgetMeasuring && error is not NotSupportedException && UiObservationBoundary.IsOrdinary(error))
        {
            throw new InvalidOperationException("Budget measurement failed at '" + node.Path + "' (" + spec.Kind + ").", error);
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
    private static MeasureContextStamp MeasureContext(UiWidgetContext ctx) => new(ctx, true);
    private static SourceSnapshot MeasureSources(UiElementSpec spec, IUiBindingAccess bindings, SourceSnapshot? prior)
    {
        string[]? keys = spec.MeasureSourceKeys;
        if (keys == null)
        {
            string[] declared = { ReadDeclaredBindKey(spec), ReadAttribute(spec, ActionBindAttribute), ReadAttribute(spec, OptionsBindAttribute),
                ReadAttribute(spec, WidthBindAttribute), ReadAttribute(spec, VisibleBindAttribute), ReadAttribute(spec, "SelectedBind"),
                ReadAttribute(spec, TabAttribute).Length > 0 ? UiBindings.ActiveTabBind : "" };
            var nonempty = new List<string>();
            foreach (string key in declared) if (key.Length > 0) nonempty.Add(key);
            spec.MeasureSourceKeys = keys = nonempty.ToArray();
        }
        SourceRevision[]? previous = prior?.Entries, values = null;
        // Retain the same ordered reads, including repeated keys and the first throwing read.
        for (int i = 0; i < keys.Length; i++)
        {
            int revision = bindings.GetRevision(keys[i]);
            if (values == null && previous != null && previous[i].Revision == revision) continue;
            if (values == null) values = previous == null ? new SourceRevision[keys.Length] : (SourceRevision[])previous.Clone();
            values[i] = new SourceRevision(keys[i], revision);
        }
        return new SourceSnapshot(values ?? previous ?? Array.Empty<SourceRevision>());
    }
    private LeafMeasure? FindLeafMeasure(List<LeafMeasure> records, UiElementSpec spec, MeasureContextStamp context, bool requireContext)
    {
        for (int i = records.Count - 1; i >= 0; i--)
        {
            LeafMeasure value = records[i];
            if (ReferenceEquals(value.Spec, spec) && ReferenceEquals(value.Metrics, textMetricsCache!.Source)
                && (!requireContext || context.Equals(value.Context))) return value;
        }
        return null;
    }
    private float MeasureWidgetWithReuse(UiElementSpec spec, IUiWidget widget, UiWidgetContext ctx, UiNode node)
    {
        LayoutScope scope = activeLayoutScope!;
        if (!scope.Leaves.TryGetValue(node.Id, out List<LeafMeasure>? records))
            scope.Leaves.Add(node.Id, records = new List<LeafMeasure>());
        MeasureContextStamp context = MeasureContext(ctx);
        LeafMeasure? prior = FindLeafMeasure(records, spec, context, true);
        bool enrolled = widget is IUiMeasureParticipant;
        if (ctx.Session.IsBudgetMeasuring && !enrolled)
            throw new NotSupportedException("Non-committing measurement requires IUiMeasureParticipant at '" + node.Path + "' (" + widget.Kind + "). Legacy Measure remains supported by MeasureAndArrange.");
        bool contextEqual = prior != null;
        SourceSnapshot sources = MeasureSources(spec, ctx.Bindings, prior?.Sources);
        if (prior == null && enrolled) prior = FindLeafMeasure(records, spec, context, false);
        if (prior != null && !ctx.Session.IsTripped(node) && !enrolled && prior.Pass == layoutPass) return prior.Height;
        UiMeasureInputs? environment = UiTextFonts.Environment(ctx);
        if (validatedLeaves.TryGetValue(node.Id, out List<LeafMeasure>? validated))
        {
            LeafMeasure? ready = null;
            for (int i = validated.Count - 1; i >= 0; i--)
            {
                LeafMeasure value = validated[i];
                if (value.Pass == layoutPass && ReferenceEquals(value.Spec, spec)
                    && ReferenceEquals(value.Metrics, textMetricsCache!.Source) && context.Equals(value.Context) && sources.Equals(value.Sources)
                    && (!value.Inputs.UsesTextMetrics || value.Environment != null && value.Environment.Equals(environment)))
                { ready = value; break; }
            }
            if (ready != null)
            {
                records = WritableLeaves(scope, node, records);
                for (int i = records.Count - 1; i >= 0; i--)
                    if (ReferenceEquals(records[i].Spec, spec) && context.Equals(records[i].Context)) records.RemoveAt(i);
                records.Add(ready);
                return ready.Height;
            }
        }


        bool environmentEqual = prior != null && (!prior.Inputs.UsesTextMetrics || environment != null && environment.Equals(prior.Environment));
        bool validation = ctx.Session.IsTripped(node) || pendingInputValidation && !incrementalValidation || forceMeasurement || node.IsDirty || incrementalValidation && invalidatedInputs.Contains(node) || prior == null || !contextEqual || !environmentEqual || enrolled && !sources.Equals(prior.Sources);
        if (prior != null && !validation && (enrolled || !layoutRequested)) return prior.Height;
        // Preserve the pre-existing idle snapshot, for numeric and legacy alike. No synchronization
        // was requested here. At a synchronization (layoutRequested), unknown environments still
        // cannot authorize text reuse; the normal environment/CanReuse guards above and below apply.
        if (contextEqual && prior != null && !ctx.Session.IsTripped(node) && !layoutRequested && !forceMeasurement && !node.IsDirty && (!enrolled || sources.Equals(prior.Sources)) && prior.Environment == null && environment == null) return prior.Height;

        if (incrementalValidation && (conservativeValidation || !enrolled))
        { validationRequiresFull = true; return prior?.Height ?? 0; }

        UiMeasurement? request = enrolled ? ((IUiMeasureParticipant)widget).CaptureMeasurement(ctx) : null;
        if (request == null && enrolled) throw new InvalidOperationException("CaptureMeasurement returned null at " + node.Path + ".");
        UiMeasureInputs inputs = UiTextFonts.MeasureInputs(ctx, request?.Inputs ?? new UiMeasureInputs(usesTextMetrics: true));
        if (prior != null && !ctx.Session.IsTripped(node) && enrolled && UiMeasureValidity.CanReuse(prior.Inputs, inputs, prior.Environment, environment))
        {
            records = WritableLeaves(scope, node, records);
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
            records = WritableLeaves(scope, node, records);
            if (contextEqual && prior != null) records.Remove(prior);
            records.Add(new LeafMeasure(spec, textMetricsCache!.Source, context, sources, inputs, environment, height, layoutPass));
            if (records.Count > 4) records.RemoveAt(0);
        }
        return height;
    }
    private List<LeafMeasure> WritableLeaves(LayoutScope scope, UiNode node, List<LeafMeasure> records)
    {
        if (incrementalValidation && layoutScopes.TryGetValue(node.UpdateScopeRoot, out var committed)
            && committed.Leaves.TryGetValue(node.Id, out var original) && ReferenceEquals(original, records))
        {
            records = new List<LeafMeasure>(records);
            scope.Leaves[node.Id] = records;
            node.Owner.LayoutWork.CopiedLeaves += records.Count;
        }
        return records;
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
        UiMeasureInputs inputs = new UiMeasureInputs(text.ToArray(), values.ToArray());
        UiLayoutReuse next = UiLayoutReuse.Resolve(prior, inputs, measure, calculate, out bool reused);
        RecordStack(node, phase, frame, visible, children, inputs, next, key);
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
