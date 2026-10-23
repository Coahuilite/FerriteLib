using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// Greenfield Host. Owns one manifest, one session, typed bindings/theme/metrics/translation and a
/// layout engine. Closing the host disposes the session; reopening creates a new Host/session.
/// </summary>
public sealed partial class UiHost : IDisposable
{
    private readonly string source;
    private readonly IUiBindingReader bindings;
    private UiBindingPresentation? reactivePresentation;
    private UiBindings bindingOwner;
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

    // A layout commit's destructive half, held until the whole change batch has staged successfully. The
    // tree, resolver, engine and theme are swapped first; the removed/kind-changed element state and the
    // held interaction capture are only destroyed by SealDocumentCommit, which the service calls after every
    // host in the batch committed. A rolled-back batch therefore loses nothing the user had not committed.
    private UiLayoutManifest? pendingPrune;
    private bool pendingDocumentCommit;



    // The service this host reads documents through, so closing the host releases its dependency instead of
    // leaving a closed session referenced by a live service.
    private UiDocumentService? documentService;


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
    internal static Action<string>? StyleWarningOverride = null;

    private bool warnedDroppedStyleDocument;
    private int lastLayoutRevision = -1;
    private int presentationRevision;
    private UiLayoutSnapshot? lastSuccessfulSnapshot;

    public UiHost(
        string source,
        UiLayoutManifest manifest,
        IUiBindingReader bindings,
        UiTheme theme,
        ITextMetrics metrics,
        IUiTranslation translation,
        UiStyleDocument? document = null)
    {
        this.source = source ?? throw new ArgumentNullException(nameof(source));
        this.manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        this.bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
        bindingOwner = UiBindings.OwnerOf(bindings);
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
        styleResolver = new UiStyleResolver(theme, styleDocument);
        styleResolver.ApplyPageTo(theme);

        engine = new UiLayoutEngine(source, styleResolver, manifest.Templates);
        session = new UiSession();
        reactivePresentation = bindingOwner.CreateHostPresentation(source + "/" + session.Identity);

        // A document that went wrong must not go quiet, and the consumer must not have to remember to ask:
        // every drop the parser or the resolver recorded is published on the fit audit's appearance
        // channel (UiFitAudit.AttachStyleFallback) here and after each arrangement.
        PublishStyleIssues();

        // The runtime half of the dependency-reality rule: building a tree is what makes this mod a
        // live consumer rather than a declared one, and Require reports who has done it.
        UiHostLedger.Record(source);
        TrackObservationHost(this);
    }

    public IUiBindingReader Bindings => bindings;

    public UiSession Session => session;
    public UiHostInputState InputState => new(session.IsActive, session.ActiveEditNode != null, session.OwnedHotControl.HasValue || session.Interactions.CaptureOwner.HasValue);
    public UiElementProperties ElementProperties(string elementId) => Element(elementId).Properties;
    public UiLayoutPoint GetScrollPosition(string elementId) => session.GetScrollPosition(Element(elementId));
    public void SetScrollPosition(string elementId, UiLayoutPoint position) => session.SetScrollPosition(Element(elementId), position);
    public void Invalidate() => session.BumpContentRevision();
    public void SetInteractionScope(string elementId, string scope)
    {
        UiNode node = Element(elementId);
        if (node.InteractionScope == scope) return;
        session.SuspendFailedInput(node);
        node.InteractionScope = scope ?? throw new ArgumentNullException(nameof(scope));
    }
    private UiNode Element(string id) => session.GetNodeByElementId(id) ?? throw new ArgumentException("Element '" + id + "' is not in this Host.", nameof(id));
    /// <summary>Managed fault facts and pure Retry entry, owned by this view, not by the business service.</summary>
    public UiBindingPresentation? Presentation => reactivePresentation;
    public UiReactiveFault? LayoutFault { get; private set; }
    public bool DisplayRequired { get; private set; } = true;
    public void SetDisplayDemand(bool required)
    {
        if (!session.IsActive) return;
        if (DisplayRequired == required) return;
        DisplayRequired = required;
        session.AbortHitPass();
        if (!required) session.SuspendDisplayInput();
    }

