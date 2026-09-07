using System;
using System.Collections.Generic;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// Greenfield Host. Owns one manifest, one session, typed bindings/theme/metrics/translation and a
/// layout engine. Closing the host disposes the session; reopening creates a new Host/session.
/// </summary>
public sealed class UiHost : IDisposable
{
    private readonly string source;
    private readonly UiLayoutManifest manifest;
    private readonly IUiBindings bindings;
    private readonly UiTheme theme;
    private readonly ITextMetrics metrics;
    private readonly IUiTranslation translation;
    private readonly UiLayoutEngine engine;
    private readonly UiSession session;

    public UiHost(
        string source,
        UiLayoutManifest manifest,
        IUiBindings bindings,
        UiTheme theme,
        ITextMetrics metrics,
        IUiTranslation translation)
    {
        this.source = source ?? throw new ArgumentNullException(nameof(source));
        this.manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        this.bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
        this.theme = theme ?? throw new ArgumentNullException(nameof(theme));
        this.metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        this.translation = translation ?? throw new ArgumentNullException(nameof(translation));

        UiWidgetRegistry.InitializeCore();
        ValidateManifest();

        engine = new UiLayoutEngine(source);
        session = new UiSession();

        // The runtime half of the dependency-reality rule: building a tree is what makes this mod a
        // live consumer rather than a declared one, and Require reports who has done it.
        UiHostLedger.Record(source);
    }

    public IUiBindings Bindings => bindings;

    public UiSession Session => session;

    public string Source => source;

    public UiLayoutManifest Manifest => manifest;

    public UiWidgetContext CreateContext(float viewWidth, string elementPath = "root")
    {
        return new UiWidgetContext(source, session, metrics, theme, translation, bindings, viewWidth, elementPath);
    }

    public UiLayoutSnapshot MeasureAndArrange(Vector2 available)
    {
        UiWidgetContext ctx = CreateContext(available.x);
        return engine.ArrangeRoots(ctx, available, manifest.Roots);
    }

    public void Draw(Rect viewport, UiLayoutSnapshot snapshot)
    {
        // Popups are drawn after content and must clamp themselves into the frame's usable window
        // space; publish it here, the one place that knows both the viewport and the session.
        session.SetHostViewport(viewport);
        UiWidgetContext ctx = CreateContext(viewport.width);
        engine.Draw(ctx, snapshot, viewport);

        // Session-owned popups draw after normal content in the same OnGUI pass.
        foreach (Action popupDraw in session.PopupDrawActions)
        {
            popupDraw();
        }
    }

    public void BeginFrame()
    {
        session.BeginFrame();
    }

    public void EndFrame()
    {
        session.EndFrame();
    }

    /// <summary>
    /// Runs one complete synchronous IMGUI frame for this host. The host owns frame boundaries so a
    /// consumer cannot accidentally leave a session mid-frame when drawing throws.
    /// </summary>
    public void DrawFrame(Rect viewport)
    {
        BeginFrame();
        try
        {
            UiLayoutSnapshot snapshot = MeasureAndArrange(new Vector2(viewport.width, viewport.height));
            ApplyScrollTarget(snapshot);
            Draw(viewport, snapshot);
        }
        finally
        {
            EndFrame();
        }
    }

    /// <summary>
    /// Resolves a session scroll-target request (set by a widget after a scroll-to action) against
    /// the arranged snapshot. Applies to the scroll container that contains the target element and
    /// keeps the request pending until it can be resolved (e.g. the target section becomes visible
    /// after a tab switch on the following frame).
    /// </summary>
    private void ApplyScrollTarget(UiLayoutSnapshot snapshot)
    {
        string? targetId = session.ScrollTargetElementId;
        if (targetId == null || targetId.Length == 0) return;
        if (!snapshot.RectById.TryGetValue(targetId, out Rect targetRect)) return;

        foreach (KeyValuePair<string, Rect> pair in snapshot.Viewports)
        {
            string scrollKey = pair.Key;
            if (!snapshot.ScrollContents.TryGetValue(scrollKey, out Rect content)) continue;

            Rect viewport = pair.Value;
            float contentLocalY = targetRect.y - viewport.y;
            if (contentLocalY < -1f || contentLocalY > content.height + 1f) continue;

            float maxY = Math.Max(0f, content.height - viewport.height);
            float clampedY = Mathf.Clamp(contentLocalY, 0f, maxY);
            session.SetScrollPosition(scrollKey, new Vector2(session.GetScrollPosition(scrollKey).x, clampedY));
            session.ClearScrollTarget();
            return;
        }
    }

