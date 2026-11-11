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
    internal bool Detaching;
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
    internal bool HasAttachments => attachments.Count != 0;
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
        RequireOwner(); region.RetryClock++; host.RegionInputsChanged();
    }
    internal void Detach(UiRegionAttachment region)
    {
        RequireOwner(); region.Detaching = true; region.Report(UiRegionState.Detached, "pending-safe-boundary", current: false);
        host.RegionInputsChanged();
        if (!busy) RetireDetached();
    }
    private void RetireDetached()
    {
        for (int i = attachments.Count - 1; i >= 0; i--)
            if (attachments[i].Detaching) { attachments[i].End(UiRegionState.Detached); attachments.RemoveAt(i); }
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
                region.CandidateDeclaration = Find(manifest.Roots, region.RootId);
                if (region.CandidateDeclaration == null || region.CandidateDeclaration.Kind != region.Declaration.Kind)
                { region.Detaching = true; continue; }
                using IDisposable pure = UiBindings.OwnerOf(context.Bindings).Reactive.Pure();
                try { region.Candidate = region.Capture!(); }
                catch (Exception error)
                { region.Report(UiRegionState.Failed, error.GetType().Name + ": " + error.Message, current: false); throw; }
                if (!session.IsActive) throw new ObjectDisposedException(nameof(UiHost));
            }
        }
        catch { End(false); throw; }
    }
    internal void End(bool success)
    {
        if (!busy) return;
        try
        {
            if (!closed && success)
                foreach (UiRegionAttachment region in attachments)
                {
                    if (region.Detaching) continue;
                    if (!ReferenceEquals(region.Committed, region.Candidate)) region.SourceClock++;
                    region.Committed = region.Candidate; region.Declaration = region.CandidateDeclaration!;
                    region.LayoutClock++;
                    region.Report(region.Committed == null ? UiRegionState.Unknown : UiRegionState.Fallback,
                        region.Committed == null ? "source-unknown" : "legacy-layout", current: region.Committed != null);
                }
        }
        finally
        {
            foreach (UiRegionAttachment region in attachments) { region.Candidate = null; region.CandidateDeclaration = null; }
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
            if (region.Detaching || region.Owner == null) continue;
            if (spec != null && spec.Id == region.RootId) return region;
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
        if (route.Type != typeof(T)) throw new InvalidOperationException("Region map Value type mismatch: " + key + ".");
        value = (source == null ? input : input.Item(source, item!)).Get<T>(field); return true;
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
