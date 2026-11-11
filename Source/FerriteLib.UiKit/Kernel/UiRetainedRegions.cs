using System;
using System.Collections.Generic;
using System.Globalization;

namespace FerriteLib.UiKit.Kernel;

/// <summary>Explicit enrollment in the Host-owned representation port. Ordinary binding calls are unchanged.</summary>
public static class UiRetainedRegions
{
    public static UiRegionAttachment Attach(UiHost host, string rootElementId,
        Func<UiRegionSnapshot?> captureCurrent, UiRegionReadMap reads)
    {
        if (host == null) throw new ArgumentNullException(nameof(host));
        return host.AttachRegion(rootElementId, captureCurrent, reads);
    }
}

public sealed class UiRegionAttachment : IDisposable
{
    internal UiRegionRuntime? Owner;
    internal readonly string RootId;
    internal UiElementSpec Declaration;
    internal readonly UiRegionReadMap Reads;
    internal Func<UiRegionSnapshot?>? Capture;
    internal UiRegionSnapshot? Committed, Candidate;
    internal UiElementSpec? CandidateDeclaration;
    internal long SourceClock, LayoutClock, RetryClock;
    internal long? ValidatedUiClock;
    internal bool Detaching, CandidateRetire;
    internal bool Used, Faulted;
    internal string? Mount;
    private UiRegionStatus status;
    internal UiRegionAttachment(UiRegionRuntime owner, string root, UiElementSpec declaration,
        Func<UiRegionSnapshot?> capture, UiRegionReadMap reads)
    {
        Owner = owner; RootId = root; Declaration = declaration; Capture = capture; Reads = reads;
        status = new(root, null, UiRegionState.Unknown, "not-captured", null, null, null, null);
    }
    public UiRegionStatus Status => status;
    public void Retry()
    {
        UiRegionRuntime owner = Owner ?? throw new ObjectDisposedException(nameof(UiRegionAttachment));
        owner.Retry(this);
    }
    public void Dispose() { Owner?.Detach(this); }
    internal void Report(UiRegionState state, string reason, long? paint = null, bool? current = null)
        => status = new(RootId, Mount, state, reason, SourceClock == 0 ? null : SourceClock,
            LayoutClock == 0 ? null : LayoutClock, paint ?? status.LastPaintPass, current);
    internal void End(UiRegionState state)
    {
        Capture = null; Candidate = Committed = null; CandidateDeclaration = null; Owner = null;
        Report(state, state == UiRegionState.Closed ? "host-closed" : "attachment-detached", current: false);
    }
}

