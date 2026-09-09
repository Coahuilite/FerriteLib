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

    /// <summary>Size of the close affordance.</summary>
    protected virtual Vector2 CloseButtonSize => new Vector2(110f, 30f);

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
            DrawNotice(content, UiWindowNotice.PageUnavailable);
            return;
        }

        if (!PrerequisiteVerified)
        {
            DrawNotice(content, UiWindowNotice.Prerequisite);
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
            DrawNotice(content, UiWindowNotice.PageUnavailable);
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

    private void DrawChrome(Rect rect)
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
