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

    // Identity the shell's own text is attributed to in the fit audit, built once because the concrete
    // type is already known here and a per-frame string would be a per-frame allocation.
    private readonly string chromeScope;
    private readonly string noticeScope;

    protected UiWindowHost()
    {
        chromeScope = GetType().Name + "/chrome";
        noticeScope = GetType().Name + "/notice";

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

    public sealed override void DoWindowContents(Rect inRect)
    {
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

    public override void PreClose()
    {
        // Closing disposes the session with the host, so reopening gets a clean retry.
        host?.Dispose();
        host = null;
        base.PreClose();
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
    /// Trade-off, recorded rather than hidden: the identity is the concrete window TYPE, not a manifest
    /// element path, because the shell has no id and no manifest yet when it paints chrome. Two instances
    /// of one window class therefore share a chrome path; findings are keyed by path and text, so a
    /// second instance's identical finding is deduplicated rather than misattributed. The <c>/chrome</c>
    /// and <c>/notice</c> suffixes keep the two bands apart.
    /// </para>
    /// </summary>
    private void DrawChrome(Rect rect)
    {
        UiFitAudit.BeginElement(chromeScope);
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
        UiFitAudit.BeginElement(noticeScope);
        try
        {
            DrawNotice(content, notice);
        }
        finally
        {
            UiFitAudit.EndElement();
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
