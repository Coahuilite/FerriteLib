namespace FerriteLib.UiKit.Kernel;

// Owner-local work facts, not frame/FPS counters. Reported only through existing requested diagnostics.
internal sealed class UiLayoutWork
{
    internal long Passes, FullCandidates, Reused, SparseCandidates, CapturedNodes, CopiedScopes, CopiedLeaves, GeometryEntries, PublishedEntries;
    internal string Last = "none";
    public override string ToString() => "passes=" + Passes + " full=" + FullCandidates + " reused=" + Reused
        + " sparse=" + SparseCandidates + " captured-nodes=" + CapturedNodes + " copied-scopes=" + CopiedScopes
        + " copied-leaf-records=" + CopiedLeaves + " geometry-visits=" + GeometryEntries + " published-entries=" + PublishedEntries + " last=" + Last;
}
