using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel;
/// <summary>
/// Stable output of a Measure/Arrange pass. Hosts and tests can locate any element rect, iterate
/// visible ids, and read scroll viewport/content rects without depending on internal engine state.
/// </summary>
public sealed class UiLayoutSnapshot
{
    public Vector2 ContentSize { get; }

    public IReadOnlyDictionary<string, Rect> RectById { get; }

    public IReadOnlyList<string> VisibleIds { get; }

    /// <summary>Structural viewport rects keyed by scroll container id/path.</summary>
    public IReadOnlyDictionary<string, Rect> Viewports { get; }

    /// <summary>
    /// Scroll content rects keyed by the same key as <see cref="Viewports"/>. Content rects are in
    /// scroll-local coordinates (origin 0,0) and are the rects passed to
    /// <c>Verse.Widgets.BeginScrollView</c>.
    /// </summary>
    public IReadOnlyDictionary<string, Rect> ScrollContents { get; }

    public int DefinitionRevision { get; }
    public IReadOnlyList<UiLayoutDiagnostic> Diagnostics { get; }

    public UiLayoutSnapshot(
        Vector2 contentSize,
        IReadOnlyDictionary<string, Rect> rectById,
        IReadOnlyList<string> visibleIds,
        IReadOnlyDictionary<string, Rect> viewports,
        int definitionRevision)
        : this(
            contentSize,
            rectById,
            visibleIds,
            viewports,
            new Dictionary<string, Rect>(),
            definitionRevision)
    {
    }

    public UiLayoutSnapshot(Vector2 contentSize, IReadOnlyDictionary<string, Rect> rectById, IReadOnlyList<string> visibleIds, IReadOnlyDictionary<string, Rect> viewports, IReadOnlyDictionary<string, Rect> scrollContents, int definitionRevision)
        : this(contentSize, rectById, visibleIds, viewports, scrollContents, definitionRevision, System.Array.Empty<UiLayoutDiagnostic>())
    {
    }

    public UiLayoutSnapshot(Vector2 contentSize, IReadOnlyDictionary<string, Rect> rectById, IReadOnlyList<string> visibleIds, IReadOnlyDictionary<string, Rect> viewports, IReadOnlyDictionary<string, Rect> scrollContents, int definitionRevision, IReadOnlyList<UiLayoutDiagnostic> diagnostics)
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
    private sealed class RectView : IReadOnlyDictionary<string, Rect>
    {
        private readonly IReadOnlyDictionary<string, Rect> source;
        internal RectView(IReadOnlyDictionary<string, Rect> source) => this.source = source;
        public Rect this[string key] => source[key];
        public IEnumerable<string> Keys => source.Keys;
        public IEnumerable<Rect> Values => source.Values;
        public int Count => source.Count;
        public bool ContainsKey(string key) => source.ContainsKey(key);
        public bool TryGetValue(string key, out Rect value) => source.TryGetValue(key, out value);
        public IEnumerator<KeyValuePair<string, Rect>> GetEnumerator() => source.GetEnumerator();
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
