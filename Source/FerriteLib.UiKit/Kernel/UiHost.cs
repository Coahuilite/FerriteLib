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
    private readonly UiStyleResolver styleResolver;
    private int lastLayoutRevision;

    // How far the two issue records have been published on the fit audit's appearance channel. Each one
    // only ever moves forward, so an issue is reported exactly once however many frames follow it.
    private int publishedDocumentIssues;
    private int publishedResolutionIssues;

    public UiHost(
        string source,
        UiLayoutManifest manifest,
        IUiBindings bindings,
        UiTheme theme,
        ITextMetrics metrics,
        IUiTranslation translation,
        UiStyleDocument? document = null)
    {
        this.source = source ?? throw new ArgumentNullException(nameof(source));
        this.manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        this.bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
        this.theme = theme ?? throw new ArgumentNullException(nameof(theme));
        this.metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        this.translation = translation ?? throw new ArgumentNullException(nameof(translation));

        UiWidgetRegistry.InitializeCore();
        ValidateManifest();

        // The style document enters here, and the host owns it: a caller hands in a standalone document
        // (the appearance-authoring origin) or, when it hands in none, the manifest's own <Styles>
        // section is the document. Nothing else about the page changes - an element's Scheme/Density
        // attributes are resolved per element, against the theme below.
        UiStyleDocument styleDocument = document ?? manifest.Styles;

        // Two sources at once would mean one of them is ignored, and a library that quietly picks one is
        // exactly the silent fallback this one refuses. The document handed in wins - the caller named it
        // explicitly, and it is the appearance-authoring origin - while the manifest's own section, when it
        // carried anything at all, is reported as displaced through the same appearance channel. Nothing
        // here throws: which of two style sources was read is appearance-class, like every other drop.
        if (document != null && (!manifest.Styles.IsEmpty || manifest.Styles.Issues.Count > 0))
        {
            UiFitAudit.ReportStyleFallback(
                source + "#styles",
                "Styles",
                "Document",
                "the manifest's own <Styles> section (a second style source)",
                "the document handed to the host; the section is ignored");
        }

        // resolve-before-Measure: the page level lands on the injected theme before the first arrange, and
        // because applying a token moves the theme's own layout revision - the clock the band cache already
        // compares - the very first frame follows the document. No second clock is introduced anywhere.
        styleResolver = new UiStyleResolver(theme, styleDocument);
        styleResolver.ApplyTo(theme);

        engine = new UiLayoutEngine(source, styleResolver);
        session = new UiSession();

        // A document that went wrong must not go quiet, and the consumer must not have to remember to ask:
        // every drop the parser or the resolver recorded is published on the fit audit's appearance
        // channel (UiFitAudit.AttachStyleFallback) here and after each arrangement.
        PublishStyleIssues();

        // The runtime half of the dependency-reality rule: building a tree is what makes this mod a
        // live consumer rather than a declared one, and Require reports who has done it.
        UiHostLedger.Record(source);
    }

    public IUiBindings Bindings => bindings;

    public UiSession Session => session;

    public string Source => source;

    public UiLayoutManifest Manifest => manifest;

    /// <summary>
    /// The document resolver this host built: the document it resolved (the one handed to the constructor,
    /// otherwise the manifest's own <c>&lt;Styles&gt;</c> section) plus the drops it recorded while
    /// resolving. Never null - a page with no document still needs one to report that an element named a
    /// scheme nobody declared, which is an appearance fallback like any other.
    /// </summary>
    public UiStyleResolver StyleResolver => styleResolver;

    public UiWidgetContext CreateContext(float viewWidth, string elementPath = "root")
    {
        return new UiWidgetContext(source, session, metrics, theme, translation, bindings, viewWidth, elementPath);
    }

    public UiLayoutSnapshot MeasureAndArrange(Vector2 available)
    {
        // The band cache compares the available size and the content/definition/translation revisions,
        // and it cannot see the theme. A density or font-size change would therefore keep the previous
        // geometry while the draw resolves the new font - exactly the measure/draw disagreement a
        // resolved-value store exists to prevent. The theme reports its own layout-bearing revision, and
        // the host turns a change into the one cache clock the engine already understands rather than
        // inventing a second one.
        if (lastLayoutRevision != theme.LayoutRevision)
        {
            lastLayoutRevision = theme.LayoutRevision;
            session.BumpContentRevision();
        }

        UiWidgetContext ctx = CreateContext(available.x);
        UiLayoutSnapshot snapshot = engine.ArrangeRoots(ctx, available, manifest.Roots);

        // A region scope is resolved the first time the engine meets it, and that resolution can drop a
        // name (a scheme or density nobody declared). Publishing here - on the same frame, right after the
        // arrange that discovered it - is what keeps such a drop from waiting a frame to be visible. On a
        // cached arrangement there is nothing new to publish and the call costs two integer comparisons.
        PublishStyleIssues();
        return snapshot;
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
            // The engine consumes a pending scroll-target request at the end of its own arrange, by node
            // (task-18): resolution belongs where the entries and the nodes are, so the host does not
            // re-derive it from string-keyed snapshot views.
            UiLayoutSnapshot snapshot = MeasureAndArrange(new Vector2(viewport.width, viewport.height));
            Draw(viewport, snapshot);
        }
        finally
        {
            EndFrame();
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

    /// <summary>
    /// Publishes every style drop this page has recorded so far, parser-side and resolver-side, each as one
    /// <see cref="UiStyleFallbackReport"/> on <see cref="UiFitAudit"/>'s appearance channel. The host owns
    /// visibility deliberately: a consumer that never attaches a sink still moves the audit's count and last
    /// diagnostic, so "fail-soft must not mean silent" holds without a consumer remembering anything.
    /// </summary>
    private void PublishStyleIssues()
    {
        IReadOnlyList<UiStyleIssue> parseIssues = styleResolver.Document.Issues;
        for (; publishedDocumentIssues < parseIssues.Count; publishedDocumentIssues++)
        {
            PublishStyleIssue(parseIssues[publishedDocumentIssues]);
        }

        IReadOnlyList<UiStyleIssue> resolutionIssues = styleResolver.Issues;
        for (; publishedResolutionIssues < resolutionIssues.Count; publishedResolutionIssues++)
        {
            PublishStyleIssue(resolutionIssues[publishedResolutionIssues]);
        }
    }

    /// <summary>
    /// One dropped declaration in the audit's own shape: which style origin it came from, which element of
    /// the vocabulary carried it, what the author wrote and what the page fell back to. The authored text is
    /// the drop's own message, because a style drop is already a sentence about itself by the time it gets
    /// here, and the record's whole job is to keep an author from having to guess.
    /// </summary>
    private void PublishStyleIssue(UiStyleIssue issue)
    {
        UiFitAudit.ReportStyleFallback(source + "#styles", "Styles", "Declaration", issue.ToString(), "defaults");
    }

    private void ValidateManifest()
    {
        foreach (UiElementSpec root in manifest.Roots)
        {
            ValidateElement(root, root.Id.Length > 0 ? root.Id : root.Kind, parentNarrowCapable: false);
        }
    }

    // Layout vocabulary belongs to the tree, not to the kind: Width/MinWidth/MaxWidth and the
    // narrow-state visibility variant are read by the engine for every child, so they are allowed
    // on widgets too. Before round 3 this was a live defect: ResolveColumnWidths already read
    // Width on any child, yet no core kind's schema listed it — a manifest could not size a
    // stepper-slider column at all (US->FL round 3, N1's library-side specimen).
    // Scheme and Density are the scope vocabulary: they are allowed on every kind - a widget narrows its
    // own scope, a container is a region its subtree inherits - because the engine reads them for every
    // child of the tree and the style chain, not the kind, is what carries them. Before batch B neither
    // name existed, so an author could not reach the document's schemes at all.
    private static readonly HashSet<string> CommonWidgetAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Id", "Kind", "Hidden", "Tab", "Width", "MinWidth", "MaxWidth", "NarrowHidden", "Scheme", "Density"
    };

    private static readonly HashSet<string> ContainerAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Id", "Kind", "Gap", "Padding", "Height", "Title", "TitleKey", "Hidden", "Width", "Fill",
        "MinWidth", "MaxWidth", "Breakpoint", "Narrow", "Cols", "NarrowCols", "NarrowHidden",
        "Scheme", "Density"
    };

    private void ValidateElement(UiElementSpec spec, string path, bool parentNarrowCapable)
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

        bool selfNarrowCapable = spec.TryGetAttribute("Breakpoint", out _);
        ValidateLayoutAttributes(spec, path, isContainer: !isWidget, selfNarrowCapable, parentNarrowCapable);

        foreach (UiElementSpec child in spec.Children)
        {
            string childPath = path + "/" + (child.Id.Length > 0 ? child.Id : child.Kind);
            ValidateElement(child, childPath, selfNarrowCapable);
        }
    }

    /// <summary>
    /// Creation-time grammar for the responsive vocabulary (US->FL round 3, N1+N2). The engine
    /// degrades malformed values to "not declared" so a programmatically built spec cannot crash a
    /// frame; a MANIFEST that ships them is a contract error, caught here where the element, the
    /// attribute and the path are all still known.
    /// </summary>
    private void ValidateLayoutAttributes(
        UiElementSpec spec, string path, bool isContainer, bool selfNarrowCapable, bool parentNarrowCapable)
    {
        if (spec.TryGetAttribute("Width", out string widthRaw))
        {
            string width = widthRaw.Trim();
            bool auto = string.Equals(width, "Auto", StringComparison.OrdinalIgnoreCase);
            if (!auto && (!float.TryParse(width, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float parsed) || parsed <= 0f))
            {
                throw new UiContractException(
                    $"Element id=\"{spec.Id}\" at '{path}' has invalid Width '{widthRaw}'; expected a positive number or Auto.",
                    source, spec.Id, spec.Kind, path);
            }
        }

        ValidatePositiveNumber(spec, path, "MinWidth");
        ValidatePositiveNumber(spec, path, "MaxWidth");
        if (spec.TryGetAttribute("MinWidth", out string minRaw)
            && spec.TryGetAttribute("MaxWidth", out string maxRaw)
            && float.TryParse(minRaw.Trim(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float minW)
            && float.TryParse(maxRaw.Trim(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float maxW)
            && minW > maxW)
        {
            throw new UiContractException(
                $"Element id=\"{spec.Id}\" at '{path}' declares MinWidth {minRaw} above MaxWidth {maxRaw}.",
                source, spec.Id, spec.Kind, path);
        }

        // A narrow-state attribute under no Breakpoint is the silent no-op this library's creation
        // contract exists to stop: the author believes they declared a variant that can never fire.
        if (spec.TryGetAttribute("NarrowHidden", out _) && !parentNarrowCapable)
        {
            throw new UiContractException(
                $"Element id=\"{spec.Id}\" at '{path}' declares NarrowHidden but its parent carries no Breakpoint; nothing can ever make it narrow.",
                source, spec.Id, spec.Kind, path);
        }

        if (!isContainer)
        {
            foreach (string containerOnly in new[] { "Breakpoint", "Narrow", "Cols", "NarrowCols" })
            {
                if (spec.TryGetAttribute(containerOnly, out _))
                {
                    throw new UiContractException(
                        $"'{containerOnly}' at '{path}' is container vocabulary; the engine never reads it on a widget.",
                        source, spec.Id, spec.Kind, path);
                }
            }

            return;
        }

        ValidatePositiveNumber(spec, path, "Breakpoint");
        ValidatePositiveInteger(spec, path, "Cols");
        ValidatePositiveInteger(spec, path, "NarrowCols");

        if (spec.TryGetAttribute("Narrow", out string narrowRaw) && !selfNarrowCapable)
        {
            throw new UiContractException(
                $"Container id=\"{spec.Id}\" at '{path}' declares Narrow but carries no Breakpoint; the variant could never be selected.",
                source, spec.Id, spec.Kind, path);
        }

        if (spec.TryGetAttribute("Narrow", out narrowRaw))
        {
            string narrow = narrowRaw.Trim();
            bool rowBecomesStacked = string.Equals(spec.Kind, "Row", StringComparison.Ordinal)
                && (string.Equals(narrow, "Column", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(narrow, "Stack", StringComparison.OrdinalIgnoreCase));
            bool stackBecomesRow = (string.Equals(spec.Kind, "Column", StringComparison.Ordinal)
                    || string.Equals(spec.Kind, "Stack", StringComparison.Ordinal)
                    || string.Equals(spec.Kind, "Section", StringComparison.Ordinal)
                    || string.Equals(spec.Kind, "Surface", StringComparison.Ordinal))
                && string.Equals(narrow, "Row", StringComparison.OrdinalIgnoreCase);
            if (!rowBecomesStacked && !stackBecomesRow)
            {
                throw new UiContractException(
                    $"Container id=\"{spec.Id}\" at '{path}' declares Narrow '{narrowRaw}'; only Row may name Column/Stack and only a vertical stack may name Row.",
                    source, spec.Id, spec.Kind, path);
            }
        }

        if (spec.TryGetAttribute("NarrowCols", out _))
        {
            if (!spec.TryGetAttribute("Cols", out _))
            {
                throw new UiContractException(
                    $"Wrap id=\"{spec.Id}\" at '{path}' declares NarrowCols without Cols; the narrow variant needs the wide value to select between.",
                    source, spec.Id, spec.Kind, path);
            }

            if (!selfNarrowCapable)
            {
                throw new UiContractException(
                    $"Wrap id=\"{spec.Id}\" at '{path}' declares NarrowCols but carries no Breakpoint; nothing can ever make it narrow.",
                    source, spec.Id, spec.Kind, path);
            }
        }
    }

    private void ValidatePositiveNumber(UiElementSpec spec, string path, string attribute)
    {
        if (!spec.TryGetAttribute(attribute, out string raw)) return;
        if (!float.TryParse(raw.Trim(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float value) || value <= 0f)
        {
            throw new UiContractException(
                $"Element id=\"{spec.Id}\" at '{path}' has invalid {attribute} '{raw}'; expected a positive number.",
                source, spec.Id, spec.Kind, path);
        }
    }

    private void ValidatePositiveInteger(UiElementSpec spec, string path, string attribute)
    {
        if (!spec.TryGetAttribute(attribute, out string raw)) return;
        if (!int.TryParse(raw.Trim(), System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out int value) || value <= 0)
        {
            throw new UiContractException(
                $"Element id=\"{spec.Id}\" at '{path}' has invalid {attribute} '{raw}'; expected a positive integer.",
                source, spec.Id, spec.Kind, path);
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
