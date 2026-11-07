using System;
using System.Collections.Generic;
using System.Threading;

namespace FerriteLib.UiKit.Kernel;
public sealed partial class UiHost
{
    private readonly int toolOwnerThread = Thread.CurrentThread.ManagedThreadId;
    private UiToolConnection? toolConnection;
    private bool toolOperationAttempted;
    private Action<UiLayoutPoint>? previewPreparation;
    private bool preparingPreview;
    private UiCostSampleAttachment? costSampleProvider;
    internal void AttachPreviewPreparation(Action<UiLayoutPoint> prepare)
    {
        RequireToolThread();
        if (!session.IsActive) throw new ObjectDisposedException(nameof(UiHost));
        if (previewPreparation != null) throw new InvalidOperationException("This Host already has author preview preparation.");
        previewPreparation = prepare;
    }
    private bool PreparePreview(UiLayoutPoint available)
    {
        if (previewPreparation == null) return session.IsActive;
        RequireToolThread();
        if (preparingPreview) throw new InvalidOperationException("Author preparation cannot recursively advance its Host.");
        preparingPreview = true;
        try { previewPreparation(available); }
        finally { preparingPreview = false; }
        return session.IsActive;
    }
    /// <summary>Opt in the actual window's existing sampler. Replacement/detach retires pending requests, never retargets them.</summary>
    public void SetCostSampleProvider(IUiCostSampleProvider? provider)
    {
        RequireToolThread();
        if (!session.IsActive) throw new ObjectDisposedException(nameof(UiHost));
        if (ReferenceEquals(costSampleProvider?.Provider, provider)) return;
        costSampleProvider = provider == null ? null : new UiCostSampleAttachment(provider);
    }
    internal UiCostSampleAttachment? CostSampleProvider => costSampleProvider;
    /// <summary>Explicit tool attachment on the UI thread. Existing R9 and diagnostic calls need no connection.</summary>
    public UiToolConnection ConnectTools(Version minimum, Version maximumExclusive, UiToolAccess access = UiToolAccess.ReadOnly)
    {
        RequireToolThread();
        if (!session.IsActive) throw new ObjectDisposedException(nameof(UiHost));
        if (!FerriteLibVersion.Require(minimum, maximumExclusive, "uikit.tools", out string diagnostic)) throw new InvalidOperationException(diagnostic);
        if (toolConnection != null && !toolConnection.IsClosed) throw new InvalidOperationException("This Host already has a tool connection.");
        toolConnection?.ReleaseInstruments();
        toolConnection = new UiToolConnection(this, access);
        session.ToolsClosing = toolConnection.Dispose;
        return toolConnection;
    }
    public static UiToolConnection ConnectLiveTools(int identity, Version minimum, Version maximumExclusive, UiToolAccess access = UiToolAccess.ReadOnly)
    {
        foreach (WeakReference<UiHost> reference in observedHosts)
            if (reference.TryGetTarget(out UiHost? host) && host.session.IsActive && host.session.Identity == identity)
                return host.ConnectTools(minimum, maximumExclusive, access);
        throw new InvalidOperationException("No discovered live Host with this identity; never select another Host implicitly.");
    }
    private void RequireToolThread()
    { if (Thread.CurrentThread.ManagedThreadId != toolOwnerThread) throw new InvalidOperationException("Live Host access requires its owning UI thread."); }
    internal void DetachTools(UiToolConnection connection)
    {
        if (!ReferenceEquals(toolConnection, connection)) return;
        toolConnection = null;
        session.ToolsClosing = null;
        session.ToolSelection = null;
        session.ClearToolItems();
    }
    internal IReadOnlyList<UiToolCapability> ReadToolTargets(UiHostObservation observation, UiObservationBudget budget, out bool limited)
    {
        RequireToolThread();
        return engine.ReadToolTargets(CreateContext(0), observation, budget, out limited);
    }
    private void PumpTools()
    {
        toolOperationAttempted = false;
        UiToolConnection? connection = toolConnection;
        if (connection == null) return;
        connection.RequireThread();
        if (connection.IsClosed) { connection.ReleaseInstruments(); DetachTools(connection); return; }
        // Refresh only explicitly captured or pending targets. No connection/capture means no semantic polling.
        HashSet<string>? interested = connection.InterestedNodes();
        if (interested != null) engine.RefreshToolItems(CreateContext(0), interested);
        // One attempt per explicit advance/native callback; no timer, retries or background UI access.
        UiToolConnection.Pending? pending = connection.Take();
        if (pending == null) return;
        bool attempted = false;
        if (pending.CostRequest != null)
        {
            try
            {
                if (!session.IsActive || !ReferenceEquals(costSampleProvider, pending.CostProvider))
                    connection.Complete(pending, UiToolRequestStatus.Closed, "sample-provider-closed-or-replaced", false);
                else
                {
                    attempted = true;
                    bool accepted = pending.CostProvider!.Provider.RequestSample(pending.CostRequest);
                    connection.Complete(pending, accepted ? UiToolRequestStatus.Handled : UiToolRequestStatus.Rejected,
                        accepted ? "owner-accepted; sample-completion-not-implied" : "sample-owner-declined", true);
                }
            }
            catch (Exception error) when (UiObservationBoundary.IsOrdinary(error))
            { connection.Complete(pending, UiToolRequestStatus.Failed, error.GetType().Name + ": " + error.Message, attempted); }
            return; // Sampling does not request another arrangement or take a second queue entry.
        }
        try
        {
            string reason = engine.ExecuteTool(CreateContext(0), pending.Target, pending.Capability!.Action, pending.Value, DisplayRequired, out attempted);
            connection.Complete(pending, reason.Length == 0 ? UiToolRequestStatus.Handled : !session.IsActive || reason == "mount-closed-or-replaced" || reason == "part-or-item-lifetime-closed" ? UiToolRequestStatus.Closed : UiToolRequestStatus.Rejected,
                reason.Length == 0 ? "owner-returned; business-acceptance-and-persistence-not-implied" : reason, attempted);
        }
        catch (Exception error) when (UiObservationBoundary.IsOrdinary(error))
        { connection.Complete(pending, UiToolRequestStatus.Failed, error.GetType().Name + ": " + error.Message, attempted); }
        finally
        {
            toolOperationAttempted = attempted;
            if (!connection.HoldsNode(pending.Target.Node)) session.FindObservationNode(pending.Target.Node)?.ClearToolItems();
        }
    }
    /// <summary>Explicit non-rendering product advance. Uses the real page/registry/bindings; metrics are author-supplied, not native glyph evidence.</summary>
    public void AdvanceHeadless(UiLayoutPoint available)
    {
        RequireToolThread();
        if (!session.IsActive) throw new ObjectDisposedException(nameof(UiHost));
        if (preparingPreview) throw new InvalidOperationException("Author preparation cannot recursively advance its Host.");
        if (session.CurrentExecution != null) throw new InvalidOperationException("A native-owned Host must advance on its actual native callback, not through the headless product entry.");
        BeginFrame();
        try
        {
            if (!PreparePreview(available)) return;
            MeasureAndArrange(available);
            PumpTools();
            if (session.IsActive && toolOperationAttempted && PreparePreview(available)) MeasureAndArrange(available);
        }
        finally { if (session.IsActive) EndFrame(); }
    }
}
