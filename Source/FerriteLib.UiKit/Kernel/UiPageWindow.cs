using System;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// The concrete page shell: one instantiable window that hosts an ordinary XML page, so a page no longer
/// needs a bespoke C# <see cref="Verse.Window"/> subclass to exist.
/// <para>
/// <b>What the consumer supplies.</b> A manifest (or, once the document service lands, the manifest a
/// document resolved to), typed bindings, a theme, a translation seam, and every visible word - title,
/// close label and the notice text for each <see cref="UiWindowNotice"/> - because the library owns zero
/// strings. Everything else is the inherited shell: chrome, one <see cref="UiHost"/>, the deferred
/// notice contract and the keyed identity the catalog gives it.
/// </para>
/// <para>
/// <b>What it does not take.</b> Modality and size are <see cref="UiWindowOptions"/> on the
/// registration, not constructor arguments: one policy per kind belongs with the kind, and a second
/// place to set it would be a second source of truth. Constructing a page shell outside a catalog is
/// legal - it draws - but the options then come from nowhere, which is exactly how the catalog is the
/// entry point.
/// </para>
/// </summary>
public sealed class UiPageWindow : UiWindowHost
{
    private readonly UiWindowKey key;
    private readonly UiLayoutManifest manifest;
    private readonly IUiBindings bindings;
    private readonly UiTheme theme;
    private readonly IUiTranslation translation;
    private readonly ITextMetrics metrics;
    private readonly UiStyleDocument? document;
    private readonly string title;
    private readonly string closeText;
    private readonly Func<UiWindowNotice, string> noticeText;

    public UiPageWindow(
        UiWindowKey key,
        UiLayoutManifest manifest,
        IUiBindings bindings,
        UiTheme theme,
        IUiTranslation translation,
        string title,
        string closeText,
        Func<UiWindowNotice, string> noticeText,
        ITextMetrics? metrics = null,
        UiStyleDocument? document = null)
    {
        this.key = key;
        this.manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        this.bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
        this.theme = theme ?? throw new ArgumentNullException(nameof(theme));
        this.translation = translation ?? throw new ArgumentNullException(nameof(translation));
        this.title = title ?? "";
        this.closeText = closeText ?? "";
        this.noticeText = noticeText ?? throw new ArgumentNullException(nameof(noticeText));
        this.metrics = metrics ?? VerseFerriteTextMetrics.Instance;
        this.document = document;

        // Set here as well as by the catalog so a shell built outside one still names itself in the fit
        // audit; the catalog overwrites it only with the same value (a mismatched factory is refused).
        Key = key;
    }

    /// <summary>
    /// The page engine this window hosts, or null before the first draw pass and after a failure disposed
    /// it. The base shell builds the host lazily inside its guarded pass, so a consumer reads this after
    /// the window has drawn at least once.
    /// <para>
    /// <b>Why this exists at all.</b> The inherited host property is protected and this class is sealed, so
    /// a consumer holding the generic shell had no way to reach its own page's engine - and therefore no
    /// way to opt the page into per-host diagnostics (<see cref="UiHost.Diagnostics"/>) or to attach a
    /// reload report subscription. Two ordinary XML pages then collapsed into the one process-wide channel,
    /// which is the isolation defect this shell was most exposed to precisely because it is the shape most
    /// consumers use.
    /// </para>
    /// <para>
    /// <b>Read-only and side-effect free.</b> It never creates a host and never subscribes anything:
    /// diagnostics stay opt-in, and a page nobody subscribed to stays exactly as cheap and quiet as before.
    /// It does not widen <see cref="UiWindowHost"/>'s protected surface, which remains the only door a
    /// subclass has.
    /// </para>
    /// </summary>
    public UiHost? PageHost => Host;

    protected override UiTheme Theme => theme;

    protected override string Title => title;

    protected override string CloseText => closeText;

    protected override ITextMetrics Metrics => metrics;

    /// <summary>
    /// The registration's <see cref="UiWindowOptions.InitialSize"/> when it has one; null otherwise, which
    /// keeps the game's own <c>Window.InitialSize</c> rather than a library number.
    /// </summary>
    protected override Func<Vector2>? InitialSizePolicy
    {
        get
        {
            Vector2? size = AppliedOptions?.InitialSize;
            return size.HasValue ? () => size.Value : (Func<Vector2>?)null;
        }
    }

    /// <summary>The registration's close veto, or "allowed" when there is none.</summary>
    protected override bool CanClose()
    {
        Func<UiWindowKey, bool>? veto = AppliedOptions?.CanClose;
        return veto == null || veto(key);
    }

    protected override UiHost CreateHost()
    {
        // The page's identity for contract errors, the audit and the host ledger is the registration,
        // not the context key: two contexts of one page are one authored document.
        string source = key.Consumer + "/" + key.WindowKind;
        return new UiHost(source, manifest, bindings, theme, metrics, translation, document);
    }

    protected override void DrawNotice(Rect rect, UiWindowNotice notice)
    {
        UiThemeDraw.Label(rect, noticeText(notice), theme, singleLine: false);
    }
}
