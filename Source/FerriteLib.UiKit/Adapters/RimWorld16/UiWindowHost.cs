using System;
using UnityEngine;
using Verse;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// Library-owned window chrome: a <see cref="Verse.Window"/> that hosts exactly one <see cref="UiHost"/>
/// and owns nothing else (US→FL round 1, P2).
/// <para>
/// This is the item that shrinks the escape surface instead of adding capability. A consumer forced to
/// write its own Window keeps being tempted to write widgets there too, and the proven result was a
/// settings window whose close button reached past the kernel for raw backend calls. With the shell
/// here, "chrome" is a defined boundary with an owner instead of an exemption request.
/// </para>
/// <para>
/// <b>Boundary.</b> Page drawing uses the explicit execution port. This native adapter owns pointer
/// attribution and its own close chrome; KernelContainmentTests pins their actual path/member contacts.
/// Core controls and drafts remain in the neutral Session, never in the native window.
/// </para>
/// <para>
/// <b>No consumer numbers as library defaults.</b> Window <em>size</em> is a provider
/// (<see cref="InitialSizePolicy"/>) whose unset default is the game's own <see cref="Window.InitialSize"/>;
/// one consumer's screen-fraction clamp stays that consumer's policy. The <em>chrome geometry</em>
/// virtuals are different in kind — they are this shell's own layout, generalised from the first
/// implementation that shipped and proven, and they exist to be overridden.
/// </para>
/// </summary>
public abstract class UiWindowHost : Window
{
    private Shell? shell;
    private Shell WindowShell => shell ??= new Shell(this);
    private UiHost? host => shell?.Host;
    protected VerseUiExecution Execution { get; } = new VerseUiExecution();
    private VerseTextExecution? textExecution;
    private VerseTextExecution TextExecution => textExecution ??= new VerseTextExecution(Execution);
    private ITextMetrics? originalTextMetrics, composedTextMetrics;
    private ITextMetrics TextMetrics
    {
        get
        {
            ITextMetrics current = Metrics;
            if (!ReferenceEquals(current, originalTextMetrics))
            { originalTextMetrics = current; composedTextMetrics = VerseTextMetrics.Compose(current); }
            return composedTextMetrics!;
        }
    }
    /// <summary>Window-owned native image references. Bind the returned neutral tokens; closing drops references, never supplier textures.</summary>
    public VerseUiImages Images => Execution.Images;
    private bool activeTarget = true;
    private UiWindowCatalog? catalog;
    private bool displayRequired = true;
    /// <summary>Reliable adapter/consumer visibility, distinct from active input target and modality.</summary>
    public bool DisplayRequired => displayRequired;
    public void SetDisplayDemand(bool required)
    {
        displayRequired = required;
        WindowShell.SetDisplayDemand(required);
    }

    protected UiWindowHost()
    {
        // The shell paints its own background, accent rule and close affordance, so the game's
        // equivalents must be off: two backgrounds or two close buttons is the defect this type
        // exists to prevent, not a style preference. Modality (forcePause, absorbInputAroundWindow,
        // preventCameraMotion, layer) is never decided here — that is the consumer's call.
        doWindowBackground = false;
        doCloseX = false;
        doCloseButton = false;
        drawShadow = false;
    }

    // ---- the consumer's required surface ----

    /// <summary>The palette the chrome is painted with. One theme per window; no second palette.</summary>
    protected abstract UiTheme Theme { get; }

    /// <summary>
    /// Builds the page this window hosts. Called at most once per window instance, inside the guarded
    /// draw pass, so a factory that throws is reported through the failure contract rather than
    /// reaching the window stack.
    /// </summary>
    protected abstract UiHost CreateHost();

    /// <summary>
    /// Paints the honest replacement for the page. Required because the library owns no strings:
    /// a failed window must say what happened in the consumer's vocabulary, or it is a blank window.
    /// </summary>
    protected abstract void DrawNotice(Rect rect, UiWindowNotice notice);

    // ---- optional surface, all of it defaulted to "the game's own behaviour" ----

