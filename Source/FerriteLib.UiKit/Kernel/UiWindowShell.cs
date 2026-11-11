using System;

namespace FerriteLib.UiKit.Kernel;

/// <summary>Why the formal window shell cannot show its page. Notice text belongs to the author.</summary>
public enum UiWindowNotice
{
    Prerequisite,
    PageUnavailable
}

/// <summary>
/// The formal window face and its single page lifetime, shared by the native window and isolated hosts.
/// Execution, text resources, documents and business state are borrowed. Dispose this shell before its execution owner.
/// </summary>
public abstract class UiWindowShell : IDisposable
{
    public const int ContractVersion = 1;
    private UiHost? host;
    private bool pageUnavailable, noticeDueNextFrame, closed, hostCreated;
    private bool releasingHost, disposing, closeNotified;
    private bool displayRequired = true;
    private IUiExecution? execution, closeExecution;
    private IUiDrawing? measurementDrawing;
    private UiFrame? frame;
    private string? chromeScope, noticeScope;
    private readonly UiControlKey closeControl = new("window-shell/" + Guid.NewGuid().ToString("N") + "/close");

    protected abstract UiTheme Theme { get; }
    protected abstract ITextMetrics Metrics { get; }
    protected virtual ITextMetrics TextMetrics => Metrics;
    protected virtual IUiDrawing? TextDrawing => measurementDrawing ?? execution;
    protected IUiExecution Execution => execution ?? throw new InvalidOperationException("Window painting requires its synchronous Draw call.");
    protected virtual UiHost CreateHost() => throw new InvalidOperationException("Supply a page factory or adopt the preview's existing Host.");
    protected abstract void DrawNotice(UiLayoutRect rect, UiWindowNotice notice);
    protected virtual string Title => "";
    protected virtual string Subtitle => "";
    protected virtual string CloseText => "";
    protected virtual bool PrerequisiteVerified => true;
    protected virtual string ScopeIdentity => GetType().Name;
    protected virtual void BeforeDraw(UiLayoutRect content) { }
    protected virtual void OnDrawFailure(Exception error) { }
    protected virtual void DrawBackground(UiLayoutRect rect) { }
    protected virtual void RequestClose() => Dispose();
    protected virtual UiFont TitleFont => UiFont.Medium;
    protected virtual UiFont SubtitleFont => UiFont.Tiny;
    protected virtual UiFont CloseFont => UiFont.Tiny;
    protected virtual float SidePadding => 20f;
    protected virtual float AccentBarHeight => 3f;
    protected virtual float TitleBarHeight => DefaultTitleBarHeight;
    protected virtual UiLayoutPoint CloseButtonSize => DefaultCloseButtonSize;

    // The native bridge also uses these defaults, so legacy overrides replace the same computation.
    internal float DefaultTitleBarHeight => Theme.ReadFontSize.HasValue
        ? Math.Max(56f, Math.Max(6f + ChromeTextHeight(TitleFont, 30f) + (Subtitle.Length > 0 ? ChromeTextHeight(SubtitleFont, 18f) : 0f), ChromeTextHeight(CloseFont, 30f) + 16f))
        : 56f;
    internal UiLayoutPoint DefaultCloseButtonSize
    {
        get
        {
            UiTextRun text = UiTextFonts.Resolve(TextMetrics, TextDrawing, new UiTextFont(CloseFont, Theme.ReadFontSize), elementPath: ChromeScope);
            return new UiLayoutPoint(Math.Max(110f, text.MeasureWidth(CloseText) + 10f * 2f),
                text.Font.Size.HasValue ? Math.Max(30f, text.MeasureText("Ag", 100000) + Theme.Geometry.Spacing * 2) : 30f);
        }
    }

    public UiHost? Host => host;
    public Exception? LastFailure { get; private set; }
    public bool IsClosed => closed;
    public bool DisplayRequired => displayRequired;
    public event Action<UiHost>? HostAttached;
    public event Action<UiHost>? HostDetached;
    internal event Action? Closed;

    public void SetDisplayDemand(bool required)
    {
        displayRequired = required;
        host?.SetDisplayDemand(required);
        if (!required)
        {
            try { closeExecution?.Release(closeControl); }
            finally { closeExecution = null; }
        }
    }

