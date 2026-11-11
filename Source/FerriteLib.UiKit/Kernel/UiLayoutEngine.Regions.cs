using System;
using System.Collections.Generic;

namespace FerriteLib.UiKit.Kernel;

// This is an explicit engine port. It does not replace, wrap or intercept IUiBindingAccess.
internal sealed partial class UiLayoutEngine
{
    internal UiRegionRuntime? Regions;
    private Dictionary<UiNodeId, RegionBox> regionBoxes = new();
    private Dictionary<UiNodeId, RegionLeaf> regionLeaves = new();
    private RegionFrame? regionFrame;
    private UiWidgetContext? regionEnvironment;
    private UiWidgetContext? regionDrawEnvironment;
    private int regionConstraintRevision;
    internal void RegionConstraintsChanged() => regionConstraintRevision++;
    private static bool RegionBackend(UiWidgetContext ctx)
    {
        if (ctx.Session.IsBudgetMeasuring) return true;
        IUiExecution? execution = ctx.Session.CurrentExecution;
        if (execution is IUiExecutionOwner owner) execution = owner.NativeOwner;
        return execution is IUiIsolatedPaint paint && paint.CanIsolatePaint;
    }

    private sealed class RegionFont
    {
        internal UiTextFont Requested;
        internal UiTextPurpose Purpose;
        internal UiTextFont Effective;
        internal UiMeasureInputs? Environment;
        internal bool DirectMetric;
        internal bool Matches(UiWidgetContext ctx)
        {
            if (DirectMetric)
            {
                UiMeasureInputs? current = Requested.Size.HasValue
                    ? (ctx.Metrics as IUiTextFontMetrics)?.CaptureEnvironment(Requested, Purpose)
                    : (ctx.Metrics as IUiTextMetricsEnvironment)?.CaptureEnvironment(Requested.Alias);
                return Environment != null && Environment.Equals(current);
            }
            UiTextRun run = UiTextFonts.Resolve(ctx.Metrics, ctx.Session.CurrentExecution, Requested, Purpose);
            return run.Font == Effective && Environment != null && Environment.Equals(run.CaptureEnvironment());
        }
    }
    private sealed class RegionLeaf
    {
        internal UiRegionAttachment Attachment = null!;
        internal UiRegionSnapshot Inputs = null!;
        internal UiElementSpec Spec = null!;
        internal UiNode Node = null!;
        internal long Mount;
        internal IUiRegionParticipant? Participant;
        internal UiRegionDisplay? Display;
        internal UiRegionDisplay? LastPaintDisplay;
        internal LayoutStamp Layout;
        internal long State, Retry;
        internal long ReaderSchema;
        internal bool Failed;
        internal bool RecoveryPaintFailed;
        internal List<RegionFont> Fonts = new();
        internal int Language, Properties, Colours, HostLayout, Definition, Constraints;
        internal readonly List<(UiNode Node, object Generation)> Ancestors = new();
        internal ITextMetrics Metrics = null!;
        internal IUiTranslation Translation = null!;
        internal IUiBindingAccess Bindings = null!;
        internal UiBindings BindingOwner = null!;
        internal IUiExecution? Execution;
        internal UiWidgetContext? DrawContext;
    }
    private sealed class RegionBox
    {
        internal RegionLeaf Proof = null!;
        internal MeasuredBox Box = null!;
        internal float ConstraintWidth, AvailableHeight, ReferenceHeight;
        internal RegionBox? Alternate;
        internal RegionBox TerminalCopy()
        { var copy = (RegionBox)MemberwiseClone(); copy.Alternate = null; return copy; }
        internal List<MaterializedRepeat> Repeats = new();
        internal Dictionary<string, List<UiNode>> Keys = new(StringComparer.Ordinal);
        internal Dictionary<UiNode, float> Labels = new();
    }
    private sealed class RegionFrame
    {
        internal bool Complete = true;
        internal List<RegionFont> Fonts = new();
    }
    private static ITextMetrics RegionMetrics(UiWidgetContext ctx)
        => ctx.Metrics is UiTextMetricsCache cached ? cached.Source : ctx.Metrics;
    private UiRegionSnapshot? RegionPacket(UiRegionAttachment region, UiElementSpec spec)
    {
        UiRegionSnapshot? root = Regions?.Inputs(region);
        if (root == null || spec.RegionItemSource == null) return root;
        if (root.TryAddressedItem(spec.RegionItemSource, spec.RegionItemKey!, out UiRegionSnapshot packet)) return packet;
        if (!region.Reads.HasItemSource(spec.RegionItemSource)
            && !region.Reads.Find(spec.RegionItemSource, false, UiRegionUse.Layout, out _, out _, out _, out _))
        { if (regionFrame != null) regionFrame.Complete = false; return null; }
        region.Faulted = true;
        region.Report(UiRegionState.Failed, "missing-item:" + spec.RegionItemSource + "/" + spec.RegionItemKey, current: false);
        throw new UiRegionSourceFailure(region.RootId, new KeyNotFoundException("Region item is missing: " + spec.RegionItemSource + "/" + spec.RegionItemKey));
    }
    private bool RegionValue<T>(UiElementSpec spec, UiWidgetContext ctx, string key, out T value, UiNode? parent = null)
    {
        UiRegionAttachment? region = Regions?.For(spec, parent ?? ctx.Node);
        if (RegionBackend(ctx) && Regions != null && Regions.TryValue(region, key, UiRegionUse.Layout, out value)) return true;
        if (region != null && regionFrame != null) regionFrame.Complete = false;
        value = default!; return false;
    }
    private bool RegionStructure(UiElementSpec spec, UiRegionAttachment region)
    {
        foreach (string name in new[] { VisibleBindAttribute, WidthBindAttribute, RepeatItemsAttribute })
        {
            string key = ReadAttribute(spec, name);
            if (key.Length != 0 && !region.Reads.Find(key, false, UiRegionUse.Layout, out _, out _, out _, out _)) return false;
        }
        return ReadAttribute(spec, TabAttribute).Length == 0
            || region.Reads.Find(UiBindings.ActiveTabBind, false, UiRegionUse.Layout, out _, out _, out _, out _);
    }
    private RegionLeaf RegionProof(UiWidgetContext local, UiElementSpec spec, UiRegionAttachment attachment, UiRegionSnapshot packet)
    {
        UiWidgetContext env = regionEnvironment ?? local;
        if (!ReferenceEquals(local.Theme, env.Theme) && !ReferenceEquals(local.Theme, styleResolver?.Baseline))
            local.Theme.ObserveRegionOwner(local.Node);
        var proof = new RegionLeaf { Attachment = attachment, Inputs = packet, Spec = spec, Node = local.Node, Mount = local.Node.MountLifetime, Layout = new LayoutStamp(local),
            State = local.Node.RegionRevision, Retry = attachment.RetryClock, Language = env.Translation.TranslationRevision,
            Translation = env.Translation, Properties = env.Theme.PropertyRevision, Colours = env.Theme.ColourRevision,
            HostLayout = env.Theme.LayoutRevision, Constraints = regionConstraintRevision, Definition = DefinitionRevision(env),
            Metrics = RegionMetrics(env), Execution = env.Session.CurrentExecution,
            Bindings = env.Bindings, BindingOwner = UiBindings.OwnerOf(env.Bindings), ReaderSchema = UiBindings.OwnerOf(env.Bindings).RegionSchemaRevision };
        for (UiNode? parent = local.Node.Parent; parent != null; parent = parent.Parent)
            proof.Ancestors.Add((parent, parent.Properties.RegionInputGeneration));
        return proof;
    }
    private static bool RegionAncestorsCurrent(RegionLeaf proof, UiNode node)
    {
        int i = 0;
        for (UiNode? parent = node.Parent; parent != null; parent = parent.Parent)
            if (i >= proof.Ancestors.Count || !ReferenceEquals(proof.Ancestors[i].Node, parent)
                || !ReferenceEquals(proof.Ancestors[i++].Generation, parent.Properties.RegionInputGeneration)) return false;
        return i == proof.Ancestors.Count;
    }
    private bool RegionCurrent(RegionLeaf proof, UiWidgetContext env, UiElementSpec spec, UiNode node, UiRegionSnapshot? packet)
    {
        if (proof.Attachment.Owner == null || proof.Attachment.Detaching || packet == null
            || !ReferenceEquals(proof.Inputs, packet) || !ReferenceEquals(proof.Spec, spec)
            || !ReferenceEquals(proof.Node, node) || proof.Mount != node.MountLifetime
            || proof.State != node.RegionRevision || proof.Retry != proof.Attachment.RetryClock
            || proof.Language != env.Translation.TranslationRevision || !ReferenceEquals(proof.Translation, env.Translation)
            || proof.Properties != env.Theme.PropertyRevision || proof.Colours != env.Theme.ColourRevision || proof.HostLayout != env.Theme.LayoutRevision
            || proof.Constraints != regionConstraintRevision || !RegionAncestorsCurrent(proof, node) || proof.Definition != DefinitionRevision(env)
            || !ReferenceEquals(proof.Bindings, env.Bindings) || !ReferenceEquals(proof.BindingOwner, UiBindings.OwnerOf(env.Bindings))
            || proof.ReaderSchema != proof.BindingOwner.RegionSchemaRevision
            || !ReferenceEquals(proof.Metrics, RegionMetrics(env)) || !ReferenceEquals(proof.Execution, env.Session.CurrentExecution)) return false;
        foreach (RegionFont font in proof.Fonts) if (!font.Matches(env)) return false;
        return true;
    }
    private static void MergeFonts(List<RegionFont> target, List<RegionFont> incoming)
    {
        foreach (RegionFont font in incoming)
        {
            bool found = false;
            foreach (RegionFont prior in target)
                if (prior.Requested == font.Requested && prior.Purpose == font.Purpose && prior.Effective == font.Effective && prior.DirectMetric == font.DirectMetric)
                { found = true; break; }
            if (!found) target.Add(font);
        }
    }
    private static List<RegionFont> RegionFonts(UiWidgetContext ctx)
    {
        var result = new List<RegionFont>();
        foreach (var fact in ctx.Properties.TextRuns)
        {
            UiTextRun run = UiTextFonts.Resolve(ctx.Metrics, ctx.Session.CurrentExecution, fact.Value.Requested, fact.Key);
            result.Add(new RegionFont { Requested = run.Requested, Purpose = run.Purpose, Effective = run.Font, Environment = run.CaptureEnvironment() });
        }
        return result;
    }
    private bool TryRegionLeaf(UiElementSpec spec, IUiWidget widget, UiWidgetContext ctx, out RegionLeaf? leaf)
    {
        leaf = null;
        if (Regions == null || !RegionBackend(ctx)) return false;
        UiRegionAttachment? attachment = Regions?.For(spec, ctx.Node);
        UiRegionSnapshot? packet = attachment == null ? null : RegionPacket(attachment, spec);
        if (packet == null) return false;
        ctx.Node.RegionAttachment = attachment;
        if (widget is IUiLayoutDiagnosticSource && regionFrame != null) regionFrame.Complete = false;
        IUiRegionParticipant? participant = widget as IUiRegionParticipant;
        if (widget is IUiRegionBuiltin builtin && !builtin.Supports(attachment!.Reads, spec)) participant = null;
        if (participant == null)
        {
            // This builtin's natural/label height has no business reads. Its Draw and pointer
            // producer deliberately remain an ordinary hole (real choices/eligibility per operation).
            if (widget is Widgets.DropdownWidget && ReadAttribute(spec, "LabelBind").Length == 0)
            {
                leaf = RegionProof(ctx, spec, attachment!, packet);
                leaf.Fonts = RegionFonts(ctx);
                return false;
            }
            if (regionFrame != null) regionFrame.Complete = false;
            return false;
        }
        UiWidgetContext env = regionEnvironment ?? ctx;
        if (regionLeaves.TryGetValue(ctx.Node.Id, out RegionLeaf? prior) && RegionCurrent(prior, env, spec, ctx.Node, packet)
            && prior.Layout.Matches(ctx))
        { leaf = prior; if (prior.Failed) attachment!.Faulted = true; return true; }
        leaf = RegionProof(ctx, spec, attachment!, packet); leaf.Participant = participant;
        leaf.LastPaintDisplay = prior?.LastPaintDisplay;
        var usedFonts = new List<RegionFont>();
        UiTextMetricsCache? metricCache = ctx.Metrics as UiTextMetricsCache;
        Action<UiTextFont, UiTextPurpose, UiMeasureInputs?>? previousReads = metricCache?.RegionEnvironmentRead;
        if (metricCache != null) metricCache.RegionEnvironmentRead = (font, purpose, environment) =>
        {
            previousReads?.Invoke(font, purpose, environment);
            foreach (RegionFont used in usedFonts) if (used.Requested == font && used.Purpose == purpose) return;
            usedFonts.Add(new RegionFont { Requested = font, Effective = font, Purpose = purpose, Environment = environment, DirectMetric = true });
        };
        try
        {
            using IDisposable pure = UiBindings.OwnerOf(ctx.Bindings).Reactive.Pure();
            using IDisposable pureUi = ctx.Session.RegionPure("Build");
            UiRegionWork.Count(ctx.Session, UiRegionWork.Kind.Build);
            leaf.Display = (widget is IUiRegionBuiltin builtIn
                ? builtIn.BuildRegion(ctx, packet, Regions!.Inputs(attachment!)!, attachment!.Reads)
                : participant.Build(ctx, packet)) ?? throw new InvalidOperationException("Region Build returned null at '" + ctx.ElementPath + "'.");
            if (!ctx.Session.IsActive) throw new ObjectDisposedException(nameof(UiHost));
            if (!ctx.Session.IsBudgetMeasuring)
            {
                if (prior?.Failed == true) ctx.Session.RegionBuildRecovered(ctx.Node);
                ctx.Node.PresentationUnavailable = false;
            }
        }
        catch (Exception error) when (UiObservationBoundary.IsOrdinary(error))
        {
            if (ctx.Session.IsBudgetMeasuring) throw;
            if (!ctx.Session.IsActive) throw;
            leaf.Failed = true; leaf.Display = prior?.LastPaintDisplay;
            ctx.Node.PresentationUnavailable = true; ctx.Session.SuspendFailedInput(ctx.Node);
            attachment!.Faulted = true;
            attachment.Report(UiRegionState.Failed, "build:" + ctx.ElementPath + ":" + error.Message, current: false);
            UiSessionGuard.RecordRegionFailure(ctx, widget, error);
        }
        finally { if (metricCache != null) metricCache.RegionEnvironmentRead = previousReads; }
        leaf.State = ctx.Node.RegionRevision;
        leaf.Fonts = RegionFonts(ctx);
        MergeFonts(leaf.Fonts, usedFonts);
        regionLeaves[ctx.Node.Id] = leaf;
        return true;
    }
    private MeasuredBox MeasureRegion(UiWidgetContext ctx, UiElementSpec spec, float width, float availableHeight,
        UiNode node, float contentHeightReference)
    {
        UiRegionAttachment? attachment = Regions?.For(spec, node);
        UiRegionSnapshot? packet = attachment == null ? null : RegionPacket(attachment, spec);
        if (packet == null || !RegionBackend(ctx))
        {
            regionBoxes.Remove(node.Id); regionLeaves.Remove(node.Id);
            return MeasureElementCore(ctx, spec, width, availableHeight, node, contentHeightReference);
        }
        node.RegionAttachment = attachment;
        UiWidgetContext local = ElementScope(ctx, spec, node).WithNode(node).WithViewWidth(width);
        UiWidgetContext env = regionEnvironment ?? ctx;
        regionBoxes.TryGetValue(node.Id, out RegionBox? previousBox);
        RegionBox? cached = previousBox;
        while (cached != null && !(cached.ConstraintWidth == width
            && RegionCurrent(cached.Proof, env, spec, node, packet) && cached.Proof.Layout.Matches(local)
            && cached.AvailableHeight == availableHeight && cached.ReferenceHeight == contentHeightReference)) cached = cached.Alternate;
        if (cached != null)
        {
            if (!ReferenceEquals(cached, previousBox))
            {
                RegionBox promoted = cached.TerminalCopy();
                promoted.Alternate = previousBox?.TerminalCopy();
                regionBoxes[node.Id] = cached = promoted;
            }
            if (regionFrame != null) MergeFonts(regionFrame.Fonts, cached.Proof.Fonts);
            // Restore only retained topology/receipts. No Configure, source getter, context per child,
            // presentation demand, Build, label calculation or measurement occurs in this branch.
            foreach (PlacedEntry entry in cached.Box.Entries)
            {
                if (entry.Node != node && entry.Node.Parent is UiNode parent) parent.AddChild(entry.Node);
                if (entry.Region != null) regionLeaves[entry.Id] = entry.Region;
            }
            foreach (var label in cached.Labels) label.Key.SharedLabelWidth = label.Value;
            foreach (var pair in cached.Keys)
            {
                if (!keyNodes.TryGetValue(pair.Key, out List<UiNode>? owners)) keyNodes.Add(pair.Key, owners = new());
                foreach (UiNode owner in pair.Value) if (!owners.Contains(owner)) owners.Add(owner);
                CaptureBindingRevision(pair.Key, ctx.Bindings);
            }
            foreach (MaterializedRepeat repeat in cached.Repeats)
                if (repeatStates.TryGetValue(repeat.Node, out RepeatState? state)) materializedRepeats.Add(new MaterializedRepeat(repeat.Node, state));
            attachment!.Used = true;
            if (spec.Id == attachment.RootId) attachment.ValidatedUiClock = cached.Proof.State;
            UiRegionWork.Count(ctx.Session, UiRegionWork.Kind.Reuse);
            return OffsetBox(cached.Box, 0, 0);
        }
        RegionFrame? parentFrame = regionFrame;
        // An arbitrary forwarding reader can change its endpoint ownership without changing its
        // BindingOwner. Keep its original real-traversal reader verification; no DerivedRow token.
        var frame = new RegionFrame { Complete = RegionStructure(spec, attachment!)
            && ReferenceEquals(ctx.Bindings, UiBindings.OwnerOf(ctx.Bindings)) };
        int diagnosticsBefore = layoutDiagnostics.Count;
        regionFrame = frame;
        try
        {
            MeasuredBox box = MeasureElementCore(ctx, spec, width, availableHeight, node, contentHeightReference);
            if (spec.Id == attachment!.RootId) attachment.ValidatedUiClock = node.RegionRevision;
            if (diagnosticsBefore != layoutDiagnostics.Count) frame.Complete = false;
            MergeFonts(frame.Fonts, RegionFonts(local));
            foreach (PlacedEntry entry in box.Entries)
                if (entry.Region != null) { MergeFonts(frame.Fonts, entry.Region.Fonts); if (entry.Region.Failed) frame.Complete = false; }
            foreach (RegionFont font in frame.Fonts) if (font.Environment == null) frame.Complete = false;
            if (frame.Complete)
            {
                RegionLeaf proof = RegionProof(local, spec, attachment!, packet);
                proof.Fonts = frame.Fonts;
                // Containers also have an immutable prepared context, not just participant leaves.
                // Native groups/qualification still execute with the current origin every event.
                if (box.Entries.Count > 0 && box.Entries[0].IsContainer) box.Entries[0].Region = proof;
                var record = new RegionBox { Proof = proof,
                    Box = OffsetBox(box, 0, 0), ConstraintWidth = width, AvailableHeight = availableHeight, ReferenceHeight = contentHeightReference };
                // Scroll has two real constraint variants (unreserved/reserved width). Retain both,
                // so changing one row does not rebuild every unchanged row twice. Never mutate a
                // committed variant while staging; this bounded pair is still published atomically.
                RegionBox? other = previousBox;
                if (other != null && other.ConstraintWidth == width && other.AvailableHeight == availableHeight && other.ReferenceHeight == contentHeightReference)
                    other = other.Alternate;
                if (other != null && ReferenceEquals(other.Proof.Attachment, attachment) && ReferenceEquals(other.Proof.Inputs, packet)
                    && other.Proof.State == node.RegionRevision) record.Alternate = other.TerminalCopy();
                var members = new HashSet<UiNode>(); foreach (PlacedEntry entry in box.Entries) members.Add(entry.Node);
                foreach (UiNode member in members) record.Labels.Add(member, member.SharedLabelWidth);
                foreach (var pair in keyNodes)
                    foreach (UiNode owner in pair.Value) if (members.Contains(owner))
                    { if (!record.Keys.TryGetValue(pair.Key, out List<UiNode>? owners)) record.Keys.Add(pair.Key, owners = new()); owners.Add(owner); }
                foreach (MaterializedRepeat repeat in materializedRepeats)
                    if (members.Contains(ctx.Session.GetNode(repeat.Node)!)) record.Repeats.Add(repeat);
                regionBoxes[node.Id] = record; attachment!.Used = true;
            }
            else regionBoxes.Remove(node.Id);
            return box;
        }
        finally
        {
            regionFrame = parentFrame;
            if (parentFrame != null) { parentFrame.Complete &= frame.Complete; MergeFonts(parentFrame.Fonts, frame.Fonts); }
        }
    }
    private bool RegionRangeCurrent(PlacedEntry entry, UiWidgetContext env)
    {
        bool current = regionBoxes.TryGetValue(entry.Id, out RegionBox? box) && RegionCurrent(box.Proof, env, entry.Spec, entry.Node,
            RegionPacket(box.Proof.Attachment, entry.Spec));
        if (current)
        {
            box!.Proof.Attachment.Used = true;
            if (entry.Spec.Id == box.Proof.Attachment.RootId) box.Proof.Attachment.ValidatedUiClock = box.Proof.State;
        }
        return current;
    }
    private bool RegionAncestorCurrent(PlacedEntry entry, UiWidgetContext env)
    {
        for (UiNode? node = entry.Node; node != null; node = node.Parent)
            if (entryIndex.TryGetValue(node.Id, out int index) && RegionRangeCurrent(lastEntries[index], env)) return true;
        return false;
    }
    private static float RegionHeight(UiElementSpec spec, RegionLeaf leaf, float? reference)
    {
        if (spec.TryGetNumber("Height", out float height)) return Math.Max(0, height);
        if (UiPlacement.IsMatchContentHeight(spec) && reference.HasValue) return reference.Value;
        return leaf.Display?.NaturalHeight ?? RecoveryBandHeight;
    }
    private void DrawRegion(PlacedEntry entry, UiLayoutRect bounds, UiWidgetContext ctx)
    {
        if (!repaintDraw && (entry.Widget is Widgets.WrappedTextWidget || entry.Widget is Widgets.RuleWidget)) return;
        DrawRegionCore(entry, bounds, ctx);
    }
    private void DrawRegionCore(PlacedEntry entry, UiLayoutRect bounds, UiWidgetContext ctx)
    {
        RegionLeaf leaf = entry.Region!;
        // A paired layout remains paired even when an attachment is detached between manual phases.
        // Re-entry/dispose/close never changes the participant under an in-flight callback.
        IUiRegionParticipant participant = leaf.Participant!;
        UiRegionDisplay? display = leaf.Display;
        bool failed = leaf.Failed;
        bool didPaint = false;
        bool purePhase = false;
        try
        {
            ctx.Session.Execution.WithState(() =>
            {
                if (!failed && display != null)
                {
                    if (entry.Widget is not Widgets.WrappedTextWidget && entry.Widget is not Widgets.RuleWidget)
                    {
                        UiRegionWork.Count(ctx.Session, UiRegionWork.Kind.Interactive);
                        participant.Operate(bounds, ctx, display);
                    }
                    if (!ctx.Session.IsActive) return;
                }
                if (!repaintDraw) return;
                purePhase = true;
                void Paint()
                {
                    if (display == null) { UiThemeDraw.RecoveryBand(ctx.Drawing, bounds, ctx.Theme, entry.Node.Path); return; }
                    if (failed && leaf.RecoveryPaintFailed) { UiThemeDraw.RecoveryBand(ctx.Drawing, bounds, ctx.Theme, entry.Node.Path); return; }
                    didPaint = PaintRegionDisplay(leaf, display, bounds, ctx);
                    if (ctx.Session.IsActive && failed) UiThemeDraw.FailureMarker(ctx.Drawing, bounds, ctx.Theme, entry.Node.Path);
                }
                displayIsolation!.IsolatePaint(Paint);
                if (didPaint && !failed) leaf.LastPaintDisplay = display;
            });
            if (ctx.Session.IsActive && !failed) entry.Node.PresentationUnavailable = false;
            if (didPaint && ctx.Session.IsActive)
            {
                bool faulted = failed || leaf.Attachment.Faulted;
                leaf.Attachment.Report(faulted ? UiRegionState.Failed : UiRegionState.Active,
                    faulted ? leaf.Attachment.Status.Reason : "paired-display", ctx.Session.Frame,
                    !faulted && entry.Node.RegionRevision == leaf.State);
            }
        }
        catch (UiRequestFailure failure)
        {
            leaf.Attachment.Faulted = true;
            leaf.Attachment.Report(UiRegionState.Failed, "request-failure:" + entry.Node.Path, current: false);
            ctx.Session.RecordRequestFailure(entry.Node, failure); UiThemeDraw.RecoveryBand(ctx.Drawing, bounds, ctx.Theme, entry.Node.Path);
        }
        catch (Exception error) when (UiObservationBoundary.IsOrdinary(error))
        {
            // Only pure display failure is retained; an Operate failure keeps the original retry
            // condition on the next operation and is never replayed as a command.
            entry.Node.PresentationUnavailable = true; ctx.Session.SuspendFailedInput(entry.Node);
            leaf.Attachment.Faulted = true;
            if (purePhase)
            {
                leaf.Failed = true; leaf.Attachment.Faulted = true;
                if (failed) leaf.RecoveryPaintFailed = true;
                leaf.Display = leaf.LastPaintDisplay;
                for (UiNode? node = entry.Node; node != null; node = node.Parent) regionBoxes.Remove(node.Id);
            }
            leaf.Attachment.Report(UiRegionState.Failed, "region-dispatch:" + entry.Node.Path + ":" + error.Message, current: false);
            UiSessionGuard.RecordRegionFailure(ctx, entry.Widget!, error);
            UiThemeDraw.RecoveryBand(ctx.Drawing, bounds, ctx.Theme, entry.Node.Path);
        }
    }
    private static bool PaintRegionDisplay(RegionLeaf leaf, UiRegionDisplay display, UiLayoutRect bounds, UiWidgetContext ctx)
    {
        UiLayoutRect? effective = ctx.Session.CurrentClip;
        if (effective.HasValue && (effective.Value.Width <= 0 || effective.Value.Height <= 0))
        { UiRegionWork.Count(ctx.Session, UiRegionWork.Kind.Clipped); return false; }
        using IDisposable pure = UiBindings.OwnerOf(ctx.Bindings).Reactive.Pure();
        using IDisposable pureUi = ctx.Session.RegionPure("Paint");
        if (display.PaintClip is UiLayoutRect local)
        {
            var clip = new UiLayoutRect(bounds.X + local.X, bounds.Y + local.Y, local.Width, local.Height);
            var window = new UiLayoutRect(clip.X + ctx.WindowOrigin.X, clip.Y + ctx.WindowOrigin.Y, clip.Width, clip.Height);
            if (clip.Width <= 0 || clip.Height <= 0 || effective.HasValue &&
                (IntersectClip(effective, window).Width <= 0 || IntersectClip(effective, window).Height <= 0))
            { UiRegionWork.Count(ctx.Session, UiRegionWork.Kind.Clipped); return false; }
            IUiDrawing drawing = ctx.Drawing;
            drawing.Clip(clip, () =>
            {
                using IDisposable coordinates = ctx.Session.RegionDrawing(new UiRegionDrawing(drawing, clip.Position));
                UiRegionWork.Count(ctx.Session, UiRegionWork.Kind.Paint); leaf.Participant!.Paint(bounds, ctx, display);
            });
        }
        else { UiRegionWork.Count(ctx.Session, UiRegionWork.Kind.Paint); leaf.Participant!.Paint(bounds, ctx, display); }
        return true;
    }
    private bool RegionNeedsFull(UiWidgetContext env)
    {
        if (RegionBackend(env) && !ReferenceEquals(env.Bindings, UiBindings.OwnerOf(env.Bindings))) return true;
        for (int i = 0; i < lastEntries.Count; i++)
        {
            PlacedEntry entry = lastEntries[i];
            if (regionBoxes.ContainsKey(entry.Id))
            { if (!RegionRangeCurrent(entry, env)) return true; i += entry.SubtreeCount - 1; }
            else if (entry.Region != null)
            {
                if (entry.Region.Failed) entry.Region.Attachment.Faulted = true;
                if (!RegionCurrent(entry.Region, env, entry.Spec, entry.Node, RegionPacket(entry.Region.Attachment, entry.Spec))) return true;
            }
        }
        // Root source changes can expose members which were previously hidden, including an empty Repeat.
        if (Regions != null && Regions.SourceChanged) return true;
        return false;
    }
    internal void DrawRegionRecovery(UiWidgetContext ctx, UiLayoutRect viewport)
    {
        if (!ctx.Session.IsActive || ctx.Session.Input.Phase != UiFramePhase.Repaint) return;
        regionDrawEnvironment = ctx;
        IUiExecution execution = ctx.Session.Execution;
        execution.WithState(() =>
        {
            Recover(0, lastEntries.Count, new UiLayoutPoint(viewport.X, viewport.Y), null, UiLayoutPoint.Zero);
        });
        void Recover(int start, int end, UiLayoutPoint position, UiLayoutPoint? nativeOrigin, UiLayoutPoint windowOrigin)
        {
            for (int i = start; i < end && ctx.Session.IsActive; i++)
            {
                PlacedEntry entry = lastEntries[i];
                UiLayoutRect rect = ToDrawRect(entry.Rect, position, nativeOrigin);
                UiWidgetContext local = DrawContextForEntry(entry, ctx, windowOrigin);
                if (IsScopedContainer(entry.ContainerKind))
                {
                    UiLayoutPoint scroll = entry.ContainerKind == "Scroll" ? ctx.Session.GetScrollPosition(entry.Node) : UiLayoutPoint.Zero;
                    UiLayoutPoint origin = ScopeWindowOrigin(rect, nativeOrigin, windowOrigin) - scroll;
                    UiLayoutRect? priorClip = ctx.Session.CurrentClip;
                    UiLayoutPoint clipOrigin = ScopeWindowOrigin(rect, nativeOrigin, windowOrigin);
                    ctx.Session.ConstrainClip(new UiLayoutRect(clipOrigin.X, clipOrigin.Y, rect.Width, rect.Height));
                    try { ctx.Drawing.Clip(rect, () => Recover(i + 1, i + entry.SubtreeCount, position,
                        new UiLayoutPoint(entry.Rect.X + scroll.X, entry.Rect.Y + scroll.Y), origin)); }
                    finally { if (ctx.Session.IsActive) ctx.Session.CurrentClip = priorClip; }
                    i += entry.SubtreeCount - 1; continue;
                }
                try
                {
                    if (entry.Region?.Participant != null)
                    {
                        RegionLeaf leaf = entry.Region;
                        if (leaf.LastPaintDisplay != null && !leaf.RecoveryPaintFailed && RegionBackend(local))
                        {
                            PaintRegionDisplay(leaf, leaf.LastPaintDisplay, rect, local);
                            UiThemeDraw.FailureMarker(ctx.Drawing, rect, local.Theme, entry.Node.Path);
                        }
                        else UiThemeDraw.RecoveryBand(ctx.Drawing, rect, local.Theme, entry.Node.Path);
                    }
                    else if (entry.Widget is IUiRetainedDisplay retained && retained.PaintLast(rect, local))
                        UiThemeDraw.FailureMarker(ctx.Drawing, rect, local.Theme, entry.Node.Path);
                    else if (entry.IsContainer) DrawContainer(entry, rect, local);
                    else UiThemeDraw.RecoveryBand(ctx.Drawing, rect, local.Theme, entry.Node.Path);
                }
                catch (Exception error) when (UiObservationBoundary.IsOrdinary(error))
                {
                    if (!ctx.Session.IsActive) return;
                    if (entry.Region != null) entry.Region.RecoveryPaintFailed = true;
                    UiThemeDraw.RecoveryBand(ctx.Drawing, rect, local.Theme, entry.Node.Path);
                }
            }
        }
    }
    private void PruneRegions(UiSession session)
    {
        var gone = new List<UiNodeId>();
        foreach (var pair in regionBoxes)
            if (pair.Value.Proof.Attachment.Owner == null || pair.Value.Proof.Attachment.Detaching
                || !ReferenceEquals(session.GetNode(pair.Key), pair.Value.Proof.Node) || pair.Value.Proof.Mount != pair.Value.Proof.Node.MountLifetime) gone.Add(pair.Key);
        foreach (UiNodeId key in gone) regionBoxes.Remove(key);
        gone.Clear();
        foreach (var pair in regionLeaves)
            if (pair.Value.Attachment.Owner == null || pair.Value.Attachment.Detaching
                || !ReferenceEquals(session.GetNode(pair.Key), pair.Value.Node) || pair.Value.Mount != pair.Value.Node.MountLifetime) gone.Add(pair.Key);
        foreach (UiNodeId key in gone) regionLeaves.Remove(key);
    }
    internal void ReleaseRegions()
    { regionBoxes.Clear(); regionLeaves.Clear(); regionFrame = null; regionEnvironment = regionDrawEnvironment = null; }
}

internal interface IUiRegionBuiltin
{
    bool Supports(UiRegionReadMap reads, UiElementSpec spec);
    UiRegionDisplay BuildRegion(UiWidgetContext context, UiRegionSnapshot inputs, UiRegionSnapshot root, UiRegionReadMap reads);
}