    /// <summary>Title band text. Empty means no title is drawn.</summary>
    protected virtual string Title => "";

    /// <summary>Secondary line under the title. Empty means none.</summary>
    protected virtual string Subtitle => "";

    /// <summary>
    /// Close affordance label. Empty still draws a clickable button — the affordance is structural
    /// once <c>doCloseX</c> is off; only the word is the consumer's.
    /// </summary>
    protected virtual string CloseText => "";

    /// <summary>
    /// Window size provider. Null keeps <see cref="Window.InitialSize"/>'s own behaviour, which is the
    /// point: the library must not freeze a consumer's screen math into the freeze surface.
    /// </summary>
    protected virtual Func<Vector2>? InitialSizePolicy => null;

    /// <summary>
    /// False blocks the page from being entered at all — the named carrier/consumer desync case, so a
    /// <c>TypeLoadException</c> never reaches the draw path and the player sees the real cause.
    /// </summary>
    protected virtual bool PrerequisiteVerified => true;

    /// <summary>The exception behind a <see cref="UiWindowNotice.PageUnavailable"/>, for the notice body.</summary>
    protected Exception? LastFailure => shell?.LastFailure;

    /// <summary>Runs once per pass before the page or the notice, for consumer-side frame bookkeeping.</summary>
    protected virtual void BeforeDraw(Rect contentRect)
    {
    }

    /// <summary>Consumer logging seam for a failed pass. Called once per failure, never for the notice pass.</summary>
    protected virtual void OnDrawFailure(Exception error)
    {
    }

    // ---- chrome geometry: this shell's own layout, overridable, not consumer policy ----

    /// <summary>Vertical space reserved for title, subtitle and close affordance.</summary>
    protected virtual float TitleBarHeight => WindowShell.DefaultTitleBarHeight;

    /// <summary>Left/right/bottom inset of the page inside the window.</summary>
    protected virtual float SidePadding => 20f;

    /// <summary>Accent rule painted along the top edge.</summary>
    protected virtual float AccentBarHeight => 3f;

    /// <summary>
    /// The text ruler the chrome measures with. It is the same <see cref="ITextMetrics"/> seam a host
    /// hands the fit audit (<see cref="UiFitAudit.Attach"/>), and it has to be the same *model* the audit
    /// counts with: the shell now sizes its close affordance from a measurement, so two rulers would mean
    /// the audit reporting a width the shell had just made enough room for. Defaults to the production
    /// seam; a harness or a consumer with its own ruler overrides it and hands the same instance to both.
    /// </summary>
    protected virtual ITextMetrics Metrics => VerseFerriteTextMetrics.Instance;

    /// <summary>
    /// Size of the close affordance, derived from the label it has to hold: at least
    /// 110 logical units wide, otherwise the measured label plus 10 units on each side.
    /// <para>
    /// A fixed box was this shell's shipped shape and it is the wrong default <b>for a library</b>:
    /// <see cref="CloseText"/> is the consumer's string, so any consumer whose wording or translation
    /// grows past the constant gets its own label clipped by the shell's geometry — and the finding lands
    /// on the chrome, not on the consumer who wrote the text. Sizing to the text is what makes that class
    /// of defect impossible once, in the shell, instead of once per consumer; the floor keeps the shipped
    /// look unchanged for the short labels it was designed around. The measurement runs through
    /// <see cref="Metrics"/>, one unwrapped extent per frame, which is the same order of cost the audit
    /// pays per label while it is enabled.
    /// </para>
    /// <para>
    /// Overriding this property replaces the computation entirely — that is the escape hatch for a
    /// consumer with its own chrome geometry, and it is the only supported way to keep a fixed size.
    /// </para>
    /// </summary>
    protected virtual Vector2 CloseButtonSize => VerseUiExecution.Point(WindowShell.DefaultCloseButtonSize);

    /// <summary>Font size of the title line.</summary>
    protected virtual UiFont TitleFont => UiFont.Medium;

    /// <summary>Font size of the subtitle line.</summary>
    protected virtual UiFont SubtitleFont => UiFont.Tiny;