    // The retained native Window may be reused after PreClose without another PreOpen. Keep that
    // original entry condition; isolated preview instances remain terminal after Dispose.
    internal void ReopenNative()
    {
        if (!closed || execution != null) return;
        closed = false;
        hostCreated = false;
        closeNotified = false;
    }

    /// <summary>The actual page box, in the same local coordinates as the window face.</summary>
    public UiLayoutRect ContentRect(UiLayoutRect face) => new(face.X + SidePadding, face.Y + TitleBarHeight,
        Math.Max(1f, face.Width - SidePadding * 2f), Math.Max(1f, face.Height - TitleBarHeight - SidePadding));

    /// <summary>Measure outside a draw with the actual paired text capability, including numeric fonts.</summary>
    public UiLayoutRect ContentRect(UiLayoutRect face, IUiDrawing drawing)
    {
        IUiDrawing? previous = measurementDrawing;
        measurementDrawing = drawing ?? throw new ArgumentNullException(nameof(drawing));
        try { return ContentRect(face); }
        finally { measurementDrawing = previous; }
    }

    /// <summary>Present geometry in the caller's outer/window space; completed draws retain their own snapshot.</summary>
    public UiWindowChrome GetChrome(UiLayoutRect outer)
    {
        var face = new UiLayoutRect(0, 0, outer.Width, outer.Height);
        UiLayoutRect Lift(UiLayoutRect local) => new(outer.X + local.X, outer.Y + local.Y, local.Width, local.Height);
        return new UiWindowChrome(outer, Lift(TitleBandRect(face)), Lift(CloseButtonRect(face)), Lift(ContentRect(face)), SidePadding, TitleBarHeight);
    }

    /// <summary>Present geometry measured with the actual paired text capability, without drawing or advancing the page.</summary>
    public UiWindowChrome GetChrome(UiLayoutRect outer, IUiDrawing drawing)
    {
        IUiDrawing? previous = measurementDrawing;
        measurementDrawing = drawing ?? throw new ArgumentNullException(nameof(drawing));
        try { return GetChrome(outer); }
        finally { measurementDrawing = previous; }
    }

    /// <summary>One synchronous owner pass. The supplied execution includes the owner's paired text capabilities.</summary>
    public void Draw(UiLayoutRect face, UiFrame input, IUiExecution drawing, UiLayoutPoint windowOrigin = default)
    {
        if (closed || !displayRequired) return;
        if (execution != null) throw new InvalidOperationException("A window shell cannot recursively draw itself.");
        frame = input ?? throw new ArgumentNullException(nameof(input));
        execution = drawing ?? throw new ArgumentNullException(nameof(drawing));
        try
        {
            using (UiDiagnosticHub.EnterHost(host?.CurrentDiagnostics, host == null ? null : TextMetrics, host?.Session))
                DrawShell(face, windowOrigin);
        }
        finally { execution = null; frame = null; }
    }

    // Native adapters capture after chrome/BeforeDraw/attach, retaining the original event qualification.
    protected virtual UiFrame CaptureFrame() => frame!;
    protected virtual bool CloseHovered(UiLayoutRect button) => frame!.InputAllowed && button.Contains(frame.Pointer);
    protected virtual bool ClosePressed(UiLayoutRect button)
    {
        UiFrame input = frame!;
        if (closeExecution != null && !ReferenceEquals(closeExecution, Execution))
        {
            try { closeExecution.Release(closeControl); }
            finally { closeExecution = null; }
        }
        Execution.PrepareControl(closeControl);
        bool held = Execution.HasCapture(closeControl);
        if (!input.InputAllowed)
        {
            if (held) Execution.Release(closeControl);
            closeExecution = null;
            return false;
        }
        if (input.Phase == UiFramePhase.MouseDown && input.Button == 0 && CloseHovered(button) && Execution.CaptureAvailable)
        {
            Execution.Capture(closeControl); closeExecution = Execution;
            input.Consume(); Execution.ConsumeEvent();
        }
        else if (input.Phase == UiFramePhase.MouseUp && input.Button == 0 && held)
        {
            Execution.Release(closeControl); closeExecution = null;
            input.Consume(); Execution.ConsumeEvent();
            return CloseHovered(button);
        }
        return false;
    }

