namespace FerriteLib.UiKit.Kernel;

// Owner-local work facts, not frame/FPS counters. Reported only through existing requested diagnostics.
internal sealed class UiLayoutWork
{
    internal long Passes, FullCandidates, Reused, SparseCandidates, CapturedNodes, CopiedScopes, CopiedLeaves, GeometryEntries, PublishedEntries;
    internal string Last = "none";
    internal bool RegionSampled;
    internal long BoundaryCaptures, Builds, ReusedRegions, ScheduleReuses, InteractiveCalls, UnknownCalls, PaintCalls, ClippedDisplays;
    internal string RegionSummary => !RegionSampled ? "NotSampled" : "BoundaryCaptures=" + BoundaryCaptures + " Builds=" + Builds
        + " ReusedRegions=" + ReusedRegions + " ScheduleReuses=" + ScheduleReuses + " InteractiveCalls=" + InteractiveCalls
        + " UnknownCalls=" + UnknownCalls + " PaintCalls=" + PaintCalls + " ClippedDisplays=" + ClippedDisplays;
    public override string ToString() => "passes=" + Passes + " full=" + FullCandidates + " reused=" + Reused
        + " sparse=" + SparseCandidates + " captured-nodes=" + CapturedNodes + " copied-scopes=" + CopiedScopes
        + " copied-leaf-records=" + CopiedLeaves + " geometry-visits=" + GeometryEntries + " published-entries=" + PublishedEntries + " last=" + Last
        + " region-work " + RegionSummary;
}
