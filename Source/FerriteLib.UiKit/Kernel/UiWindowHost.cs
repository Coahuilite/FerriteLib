using System;
using UnityEngine;
using Verse;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// Why a window shell is not showing its page. The distinction is the consumer's to phrase: this
/// library ships zero translation keys, so the strings belong to whoever overrides
/// <see cref="UiWindowHost.DrawNotice"/>.
/// </summary>
public enum UiWindowNotice
{
    /// <summary>The kernel page cannot be entered at all; see <see cref="UiWindowHost.PrerequisiteVerified"/>.</summary>
    Prerequisite,

    /// <summary>Creating or drawing the page failed; the host has been disposed and will not be retried.</summary>
    PageUnavailable
}

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
/// <b>Boundary.</b> Every pixel this type paints goes through <see cref="UiThemeDraw"/> and every hit
/// test through <see cref="UiNative.Button"/> / <see cref="UiNative.IsMouseOver"/>. It deliberately does
/// NOT appear in the harness containment allowlist, so a raw backend call added here reddens
/// <c>KernelContainmentTests</c> rather than being caught in review — the acceptance line the review
/// asked for (condition d), enforced by a gate instead of by trust.
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
    private UiHost? host;
    private bool pageUnavailable;
    private bool noticeDueNextFrame;
    private Exception? lastFailure;
    private bool activeTarget = true;
    private UiWindowCatalog? catalog;

    // Identity the shell's own text is attributed to in the fit audit: built lazily and cached, because
    // the concrete type is known here but the window key is attached by the catalog after construction,
    // and a per-frame string would be a per-frame allocation. See BuildScope for the identity itself.
    private string? chromeScope;
    private string? noticeScope;

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
    protected Exception? LastFailure => lastFailure;

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
    protected virtual float TitleBarHeight => 56f;

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

    /// <summary>Narrowest the close affordance may be: the shipped width for a short label.</summary>
    private const float CloseButtonMinWidth = 110f;

    /// <summary>Height of the close affordance. Text moves the width only; the band is fixed.</summary>
    private const float CloseButtonHeight = 30f;

    /// <summary>Breathing room left around the close label, per side.</summary>
    private const float CloseButtonPadding = 10f;

    /// <summary>
    /// Size of the close affordance, derived from the label it has to hold: at least
    /// <see cref="CloseButtonMinWidth"/> wide, otherwise the measured label plus
    /// <see cref="CloseButtonPadding"/> on each side.
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
    protected virtual Vector2 CloseButtonSize
    {
        get
        {
            float measured = Metrics.MeasureWidth(CloseText, CloseFont);
            return new Vector2(
                Math.Max(CloseButtonMinWidth, measured + CloseButtonPadding * 2f),
                CloseButtonHeight);
        }
    }

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
                int? owned = session.OwnedHotControl;
                if (owned.HasValue)
                {
                    session.ReleaseHotControl(owned.Value);
                }
            }
        }
    }

    public sealed override void DoWindowContents(Rect inRect)
    {
        ResolvePointerDown(inRect);
        DrawChrome(inRect);
        Rect content = ContentRect(inRect);
        BeforeDraw(content);

        if (pageUnavailable)
        {
            DrawNoticeScoped(content, UiWindowNotice.PageUnavailable);
            return;
        }

        if (!PrerequisiteVerified)
        {
            DrawNoticeScoped(content, UiWindowNotice.Prerequisite);
            return;
        }

        // A failed pass must not switch pages inside the IMGUI pass that threw: that pass has already
        // claimed layout state for the page. Trip the notice on the NEXT pass, drawn from a clean one.
        // This is the non-obvious part of the contract and it is deliberately not "improved" into a
        // synchronous retry.
        if (noticeDueNextFrame)
        {
            noticeDueNextFrame = false;
            pageUnavailable = true;
            DrawNoticeScoped(content, UiWindowNotice.PageUnavailable);
            return;
        }

        try
        {
            host ??= CreateHost();
            host.DrawFrame(content);
        }
        catch (Exception ex)
        {
            noticeDueNextFrame = true;
            lastFailure = ex;
            host?.Dispose();
            host = null;
            OnDrawFailure(ex);
        }
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
        host?.Dispose();
        host = null;
        base.PreClose();
    }

    public override void PostClose()
    {
        base.PostClose();
        catalog?.NotifyPostClose(this);
    }

    private Rect ContentRect(Rect inRect)
    {
        return new Rect(
            inRect.x + SidePadding,
            inRect.y + TitleBarHeight,
            Math.Max(1f, inRect.width - SidePadding * 2f),
            Math.Max(1f, inRect.height - TitleBarHeight - SidePadding));
    }

    /// <summary>
    /// The shell's own text is drawn inside a named scope, so a fit finding about it is addressable.
    /// <para>
    /// It exists because of what an unscoped finding costs: two windows can be on screen at once, and a
    /// report that only says <c>"(unscoped)"</c> cannot say which window or which band produced it — the
    /// ambiguity that made a real-machine investigation attribute a 128px finding to the wrong window.
    /// The chrome is drawn before the page and outside every element scope, so it is exactly the text this
    /// scope has to name; the notice is the consumer's own drawing but the shell is what calls it, so the
    /// shell is what can say whose notice it is.
    /// </para>
    /// <para>
    /// Identity, recorded rather than hidden: the concrete window TYPE plus the window key when a catalog
    /// attached one, because the shell has no manifest element id when it paints chrome. The key was
    /// added in the 0.5.x window round: a single generic page shell serves every ordinary page, so
    /// type-only identity would put two open panels on the same chrome path and a finding about one would
    /// read as a finding about the other. Findings are keyed by path and text, so with the key in the path
    /// two instances are told apart instead of deduplicated into one. The <c>/chrome</c> and
    /// <c>/notice</c> suffixes keep the two bands apart.
    /// </para>
    /// </summary>
    private void DrawChrome(Rect rect)
    {
        UiFitAudit.BeginElement(ChromeScope);
        try
        {
            DrawChromeCore(rect);
        }
        finally
        {
            UiFitAudit.EndElement();
        }
    }

    /// <summary>Draws the notice inside the shell's notice scope; see <see cref="DrawChrome"/>.</summary>
    private void DrawNoticeScoped(Rect content, UiWindowNotice notice)
    {
        UiFitAudit.BeginElement(NoticeScope);
        try
        {
            DrawNotice(content, notice);
        }
        finally
        {
            UiFitAudit.EndElement();
        }
    }

    /// <summary>The fit-audit scope the chrome is drawn inside; see <see cref="DrawChrome"/>.</summary>
    private string ChromeScope => chromeScope ??= BuildScope("chrome");

    /// <summary>The fit-audit scope the notice is drawn inside; see <see cref="DrawChrome"/>.</summary>
    private string NoticeScope => noticeScope ??= BuildScope("notice");

    /// <summary>
    /// The audit identity for one of the shell's own bands: the concrete type, plus the window key in
    /// brackets when a catalog attached one. Built lazily and cached because the key is attached after
    /// construction, so the string cannot be decided once in the constructor.
    /// </summary>
    private string BuildScope(string band)
    {
        string identity = Key is UiWindowKey key ? GetType().Name + "[" + key + "]" : GetType().Name;
        return identity + "/" + band;
    }

    /// <summary>
    /// The active-target half of the shell's input rule, run before anything draws. A pointer-down asks
    /// the catalog which instance the pointer is over: the click that selects a deactivated window is
    /// consumed here so it cannot also operate a control of that window (the first click selects, the
    /// second operates); a click outside every instance clears the target, so a control in the window
    /// that was selected before does not keep taking input.
    /// <para>
    /// <b>What the harness cannot prove.</b> Real keyboard routing after the target moves, and how this
    /// selection click composes with the vanilla stack's own click handling, are in-game behaviour: the
    /// stubs model the library's rule, not IMGUI's. The 0.5 verification checklist (A1/A2/A3/A3b) is
    /// where that half is confirmed.
    /// </para>
    /// </summary>
    private void ResolvePointerDown(Rect contentRect)
    {
        if (catalog == null || !UiNative.IsPointerDown())
        {
            return;
        }

        bool wasActive = activeTarget;
        catalog.NotifyPointerDown(UiNative.PointerPosition(), this, contentRect);

        if (!wasActive && activeTarget)
        {
            // Consumed so the activating click cannot reach the page this pass: a control drawn while the
            // window was deactivated must not fire from the click that selected the window.
            UiNative.ConsumePointerEvent();
        }
    }

    private void DrawChromeCore(Rect rect)
    {
        UiTheme theme = Theme;
        UiThemeDraw.Workspace(rect, theme);
        UiThemeDraw.Solid(new Rect(rect.x, rect.y, rect.width, AccentBarHeight), theme.AccentGold);

        if (Title.Length > 0)
        {
            UiThemeDraw.Label(
                new Rect(rect.x + SidePadding, rect.y + 6f, Math.Max(1f, rect.width * 0.6f), 30f),
                Title,
                theme,
                theme.TextPrimary,
                TitleFont,
                singleLine: true);
        }

        if (Subtitle.Length > 0)
        {
            UiThemeDraw.Label(
                new Rect(rect.x + SidePadding, rect.y + 36f, Math.Max(1f, rect.width * 0.6f), 18f),
                Subtitle,
                theme,
                theme.TextSecondary,
                SubtitleFont,
                singleLine: true);
        }

        DrawCloseButton(rect);
    }

    private void DrawCloseButton(Rect rect)
    {
        UiTheme theme = Theme;
        Vector2 size = CloseButtonSize;
        var button = new Rect(
            rect.xMax - size.x - SidePadding,
            rect.y + (TitleBarHeight - size.y) * 0.5f,
            size.x,
            size.y);

        bool hovered = UiNative.IsMouseOver(button);
        UiThemeDraw.Surface(
            button,
            theme,
            hovered ? theme.Hover : theme.Panel,
            hovered ? theme.BorderStrong : theme.Border);
        UiThemeDraw.Label(
            button,
            CloseText,
            theme,
            hovered ? theme.TextPrimary : theme.TextSecondary,
            CloseFont,
            TextAnchor.MiddleCenter,
            singleLine: true);

        if (UiNative.Button(button))
        {
            Close();
        }
    }
}
