using System;
using System.Collections.Generic;
using System.Linq;
using FerriteLib.UiKit.Kernel;

// An ordinary object, with independently connected properties. No FL model base class.
internal sealed class InventoryItem
{
    internal string Key { get; }
    internal int Code { get; }
    private readonly UiReactiveValue<int> count;
    internal int Reads;
    internal int Subscribers => count.SubscriberCount;
    internal InventoryItem(UiReactiveRuntime runtime, string key, int code, int initial)
    { Key = key; Code = code; count = runtime.Int(initial); }
    internal int Count { get { Reads++; return count.Value; } set => count.Value = value; }
}

// Compiled by both the no-platform target and real Host adapter harness.
internal sealed class InventoryPageExample : IDisposable
{
    internal readonly UiReactiveRuntime Runtime = new();
    internal readonly UiBindings Bindings;
    internal readonly UiReactiveValue<UiReactiveList<InventoryItem>> Items;
    internal readonly UiReactiveValue<InventoryItem?> Focus;
    internal readonly UiReactiveValue<int> Selected;
    internal readonly UiReactiveOwner BusinessOwner;
    internal readonly UiReactiveComputed<int> Total;
    internal int TotalRuns, ChoiceRuns, KeyRuns, Observations, ObservedTotal, Requests;
    private readonly HashSet<string> registered = new(StringComparer.Ordinal);
    internal InventoryPageExample(bool seed = true)
    {
        Bindings = new UiBindings(Runtime);
        var list = Runtime.List(Array.Empty<InventoryItem>(), (a, b) => ReferenceEquals(a, b));
        Items = Runtime.Value(list, (a, b) => ReferenceEquals(a, b));
        Focus = Runtime.Value<InventoryItem?>(null, (a, b) => ReferenceEquals(a, b));
        Selected = Runtime.Int(1);
        BusinessOwner = Runtime.CreateOwner("inventory-business");
        Total = BusinessOwner.Compute("total", () => { TotalRuns++; return Items.Value.Sum(item => item.Count); });
        BusinessOwner.Respond("audit", () => { ObservedTotal = Total.Value; Observations++; });
        Bindings.BindTracked("total", () => Total.Value);
        Bindings.BindTracked("caption", () => "Total " + Total.Value);
        Bindings.BindTracked("focus", () => Focus.Value?.Count ?? 0);
        Bindings.BindTracked<IReadOnlyList<string>>("items", () => { KeyRuns++; return Items.Value.Select(item => item.Key).ToList().AsReadOnly(); }, equal: SameKeys);
        Bindings.BindTracked("selected", () => Selected.Value, value => { Requests++; Selected.Value = value; });
        Bindings.BindTrackedOptions("selected", () =>
        {
            ChoiceRuns++;
            return Items.Value.Select(item => new UiChoice<int>(item.Key + ": " + item.Count, item.Code)).ToList().AsReadOnly();
        }, (a, b) => a.Text == b.Text && a.Value == b.Value);
        Bindings.BindCommand("increment", () => { Requests++; InventoryItem? first = Items.Value.FirstOrDefault(); if (first != null) first.Count++; }, () => Items.Value.Count != 0);
        if (seed)
        {
            using (Runtime.Batch())
            {
                Add("a", 1, 1); Add("b", 2, 2); Focus.Value = Items.Value[0];
            }
        }
    }
    private static bool SameKeys(IReadOnlyList<string> a, IReadOnlyList<string> b) => a.SequenceEqual(b, StringComparer.Ordinal);
    internal InventoryItem Add(string key, int code, int count)
    {
        var item = new InventoryItem(Runtime, key, code, count);
        RegisterKey(key); Items.Value.Add(item); return item;
    }
    internal InventoryItem? Find(string key) => Items.Value.FirstOrDefault(item => item.Key == key);
    internal void RegisterKey(string key)
    {
        if (!registered.Add(key)) return;
        // Resolve the current target by stable business identity, not a captured mutable item reference.
        Bindings.BindTracked("items." + key + ".caption", () => { InventoryItem? item = Find(key); return item == null ? "" : item.Key + ": " + item.Count; });
        Bindings.BindTracked("items." + key + ".count", () => (float)(Find(key)?.Count ?? 0), value => { InventoryItem? item = Find(key); if (item != null) item.Count = (int)value; }, (a, b) => a == b);
        Bindings.BindCommand("items." + key + ".increment", () => { Requests++; InventoryItem? item = Find(key); if (item != null) item.Count++; });
    }
    public void Dispose() => BusinessOwner.Dispose();
}
