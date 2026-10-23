using System;
using System.Collections.Generic;
using FerriteLib.UiKit.Kernel;

// Compiled unchanged by both targets. Business models require no FL base class or registration.
internal sealed class ItemSnapshot
{
    internal readonly string Key;
    internal float Count;
    internal int Requests;
    internal ItemSnapshot(string key, float count) { Key = key; Count = count; }
}
internal sealed class ItemBindingExample : IDisposable
{
    internal IReadOnlyList<ItemSnapshot> Snapshot;
    internal readonly UiBindings Bindings = new(new UiReactiveRuntime());
    internal readonly UiItemBindings<ItemSnapshot> Items;
    internal ItemBindingExample()
    {
        Snapshot = new[] { new ItemSnapshot("a", 10), new ItemSnapshot("b", 30) };
        Items = new UiItemBindings<ItemSnapshot>(Bindings, "items", () => Snapshot, row => row.Key, fields =>
        {
            fields.Value("caption", row => row.Key + ": " + row.Count);
            fields.Value("count", row => row.Count, (row, value) => { row.Count = value; row.Requests++; Items!.Refresh(); }, (a, b) => a == b);
            fields.Command("increment", row => { row.Count++; row.Requests++; Items!.Refresh(); });
        });
    }
    public void Dispose() => Items.Dispose();
}

