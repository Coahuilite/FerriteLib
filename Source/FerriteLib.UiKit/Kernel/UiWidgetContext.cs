using System;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel;

/// <summary>Host-provided translation seam. RimWorld keys are resolved by the host adapter.</summary>
public interface IUiTranslation
{
    string Translate(string key);

    /// <summary>
    /// A value that differs whenever the text resolved for a key may differ - in practice, the active
    /// game language. Measured text bands are derived from resolved strings, so the layout engine
    /// compares this by equality as part of the snapshot cache key: switching language while the size,
    /// the manifest and the content revision all stay put must force a re-measure instead of reusing
    /// the previous language's geometry. Hosts must not return a value that changes on every call.
    /// </summary>
    int TranslationRevision { get; }
}

/// <summary>
/// Per-frame context passed to widgets. It carries every cross-cutting dependency a widget needs
/// without exposing the Host itself: session, metrics, theme, translation, bindings and diagnostics.
/// </summary>
public sealed class UiWidgetContext
{
    public string Source { get; }

    public UiSession Session { get; }

    public ITextMetrics Metrics { get; }

    public UiTheme Theme { get; }

    public IUiTranslation Translation { get; }

    public IUiBindings Bindings { get; }

    public float ViewWidth { get; }

    /// <summary>
    /// The element this context belongs to. The layout engine sets it per arranged entry, so a widget
    /// measures and draws with its own identity (the same value in both halves of a pass), and the
    /// session resolves per-element state against it. <see cref="UiNodeId.None"/> for a context a
    /// caller built by hand, which is the historical "no element" behaviour.
    /// </summary>
    public UiNodeId ElementId { get; }

    /// <summary>
    /// This element's arranged path. The engine sets it per entry (0.4.0 identity layer) instead of
    /// handing every element the host's page-root path; for a hand-built context it is whatever the
    /// caller named. It is the display path: diagnostics read this, never <see cref="UiNodeId.Key"/>.
    /// </summary>
    public string ElementPath { get; }

    /// <summary>
    /// The element's node: the object that owns its state and its dirty flag (0.4.0 node step 1). The
    /// engine sets it per arranged entry, so a widget can read its own state through
    /// <see cref="UiNode.State"/> and ask for a re-measure with <see cref="UiNode.MarkDirty"/> instead of
    /// naming a string slot. Null for a context a caller built by hand.
    /// </summary>
    public UiNode? Node { get; }

    /// <summary>
    /// Offset from the widget's draw rect to the Host's final usable window space. The layout
    /// engine sets this while drawing inside scrolled/grouped containers so popups (drawn after
    /// the content pass, outside any IMGUI group or scroll matrix) share one coordinate space
    /// with their trigger anchor.
    /// </summary>
    public Vector2 WindowOrigin { get; }

    public UiWidgetContext(
        string source,
        UiSession session,
        ITextMetrics metrics,
        UiTheme theme,
        IUiTranslation translation,
        IUiBindings bindings,
        float viewWidth,
        string elementPath,
        Vector2 windowOrigin = default,
        UiNodeId elementId = default,
        UiNode? node = null)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Session = session ?? throw new ArgumentNullException(nameof(session));
        Metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        Theme = theme ?? throw new ArgumentNullException(nameof(theme));
        Translation = translation ?? throw new ArgumentNullException(nameof(translation));
        Bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
        ViewWidth = viewWidth;
        ElementPath = elementPath ?? "";
        WindowOrigin = windowOrigin;
        ElementId = node?.Id ?? elementId;
        Node = node;
    }

    public UiWidgetContext ForChild(string childId)
    {
        string path = ElementPath.Length == 0 ? childId : ElementPath + "/" + childId;
        return new UiWidgetContext(Source, Session, Metrics, Theme, Translation, Bindings, ViewWidth, path, WindowOrigin, ElementId, Node);
    }

    /// <summary>
    /// Returns a context bound to <paramref name="node"/>: its identity, its display path and the node
    /// itself, so a widget can read its own state and mark itself dirty without a string key. Replaces
    /// the earlier <c>WithElement(UiNodeId)</c> in the same 0.4.0 window, because the node - not a value
    /// handed around by hand - is now what carries identity.
    /// </summary>
    public UiWidgetContext WithNode(UiNode node)
    {
        if (node == null) throw new ArgumentNullException(nameof(node));
        return new UiWidgetContext(
            Source, Session, Metrics, Theme, Translation, Bindings, ViewWidth,
            node.Path, WindowOrigin, node.Id, node);
    }

    /// <summary>
    /// Returns a context with a different view width. The layout engine uses this so widgets nested
    /// in columns measure and draw against their arranged column width instead of the page width.
    /// </summary>
    public UiWidgetContext WithViewWidth(float viewWidth)
    {
        return new UiWidgetContext(Source, Session, Metrics, Theme, Translation, Bindings, viewWidth, ElementPath, WindowOrigin, ElementId, Node);
    }

    /// <summary>Returns a context whose draw rects are offset into Host window space by <paramref name="windowOrigin"/>.</summary>
    public UiWidgetContext WithWindowOrigin(Vector2 windowOrigin)
    {
        return new UiWidgetContext(Source, Session, Metrics, Theme, Translation, Bindings, ViewWidth, ElementPath, windowOrigin, ElementId, Node);
    }

    /// <summary>Converts a draw-local rect (as passed to <c>IUiWidget.Draw</c>) into Host window space.</summary>
    public Rect ToWindowRect(Rect rect)
    {
        return new Rect(rect.x + WindowOrigin.x, rect.y + WindowOrigin.y, rect.width, rect.height);
    }
}
