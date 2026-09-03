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

    public UiLayoutSnapshot(
        Vector2 contentSize,
        IReadOnlyDictionary<string, Rect> rectById,
        IReadOnlyList<string> visibleIds,
        IReadOnlyDictionary<string, Rect> viewports,
        IReadOnlyDictionary<string, Rect> scrollContents,
        int definitionRevision)
    {
        ContentSize = contentSize;
        RectById = rectById;
        VisibleIds = visibleIds;
        Viewports = viewports;
        ScrollContents = scrollContents;
        DefinitionRevision = definitionRevision;
    }
}
