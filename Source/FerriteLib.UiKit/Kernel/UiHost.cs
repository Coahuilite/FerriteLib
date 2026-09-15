using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Verse;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// Greenfield Host. Owns one manifest, one session, typed bindings/theme/metrics/translation and a
/// layout engine. Closing the host disposes the session; reopening creates a new Host/session.
/// </summary>
public sealed class UiHost : IDisposable
{
    private readonly string source;
    private readonly IUiBindings bindings;
    private readonly UiTheme theme;
    private readonly ITextMetrics metrics;
    private readonly IUiTranslation translation;
    private readonly UiSession session;

    // The three fields a document reload swaps. They are not readonly because a host is the thing a
    // reload commits into: the manifest, the resolver built over the effective style document, and the
    // engine that arranges those roots. Everything else - the session, the bindings, the theme identity -
    // is deliberately untouched, which is what keeps state and the business model out of the swap.
    private UiLayoutManifest manifest;
    private UiLayoutEngine engine;
    private UiStyleResolver styleResolver;

    // The standalone style document handed to the constructor (null when the manifest's own <Styles>
    // section is the origin), so a reload can replace appearance without touching the tree and a layout
    // reload can keep the appearance it already had.
    private UiStyleDocument? attachedStyleDocument;

    // The theme's tokens as the consumer handed them in, captured before the first document is applied.
    // Applying a document is a mutation of the injected theme, so committing the next version has to start
    // from the pre-document values: without this, a declaration the new document no longer carries would
    // keep applying forever.
    private readonly UiTheme styleBaseline;

    // The service this host reads documents through, so closing the host releases its dependency instead of
    // leaving a closed session referenced by a live service.
    private UiDocumentService? documentService;

    private int lastLayoutRevision;

    // How far the two issue records have been published on the fit audit's appearance channel. Each one
    // only ever moves forward, so an issue is reported exactly once however many frames follow it.
    private int publishedDocumentIssues;
    private int publishedResolutionIssues;

    // Per-host diagnostics are opt-in: null until the consumer asks for them, which is what keeps the
    // unsubscribed path a null check. Created through UiDiagnosticHub.Subscribe and released by Close.
    private UiDiagnosticSubscription? diagnostics;

    // Phase-timing accumulators. Timing is sampled, never logged per frame: a window of
    // TimingSampleFrames frames closes into one aggregate event. They are plain numbers on the host, so a
    // host whose subscription has timing off pays one boolean test per phase and nothing else.
    private int arrangeTimingSamples;
    private double arrangeTimingTotal;
    private double arrangeTimingMax;
    private int drawTimingSamples;
    private double drawTimingTotal;
    private double drawTimingMax;

    // Test seam: lets FerriteLib.UiKit.Tests capture the dropped-style warning without a real game log,
    // the same shape UiSessionGuard.LogWarningOverride already uses for recovery warnings.
    internal static Action<string>? StyleWarningOverride;

    private bool warnedDroppedStyleDocument;

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
        ValidateManifest(manifest);

        // The style document enters here, and the host owns it: a caller hands in a standalone document
        // (the appearance-authoring origin) or, when it hands in none, the manifest's own <Styles>
        // section is the document. Nothing else about the page changes - an element's Scheme/Density
        // attributes are resolved per element, against the theme below.
        UiStyleDocument styleDocument = document ?? manifest.Styles;
        attachedStyleDocument = document;

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
        styleBaseline = theme.Clone();
        styleResolver = new UiStyleResolver(theme, styleDocument);
        styleResolver.ApplyTo(theme);

        engine = new UiLayoutEngine(source, styleResolver, manifest.Templates);
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
    /// Opts this host in to per-host diagnostics and returns its bounded subscription. It is created on
    /// first access, so a host that never asks pays nothing: without it the fit audit, the recovery guard
    /// and the reload hook all take their pre-existing no-subscriber path. The subscription is released by
    /// <see cref="Close"/> and therefore by <see cref="Dispose"/> as well; the session that owns it
    /// releases it a second time, idempotently.
    /// </summary>
    public UiDiagnosticSubscription Diagnostics =>
        diagnostics ?? UiDiagnosticHub.Subscribe(this, UiDiagnosticHub.DefaultBudget);

    /// <summary>The subscription this host already holds, or null. The hub reads it to stay idempotent.</summary>
    internal UiDiagnosticSubscription? CurrentDiagnostics => diagnostics;

