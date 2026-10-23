using System;
using System.Linq;
using FerriteLib.UiKit.Kernel;

internal static class ItemBindingTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var page = new ItemBindingExample(); var b = page.Bindings;
        using var view = b.CreatePresentation(); view.Synchronize();
        ItemSnapshot old = page.Snapshot[0], replacement = new("a", 50);
        page.Snapshot = new[] { page.Snapshot[1], replacement }; page.Items.Refresh(); view.Synchronize();
        using (view.Enter()) check(b.Get<float>(new UiBindingAddress("items","a","count").Key) == 50 && b.Get<System.Collections.Generic.IReadOnlyList<string>>("items").SequenceEqual(new[] { "b", "a" }), "ITEM same-key new snapshot and business order synchronize without stale target");
        b.Invoke(new UiBindingAddress("items","a","increment").Key); check(replacement.Count == 51 && replacement.Requests == 1 && old.Count == 10, "ITEM command resolves current object, not old reference or index");
        replacement.Count = 55; page.Items.Refresh(); view.Synchronize();
        using (view.Enter()) check(b.Get<float>(new UiBindingAddress("items","a","count").Key) == 55, "ITEM explicit external Refresh reports ordinary field changes on retained reference");
        page.Snapshot = new[] { replacement, new ItemSnapshot("a", 60) }; bool duplicate = false;
        try { page.Items.Refresh(); } catch (InvalidOperationException ex) { duplicate = ex.Message.Contains("items") && ex.Message.Contains("a") && ex.Message.Contains("index 1"); }
        check(duplicate && page.Items.Keys.SequenceEqual(new[] { "b", "a" }), "ITEM duplicate key diagnostic locates source/key/index before changing connections");
        page.Snapshot = new[] { replacement }; page.Items.Refresh(); view.Synchronize();
        check(!b.ContainsBinding(new UiBindingAddress("items","b","count").Key) && view.GetStatus(new UiBindingAddress("items","b","count").Key).HasLastValue == false, "ITEM removal releases binding and presentation caches immediately");
        var row = b.Reactive.Int(2); var connected = new UiBindings(b.Reactive);
        ItemSnapshot tracked = new("tracked", 0); var source = new[] { tracked };
        using (var items = new UiItemBindings<ItemSnapshot>(connected, "tracked", () => source, r => r.Key, f => f.Value("value", _ => row.Value)))
        {
            using var projected = connected.CreatePresentation(); projected.Synchronize(); check(row.SubscriberCount == 1, "ITEM actual item presentation owns its property connection");
            source = Array.Empty<ItemSnapshot>(); items.Refresh(); check(row.SubscriberCount == 0, "ITEM deletion releases actual reactive subscriptions before another draw");
        }
        page.Dispose(); page.Dispose(); check(!b.ContainsBinding("items") && replacement.Count == 55, "ITEM idempotent UI disposal releases own registrations, never business object");
        Console.WriteLine("ITEM-TRACE same-key=a old-count=" + old.Count + " current-count=" + replacement.Count + " requests=" + replacement.Requests + " removed=b subscriptions=0");
    }
}