    private void DrawShell(UiLayoutRect face, UiLayoutPoint windowOrigin)
    {
        if (!DrawChrome(face) || closed) return;
#if FER_DEV || FER_TOOLS
        UiWindowChrome chromeNow = GetChrome(new UiLayoutRect(windowOrigin.X + face.X, windowOrigin.Y + face.Y, face.Width, face.Height));
        UiLayoutRect titleNow = TitleBandRect(face), closeNow = CloseButtonRect(face);
#endif
        UiLayoutRect content = ContentRect(face);
        BeforeDraw(content);
        if (closed) return;
        if (pageUnavailable) { DrawNoticeScoped(content, UiWindowNotice.PageUnavailable); return; }
        if (!PrerequisiteVerified) { DrawNoticeScoped(content, UiWindowNotice.Prerequisite); return; }
        if (noticeDueNextFrame)
        {
            noticeDueNextFrame = false; pageUnavailable = true;
            DrawNoticeScoped(content, UiWindowNotice.PageUnavailable); return;
        }
        try
        {
            if (host == null) AttachHost(CreateHost());
            // An attach handler or original owner may close the window synchronously.
            if (host == null || closed) return;
            host.DrawFrame(content, CaptureFrame(), Execution);
#if FER_DEV || FER_TOOLS
            using (UiDiagnosticHub.EnterHost(host?.CurrentDiagnostics, TextMetrics))
            {
                UiDevGeometryProbe.RecordShellChrome(chromeNow);
                UiDevGeometryProbe.OutlineShellChrome(Execution, face, titleNow, closeNow);
            }
#endif
        }
        catch (Exception error) { RecordDrawFailure(error); }
    }

    // Preview adoption transfers its already-created, identical Host; no second tree/factory/advance.
    internal void RequireAvailable()
    {
        if (closed || hostCreated || pageUnavailable || noticeDueNextFrame)
            throw new InvalidOperationException("A window shell can own only one page lifetime.");
    }
    internal void AdoptPreviewHost(UiHost page)
    {
        RequireAvailable();
        try { AttachHost(page); }
        catch (Exception error) { RecordDrawFailure(error); throw; }
    }
    private void AttachHost(UiHost page)
    {
        if (hostCreated) throw new InvalidOperationException("A window shell cannot recreate its page.");
        hostCreated = true;
        if (closed) { page?.Dispose(); return; }
        host = page ?? throw new InvalidOperationException("The window page factory returned null.");
        // Own before calling metrics/attachments, so failure releases even a partially attached Host.
        page.AdoptTextMetrics(Metrics, TextMetrics);
        page.SetDisplayDemand(displayRequired);
        HostAttached?.Invoke(page);
    }
    private void RecordDrawFailure(Exception error)
    {
        noticeDueNextFrame = true; LastFailure = error;
        host?.PublishWindowFailure(error);
        ReleaseHost();
        OnDrawFailure(error);
    }
    private void ReleaseHost()
    {
        UiHost? released = host;
        host = null;
        if (released == null) return;
        releasingHost = true;
        try { HostDetached?.Invoke(released); }
        catch (Exception error)
        {
            if (LastFailure == null) LastFailure = error;
            released.PublishWindowFailure(error, cleanup: true);
        }
        finally
        {
            try { released.Dispose(); }
            finally
            {
                releasingHost = false;
                if (closed && !disposing) NotifyClosed();
            }
        }
    }
    public void Dispose()
    {
        if (closed) return;
        closed = true;
        disposing = true;
        try { ReleaseHost(); }
        finally
        {
            try { closeExecution?.Release(closeControl); }
            finally { closeExecution = null; disposing = false; if (!releasingHost) NotifyClosed(); }
        }
    }
    private void NotifyClosed()
    {
        if (closeNotified) return;
        closeNotified = true;
        Closed?.Invoke();
    }

