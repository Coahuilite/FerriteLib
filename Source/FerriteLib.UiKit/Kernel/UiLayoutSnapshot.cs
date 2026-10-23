using System.Collections;
using System.Collections.Generic;

namespace FerriteLib.UiKit.Kernel;
/// <summary>
/// Stable output of a Measure/Arrange pass. Hosts and tests can locate any element rect, iterate
/// visible ids, and read scroll viewport/content rects without depending on internal engine state.
/// </summary>
public sealed class UiLayoutSnapshot
{
    public UiLayoutPoint ContentSize { get; }

    public IReadOnlyDictionary<string, UiLayoutRect> RectById { get; }

    public IReadOnlyList<string> VisibleIds { get; }

    /// <summary>Structural viewport rects keyed by scroll container id/path.</summary>
    public IReadOnlyDictionary<string, UiLayoutRect> Viewports { get; }

    /// <summary>
    /// Scroll content rects keyed by the same key as <see cref="Viewports"/>. Content rects are in
    /// scroll-local coordinates (origin 0,0) and are the rects passed to
    /// <c>Verse.Widgets.BeginScrollView</c>.
    /// </summary>
    public IReadOnlyDictionary<string, UiLayoutRect> ScrollContents { get; }

    public int DefinitionRevision { get; }
    public IReadOnlyList<UiLayoutDiagnostic> Diagnostics { get; }

    public UiLayoutSnapshot(
        UiLayoutPoint contentSize,
        IReadOnlyDictionary<string, UiLayoutRect> rectById,
        IReadOnlyList<string> visibleIds,
        IReadOnlyDictionary<string, UiLayoutRect> viewports,
        int definitionRevision)
        : this(
            contentSize,
            rectById,
            visibleIds,
            viewports,
            new Dictionary<string, UiLayoutRect>(),
            definitionRevision)
    {
    }

    public UiLayoutSnapshot(UiLayoutPoint contentSize, IReadOnlyDictionary<string, UiLayoutRect> rectById, IReadOnlyList<string> visibleIds, IReadOnlyDictionary<string, UiLayoutRect> viewports, IReadOnlyDictionary<string, UiLayoutRect> scrollContents, int definitionRevision)
        : this(contentSize, rectById, visibleIds, viewports, scrollContents, definitionRevision, System.Array.Empty<UiLayoutDiagnostic>())
    {
    }

    public UiLayoutSnapshot(UiLayoutPoint contentSize, IReadOnlyDictionary<string, UiLayoutRect> rectById, IReadOnlyList<string> visibleIds, IReadOnlyDictionary<string, UiLayoutRect> viewports, IReadOnlyDictionary<string, UiLayoutRect> scrollContents, int definitionRevision, IReadOnlyList<UiLayoutDiagnostic> diagnostics)
    {
        Diagnostics = new ListView<UiLayoutDiagnostic>(diagnostics);
        ContentSize = contentSize;
        RectById = new RectView(rectById);
        VisibleIds = new ListView<string>(visibleIds);
        Viewports = new RectView(viewports);
        ScrollContents = new RectView(scrollContents);
        DefinitionRevision = definitionRevision;
    }

    // Accept every existing IReadOnly* source without copying, changing its comparer or exposing
    // mutable interfaces. The producer still owns updates; the published outlet grants reads only.
    private sealed class RectView : IReadOnlyDictionary<string, UiLayoutRect>
    {
        private readonly IReadOnlyDictionary<string, UiLayoutRect> source;
        internal RectView(IReadOnlyDictionary<string, UiLayoutRect> source) => this.source = source;
        public UiLayoutRect this[string key] => source[key];
        public IEnumerable<string> Keys => source.Keys;
        public IEnumerable<UiLayoutRect> Values => source.Values;
        public int Count => source.Count;
        public bool ContainsKey(string key) => source.ContainsKey(key);
        public bool TryGetValue(string key, out UiLayoutRect value) => source.TryGetValue(key, out value);
        public IEnumerator<KeyValuePair<string, UiLayoutRect>> GetEnumerator() => source.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class ListView<T> : IReadOnlyList<T>
    {
        private readonly IReadOnlyList<T> source;
        internal ListView(IReadOnlyList<T> source) => this.source = source;
        public T this[int index] => source[index];
        public int Count => source.Count;
        public IEnumerator<T> GetEnumerator() => source.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}


