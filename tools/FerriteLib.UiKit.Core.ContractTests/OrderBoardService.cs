using System;
using System.Collections.Generic;

// Independent business authority: its own lock, immutable result values, no FL dependency.
internal sealed class OrderBoardService
{
    internal readonly struct Row
    {
        internal Row(string key, int code, int count) { Key = key; Code = code; Count = count; }
        internal string Key { get; }
        internal int Code { get; }
        internal int Count { get; }
    }
    internal sealed class Snapshot
    {
        internal Snapshot(IReadOnlyList<Row> rows, bool accepting) { Rows = rows; Accepting = accepting; }
        internal IReadOnlyList<Row> Rows { get; }
        internal bool Accepting { get; }
    }
    private readonly object gate = new();
    private readonly List<Row> rows = new() { new Row("a", 1, 1), new Row("b", 2, 2) };
    private bool accepting = true;
    internal int Requests { get; private set; }
    internal bool Increment()
    {
        lock (gate)
        {
            Requests++;
            if (!accepting || rows.Count == 0) return false;
            Row row = rows[0]; rows[0] = new Row(row.Key, row.Code, row.Count + 1); return true;
        }
    }
    internal void SetAccepting(bool value) { lock (gate) accepting = value; }
    internal void Replace(params Row[] value) { lock (gate) { rows.Clear(); rows.AddRange(value); } }
    internal Snapshot ReadSnapshot() { lock (gate) return new Snapshot(new List<Row>(rows).AsReadOnly(), accepting); }
}