    private string ChromeScope => chromeScope ??= ScopeIdentity + "/chrome";
    private string NoticeScope => noticeScope ??= ScopeIdentity + "/notice";
    private bool DrawChrome(UiLayoutRect rect)
    {
        UiFitAudit.BeginElement(ChromeScope);
        try { return DrawChromeCore(rect); }
        finally { UiFitAudit.EndElement(); }
    }
    private void DrawNoticeScoped(UiLayoutRect rect, UiWindowNotice notice)
    {
        UiFitAudit.BeginElement(NoticeScope);
        try { DrawNotice(rect, notice); }
        finally { UiFitAudit.EndElement(); }
    }
    private bool DrawChromeCore(UiLayoutRect rect)
    {
        UiTheme theme = Theme;
        UiThemeDraw.Workspace(Execution, rect, theme);
        bool backgroundSucceeded = true;
        if (!pageUnavailable && !noticeDueNextFrame)
        {
            try { UiSessionGuard.DrawWindowBackground(Execution, rect, DrawBackground); }
            catch (Exception error) { RecordDrawFailure(error); backgroundSucceeded = false; }
        }
        if (closed) return false;
        UiThemeDraw.Solid(Execution, new UiLayoutRect(rect.X, rect.Y, rect.Width, AccentBarHeight), theme[UiPaletteRole.Accent]);
        if (Title.Length > 0)
            DrawShellText(new UiLayoutRect(rect.X + SidePadding, rect.Y + 6f, Math.Max(1f, rect.Width * 0.6f), ChromeTextHeight(TitleFont, 30f)),
                Title, theme.Styles.Resolve(UiStatusTone.Neutral).Text, TitleFont, ChromeScope);
        if (Subtitle.Length > 0)
            DrawShellText(new UiLayoutRect(rect.X + SidePadding, rect.Y + 6f + ChromeTextHeight(TitleFont, 30f), Math.Max(1f, rect.Width * 0.6f), ChromeTextHeight(SubtitleFont, 18f)),
                Subtitle, theme.Styles.Resolve(UiStatusTone.Neutral, UiEmphasis.Muted).Text, SubtitleFont, ChromeScope);
        DrawCloseButton(rect);
        return backgroundSucceeded;
    }
    private void DrawCloseButton(UiLayoutRect face)
    {
        UiTheme theme = Theme;
        UiLayoutRect button = CloseButtonRect(face);
        bool hovered = CloseHovered(button);
        UiThemeDraw.Surface(Execution, button, theme,
            hovered ? theme[UiPaletteRole.HoverSurface] : theme[UiPaletteRole.PanelSurface],
            hovered ? theme[UiPaletteRole.StrongBorder] : theme[UiPaletteRole.Border]);
        DrawShellText(button, CloseText, theme.Styles.Resolve(UiStatusTone.Neutral, hovered ? UiEmphasis.Normal : UiEmphasis.Muted).Text,
            CloseFont, ChromeScope, UiTextAnchor.MiddleCenter);
        if (ClosePressed(button)) RequestClose();
    }
    private void DrawShellText(UiLayoutRect rect, string text, UiColour colour, UiFont font, string path,
        UiTextAnchor anchor = UiTextAnchor.MiddleLeft, bool singleLine = true)
        => UiTextFonts.Resolve(TextMetrics, TextDrawing, new UiTextFont(font, Theme.ReadFontSize), elementPath: path).Draw(rect, text, colour, anchor, singleLine);
    private float ChromeTextHeight(UiFont font, float minimum)
    {
        if (!Theme.ReadFontSize.HasValue) return minimum;
        UiTextRun text = UiTextFonts.Resolve(TextMetrics, TextDrawing, new UiTextFont(font, Theme.ReadFontSize), elementPath: ChromeScope);
        return text.Font.Size.HasValue ? Math.Max(minimum, text.MeasureText("Ag", 100000) + Theme.Geometry.Spacing * 2) : minimum;
    }
    internal void DrawPageNotice(UiLayoutRect rect, string text)
        => DrawShellText(rect, text, Theme.Styles.Resolve(UiStatusTone.Neutral).Text, Theme.Styles.Font, NoticeScope, singleLine: false);
    private UiLayoutRect CloseButtonRect(UiLayoutRect face)
    {
        UiLayoutPoint size = CloseButtonSize;
        return new UiLayoutRect(face.XMax - size.X - SidePadding, face.Y + (TitleBarHeight - size.Y) * 0.5f, size.X, size.Y);
    }
    private UiLayoutRect TitleBandRect(UiLayoutRect face) => new(face.X, face.Y, face.Width, TitleBarHeight);
}
