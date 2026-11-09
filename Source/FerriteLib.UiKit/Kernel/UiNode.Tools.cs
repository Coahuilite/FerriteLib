using System;
using System.Collections.Generic;

namespace FerriteLib.UiKit.Kernel;
public sealed partial class UiNode
{
    // Only selected tool capabilities, not another model/tree. Allocated lazily, bounded per node.
    private Dictionary<string, (UiSemanticAction Action, long Lifetime)>? toolItems;
    internal long ToolItemLifetime(UiSemanticAction action, bool mint)
    {
        string key = action.IdentityKey;
        if (toolItems != null && toolItems.TryGetValue(key, out var held) && action.Same(held.Action)) return held.Lifetime;
        if (!mint) return 0;
        toolItems ??= new(StringComparer.Ordinal);
        if (toolItems.Count >= 128 && !toolItems.ContainsKey(key)) return 0;
        long lifetime = session.AllocateMountLifetime();
        toolItems[key] = (action, lifetime);
        return lifetime;
    }
    internal void ReconcileToolItems(IReadOnlyList<UiSemanticAction> current)
    {
        if (toolItems == null) return;
        var keep = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < Math.Min(128, current.Count); i++)
        { UiSemanticAction action = current[i]; keep.Add(action.IdentityKey); ToolItemLifetime(action, mint: true); }
        foreach (string key in new List<string>(toolItems.Keys)) if (!keep.Contains(key)) toolItems.Remove(key);
    }
    internal void ClearToolItems() => toolItems = null;
}

public sealed partial class UiSession
{
    internal void ClearToolItems() { foreach (UiNode node in nodes.Values) node.ClearToolItems(); }
}