internal sealed partial class UiRegionRuntime
{
    private readonly UiHost host;
    private readonly UiSession session;
    private readonly List<UiRegionAttachment> attachments = new();
    private bool busy, closed;
    internal bool LayoutFailed { get; private set; }
    internal bool HasAttachments => attachments.Count != 0;
    internal bool SourceChanged
    {
        get { foreach (UiRegionAttachment region in attachments)
            if (region.Detaching || !ReferenceEquals(region.Candidate, region.Committed)) return true; return false; }
    }
    internal UiRegionRuntime(UiHost host, UiSession session) { this.host = host; this.session = session; }
    internal UiRegionAttachment Attach(string id, Func<UiRegionSnapshot?> capture, UiRegionReadMap reads, UiLayoutManifest manifest)
    {
        RequireOwner();
        if (busy) throw new InvalidOperationException("Attach regions outside capture/build and active arrangement.");
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A declared region root is required.", nameof(id));
        if (capture == null) throw new ArgumentNullException(nameof(capture));
        if (reads == null) throw new ArgumentNullException(nameof(reads));
        UiElementSpec root = Find(manifest.Roots, id) ?? throw new ArgumentException("Region root is not a unique page declaration: " + id + ".", nameof(id));
        if (!Container(root.Kind)) throw new ArgumentException("Region root must be a structural container: " + id + ".", nameof(id));
        foreach (UiRegionAttachment other in attachments)
            if (!other.Detaching && (Contains(root, other.Declaration) || Contains(other.Declaration, root)))
                throw new ArgumentException("Region attachments cannot overlap: " + id + ".", nameof(id));
        var result = new UiRegionAttachment(this, id, root, capture, reads.Freeze());
        attachments.Add(result); host.RegionInputsChanged(); return result;
    }
    internal void RequireOwner()
    {
        host.RequireRegionOwner();
        if (closed || !session.IsActive) throw new ObjectDisposedException(nameof(UiHost));
    }
    internal void Retry(UiRegionAttachment region)
    {
        if (region.Detaching) throw new ObjectDisposedException(nameof(UiRegionAttachment));
        RequireOwner(); RequireIdle(); region.RetryClock++; host.RegionInputsChanged();
    }
    internal void RequireIdle()
    { if (busy) throw new InvalidOperationException("Region source/Build cannot reenter the Host or retry in-flight work."); }
    internal void EndLifetime(UiNode root)
    {
        foreach (UiRegionAttachment region in attachments)
            for (UiNode? mounted = session.GetNodeByElementId(region.RootId); mounted != null; mounted = mounted.Parent)
                if (ReferenceEquals(mounted, root)) { region.Detaching = true; break; }
        if (!busy) RetireDetached();
    }
    internal void DefinitionCommitted(UiLayoutManifest manifest)
    {
        foreach (UiRegionAttachment region in attachments)
        {
            UiElementSpec? declaration = Find(manifest.Roots, region.RootId);
            if (declaration == null || declaration.Kind != region.Declaration.Kind) region.Detaching = true;
        }
        if (!busy) RetireDetached();
    }
    internal void Detach(UiRegionAttachment region)
    {
        if (region.Detaching) return;
        RequireOwner(); region.Detaching = true; region.Report(UiRegionState.Detached, "pending-safe-boundary", current: false);
        host.RegionInputsChanged();
        if (!busy) RetireDetached();
    }
    private void RetireDetached()
    {
        bool removed = false;
        for (int i = attachments.Count - 1; i >= 0; i--)
            if (attachments[i].Detaching) { attachments[i].End(UiRegionState.Detached); attachments.RemoveAt(i); removed = true; }
        if (attachments.Count == 0) LayoutFailed = false;
        // A Dispose raised during arrangement must not be acknowledged by that arrangement's
        // pending-input reset. Reassert the original-path validation after the safe retirement.
        if (removed && !closed && session.IsActive) host.RegionInputsChanged();
    }
    internal void Begin(UiWidgetContext context, UiLayoutManifest manifest)
    {
        RequireOwner();
        if (busy) throw new InvalidOperationException("Region capture/build cannot reenter its Host.");
        RetireDetached(); busy = true;
        try
        {
            foreach (UiRegionAttachment region in attachments)
            {
                region.Used = region.Faulted = false;
                region.ValidatedUiClock = null;
                region.CandidateRetire = false;
                region.CandidateDeclaration = Find(manifest.Roots, region.RootId);
                if (region.CandidateDeclaration == null || region.CandidateDeclaration.Kind != region.Declaration.Kind)
                { region.CandidateRetire = true; continue; }
                using IDisposable pure = UiBindings.OwnerOf(context.Bindings).Reactive.Pure();
                using IDisposable pureUi = session.RegionPure("CaptureCurrent");
                try { UiRegionWork.Count(session, UiRegionWork.Kind.Capture); region.Candidate = region.Capture!(); }
                catch (Exception error)
                {
                    if (!session.IsActive) throw;
                    region.Faulted = true;
                    region.Report(UiRegionState.Failed, error.GetType().Name + ": " + error.Message, current: false);
                    if (error is UiReactiveFault || !UiObservationBoundary.IsOrdinary(error)) throw;
                    throw new UiRegionSourceFailure(region.RootId, error);
                }
                if (!session.IsActive) throw new ObjectDisposedException(nameof(UiHost));
            }
        }
        catch { End(false); throw; }
    }
    internal void End(bool success)
    {
        if (!busy) return;
        LayoutFailed = !success;
        try
        {
            if (!closed && success)
                foreach (UiRegionAttachment region in attachments)
                {
                    if (region.CandidateRetire) { region.Detaching = true; continue; }
                    if (region.Detaching) continue;
                    if (!ReferenceEquals(region.Committed, region.Candidate)) region.SourceClock++;
                    region.Committed = region.Candidate; region.Declaration = region.CandidateDeclaration!;
                    region.LayoutClock++;
                    bool currentUi = region.ValidatedUiClock.HasValue && session.GetNodeByElementId(region.RootId)?.RegionRevision == region.ValidatedUiClock;
                    if (!region.Faulted) region.Report(region.Committed == null ? UiRegionState.Unknown : region.Used ? UiRegionState.Active : UiRegionState.Fallback,
                        region.Committed == null ? "source-unknown" : !currentUi ? "local-invalidation-pending" : region.Used ? "retained-layout; ordinary operation holes remain live" : "unproved-layout",
                        current: region.Committed != null && currentUi);
                }
        }
        finally
        {
            foreach (UiRegionAttachment region in attachments) { region.Candidate = null; region.CandidateDeclaration = null; region.CandidateRetire = false; }
            busy = false; RetireDetached();
            if (closed) attachments.Clear();
        }
    }
    internal void Close()
    {
        if (closed) return; closed = true;
        foreach (UiRegionAttachment region in attachments) region.End(UiRegionState.Closed);
        if (!busy) attachments.Clear();
    }
    internal UiRegionAttachment? For(UiElementSpec? spec, UiNode? node)
    {
        foreach (UiRegionAttachment region in attachments)
        {
            if (region.Detaching || region.Owner == null || busy && region.CandidateRetire) continue;
            if (spec != null && spec.Id == region.RootId)
            {
                UiNode? mounted = node?.ElementId == region.RootId ? node : session.GetNodeByElementId(region.RootId);
                if (mounted != null) region.Mount = mounted.MountLifetime.ToString(CultureInfo.InvariantCulture);
                return region;
            }
            for (UiNode? current = node; current != null; current = current.Parent)
                if (current.ElementId == region.RootId)
                {
                    region.Mount = current.MountLifetime.ToString(CultureInfo.InvariantCulture); return region;
                }
        }
        return null;
    }
    internal UiRegionSnapshot? Inputs(UiRegionAttachment region) => busy ? region.Candidate : region.Committed;
    internal bool TryValue<T>(UiRegionAttachment? region, string key, UiRegionUse use, out T value)
    {
        value = default!;
        UiRegionSnapshot? input = region == null ? null : Inputs(region);
        if (input == null || !region!.Reads.Find(key, false, use, out var route, out var source, out var item, out var field)) return false;
        try
        {
            if (route.Type != typeof(T)) throw new InvalidOperationException("Region map Value type mismatch: " + key + ".");
            value = (source == null ? input : input.AddressedItem(source, item!)).Get<T>(field); return true;
        }
        catch (Exception error) when (UiObservationBoundary.IsOrdinary(error))
        {
            region.Faulted = true;
            region.Report(UiRegionState.Failed, "mapped-layout-input:" + key + ":" + error.Message, current: false);
            throw new UiRegionSourceFailure(region.RootId + "/" + key, error);
        }
    }
    internal UiRegionRuntime Budget(UiWidgetContext ctx, UiLayoutManifest manifest)
    {
        if (busy || closed) throw new InvalidOperationException("Region budget capture requires an idle live Host.");
        var fork = new UiRegionRuntime(host, session) { busy = true };
        foreach (UiRegionAttachment region in attachments)
        {
            if (region.Detaching || region.Capture == null) continue;
            UiElementSpec? declaration = Find(manifest.Roots, region.RootId);
            if (declaration == null) continue;
            var copy = new UiRegionAttachment(fork, region.RootId, declaration, region.Capture, region.Reads);
            using (session.RegionPure("CaptureCurrent")) copy.Candidate = region.Capture();
            if (!session.IsActive) throw new ObjectDisposedException(nameof(UiHost));
            fork.attachments.Add(copy);
        }
        return fork;
    }
    internal UiHostObservation Observe(UiHostObservation observation, UiObservationBudget budget)
    {
        var facts = new List<UiObservedFact>(observation.Facts);
        int count = 0;
        foreach (UiRegionAttachment region in attachments)
        {
            if (count++ >= budget.Items) break;
            UiRegionStatus status = region.Status;
            string value = "root=" + status.RootElementId + " mount=" + (status.MountId ?? "unknown") + " state=" + status.State
                + " source=" + (status.SourceVersion?.ToString(CultureInfo.InvariantCulture) ?? "unknown")
                + " layout=" + (status.LayoutVersion?.ToString(CultureInfo.InvariantCulture) ?? "unknown")
                + " paint=" + (status.LastPaintPass?.ToString(CultureInfo.InvariantCulture) ?? "NotSampled")
                + " current=" + (status.InputsCurrent?.ToString() ?? "unknown") + " reason=" + status.Reason;
            facts.Add(new UiObservedFact("region-status", budget.Limit(value),
                status.State == UiRegionState.Failed ? UiObservationStatus.Failed : status.InputsCurrent == false ? UiObservationStatus.Stale : UiObservationStatus.Sampled,
                "host/region-owner", truncated: value.Length > budget.Text));
        }
        facts.Add(new UiObservedFact("region-work", budget.Limit(session.LayoutWork.RegionSummary),
            session.LayoutWork.RegionSampled ? UiObservationStatus.Sampled : UiObservationStatus.NotSampled, "host/region-owner"));
        return new UiHostObservation(observation.Host, observation.Source, observation.Frame, observation.Definition,
            observation.LayoutDefinition, observation.SampledAtUtc, observation.Status, observation.Reason,
            observation.Truncated || count > budget.Items, observation.Nodes, Array.AsReadOnly(facts.ToArray()));
    }
    internal static UiElementSpec? Find(IReadOnlyList<UiElementSpec> roots, string id)
    {
        foreach (UiElementSpec root in roots)
        { if (root.Id == id) return root; UiElementSpec? child = Find(root.Children, id); if (child != null) return child; }
        return null;
    }
    private static bool Contains(UiElementSpec outer, UiElementSpec inner)
    { if (ReferenceEquals(outer, inner)) return true; foreach (UiElementSpec child in outer.Children) if (Contains(child, inner)) return true; return false; }
    private static bool Container(string kind) => kind == "Stack" || kind == "Column" || kind == "Row" || kind == "Wrap"
        || kind == "Section" || kind == "Surface" || kind == "Scroll" || kind == "Clip" || kind == "Overlay" || kind == "Repeat";
}

