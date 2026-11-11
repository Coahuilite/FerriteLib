using System;
using System.Collections.Generic;

namespace FerriteLib.UiKit.Kernel;

internal sealed partial class UiLayoutEngine
{
    private sealed class BoundInput<T>
    {
        internal IUiBindingAccess Bindings = null!;
        internal int Revision, Pass;
        internal bool Found;
        internal T Value = default!;
    }
    private sealed class RoleInput
    {
        internal UiElementSpec Spec = null!;
        internal MeasureContextStamp Context;
        internal UiMeasureInputs? Environment;
        internal ITextMetrics Metrics = null!;
        internal IUiTranslation Translation = null!;
        internal int TranslationRevision;
        internal float Value;
        internal float ViewWidth;
        internal UiTextFont Font;
        internal UiTextFont? EditorFont;
    }
    private Dictionary<UiNodeId, RoleInput> autoWidths = new(), labelContributions = new();
    private enum FreshReadKind { Visible, Width, Tab, Repeat, AutoWidth, Label }
    private sealed class FreshRead
    {
        internal FreshReadKind Kind;
        internal string Key = "";
        internal UiNodeId Node;
        internal RoleInput? Role;
    }
    // Admission order comes from the successful real traversal, not dictionary order. Each
    // controlling read must still agree before any later (possibly nested/scoped) demand runs.
    private IReadOnlyList<FreshRead>? freshReads;
    private List<FreshRead>? candidateFreshReads;
    private HashSet<string>? candidateFreshScalars;
    private HashSet<UiNodeId>? candidateFreshRepeats;
    private void RecordFreshRead(FreshReadKind kind, string key)
    {
        if (candidateFreshScalars != null && candidateFreshScalars.Add(kind + ":" + key))
            candidateFreshReads!.Add(new FreshRead { Kind = kind, Key = key });
    }
    private void RecordFreshRole(Dictionary<UiNodeId, RoleInput> records, UiWidgetContext ctx, RoleInput role)
    {
        if (candidateFreshReads != null) candidateFreshReads.Add(new FreshRead
        { Kind = ReferenceEquals(records, autoWidths) ? FreshReadKind.AutoWidth : FreshReadKind.Label, Node = ctx.Node.Id, Role = role });
    }
    private float ReadLayoutRole(Dictionary<UiNodeId, RoleInput> records, UiElementSpec spec, UiWidgetContext ctx, bool shared, Func<float> calculate)
    {
        if (ctx.Node == null) return calculate();
        MeasureContextStamp context = new(ctx, shared);
        UiMeasureInputs? environment = UiTextFonts.Environment(ctx);
        if (!refreshInputs && !layoutRequested && !ctx.Node.IsDirty && records.TryGetValue(ctx.Node.Id, out RoleInput? prior)
            && ReferenceEquals(prior.Spec, spec) && ReferenceEquals(prior.Metrics, textMetricsCache!.Source)
            && ReferenceEquals(prior.Translation, ctx.Translation) && prior.TranslationRevision == ctx.Translation.TranslationRevision
            && context.Equals(prior.Context) && (environment != null ? environment.Equals(prior.Environment) : prior.Environment == null))
        { RecordFreshRole(records, ctx, prior); return prior.Value; }
        float result = calculate();
        records[ctx.Node.Id] = new RoleInput { Spec = spec, Context = context, Environment = environment, Metrics = textMetricsCache!.Source,
            Translation = ctx.Translation, TranslationRevision = ctx.Translation.TranslationRevision, Font = UiTextFonts.For(ctx).Font, ViewWidth = ctx.ViewWidth,
            EditorFont = UiTextFonts.UsesEditor(ctx) ? UiTextFonts.For(ctx, purpose: UiTextPurpose.Editor).Font : null, Value = result };
        RecordFreshRole(records, ctx, records[ctx.Node.Id]);
        return result;
    }
    private Dictionary<string, BoundInput<bool>> visibleInputs = new(StringComparer.Ordinal);
    private Dictionary<string, BoundInput<float>> widthInputs = new(StringComparer.Ordinal);
    private Dictionary<string, BoundInput<string>> tabInputs = new(StringComparer.Ordinal);
    private static void PruneRoleInputs(UiSession session, Dictionary<UiNodeId, RoleInput> records)
    {
        var gone = new List<UiNodeId>();
        foreach (UiNodeId id in records.Keys) if (session.GetNode(id) == null) gone.Add(id);
        foreach (UiNodeId id in gone) records.Remove(id);
    }
    private void PruneBindingInputs<T>(Dictionary<string, BoundInput<T>> records)
    {
        var gone = new List<string>();
        foreach (string key in records.Keys) if (!keyNodes.ContainsKey(key)) gone.Add(key);
        foreach (string key in gone) records.Remove(key);
    }
    private bool InputCurrent<T>(BoundInput<T> prior, IUiBindingAccess bindings, string key)
        => ReferenceEquals(prior.Bindings, bindings) && prior.Revision == bindings.GetRevision(key)
            && ((!refreshInputs && !layoutRequested) || prior.Pass == layoutPass);

