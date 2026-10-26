using System;
using System.Collections;
using System.Collections.Generic;

namespace FerriteLib.UiKit.Kernel;

internal sealed partial class UiLayoutEngine
{
    // Stable membership shares its original ordered table. A candidate replaces only touched pages;
    // published snapshots never observe the candidate's mutable patch dictionary.
    private sealed class EntryPages : IReadOnlyList<PlacedEntry>
    {
        private const int PageShift = 5;
        private readonly IReadOnlyList<PlacedEntry> original;
        private readonly Dictionary<int, PlacedEntry[]>? pages;
        internal EntryPages(IReadOnlyList<PlacedEntry> original) { this.original = original; }
        private EntryPages(IReadOnlyList<PlacedEntry> original, Dictionary<int, PlacedEntry[]> pages)
        { this.original = original; this.pages = pages; }
        public int Count => original.Count;
        public PlacedEntry this[int index] => pages != null && pages.TryGetValue(index >> PageShift, out var page)
            ? page[index & 31] : original[index];
        internal EntryPages Replace(Dictionary<int, PlacedEntry> changes)
        {
            var next = pages == null ? new Dictionary<int, PlacedEntry[]>() : new Dictionary<int, PlacedEntry[]>(pages);
            var owned = new HashSet<int>();
            foreach (var pair in changes)
            {
                int number = pair.Key >> PageShift;
                if (owned.Add(number))
                {
                    int first = number << PageShift;
                    var page = new PlacedEntry[Math.Min(32, Count - first)];
                    for (int i = 0; i < page.Length; i++) page[i] = this[first + i];
                    next[number] = page;
                }
                next[number][pair.Key & 31] = pair.Value;
            }
            return new EntryPages(original, next);
        }
        internal int FindIndex(Predicate<PlacedEntry> match)
        { for (int i = 0; i < Count; i++) if (match(this[i])) return i; return -1; }
        public IEnumerator<PlacedEntry> GetEnumerator() { for (int i = 0; i < Count; i++) yield return this[i]; }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    private sealed class GeometryView : IReadOnlyDictionary<string, UiLayoutRect>
    {
        private readonly IReadOnlyDictionary<string, int> index;
        private readonly EntryPages entries;
        private readonly bool content;
        internal GeometryView(IReadOnlyDictionary<string, int> index, EntryPages entries, bool content = false)
        { this.index = index; this.entries = entries; this.content = content; }
        public int Count => index.Count;
        public IEnumerable<string> Keys => index.Keys;
        public IEnumerable<UiLayoutRect> Values { get { foreach (var pair in this) yield return pair.Value; } }
        public UiLayoutRect this[string key] => Read(index[key]);
        private UiLayoutRect Read(int position) => content ? entries[position].ContentRect!.Value : entries[position].Rect;
        public bool ContainsKey(string key) => index.ContainsKey(key);
        public bool TryGetValue(string key, out UiLayoutRect value)
        { if (index.TryGetValue(key, out int position)) { value = Read(position); return true; } value = default; return false; }
        public IEnumerator<KeyValuePair<string, UiLayoutRect>> GetEnumerator()
        { foreach (var pair in index) yield return new KeyValuePair<string, UiLayoutRect>(pair.Key, Read(pair.Value)); }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    private static PlacedEntry WithRect(PlacedEntry entry, UiLayoutRect rect) => new PlacedEntry
    {
        Spec = entry.Spec, Id = entry.Id, Node = entry.Node, StyleChain = entry.StyleChain,
        Widget = entry.Widget, IsContainer = entry.IsContainer, ContainerKind = entry.ContainerKind,
        MeasureWidth = entry.MeasureWidth, Rect = rect, ContentRect = entry.ContentRect,
        SubtreeCount = entry.SubtreeCount, IsMaterializedRow = entry.IsMaterializedRow, LayoutInputs = entry.LayoutInputs
    };
}
