using System;
using System.Collections;
using System.Collections.Generic;

namespace FerriteLib.UiKit.Kernel;

internal sealed partial class UiLayoutEngine
{
    // Stable membership shares its original ordered table. A candidate replaces only touched pages;
    // published snapshots never observe the candidate's mutable patch dictionary.
    private sealed class PageTable<T> : IReadOnlyList<T>
    {
        private const int PageShift = 5;
        private readonly IReadOnlyList<T> original;
        private readonly Dictionary<int, T[]>? pages;
        internal PageTable(IReadOnlyList<T> original) { this.original = original; }
        private PageTable(IReadOnlyList<T> original, Dictionary<int, T[]> pages)
        { this.original = original; this.pages = pages; }
        public int Count => original.Count;
        public T this[int index] => pages != null && pages.TryGetValue(index >> PageShift, out var page)
            ? page[index & 31] : original[index];
        internal PageTable<T> Replace(Dictionary<int, T> changes)
        {
            var next = pages == null ? new Dictionary<int, T[]>() : new Dictionary<int, T[]>(pages);
            var owned = new HashSet<int>();
            foreach (var pair in changes)
            {
                int number = pair.Key >> PageShift;
                if (owned.Add(number))
                {
                    int first = number << PageShift;
                    var page = new T[Math.Min(32, Count - first)];
                    for (int i = 0; i < page.Length; i++) page[i] = this[first + i];
                    next[number] = page;
                }
                next[number][pair.Key & 31] = pair.Value;
            }
            return new PageTable<T>(original, next);
        }
        internal int FindIndex(Predicate<T> match)
        { for (int i = 0; i < Count; i++) if (match(this[i])) return i; return -1; }
        public IEnumerator<T> GetEnumerator() { for (int i = 0; i < Count; i++) yield return this[i]; }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    private sealed class GeometryView : IReadOnlyDictionary<string, UiLayoutRect>
    {
        private readonly IReadOnlyDictionary<string, int> index;
        private readonly PageTable<UiLayoutRect> entries;
        internal GeometryView(IReadOnlyDictionary<string, int> index, PageTable<UiLayoutRect> entries)
        { this.index = index; this.entries = entries; }
        public int Count => index.Count;
        public IEnumerable<string> Keys => index.Keys;
        public IEnumerable<UiLayoutRect> Values { get { foreach (var pair in this) yield return pair.Value; } }
        public UiLayoutRect this[string key] => Read(index[key]);
        private UiLayoutRect Read(int position) => entries[position];
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
        SubtreeCount = entry.SubtreeCount, IsMaterializedRow = entry.IsMaterializedRow, LayoutInputs = entry.LayoutInputs,
        HasContentHeightReference = entry.HasContentHeightReference
    };
}