    /// <summary>Binds a subscription the hub created; one host owns at most one.</summary>
    internal void AttachDiagnostics(UiDiagnosticSubscription subscription)
    {
        diagnostics = subscription ?? throw new ArgumentNullException(nameof(subscription));
    }

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
        // The arrange runs inside this host's diagnostic scope: an appearance fallback the engine records
        // while resolving a region is attributed to the host that is arranging, not to whichever host drew
        // last. Null still opens the scope, so an unsubscribed host never inherits another host's routing.
        UiDiagnosticSubscription? subscription = diagnostics;
        bool timing = subscription != null && subscription.TimingEnabled;
        long started = timing ? Stopwatch.GetTimestamp() : 0L;
        UiDiagnosticHub.UiDiagnosticScope scope = UiDiagnosticHub.EnterHost(subscription, metrics);
        try
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
        finally
        {
            scope.Dispose();
            if (timing)
            {
                SampleTiming("arrange", started, ref arrangeTimingSamples, ref arrangeTimingTotal, ref arrangeTimingMax);
            }
        }
    }

    public void Draw(Rect viewport, UiLayoutSnapshot snapshot)
    {
        // The draw runs inside this host's diagnostic scope: a fit-audit finding comes out of a static text
        // outlet with no session argument, and the scope is what says which host's text it was. Popups draw
        // inside the same scope, so their findings are attributed here too.
        UiDiagnosticSubscription? subscription = diagnostics;
        bool timing = subscription != null && subscription.TimingEnabled;
        long started = timing ? Stopwatch.GetTimestamp() : 0L;
        UiDiagnosticHub.UiDiagnosticScope scope = UiDiagnosticHub.EnterHost(subscription, metrics);
        try
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
        finally
        {
            scope.Dispose();
            if (timing)
            {
                SampleTiming("draw", started, ref drawTimingSamples, ref drawTimingTotal, ref drawTimingMax);
            }
        }
    }

    /// <summary>
    /// Folds one measured phase into its sampling window and publishes the aggregate when the window
    /// closes, so a subscription gets a rate rather than a line per frame. A subscription with timing off
    /// returns on the boolean test; an unsubscribed host never reaches here at all.
    /// </summary>
    private void SampleTiming(string name, long startTimestamp, ref int samples, ref double total, ref double max)
    {
        UiDiagnosticSubscription? subscription = diagnostics;
        if (subscription == null || !subscription.TimingEnabled) return;

        double milliseconds = (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency;
        samples++;
        total += milliseconds;
        if (milliseconds > max) max = milliseconds;

        int window = subscription.TimingSampleFrames;
        if (window < 1) window = 1;
        if (samples < window) return;

        subscription.PublishTiming(name, samples, total, max);
        samples = 0;
        total = 0d;
        max = 0d;
    }

    /// <summary>
    /// Starts one frame. A host that reads documents takes the service's pending change signals here,
    /// before the session's own frame starts and therefore before this pass arranges anything: the GUI pass
    /// that just finished was never mutated under itself, and the pass about to run already follows the new
    /// tree. <see cref="UiDocumentService.Pump"/> is idempotent, so a consumer that also pumps explicitly
    /// pays nothing for doing so.
    /// </summary>
    public void BeginFrame()
    {
        documentService?.Pump();
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
        // A closed host must not stay referenced by a live document service, and the service's documents
        // must survive the host: the dependency is released, the last valid version is not.
        UiDocumentService? service = documentService;
        documentService = null;
        service?.Detach(this);

        // The host's own diagnostic subscription goes with the host, before the session the hub keyed it
        // by is disposed: a torn-down window leaves no buffer, no registry entry and no ambient reference.
        diagnostics?.Dispose();
        diagnostics = null;

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
        WarnIfStyleDocumentWasDropped(parseIssues);
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
    /// A whole style document that was refused - a missing or wrong <c>Schema</c>, a foreign root, invalid
    /// XML - leaves the page drawing normally on defaults, so without a warning "I authored a density" and
    /// "my density never applied" look exactly alike on screen. That is the silent behaviour change this
    /// method exists to end: one Warning-level line per host, naming the section and the parser's own reason.
    /// A partly-bad document is not this case (its readable rest still applies) and keeps the audit channel.
    /// </summary>
    private void WarnIfStyleDocumentWasDropped(IReadOnlyList<UiStyleIssue> parseIssues)
    {
        if (warnedDroppedStyleDocument) return;
        if (parseIssues.Count == 0) return;
        if (!styleResolver.Document.IsEmpty) return;

        warnedDroppedStyleDocument = true;
        string warning = "[FerriteLib.UiKit] the <Styles> section was dropped and none of its styles took "
            + "effect (source '" + source + "'): " + parseIssues[0];

        if (StyleWarningOverride != null)
        {
            StyleWarningOverride(warning);
        }
        else
        {
            Log.Warning(warning);
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

    // --- document reload (P4) -------------------------------------------------------------------

    /// <summary>The style document this host would resolve against right now.</summary>
    internal UiStyleDocument EffectiveStyleDocument => attachedStyleDocument ?? manifest.Styles;

    /// <summary>True once this host's session has been disposed by <see cref="Close"/>.</summary>
    internal bool SessionDisposed => !session.IsActive;

    /// <summary>
    /// Attributes one document-service report to this host. The service calls it for every host a reload
    /// batch affected, so a report reaches exactly the windows that read the document and never a window
    /// that reads a different one. A host with no subscription returns on the null check, and nothing here
    /// may throw into a reload path - the caller isolates the call for that reason.
    /// </summary>
    internal void PublishReloadReport(UiReloadReport? report)
    {
        UiDiagnosticSubscription? subscription = diagnostics;
        if (subscription == null || !subscription.IsActive || report == null) return;
        subscription.PublishReload(report);
    }

    /// <summary>
    /// Validates a candidate layout in full - the same creation-time contract the constructor runs,
    /// including every widget's own Configure/Validate against this host's bindings - without changing
    /// anything. The document service calls this for every affected host before it commits any of them,
    /// which is what makes one change batch all-or-nothing.
    /// </summary>
    internal bool TryPrepareLayoutCandidate(UiLayoutManifest candidate, out string element, out string reason)
    {
        element = "";
        reason = "";
        if (candidate == null)
        {
            reason = "the candidate is null";
            return false;
        }

        try
        {
            ValidateManifest(candidate);
        }
        catch (UiContractException ex)
        {
            element = ex.ElementPath.Length > 0 ? ex.ElementPath : ex.ElementId;
            reason = ex.Message;
            return false;
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException)
        {
            reason = ex.Message;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Commits a validated layout candidate: swaps the roots and rebuilds the style resolver and the engine
    /// over the effective document, cleans up the state of elements that are gone or whose kind changed, and
    /// releases a held hot control. The session - and therefore the scroll positions, drafts, selection and
    /// expansion of every identity that survives - is not touched: state preservation here is a property of
    /// not replacing the session, not of copying anything out of it.
    /// </summary>
    internal void CommitLayoutCandidate(UiLayoutManifest candidate)
    {
        UiLayoutManifest previous = manifest;
        manifest = candidate;
        RebuildTree(previous);
    }

    /// <summary>
    /// Commits a style-document candidate. Whether the document is structurally valid was decided by the
    /// service (a whole-document refusal never reaches here); declaration-level drops inside it stay soft and
    /// are published by the rebuilt resolver.
    /// </summary>
    internal void CommitStyleCandidate(UiStyleDocument candidate)
    {
        attachedStyleDocument = candidate ?? throw new ArgumentNullException(nameof(candidate));
        RebuildTree(previous: null);
    }

    /// <summary>
    /// Pre-flights a style-document candidate the same way a layout one is pre-flighted, and it can really
    /// refuse one: the candidate's own page level must resolve inside the candidate, and the document is
    /// then applied over a throwaway clone of this host's theme - the exact path a commit runs - without
    /// mutating the host or the theme it holds.
    /// <para>
    /// The page-level rule is the page-wide half of "structure stays fail-closed, values fall soft": an
    /// authored <c>Scheme</c>/<c>Density</c> naming something the document does not declare can never take
    /// effect, and silently leaving the page on its previous values is the no-op this refuses. A per-element
    /// unknown name still falls back softly (recorded on the resolver's issues); the page-level declaration
    /// is either applied or the candidate version is refused and the last good appearance survives.
    /// </para>
    /// </summary>
    internal bool TryPrepareStyleCandidate(UiStyleDocument candidate, out string element, out string reason)
    {
        element = "";
        reason = "";
        if (candidate == null)
        {
            reason = "the candidate is null";
            return false;
        }

        if (candidate.DefaultScheme != null && !ContainsName(candidate.SchemeNames, candidate.DefaultScheme))
        {
            reason = "the page level names scheme '" + candidate.DefaultScheme
                + "', which the document does not declare; the page level could never take effect";
            return false;
        }

        if (candidate.DefaultDensity != null && !ContainsName(candidate.DensityNames, candidate.DefaultDensity))
        {
            reason = "the page level names density '" + candidate.DefaultDensity
                + "', which the document does not declare; the page level could never take effect";
            return false;
        }

        try
        {
            UiTheme probe = theme.Clone();
            new UiStyleResolver(probe, candidate).ApplyTo(probe);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            reason = ex.Message;
            return false;
        }

        return true;
    }

    private static bool ContainsName(IReadOnlyCollection<string> names, string name)
    {
        foreach (string declared in names)
        {
            if (string.Equals(declared, name, StringComparison.Ordinal)) return true;
        }

        return false;
    }

    /// <summary>
    /// Captures the state a document commit replaces, so the service can put it back if a later host in
    /// the same batch refuses the candidate. The theme is captured by value: the page level is applied to
    /// the injected theme in place, so undoing a commit has to undo that too.
    /// </summary>
    internal DocumentRollback CaptureDocumentRollback()
    {
        return new DocumentRollback(
            manifest,
            attachedStyleDocument,
            styleResolver,
            engine,
            theme.Clone(),
            publishedDocumentIssues,
            publishedResolutionIssues,
            warnedDroppedStyleDocument);
    }

    /// <summary>Puts a host back on the tree, document and theme state a commit was about to replace.</summary>
    internal void RestoreDocumentRollback(DocumentRollback rollback)
    {
        manifest = rollback.Manifest;
        attachedStyleDocument = rollback.AttachedStyle;
        styleResolver = rollback.StyleResolver;
        engine = rollback.Engine;
        publishedDocumentIssues = rollback.PublishedDocumentIssues;
        publishedResolutionIssues = rollback.PublishedResolutionIssues;
        warnedDroppedStyleDocument = rollback.WarnedDroppedStyleDocument;
        CopyThemeTokens(theme, rollback.Theme);
    }

    /// <summary>Records the service this host's documents come from; called by the service's Attach.</summary>
    internal void AttachDocumentService(UiDocumentService service)
    {
        documentService = service ?? throw new ArgumentNullException(nameof(service));
    }

    private void RebuildTree(UiLayoutManifest? previous)
    {
        // Start from the pre-document tokens and then apply the new page level: a colour the new document no
        // longer declares has to actually stop applying, or "remove the override and reload" silently keeps
        // the old value and the reload is only half true.
        RestoreStyleBaseline();
        styleResolver = new UiStyleResolver(theme, EffectiveStyleDocument);
        styleResolver.ApplyTo(theme);

        // A fresh engine has no cached arrangement, so the next ArrangeRoots measures the new tree. This is
        // the invalidation the reload needs and it is strictly local: no second revision counter is
        // introduced anywhere - the engine instance itself is the invalidated thing.
        engine = new UiLayoutEngine(source, styleResolver, manifest.Templates);

        publishedDocumentIssues = 0;
        publishedResolutionIssues = 0;
        warnedDroppedStyleDocument = false;

        if (previous != null)
        {
            PruneDetachedState(previous, manifest);
        }

        ReleaseHotControlForReload();
        PublishStyleIssues();
    }

    /// <summary>
    /// Undoes the page-level application of the document currently in force, so the incoming document is
    /// resolved against the consumer's own theme rather than on top of its predecessor's values.
    /// <para>
    /// The gate is the whole point: a document that declares neither a page scheme nor a page density
    /// applied nothing, and restoring in that case would only discard a tint the consumer applied to the
    /// theme it handed in. A reload is not an excuse to reset state a document never owned.
    /// </para>
    /// <para>
    /// <b>The residual, stated exactly.</b> The restore is not tracked per token: once the outgoing document
    /// applied <i>any</i> page-level scheme or density, the next reload restores <b>every</b>
    /// document-settable token to the value the theme had at host construction. So a consumer that re-tints
    /// any token in the bag - including a token the outgoing page level never declared - loses that tint on
    /// that reload, even when the incoming document declares nothing. A per-token restore would need the
    /// resolver to record which tokens it touched, and the name-to-property mapping exists once, privately,
    /// in <see cref="UiStyleResolver"/>; it is not duplicated here. Consumer-visible consequence: re-apply a
    /// re-tint after a reload, or declare the value in the document instead of tinting the theme.
    /// </para>
    /// </summary>
    private void RestoreStyleBaseline()
    {
        UiStyleDocument applied = styleResolver.Document;
        if (applied.DefaultScheme == null && applied.DefaultDensity == null) return;
        CopyThemeTokens(theme, styleBaseline);
    }

    /// <summary>Copies every document-settable token from <paramref name="source"/> onto <paramref name="target"/>.</summary>
    private static void CopyThemeTokens(UiTheme target, UiTheme source)
    {
        target.Base = source.Base;
        target.Panel = source.Panel;
        target.Raised = source.Raised;
        target.Hover = source.Hover;
        target.Selected = source.Selected;
        target.Success = source.Success;
        target.Danger = source.Danger;
        target.WorkspacePlane = source.WorkspacePlane;
        target.SectionBand = source.SectionBand;
        target.TextPrimary = source.TextPrimary;
        target.TextSecondary = source.TextSecondary;
        target.TextOnGold = source.TextOnGold;
        target.TextOnDanger = source.TextOnDanger;
        target.TextDisabled = source.TextDisabled;
        target.AccentGold = source.AccentGold;
        target.HoverPoint = source.HoverPoint;
        target.Border = source.Border;
        target.BorderStrong = source.BorderStrong;
        target.Divider = source.Divider;
        target.BaseBorder = source.BaseBorder;
        target.PanelBorder = source.PanelBorder;
        target.RaisedBorder = source.RaisedBorder;
        target.HoverBorder = source.HoverBorder;
        target.SelectedBorder = source.SelectedBorder;
        target.SuccessBorder = source.SuccessBorder;
        target.DangerBorder = source.DangerBorder;
        target.DefaultFont = source.DefaultFont;
        target.Geometry = source.Geometry;
    }

    /// <summary>
    /// Everything a document commit changes, captured before the commit so the service can roll a batch
    /// back when a later host in the same batch refuses. Deliberately internal and nested: it is the
    /// service's transaction token, not a consumer surface.
    /// </summary>
    internal readonly struct DocumentRollback
    {
        internal DocumentRollback(
            UiLayoutManifest manifest,
            UiStyleDocument? attachedStyle,
            UiStyleResolver styleResolver,
            UiLayoutEngine engine,
            UiTheme theme,
            int publishedDocumentIssues,
            int publishedResolutionIssues,
            bool warnedDroppedStyleDocument)
        {
            Manifest = manifest;
            AttachedStyle = attachedStyle;
            StyleResolver = styleResolver;
            Engine = engine;
            Theme = theme;
            PublishedDocumentIssues = publishedDocumentIssues;
            PublishedResolutionIssues = publishedResolutionIssues;
            WarnedDroppedStyleDocument = warnedDroppedStyleDocument;
        }

        internal UiLayoutManifest Manifest { get; }

        internal UiStyleDocument? AttachedStyle { get; }

        internal UiStyleResolver StyleResolver { get; }

        internal UiLayoutEngine Engine { get; }

        internal UiTheme Theme { get; }

        internal int PublishedDocumentIssues { get; }

        internal int PublishedResolutionIssues { get; }

        internal bool WarnedDroppedStyleDocument { get; }
    }

    /// <summary>
    /// A reload does not leave a global capture behind: the IMGUI hot control this session holds is
    /// released, and whatever a drag or text edit had already typed stays where it was, in the node state
    /// the surviving identity owns. Releasing is the "cancel the UI capture while keeping a compatible
    /// draft" half of the contract; the draft is untouched here on purpose.
    /// </summary>
    private void ReleaseHotControlForReload()
    {
        int? held = session.OwnedHotControl;
        if (held.HasValue)
        {
            session.ReleaseHotControl(held.Value);
        }
    }

    /// <summary>
    /// Cleans up the state of elements the new tree no longer keeps: an element whose identity is gone, or
    /// whose kind changed under the same identity, must not hand its draft, selection or drag state to a
    /// different control. A widget's own sub-nodes - minted under the element and absent from the element
    /// map - go with their element. A node that survives both identity and kind keeps everything.
    /// </summary>
    private void PruneDetachedState(UiLayoutManifest previous, UiLayoutManifest next)
    {
        Dictionary<UiNodeId, string> before = ElementKinds(previous);
        if (before.Count == 0) return;
        Dictionary<UiNodeId, string> after = ElementKinds(next);

        foreach (KeyValuePair<UiNodeId, string> entry in before)
        {
            bool survives = after.TryGetValue(entry.Key, out string? kind)
                && string.Equals(kind, entry.Value, StringComparison.Ordinal);
            if (survives) continue;

            UiNode? node = session.GetNode(entry.Key);
            if (node != null)
            {
                ResetDetachedState(node, after);
            }
        }
    }

    private void ResetDetachedState(UiNode node, Dictionary<UiNodeId, string> survivors)
    {
        ResetState(node.State);
        foreach (KeyValuePair<string, UiValueState> slot in node.ValueStates)
        {
            ResetState(slot.Value);
        }

        // A scroll position is state too, and it is keyed by the node for exactly this reason: a removed
        // scroll container must not leave a position behind for whatever identity claims that slot next.
        if (session.ScrollPositions.ContainsKey(node))
        {
            session.SetScrollPosition(node, new Vector2(0f, 0f));
        }

        foreach (UiNode child in node.Children)
        {
            // An element child the new tree still carries keeps its own state; everything else under a
            // removed or kind-changed element - sub-nodes included - is part of what has to be cleaned up.
            if (survivors.ContainsKey(child.Id)) continue;
            ResetDetachedState(child, survivors);
        }
    }

    private static void ResetState(UiValueState state)
    {
        state.FloatValue = 0f;
        state.EditText = "";
        state.Dragging = false;
        state.Focused = false;
        state.Cursor = 0;
    }

    private static Dictionary<UiNodeId, string> ElementKinds(UiLayoutManifest value)
    {
        var kinds = new Dictionary<UiNodeId, string>();
        for (int i = 0; i < value.Roots.Count; i++)
        {
            UiElementSpec root = value.Roots[i];
            CollectElement(UiNodeId.Root(root, i), root, kinds);
        }

        return kinds;
    }

    private static void CollectElement(UiNodeId id, UiElementSpec spec, Dictionary<UiNodeId, string> kinds)
    {
        kinds[id] = spec.Kind;
        for (int i = 0; i < spec.Children.Count; i++)
        {
            UiElementSpec child = spec.Children[i];
            CollectElement(id.Child(child, i), child, kinds);
        }
    }

    private void ValidateManifest(UiLayoutManifest candidate)
    {
        foreach (UiElementSpec root in candidate.Roots)
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
        "Id", "Kind", "Hidden", "Tab", "Width", "MinWidth", "MaxWidth", "NarrowHidden", "Scheme", "Density",
        "Visible", "VisibleKey"
    };

    private static readonly HashSet<string> ContainerAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Id", "Kind", "Gap", "Padding", "Height", "Title", "TitleKey", "Hidden", "Width", "Fill",
        "MinWidth", "MaxWidth", "Breakpoint", "Narrow", "Cols", "NarrowCols", "NarrowHidden",
        "Scheme", "Density", "Visible", "VisibleKey"
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

        // Visible is engine-wide and static, so its value is part of the creation-time contract like
        // Width: a malformed value would otherwise be interpreted per frame by the engine, and the one
        // place that still knows the element, the attribute and the path is here. VisibleKey is
        // deliberately NOT resolved against the injected bindings: a page must not fail to exist because
        // a model key is missing or not yet bound. The engine keeps such an element visible and records
        // one deduplicated appearance fallback instead - fail-soft, but not silent.
        if (spec.TryGetAttribute("Visible", out string visibleRaw))
        {
            string visible = visibleRaw.Trim();
            if (!string.Equals(visible, "true", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(visible, "false", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(visible, "1", StringComparison.Ordinal)
                && !string.Equals(visible, "0", StringComparison.Ordinal))
            {
                throw new UiContractException(
                    $"Element id=\"{spec.Id}\" at '{path}' has invalid Visible '{visibleRaw}'; expected true or false.",
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
