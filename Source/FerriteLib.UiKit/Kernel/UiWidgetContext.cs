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

    public string ElementPath { get; }

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
        Vector2 windowOrigin = default)
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
    }

    public UiWidgetContext ForChild(string childId)
    {
        string path = ElementPath.Length == 0 ? childId : ElementPath + "/" + childId;
        return new UiWidgetContext(Source, Session, Metrics, Theme, Translation, Bindings, ViewWidth, path, WindowOrigin);
    }

    /// <summary>
    /// Returns a context with a different view width. The layout engine uses this so widgets nested
    /// in columns measure and draw against their arranged column width instead of the page width.
    /// </summary>
    public UiWidgetContext WithViewWidth(float viewWidth)
    {
        return new UiWidgetContext(Source, Session, Metrics, Theme, Translation, Bindings, viewWidth, ElementPath, WindowOrigin);
    }

    /// <summary>Returns a context whose draw rects are offset into Host window space by <paramref name="windowOrigin"/>.</summary>
    public UiWidgetContext WithWindowOrigin(Vector2 windowOrigin)
    {
        return new UiWidgetContext(Source, Session, Metrics, Theme, Translation, Bindings, ViewWidth, ElementPath, windowOrigin);
    }

    /// <summary>Converts a draw-local rect (as passed to <c>IUiWidget.Draw</c>) into Host window space.</summary>
    public Rect ToWindowRect(Rect rect)
    {
        return new Rect(rect.x + WindowOrigin.x, rect.y + WindowOrigin.y, rect.width, rect.height);
    }
}