    private static bool SameBoundInput<T>(BoundInput<T> before, BoundInput<T> after)
        => before.Found == after.Found && EqualityComparer<T>.Default.Equals(before.Value, after.Value);

    private bool RefreshStructuralInputs(UiWidgetContext ctx, UiSession.ArrangementJournal journal)
    {
        // A previous non-tiered traversal did not collect this optional admission domain.
        if (freshReads == null) return false;
        foreach (FreshRead read in freshReads)
        {
            // Stop at the FIRST changed controller. The full traversal consumes its current-pass
            // scalar receipt and re-establishes the domain; no old descendant getter runs first.
            switch (read.Kind)
            {
                case FreshReadKind.Visible:
                    if (!visibleInputs.TryGetValue(read.Key, out BoundInput<bool>? visible)) return false;
                    TryLayoutBool(ctx.Bindings, read.Key, out _);
                    if (!SameBoundInput(visible, visibleInputs[read.Key])) return false;
                    break;
                case FreshReadKind.Width:
                    if (!widthInputs.TryGetValue(read.Key, out BoundInput<float>? width)) return false;
                    try { TryLayoutFloat(ctx.Bindings, read.Key, out _); }
                    catch (InvalidOperationException ex) when (ex is not UiReactiveFault)
                    { return false; } // The original TryBoundWidth owns wrong-type fallback/diagnostics.
                    if (!SameBoundInput(width, widthInputs[read.Key])) return false;
                    break;
                case FreshReadKind.Tab:
                    if (!tabInputs.TryGetValue(read.Key, out BoundInput<string>? tab)) return false;
                    TryLayoutTab(ctx.Bindings, out _);
                    if (!SameBoundInput(tab, tabInputs[read.Key])) return false;
                    break;
                default:
                    if (!entryIndex.TryGetValue(read.Node, out int index)) return false;
                    PlacedEntry entry = lastEntries[index];
                    UiWidgetContext local = PipelineContext(entry, ctx, journal);
                    if (!entry.LayoutInputs.Matches(local)) return false;
                    if (read.Kind == FreshReadKind.Repeat)
                    { if (!FreshRepeatMatches(entry, local)) return false; }
                    else
                    {
                        RoleInput role = read.Role!;
                        local = local.WithViewWidth(role.ViewWidth);
                        if (read.Kind == FreshReadKind.AutoWidth)
                        { if (MeasureLabelWidthCore(role.Spec, local) != role.Value) return false; }
                        else if (entry.Widget is not IUiLabelledWidget labelled || labelled.MeasureLabel(local) != role.Value) return false;
                    }
                    break;
            }
        }
        return true;
    }
    private bool TryLayoutBool(IUiBindingAccess bindings, string key, out bool value)
    {
        RecordFreshRead(FreshReadKind.Visible, key);
        CaptureBindingRevision(key, bindings);
        if (!visibleInputs.TryGetValue(key, out BoundInput<bool>? input) || !InputCurrent(input, bindings, key))
        {
            input = new BoundInput<bool> { Bindings = bindings, Revision = bindings.GetRevision(key), Pass = layoutPass };
            input.Found = bindings.TryGetBool(key, out input.Value); visibleInputs[key] = input;
        }
        value = input.Value; return input.Found;
    }
    private bool TryLayoutFloat(IUiBindingAccess bindings, string key, out float value)
    {
        RecordFreshRead(FreshReadKind.Width, key);
        CaptureBindingRevision(key, bindings);
        if (!widthInputs.TryGetValue(key, out BoundInput<float>? input) || !InputCurrent(input, bindings, key))
        {
            input = new BoundInput<float> { Bindings = bindings, Revision = bindings.GetRevision(key), Pass = layoutPass };
            input.Found = bindings.TryGet(key, out input.Value); widthInputs[key] = input;
        }
        value = input.Value; return input.Found;
    }
    private bool TryLayoutTab(IUiBindingAccess bindings, out string value)
    {
        string key = UiBindings.ActiveTabBind;
        RecordFreshRead(FreshReadKind.Tab, key);
        CaptureBindingRevision(key, bindings);
        if (!tabInputs.TryGetValue(key, out BoundInput<string>? input) || !InputCurrent(input, bindings, key))
        {
            input = new BoundInput<string> { Bindings = bindings, Revision = bindings.GetRevision(key), Pass = layoutPass };
            input.Found = bindings.TryGet(key, out input.Value); tabInputs[key] = input;
        }
        value = input.Value; return input.Found;
    }
}