    /// <summary>Font size of the close affordance.</summary>
    protected virtual UiFont CloseFont => UiFont.Tiny;

    /// <summary>Content inset is owned by <see cref="SidePadding"/>/title bar, so the game's margin must be zero.</summary>
    protected sealed override float Margin => 0f;

    public sealed override Vector2 InitialSize => InitialSizePolicy?.Invoke() ?? base.InitialSize;

    /// <summary>The live page host, or null before first draw and after a failure.</summary>
    protected UiHost? Host => host;

    /// <summary>
    /// The live page session, or null before the first successful pass and after a failure. Exposed
    /// because a window that stops being the active target keeps this session - its scroll, selection and
    /// drafts - and "the same session is still there" is the observable form of that promise.
    /// </summary>
    public UiSession? Session => host?.Session;
    public UiHostInputState InputState => host?.InputState ?? default;

    /// <summary>
    /// Raised once, on the draw thread, immediately after this window built its page host and before that
    /// host draws its first frame. It is the reliable public attach path a consumer needs in order to stop
    /// polling for a host that does not exist yet: a host is built lazily inside the guarded pass, so
    /// <see cref="Session"/> is null for every frame before the first one, and a consumer that read it early
    /// had no way to tell "not yet" from "failed" without guessing at the frame count.
    /// <para>
    /// <b>What the handler may do.</b> Opt the host into diagnostics, attach a reload-report sink, register
    /// this page's document dependency - the same things it could do after the first draw, only sooner. What
    /// it must not do is throw: the handler runs inside this window's guarded pass, so an exception here is
    /// treated exactly like an exception from the page and the window enters its failure notice. That is
    /// deliberate - a silent swallow would make a consumer's broken lifecycle code indistinguishable from a
    /// page that never loaded.
    /// </para>
    /// <para>
    /// <b>Who owns what.</b> The window owns this host and disposes it; the consumer owns the manifest, the
    /// bindings, the theme, the model and the view model, and this library never disposes any of them. A
    /// borrowed Model or VM is borrowed for exactly as long as the subscription the consumer made here.
    /// </para>
    /// </summary>
    public event Action<UiHost>? HostAttached
    {
        add => WindowShell.HostAttached += value;
        remove => WindowShell.HostAttached -= value;
    }

    /// <summary>
    /// Raised immediately before this window disposes the page host it owns - on close, and when a failed
    /// pass tears the host down. The host is still alive when the handler runs, so a consumer releases its
    /// subscriptions here (unsubscribe a notification adapter, detach the document dependency) rather than
    /// leaving a dead page wired into a live model.
    /// <para>
    /// The same "cannot throw" contract as <see cref="HostAttached"/> applies, and the same ownership split:
    /// this releases the page's connection, never the consumer's objects.
    /// </para>
    /// </summary>
    public event Action<UiHost>? HostDetached
    {
        add => WindowShell.HostDetached += value;
        remove => WindowShell.HostDetached -= value;
    }

    /// <summary>
    /// The key this window is addressed by, or null when no catalog attached it. A window created
    /// outside a catalog keeps the null and behaves exactly as the shell always did.
    /// </summary>
    public UiWindowKey? Key { get; internal set; }

    /// <summary>
    /// True while this window is the catalog's one active target. A window outside a catalog is always
    /// the active target, which is what keeps the pre-catalog shell's behaviour unchanged.
    /// </summary>
    public bool IsActiveTarget => activeTarget;

    /// <summary>
    /// The policy a catalog applied to this window, or null when none was applied. Readable by a consumer
    /// subclass and by the catalog in this assembly, which is why it is not merely protected: the
    /// catalog's own activation rule consults it.
    /// </summary>
    protected internal UiWindowOptions? AppliedOptions { get; private set; }