    /// <summary>Reads current model facts and the last committed layout; never arranges or paints.</summary>
    public UiHostObservation Observe(UiObservationBudget? budget = null, string? nodeKey = null)
    {
        try
        {
            return engine.Observe(CreateContext(0), budget ?? new UiObservationBudget(), nodeKey,
                lastLayoutRevision != theme.LayoutRevision);
        }
        catch (Exception ex)
        {
            return new UiHostObservation(session.Identity, source, session.Frame, session.DefinitionRevision, null,
                DateTime.UtcNow, UiObservationStatus.Failed, "observation-read:" + ex.GetType().Name, false,
                Array.AsReadOnly(Array.Empty<UiObservedNode>()));
        }
    }

    public string Source => source;

    /// <summary>The committed definition; a staged layout remains private until the existing seal boundary.</summary>
    public UiLayoutManifest Manifest => pendingPrune ?? manifest;
    public int DefinitionRevision => session.DefinitionRevision;

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

    public UiLayoutSnapshot MeasureAndArrange(UiLayoutPoint available)
    {
        RefreshBindingOwner();
        reactivePresentation?.Synchronize(false);
        if (reactivePresentation != null && presentationRevision != reactivePresentation.DemandRevision)
        {
            presentationRevision = reactivePresentation.DemandRevision;
            engine.RequestInputValidation();
        }
        using IDisposable? inputs = reactivePresentation?.Enter();
        // The arrange runs inside this host's diagnostic scope: an appearance fallback the engine records
        // while resolving a region is attributed to the host that is arranging, not to whichever host drew
        // last. Null still opens the scope, so an unsubscribed host never inherits another host's routing.
        UiDiagnosticSubscription? subscription = diagnostics;
        bool timing = subscription != null && subscription.TimingEnabled;
        long started = timing ? Stopwatch.GetTimestamp() : 0L;
        UiDiagnosticHub.UiDiagnosticScope scope = UiDiagnosticHub.EnterHost(subscription, metrics);
        try
        {
            // The engine validates effective inputs per stage; presentation changes no longer bump
            // a global cache key. Unknown legacy Measure still receives its original synchronization.
            UiWidgetContext ctx = CreateContext(available.X);
            UiLayoutSnapshot snapshot = engine.ArrangeRoots(ctx, available, manifest.Roots);
            LayoutFault = null;

            // A region scope is resolved the first time the engine meets it, and that resolution can drop a
            // name (a scheme or density nobody declared). Publishing here - on the same frame, right after the
            // arrange that discovered it - is what keeps such a drop from waiting a frame to be visible. On a
            // cached arrangement there is nothing new to publish and the call costs two integer comparisons.
            PublishStyleIssues();
            lastLayoutRevision = theme.LayoutRevision;
            lastSuccessfulSnapshot = snapshot;
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

    public void Draw(UiLayoutRect viewport, UiLayoutSnapshot snapshot)
    {
        RefreshBindingOwner();
        using IDisposable? inputs = reactivePresentation?.Enter();
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
#if FER_DEV
            subscription?.Geometry?.BeginPass(session.Frame, session.Controls.DiagnosticEventName());
#endif
            session.SetHostViewport(viewport);
            UiWidgetContext ctx = CreateContext(viewport.Width);
            engine.Draw(ctx, snapshot, viewport);
            if (!session.IsActive) return;

            // Session-owned popups draw after normal content in the same OnGUI pass.
            foreach (Action popupDraw in session.PopupDrawActions)
            {
                popupDraw();
                if (!session.IsActive) return;
            }
            session.EndPopupPass();
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
    public void DrawFrame(UiLayoutRect viewport, UiFrame frame, IUiExecution execution)
    {
        if (!DisplayRequired) return;
        session.SetFrame(frame, execution);
        BeginFrame();
        try
        {
            // The engine consumes a pending scroll-target request at the end of its own arrange, by node
            // (task-18): resolution belongs where the entries and the nodes are, so the host does not
            // re-derive it from string-keyed snapshot views.
            UiLayoutSnapshot snapshot = MeasureAndArrange(new UiLayoutPoint(viewport.Width, viewport.Height));
            Draw(viewport, snapshot);
        }
        catch (UiReactiveFault failure)
        {
            // The layout engine restores the complete previous geometry transaction. It is not
            // current hit geometry: retain the session/drafts, but publish no partial input pass.
            LayoutFault = failure;
            session.AbortHitPass();
            try
            {
                if (lastSuccessfulSnapshot != null)
                {
                    Draw(viewport, lastSuccessfulSnapshot);
                    UiThemeDraw.FailureMarker(session.Drawing,viewport, theme, failure.Identity);
                }
                else UiThemeDraw.RecoveryBand(session.Drawing,viewport, theme, failure.Identity);
            }
            catch (UiReactiveFault) { UiThemeDraw.RecoveryBand(session.Drawing,viewport, theme, failure.Identity); }
            finally { session.AbortHitPass(); }
        }
        catch
        {
            session.AbortHitPass();
            throw;
        }
        finally
        {
            if (session.IsActive) EndFrame();
        }
    }

    /// <summary>
    /// One Cancel press, one undone layer: the ladder the interaction contract asks for (§3.2, §3.5, §3.6).
    /// It is tried in this order and stops at the first layer that answers, because each step undoes
    /// something the player started MORE recently than the layer below it:
    /// <list type="number">
    /// <item>the session's open option menu closes — a menu is the topmost thing on screen and the cheapest
    /// thing to have not wanted;</item>
    /// <item>a pointer capture this session holds is released, which ends the drag owned by it (the chart
    /// clears its own drag state when it finds it no longer holds the control, so releasing is the whole
    /// action — the value already written during the drag stays, because a Cancel ends an interaction, it
    /// does not roll back a result);</item>
    /// <item>an open edit is marked <c>DiscardRequested</c>, which its own field applies at its next draw in
    /// this same pass: the draft is dropped, the model is untouched, and unparseable text is discarded by the
    /// same rule that refuses to write it;</item>
    /// <item>otherwise the walk starts at the subject — the element a consumer named through
    /// <see cref="UiSession.SetCancelTarget(string)"/>, else the last element that actually TOOK an
    /// interaction — and climbs the tree to the nearest element whose <c>CancelBind</c> names a command that
    /// can run now. A layer that declares nothing is passed through, which is what lets a section be a
    /// return layer without being a control, and a layer whose command is currently vetoed is skipped rather
    /// than run, so the walk finds the nearest layer that can actually answer.</item>
    /// </list>
    /// <para>
    /// The key that drove this may be a business selection rather than a control: the contract's starting
    /// point is the interaction subject, never the last node drawn, and a page whose rows are selected by
    /// clicking a card has no control that "holds" that choice.
    /// </para>
    /// <para>
    /// Returns false when there is nothing to cancel, and consumes nothing in that case: the caller —
    /// <see cref="UiWindowHost.OnCancelKeyPressed"/> (which then asks its own
    /// <see cref="UiWindowHost.TryHandleUnansweredCancel"/> extension point before Verse), or a consumer's own
    /// window subclass — keeps Verse's meaning of the key, which is the layer that closes the window. The
    /// preserved behaviour belongs to a window with NO library interaction open: declaring no
    /// <c>CancelBind</c> is not sufficient on its own, because the menu, the held capture and the open edit
    /// are answered by this ladder whether or not the page declares a single cancel layer.
    /// </para>
    /// </summary>
    public bool TryHandleCancel()
    {
        if (!session.IsActive || !DisplayRequired) return false;

        if (session.ActiveEditState != null && session.Controls.NativeCompositionActive) return true;

        if (session.OpenPopupId != null)
        {
            session.ClosePopup();
            session.Controls.ConsumeKeyEvent();
            return true;
        }

        UiControlKey? capture = session.OwnedHotControl;
        if (capture.HasValue && session.OwnedHotControlOwner != null)
        {
            session.ReleaseHotControl(capture.Value);
            session.Controls.ConsumeKeyEvent();
            return true;
        }

        UiControlState? edit = session.ActiveEditState;
        if (edit != null)
        {
            edit.DiscardRequested = true;
            session.Controls.ConsumeKeyEvent();
            return true;
        }

        for (UiNode? node = session.CancelTargetNode ?? session.LastInteractionNode;
            node != null;
            node = node.Parent)
        {
            string key = node.CancelBindingKey;
            if (key.Length == 0) continue;
            if (bindings.TryInvokeCommand(key))
            {
                session.Controls.ConsumeKeyEvent();
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The Accept half of the same rule: while an edit is open, Enter answers the EDIT and must not close the
    /// window (§3.5). Marking the state is all this does — the field applies it at its own next draw, in the
    /// same event pass, which is where the parse, the clamp and the binding write live. With no open edit the
    /// caller keeps Verse's meaning, so a consumer's declared <c>closeOnAccept</c> convention is untouched.
    /// </summary>
    public bool TryHandleAccept()
    {
        if (!session.IsActive || !DisplayRequired) return false;

        UiControlState? edit = session.ActiveEditState;
        if (edit == null) return false;
        if (session.Controls.NativeCompositionActive || session.Controls.NativeMultilineReturn(edit)) return true;

        edit.CommitRequested = true;
        session.Controls.ConsumeKeyEvent();
        return true;
    }

    private void RefreshBindingOwner()
    {
        UiBindings current = UiBindings.OwnerOf(bindings);
        if (ReferenceEquals(current, bindingOwner)) return;
        // Validate before dropping the previous projection. A caller can restore its backing after
        // a failed definition update; an ordinary proxy still uses ordinary getters, not tracking.
        ValidateManifest(manifest);
        UiBindingPresentation? replacement = current.CreateHostPresentation(source + "/" + session.Identity);
        reactivePresentation?.Dispose();
        reactivePresentation = replacement;
        bindingOwner = current;
        presentationRevision = -1;
        session.BumpContentRevision();
        engine.RequestInputValidation();
    }

    public void Close()
    {
        reactivePresentation?.Dispose();
        ForgetObservationHost(this);
        // A closed host must not stay referenced by a live document service, and the service's documents
        // must survive the host: the dependency is released, the last valid version is not.
        UiDocumentService? service = documentService;
        documentService = null;
        service?.Detach(this);

        // The host's own diagnostic subscription goes with the host, before the session the hub keyed it
        // by is disposed: a torn-down window leaves no buffer, no registry entry and no ambient reference.
        diagnostics?.Dispose();
        diagnostics = null;

        try { session.Dispose(); }
        finally { engine.ReleaseLayoutCaches(); }
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
            session.ReportWarning(warning);
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
        // The drop's own element when it belongs to one (an unknown scope name an element declared), the style
        // origin otherwise: a document-level drop has no element to name, and reporting it under the element of
        // whichever node happened to resolve first would be a worse lie than reporting the origin.
        string path = issue.ElementPath ?? (source + "#styles");
        UiFitAudit.ReportStyleFallback(path, "Styles", "Declaration", issue.ToString(), "defaults");
    }

    // --- document reload (P4) -------------------------------------------------------------------

    /// <summary>The style document this host would resolve against right now.</summary>
    internal UiStyleDocument EffectiveStyleDocument => attachedStyleDocument ?? manifest.Styles;

    /// <summary>True once this host's session has been disposed by <see cref="Close"/>.</summary>
    internal bool SessionDisposed => !session.IsActive;

    /// <summary>
    /// True while this host's session owns the IMGUI hot control - the library's own observable model of
    /// "the user is dragging or editing right now", which is the signal
    /// <see cref="UiDocumentService"/> defers a ready commit for.
    /// <para>
    /// The signal is the session's own capture (<see cref="UiSession.CaptureHotControl"/>), because that is
    /// the one piece of interaction state the library owns end to end: <c>UiNative</c> grants the native hot
    /// control and the session records the ownership, so a drag reports true for every frame the pointer is
    /// held (the chart's point drag captures on pointer-down and releases on pointer-up).
    /// </para>
    /// <para>
    /// <b>What it does not cover.</b> It is deliberately not a general "the user is busy" answer. A control
    /// that is merely focused - a text field after a click, a keyboard-navigated control, an OS-level IME
    /// composition in progress - holds no library-owned hot control and reports false here. There is no
    /// per-element walk in this property either: a drag that never captured through the session is
    /// invisible. A consumer that needs one of those shapes deferred must close its own capture; the
    /// deferral ceiling is what keeps the missed cases bounded rather than a promise that this property is
    /// a complete activity monitor.
    /// </para>
    /// </summary>
    internal bool IsInteracting => session.IsActive && session.OwnedHotControl.HasValue;

    /// <summary>
    /// Attributes one document-service report to this host. The service calls it for every host a reload
    /// batch affected, so a report reaches exactly the windows that read the document and never a window
    /// that reads a different one. A host with no active subscription has nothing to record; unexpected
    /// buffer failures propagate to the document service, which adds the host and reload context.
    /// </summary>
    internal void PublishReloadReport(UiReloadReport? report)
    {
        UiDiagnosticSubscription? subscription = diagnostics;
        if (subscription == null || !subscription.IsActive || report == null) return;
        subscription.PublishReload(report);
    }

    /// <summary>Replace an immutable XML/program definition through the existing prepare/stage/seal boundary.</summary>
    public void UpdateDefinition(UiLayoutManifest candidate)
    {
        if (candidate == null) throw new ArgumentNullException(nameof(candidate));
        if (SessionDisposed) throw new ObjectDisposedException(nameof(UiHost));
        ValidateManifest(candidate);
        DocumentRollback rollback = CaptureDocumentRollback();
        try
        {
            CommitLayoutCandidate(candidate);
            SealDocumentCommit();
        }
        catch
        {
            RestoreDocumentRollback(rollback);
            throw;
        }
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
    /// Stages a validated layout candidate: swaps the roots and rebuilds the style resolver and the engine
    /// over the effective document. The session - and therefore the scroll positions, drafts, selection and
    /// expansion of every identity that survives - is not touched here: state preservation is a property of
    /// not replacing the session, and the destructive half (pruning what the new tree no longer carries)
    /// waits for <see cref="SealDocumentCommit"/> so a batch that later rolls back loses nothing.
    /// </summary>
    internal void CommitLayoutCandidate(UiLayoutManifest candidate)
    {
        UiLayoutManifest previous = manifest;
        manifest = candidate;
        pendingDocumentCommit = true;
        pendingPrune = previous;
        RebuildTree();
    }

    /// <summary>
    /// Stages a style-document candidate. Whether the document is structurally valid was decided by the
    /// service (a whole-document refusal never reaches here); declaration-level drops inside it stay soft and
    /// are published by the rebuilt resolver. A style change removes no element, so there is nothing to prune.
    /// </summary>
    internal void CommitStyleCandidate(UiStyleDocument candidate)
    {
        attachedStyleDocument = candidate ?? throw new ArgumentNullException(nameof(candidate));
        pendingDocumentCommit = true;
        pendingPrune = null;
        RebuildTree();
    }

    /// <summary>
    /// The destructive half of a document commit, run only once the whole change batch has staged
    /// successfully: elements the new tree no longer carries lose their state, and a held interaction capture
    /// is released. Keeping it out of the staging step is what makes a rolled-back batch a real rollback -
    /// before this, a batch that failed on a later host had already cleared an earlier host's draft.
    /// </summary>
    internal void SealDocumentCommit()
    {
        UiLayoutManifest? previous = pendingPrune;
        pendingPrune = null;
        bool committed = pendingDocumentCommit;
        pendingDocumentCommit = false;
        if (SessionDisposed) return;
        if (committed) session.DefinitionRevision++;

        if (previous != null)
        {
            PruneDetachedState(previous, manifest);
        }

        ReleaseHotControlForReload();
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
            new UiStyleResolver(probe, candidate).ApplyPageTo(probe);
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
        // The capture always precedes a stage, so any pending prune belongs to the commit being undone and
        // must not survive it: sealing a rolled-back batch would destroy state the rollback just preserved.
        pendingPrune = null;
        pendingDocumentCommit = false;
        manifest = rollback.Manifest;
        attachedStyleDocument = rollback.AttachedStyle;
        styleResolver = rollback.StyleResolver;
        engine = rollback.Engine;
        publishedDocumentIssues = rollback.PublishedDocumentIssues;
        publishedResolutionIssues = rollback.PublishedResolutionIssues;
        warnedDroppedStyleDocument = rollback.WarnedDroppedStyleDocument;
        CopyThemeTokens(theme, rollback.Theme);
    }

    /// <summary>
    /// Records the service this host's documents come from; called by the service's Attach. A host binds to
    /// one service, so re-binding releases the previous one: leaving the old service holding a host that no
    /// longer answers to it would leak the dependency until the cap refuses later attaches. The reference
    /// check matters - the calling service has just registered the host, and detaching from itself would
    /// remove the dependency it is in the middle of creating.
    /// </summary>
    internal void AttachDocumentService(UiDocumentService service)
    {
        if (service == null) throw new ArgumentNullException(nameof(service));
        UiDocumentService? previous = documentService;
        documentService = service;
        if (!ReferenceEquals(previous, service))
        {
            previous?.Detach(this);
        }
    }

    /// <summary>
    /// The non-destructive half of a commit: re-resolve the page level, rebuild the engine over the new
    /// effective document and republish the appearance issues. It deliberately destroys no interaction
    /// state - <see cref="SealDocumentCommit"/> owns that, one whole batch later.
    /// </summary>
    private void RebuildTree()
    {
        // Start from the pre-document tokens and then apply the new page level: a colour the new document no
        // longer declares has to actually stop applying, or "remove the override and reload" silently keeps
        // the old value and the reload is only half true.
        RestoreStyleBaseline();
        styleResolver = new UiStyleResolver(theme, EffectiveStyleDocument);
        styleResolver.ApplyPageTo(theme);

        // A fresh engine has no cached arrangement, so the next ArrangeRoots measures the new tree. This is
        // the invalidation the reload needs and it is strictly local: no second revision counter is
        // introduced anywhere - the engine instance itself is the invalidated thing.
        engine = new UiLayoutEngine(source, styleResolver, manifest.Templates);

        publishedDocumentIssues = 0;
        publishedResolutionIssues = 0;
        warnedDroppedStyleDocument = false;

        PublishStyleIssues();
    }

    /// <summary>Remove only the prior page application; current host declarations stay live.</summary>
    private void RestoreStyleBaseline() => theme.ClearStyleApplications();

    private static void CopyThemeTokens(UiTheme target, UiTheme source) => target.RestoreFrom(source);
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
        UiControlKey? held = session.OwnedHotControl;
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
        node.EditingState = null;
        node.ResetCustomState();
        ResetState(node.State);
        foreach (KeyValuePair<string, UiControlState> slot in node.ValueStates)
        {
            ResetState(slot.Value);
        }

        // A scroll position is state too, and it is keyed by the node for exactly this reason: a removed
        // scroll container must not leave a position behind for whatever identity claims that slot next.
        if (session.ScrollPositions.ContainsKey(node))
        {
            session.SetScrollPosition(node, new UiLayoutPoint(0f, 0f));
        }

        foreach (UiNode child in node.Children)
        {
            // An element child the new tree still carries keeps its own state; everything else under a
            // removed or kind-changed element - sub-nodes included - is part of what has to be cleaned up.
            if (survivors.ContainsKey(child.Id)) continue;
            ResetDetachedState(child, survivors);
        }
    }

    private void ResetState(UiControlState state)
    {
        session.Controls.ReleaseEditFocus(state, session);
        session.NoteEditClosed(state);
        if (state.Editor is IUiEditState editor) editor.Suspend();
        state.Editor = null;
        state.EditorTarget = null;
        state.ChartIdentity = null;
        state.CommitRequested = false;
        state.DiscardRequested = false;
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

    private void ValidateManifest(UiLayoutManifest candidate) => new UiDefinitionCompiler(source,bindings).ValidateManifest(candidate);
}