internal sealed class UiRegionSourceFailure : Exception
{
    internal UiRegionSourceFailure(string root, Exception error) : base("Region capture failed at '" + root + "'.", error) { }
}

internal static class UiRegionWork
{
    internal enum Kind { Capture, Build, Reuse, Schedule, Interactive, Unknown, Paint, Clipped }
    internal static void Count(UiSession session, Kind kind)
    {
        if (session.IsBudgetMeasuring || !(UiDiagnosticHub.ActiveSubscription?.TimingEnabled == true || session.DiagnosticOutput?.DetailEnabled == true)) return;
        UiLayoutWork work = session.LayoutWork; work.RegionSampled = true;
        switch (kind)
        {
            case Kind.Capture: Increment(ref work.BoundaryCaptures); break;
            case Kind.Build: Increment(ref work.Builds); break;
            case Kind.Reuse: Increment(ref work.ReusedRegions); break;
            case Kind.Schedule: Increment(ref work.ScheduleReuses); break;
            case Kind.Interactive: Increment(ref work.InteractiveCalls); break;
            case Kind.Unknown: Increment(ref work.UnknownCalls); break;
            case Kind.Paint: Increment(ref work.PaintCalls); break;
            case Kind.Clipped: Increment(ref work.ClippedDisplays); break;
        }
    }
    private static void Increment(ref long value) { if (value < long.MaxValue) value++; }
}
