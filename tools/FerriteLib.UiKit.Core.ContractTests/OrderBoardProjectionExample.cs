using System;
using System.Collections.Generic;
using FerriteLib.UiKit.Kernel;

// The application delivers safe results onto Page.Runtime's context and owns result freshness.
internal sealed class OrderBoardProjectionExample : IDisposable
{
    internal readonly InventoryPageExample Page = new(false);
    internal readonly UiReactiveValue<bool> Accepting;
    private Dictionary<string, InventoryItem> current = new(StringComparer.Ordinal);
    internal OrderBoardProjectionExample(OrderBoardService service)
    {
        Accepting = Page.Runtime.Boolean();
        Page.Bindings.BindTracked("accepting", () => Accepting.Value);
        Page.Bindings.BindCommand("complete", () => { service.Increment(); Apply(service.ReadSnapshot()); }, () => Page.Bindings.Get<bool>("accepting"));
        Apply(service.ReadSnapshot());
    }
    internal void Apply(OrderBoardService.Snapshot safe)
    {
        // Verify before touching even projection bookkeeping; Batch is not cross-thread delivery or rollback.
        using (Page.Runtime.Batch())
        {
            var next = new List<InventoryItem>();
            var retained = new Dictionary<string, InventoryItem>(StringComparer.Ordinal);
            foreach (OrderBoardService.Row row in safe.Rows)
            {
                if (!current.TryGetValue(row.Key, out InventoryItem? item) || item.Code != row.Code) item = new InventoryItem(Page.Runtime, row.Key, row.Code, row.Count);
                item.Count = row.Count; Page.RegisterKey(row.Key);
                retained.Add(row.Key, item); next.Add(item);
            }
            Page.Items.Value.ReplaceAll(next);
            Accepting.Value = safe.Accepting;
            current = retained;
        }
    }
    public void Dispose() => Page.Dispose();
}