    public void Close()
    {
        session.Dispose();
    }

    public void Dispose()
    {
        Close();
    }

    private void ValidateManifest()
    {
        foreach (UiElementSpec root in manifest.Roots)
        {
            ValidateElement(root, root.Id.Length > 0 ? root.Id : root.Kind);
        }
    }

    private static readonly HashSet<string> CommonWidgetAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Id", "Kind", "Hidden", "Tab"
    };

    private static readonly HashSet<string> ContainerAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Id", "Kind", "Gap", "Padding", "Height", "Title", "TitleKey", "Hidden", "Width", "Fill"
    };

    private void ValidateElement(UiElementSpec spec, string path)
    {
        bool isWidget = string.Equals(spec.Kind, "Widget", StringComparison.Ordinal)
            || !IsContainerKind(spec.Kind);

        if (isWidget)
        {
            ValidateAttributes(spec, path, isContainer: false);
            IUiWidget widget;
            try
            {
                widget = UiWidgetRegistry.Resolve(source, spec.Kind);
            }
            catch (UiUnknownWidgetKindException ex)
            {
                throw new UiContractException(
                    ex.Message,
                    source,
                    spec.Id,
                    spec.Kind,
                    path,
                    ex);
            }

            try
            {
                widget.Configure(spec);
                widget.Validate(bindings, path);
            }
            catch (Exception ex) when (ex is InvalidOperationException or FormatException or UiContractException)
            {
                throw new UiContractException(
                    $"Widget validation failed at '{path}': {ex.Message}",
                    source,
                    spec.Id,
                    spec.Kind,
                    path,
                    ex);
            }
        }
        else
        {
            ValidateAttributes(spec, path, isContainer: true);
        }

        foreach (UiElementSpec child in spec.Children)
        {
            string childPath = path + "/" + (child.Id.Length > 0 ? child.Id : child.Kind);
            ValidateElement(child, childPath);
        }
    }

    /// <summary>
    /// Creation-time attribute contract check. Containers accept the fixed container vocabulary;
    /// widgets accept the common attributes (Id/Kind/Hidden/Tab) plus the kind's registered schema.
    /// Unknown attributes are rejected before any drawing happens.
    /// </summary>
    private void ValidateAttributes(UiElementSpec spec, string path, bool isContainer)
    {
        IReadOnlyCollection<string>? allowed = isContainer
            ? ContainerAttributes
            : UiWidgetRegistry.GetAttributeSchema(source, spec.Kind);

        if (allowed == null) return;

        foreach (KeyValuePair<string, string> pair in spec.Attributes)
        {
            bool allowedAttribute = false;
            foreach (string candidate in allowed)
            {
                if (string.Equals(candidate, pair.Key, StringComparison.OrdinalIgnoreCase))
                {
                    allowedAttribute = true;
                    break;
                }
            }

            if (allowedAttribute) continue;
            if (!isContainer && CommonWidgetAttributes.Contains(pair.Key)) continue;

            throw new UiContractException(
                $"Unknown attribute '{pair.Key}' on {spec.Kind} at '{path}' is not part of the creation-time contract.",
                source,
                spec.Id,
                spec.Kind,
                path);
        }
    }

    private static bool IsContainerKind(string kind)
    {
        return string.Equals(kind, "Stack", StringComparison.Ordinal)
            || string.Equals(kind, "Row", StringComparison.Ordinal)
            || string.Equals(kind, "Column", StringComparison.Ordinal)
            || string.Equals(kind, "Wrap", StringComparison.Ordinal)
            || string.Equals(kind, "Overlay", StringComparison.Ordinal)
            || string.Equals(kind, "Section", StringComparison.Ordinal)
            || string.Equals(kind, "Surface", StringComparison.Ordinal)
            || string.Equals(kind, "Scroll", StringComparison.Ordinal)
            || string.Equals(kind, "Clip", StringComparison.Ordinal);
    }
}
