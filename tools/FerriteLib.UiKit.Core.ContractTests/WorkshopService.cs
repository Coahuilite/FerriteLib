// Independent authoritative service: no FL types, observers or scheduling in its business state.
internal sealed class WorkshopService
{
    internal readonly struct Snapshot
    {
        internal Snapshot(int completed, bool accepting) { Completed = completed; Accepting = accepting; }
        internal int Completed { get; }
        internal bool Accepting { get; }
    }
    private readonly object gate = new();
    private int completed;
    private bool accepting = true;
    internal int Requests { get; private set; }
    internal bool Complete()
    {
        lock (gate) { Requests++; if (!accepting) return false; completed++; return true; }
    }
    internal void SetAccepting(bool value) { lock (gate) accepting = value; }
    internal Snapshot ReadSnapshot() { lock (gate) return new Snapshot(completed, accepting); }
}