    /// <summary>
    /// Applies a registration's policy. A null switch leaves the game's own value untouched: the library
    /// writes no product default into forcePause or preventCameraMotion. The one non-nullable switch,
    /// <see cref="UiWindowOptions.AllowMultipleInstances"/>, reaches the vanilla add path's exact-type
    /// rule directly, because the library's own key - not the C# type - is what deduplicates instances.
    /// </summary>
    internal void ApplyOptions(UiWindowOptions options)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));

        AppliedOptions = options;

        // The vanilla add path removes a same-typed sibling that carries onlyOneOfTypeAllowed, so this
        // flag is how the option reaches the game's own rule instead of the library re-implementing it.
        onlyOneOfTypeAllowed = !options.AllowMultipleInstances;

        if (options.ForcePause is bool forcePauseValue) forcePause = forcePauseValue;
        if (options.PreventCameraMotion is bool cameraValue) preventCameraMotion = cameraValue;
        if (options.AbsorbInputAroundWindow is bool absorbValue) absorbInputAroundWindow = absorbValue;
        if (options.Draggable is bool draggableValue) draggable = draggableValue;
        if (options.Resizeable is bool resizeableValue) resizeable = resizeableValue;
        if (options.CloseOnAccept is bool acceptValue) closeOnAccept = acceptValue;
        if (options.CloseOnCancel is bool cancelValue) closeOnCancel = cancelValue;
        if (options.CloseOnClickedOutside is bool outsideValue) closeOnClickedOutside = outsideValue;
    }

    /// <summary>Binds this window to the catalog that owns its instance identity. One catalog per window.</summary>
    internal void AttachToCatalog(UiWindowCatalog owner)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        if (catalog != null && !ReferenceEquals(catalog, owner))
        {
            throw new InvalidOperationException("A window cannot be attached to two window catalogs.");
        }

        catalog = owner;
    }

    /// <summary>
    /// Moves this window in or out of the active target. Losing the target releases the page session's
    /// IMGUI capture, which is the pointer half of "a control in a deactivated window stops receiving
    /// input": the control that captured the pointer before the target moved cannot be dragged from a
    /// window the user is no longer working in. <c>UiSession.ReleaseHotControl</c> only ever releases the
    /// session's own capture, so this cannot free another window's drag.
    /// </summary>
    internal void SetActiveTarget(bool active)
    {
        if (activeTarget == active) return;

        activeTarget = active;
        if (!active)
        {
            UiSession? session = host?.Session;
            if (session != null && session.IsActive)
            {
                UiControlKey? owned = session.OwnedHotControl;
                if (owned.HasValue)
                {
                    session.ReleaseHotControl(owned.Value);
                }
            }
        }
    }

    /// <summary>
    /// One window pass. The shell's own bands - chrome and the failure notice - are drawn outside
    /// <see cref="UiHost.DrawFrame"/>, so the ambient attribution a host opens around its own arrange/draw
    /// never covered them: their fit findings fell back to the process-wide channel, and with two windows
    /// open a chrome overflow could not be routed to the window that produced it. That is the routing half
    /// of the ambiguity the keyed chrome scope already fixes on the naming half, so the whole shell pass
    /// runs inside the owning host's diagnostic scope.
    /// <para>
    /// <b>Where no host exists the scope opens with null, never with a subscription of its own.</b> The
    /// first pass draws chrome before <see cref="CreateHost"/> has run, and a failed pass disposes the host
    /// together with its subscription; in both cases the audit keeps its pre-existing legacy path, and the
    /// null scope still clears the ambient subscription so a window can never inherit whichever host
    /// happens to be drawing around it. A host nobody subscribed to stays exactly as cheap and quiet as
    /// before: this creates no subscription and measures nothing the audit was not already measuring.
    /// </para>
    /// </summary>
    public sealed override void DoWindowContents(Rect inRect)
    {
        if (!displayRequired) return;
        ResolvePointerDown();
        WindowShell.ReopenNative();
        // The native adapter keeps its original chrome hit/close and late event qualification.
        WindowShell.Draw(VerseUiExecution.Rect(inRect), new UiFrame(UiFramePhase.None), TextExecution,
            new UiLayoutPoint(windowRect.x, windowRect.y));
    }

    /// <summary>
    /// The native Cancel key enters the page through here, which is the ONLY reason the ladder exists as a
    /// host method: Verse dispatches this hook at the top of the window's pass, before
    /// <see cref="DoWindowContents"/> has drawn anything, so a page's option menu, held drag or open edit can
    /// be undone before any widget gets a chance to react to the same key. The shell offers the ladder first
    /// refusal, then <see cref="TryHandleUnansweredCancel"/> for a policy the page's tree cannot express, and
    /// only then hands the key to <c>base</c>, which is Verse's own meaning of it — close the window when
    /// <c>closeOnCancel</c> says so. A window with no library interaction open and no executable business
    /// cancel layer therefore still behaves exactly as it did before this override existed; a window with
    /// something to undo loses one layer per press instead of the whole window.
    /// <para>
    /// NOT sealed, deliberately: a consumer that already owns a cancel policy on its shell — the wired
    /// consumer's two diagnostic windows arm a two-press Escape close in their own override — has to keep
    /// compiling against this carrier. Overriding this method wholesale bypasses the ladder, so the supported
    /// shape is to call <c>base</c> and implement <see cref="TryHandleUnansweredCancel"/>; a subclass that
    /// replaces the method entirely is choosing to own the key, which is its right and no longer the
    /// library's refusal.
    /// </para>
    /// <para>
    /// The vanilla precondition is unchanged and is a real boundary: Verse only calls this hook for a window
    /// that <c>closeOnCancel</c> or <c>forceCatchAcceptAndCancelEventEvenIfUnfocused</c> makes eligible and
    /// that the window stack lets receive input. A shell configured with <c>CloseOnCancel=false</c> does not
    /// hear the key at all, and this library does not rewrite that by forcing the catch field on a
    /// consumer's behalf — a window that sets it itself still cannot take a key from a window above it that
    /// absorbs input around itself, because eligibility is ANDed with the stack's input test. The wiring note
    /// a consumer with <c>CloseOnCancel=false</c> needs is therefore its own one-line assignment of that
    /// public Verse field, and which window a real multi-window stack hands the key to remains a human-pass
    /// observation.
    /// </para>
    /// </summary>
    public override void OnCancelKeyPressed()
    {
        if (!displayRequired) return;
        UiHost? page = host;
        if(page!=null)page.Session.SetFrame(Execution.CaptureFrame(!IsOpen || Find.WindowStack.GetsInput(this)),TextExecution);
        if (page != null && page.TryHandleCancel()) return;

        if (TryHandleUnansweredCancel())
        {
            // The shell consumes on the handler's behalf, because the handler has no other way to: a consumer
            // that keeps raw backend calls out of its own source cannot reach Event.current, and an answered
            // but unconsumed Cancel key is re-read by the game at the end of the same window pass.
            Execution.ConsumeEvent();
            return;
        }

        base.OnCancelKeyPressed();
    }

    /// <summary>
    /// The neutral extension point for a cancel policy that is neither the page's tree nor Verse's close:
    /// an arm-then-confirm exit, a window-local guard, anything whose meaning belongs to the consumer's
    /// window rather than to a page element. It is asked ONLY after the library ladder declined, so a layer
    /// the player can see on screen always outranks a policy the player cannot; returning true means the
    /// policy handled the press, and the shell then consumes the key so the same press cannot also reach the
    /// native close. The default answers false, which is the delivered behaviour: Verse's convention stays
    /// the last word.
    /// <para>
    /// Cancel only, on purpose. The evidence for an extension point here is a consumer that already owns this
    /// exact policy on two windows; nothing in any consumer owns an Accept-side equivalent, so there is no
    /// sibling hook, no opt-out switch and no second window-input authority — the ladder and this one answer
    /// are the whole surface.
    /// </para>
    /// </summary>
    protected virtual bool TryHandleUnansweredCancel() => false;

    /// <summary>
    /// The Accept key, in the same first-refusal shape but with a narrower reach: <c>Enter</c> answers ONLY an
    /// open edit the page holds (§3.5 — a committed value must not also be a closed settings window). It does
    /// not close a menu, end a drag or climb the tree; those are the Cancel key's layers, and a consumer that
    /// reads one list for both keys is reading a rule this shell does not have. With no open edit,
    /// <c>base</c> keeps the consumer's declared <c>closeOnAccept</c> convention untouched.
    /// </summary>
    public sealed override void OnAcceptKeyPressed()
    {
        if (!displayRequired) return;
        UiHost? page = host;
        if(page!=null)page.Session.SetFrame(Execution.CaptureFrame(!IsOpen || Find.WindowStack.GetsInput(this)),TextExecution);
        if (page != null && page.TryHandleAccept()) return;

        base.OnAcceptKeyPressed();
    }

    /// <summary>
    /// A window is about to enter the stack. The normal size is restored here so a reopened window comes
    /// back at its resting size, and the catalog is told before anything else runs so the instance is
    /// addressable by its key from the first hook onward.
    /// </summary>
    public override void PreOpen()
    {
        Vector2? normal = AppliedOptions?.NormalSize;
        if (normal.HasValue)
        {
            windowRect = new Rect(windowRect.x, windowRect.y, normal.Value.x, normal.Value.y);
        }

        catalog?.NotifyPreOpen(this);
        base.PreOpen();
    }

    public override void PostOpen()
    {
        base.PostOpen();
        catalog?.NotifyPostOpen(this);
    }

    /// <summary>
    /// The consumer's close veto, answered through the vanilla hook. The catalog is told only after the
    /// hook agreed to close, so a refused close never looks like a removal to the identity map.
    /// </summary>
    public override bool OnCloseRequest()
    {
        if (!CanClose())
        {
            return false;
        }

        bool allowed = base.OnCloseRequest();
        if (allowed)
        {
            catalog?.NotifyCloseRequested(this);
        }

        return allowed;
    }

    /// <summary>The consumer's veto. True by default, which is the game's own answer.</summary>
    protected virtual bool CanClose()
    {
        return true;
    }

    public override void PreClose()
    {
        // The target is dropped before the page goes away, so no input or capture reaches a closing
        // window; the catalog then removes the instance for real at PostClose.
        catalog?.NotifyPreClose(this);

        // Closing disposes the session with the host, so reopening gets a clean retry.
        try { shell?.Dispose(); }
        finally
        {
            try { Execution.Dispose(); }
            finally { base.PreClose(); }
        }
    }

    public override void PostClose()
    {
        base.PostClose();
        catalog?.NotifyPostClose(this);
    }

    /// <summary>
    /// The active-target half of the shell's input rule, run before anything draws. A pointer-down asks
    /// the catalog which instance the pointer is over: the click that selects a deactivated window is
    /// consumed here so it cannot also operate a control of that window (the first click selects, the
    /// second operates); a click outside every instance clears the target, so a control in the window
    /// that was selected before does not keep taking input.
    /// <para>
    /// <b>Coordinates.</b> <c>Event.current.mousePosition</c> is group-local inside the window's draw
    /// group, so the pointer is translated into the space the catalog compares rects in before it is
    /// handed over; see <see cref="ToWindowSpace"/>. Without that translation a click inside a second,
    /// offset window resolved to whichever window covered the local coordinate on screen.
    /// </para>
    /// <para>
    /// <b>What the harness cannot prove.</b> Real keyboard routing after the target moves, and how this
    /// selection click composes with the vanilla stack's own click handling, are in-game behaviour: the
    /// stubs model the library's rule, not IMGUI's. The 0.5 verification checklist (A1/A2/A3/A3b) is
    /// where that half is confirmed.
    /// </para>
    /// </summary>
    private void ResolvePointerDown()
    {
        if (catalog == null || !(Event.current?.type == EventType.MouseDown && Event.current.button == 0))
        {
            return;
        }

        bool wasActive = activeTarget;
        catalog.NotifyPointerDown(ToWindowSpace((Event.current?.mousePosition ?? default)));

        if (!wasActive && activeTarget)
        {
            // Consumed so the activating click cannot reach the page this pass: a control drawn while the
            // window was deactivated must not fire from the click that selected the window.
            Execution.ConsumeEvent();
        }
    }

    /// <summary>
    /// A content-local point translated into the window space the catalog's rects live in.
    /// <para>
    /// <b>Why the offset is the window's own origin.</b> The game draws the page inside a group whose
    /// rect is <c>windowRect.AtZero().ContractedBy(Margin)</c> and hands <c>DoWindowContents</c> that
    /// rect at zero (Verse.Window.InnerWindowOnGUI), so the event pointer inside it is group-local. This
    /// shell seals <c>Margin</c> to zero, which makes the group origin the window's own screen origin and
    /// the conversion exact: <c>windowRect.position + local</c>. It is also why the catalog can compare
    /// the result against every instance's screen <c>windowRect</c>.
    /// </para>
    /// </summary>
    private Vector2 ToWindowSpace(Vector2 contentLocal)
    {
        return new Vector2(windowRect.x + contentLocal.x, windowRect.y + contentLocal.y);
    }

    /// <summary>Optional bottom image, above the shell base and below all chrome/content. It must not take input.</summary>
    protected virtual void DrawBackground(Rect rect) { }

    internal void DrawPageNotice(Rect rect, string text)
        => WindowShell.DrawPageNotice(VerseUiExecution.Rect(rect), text);

    /// <summary>Present screen-space geometry computed by the same shell that paints this window.</summary>
    public UiWindowChrome ShellChrome => WindowShell.GetChrome(VerseUiExecution.Rect(windowRect));

    /// <summary>Native policy/primitive forwarding; face, page geometry and lifetime are not duplicated.</summary>
    private sealed class Shell : UiWindowShell
    {
        private readonly UiWindowHost owner;
        internal Shell(UiWindowHost owner) => this.owner = owner;
        protected override UiTheme Theme => owner.Theme;
        protected override ITextMetrics Metrics => owner.Metrics;
        protected override ITextMetrics TextMetrics => owner.TextMetrics;
        protected override IUiDrawing TextDrawing => owner.TextExecution;
        protected override UiHost CreateHost() => owner.CreateHost();
        protected override void DrawNotice(UiLayoutRect rect, UiWindowNotice notice) => owner.DrawNotice(VerseUiExecution.Rect(rect), notice);
        protected override string Title => owner.Title;
        protected override string Subtitle => owner.Subtitle;
        protected override string CloseText => owner.CloseText;
        protected override bool PrerequisiteVerified => owner.PrerequisiteVerified;
        protected override string ScopeIdentity => owner.Key is UiWindowKey key ? owner.GetType().Name + "[" + key + "]" : owner.GetType().Name;
        protected override void BeforeDraw(UiLayoutRect rect) => owner.BeforeDraw(VerseUiExecution.Rect(rect));
        protected override void OnDrawFailure(Exception error) => owner.OnDrawFailure(error);
        protected override void DrawBackground(UiLayoutRect rect) => owner.DrawBackground(VerseUiExecution.Rect(rect));
        protected override void RequestClose() => owner.Close();
        protected override UiFont TitleFont => owner.TitleFont;
        protected override UiFont SubtitleFont => owner.SubtitleFont;
        protected override UiFont CloseFont => owner.CloseFont;
        protected override float SidePadding => owner.SidePadding;
        protected override float AccentBarHeight => owner.AccentBarHeight;
        protected override float TitleBarHeight => owner.TitleBarHeight;
        protected override UiLayoutPoint CloseButtonSize => VerseUiExecution.Point(owner.CloseButtonSize);
        protected override UiFrame CaptureFrame() => owner.Execution.CaptureFrame(!owner.IsOpen || Find.WindowStack.GetsInput(owner));
        protected override bool CloseHovered(UiLayoutRect button) => Mouse.IsOver(VerseUiExecution.Rect(button));
        protected override bool ClosePressed(UiLayoutRect button) => Verse.Widgets.ButtonInvisible(VerseUiExecution.Rect(button));
    }
}
