using System;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// The second axis of a treatment, next to the tone. It carries exactly the difference the tone cannot
/// express and nothing wider: on a badge's neutral plane the label is <c>TextSecondary</c>, on a form
/// control's neutral plane it is <c>TextPrimary</c> — the two measured coordinates are a consumer's
/// status badge and this library's own dropdown field and mode-row option. Only the neutral cell differs;
/// every saturated tone paints the same text under either value, and that agreement is the shipped
/// look rather than a claim about a general emphasis scale.
/// </summary>
public enum UiEmphasis
{
    /// <summary>Form-control text: the value the page exists to show. Neutral resolves to TextPrimary.</summary>
    Normal,

    /// <summary>Compact badge text: reads as secondary information. Neutral resolves to TextSecondary.</summary>
    Muted
}

/// <summary>
/// One immutable answer from the resolved-value store: what to fill, what to outline the fill with,
/// and what to write on it. These are values, not rules — a caller paints them and cannot re-derive a
/// different answer from the same key.
/// </summary>
public readonly struct UiResolvedStyle
{
    public UiResolvedStyle(Color fill, Color border, Color text)
    {
        Fill = fill;
        Border = border;
        Text = text;
    }

    /// <summary>The colour the surface fills its rect with.</summary>
    public Color Fill { get; }

    /// <summary>The colour of the surface edge.</summary>
    public Color Border { get; }

    /// <summary>The colour any label painted on this surface uses.</summary>
    public Color Text { get; }

    /// <summary>The fill/border half of this answer, ready for the painting outlet.</summary>
    public UiSurfaceStyle Surface => new(Fill, Border);
}

/// <summary>
/// The resolved-value store: one table whose keys are (tone, emphasis[, writability]) and whose answers
/// are the fill/border/text a surface paints. It exists because the same two-token mapping had been
/// re-derived at four sites — an outlet switch, a badge switch, a dropdown field and a mode-row option —
/// and a table is the only shape in which those sites cannot drift apart: change a surface here and
/// every outlet moves with it.
/// <para>
/// Scope: one store per <see cref="UiTheme"/> instance, built by the theme's constructor and never
/// replaced or mutated afterwards. It is a read-through view of that theme's tokens, so a consumer that
/// re-tints the theme sees the new value on the next query and no second copy of a colour exists. The
/// injecting host holds one theme per window, which makes a store per-window in practice; it is
/// deliberately not a session type (it holds no element state) and it is never process-wide: nothing
/// here is static, and two themes never share a store.
/// </para>
/// <para>
/// Failure policy: a key that is not a declared value falls back instead of throwing — an appearance
/// error must not take a page down — and the fallback is recorded, because fail-soft must not mean
/// silent. The record is what a lane asserts and what a host can log.
/// </para>
/// </summary>
public sealed class UiStyleTable
{
    private readonly UiTheme theme;
    private int fallbackCount;
    private string? lastFallback;

    internal UiStyleTable(UiTheme theme)
    {
        this.theme = theme ?? throw new ArgumentNullException(nameof(theme));
    }

    /// <summary>
    /// The one font a pass must resolve: the size the drawing outlet writes with and the size the
    /// fitting audit measures with. It is read through the theme rather than copied here, so a density
    /// change cannot leave the draw and the measure looking at two different sizes.
    /// </summary>
    public UiFont Font => theme.DefaultFont;

    /// <summary>How many queries have fallen back since this store was built.</summary>
    public int FallbackCount => fallbackCount;

    /// <summary>
    /// The diagnostic of the most recent fallback, or null when nothing has fallen back. Cumulative
    /// like <see cref="FallbackCount"/>: it is a report to read, not frame state to poll.
    /// </summary>
    public string? LastFallbackDiagnostic => lastFallback;

    /// <summary>
    /// The answer for one role. <paramref name="emphasis"/> carries the badge-versus-field text
    /// difference the tone cannot express.
    /// <para>
    /// <paramref name="writable"/> is the third key slot, reserved for the read side of the bindings.
    /// False means the element's data side says it cannot be written, and the table answers with the
    /// disabled treatment whatever role was authored: state beats author, written down rather than
    /// discovered per widget. Null means "not known", which resolves by tone alone.
    /// </para>
    /// </summary>
    public UiResolvedStyle Resolve(UiStatusTone tone, UiEmphasis emphasis = UiEmphasis.Normal, bool? writable = null)
    {
        UiEmphasis resolvedEmphasis = ResolveEmphasis(emphasis);
        return Cell(writable == false ? UiStatusTone.Disabled : tone, resolvedEmphasis);
    }

    private UiResolvedStyle Cell(UiStatusTone tone, UiEmphasis emphasis)
    {
        switch (tone)
        {
            case UiStatusTone.Active:
                return From(theme.SelectedSurface, theme.TextOnGold);
            case UiStatusTone.Success:
                return From(theme.SuccessSurface, theme.TextOnGold);
            case UiStatusTone.Warning:
            case UiStatusTone.Danger:
                // One treatment for two tone names on purpose: the bag carried them as one RGB with no
                // user in either repository, and a name nobody shapes apart is debt, so one went. A
                // citation that needs them distinguishable adds a surface here and the rows divide.
                return From(theme.DangerSurface, theme.TextOnDanger);
            case UiStatusTone.Disabled:
                return new UiResolvedStyle(theme.BaseSurface.Fill, theme.Divider, theme.TextDisabled);
            case UiStatusTone.Neutral:
                return Neutral(emphasis);
            default:
                Record("UiStatusTone " + ((int)tone).ToString() + " is not a declared tone; fell back to UiStatusTone.Neutral.");
                return Neutral(emphasis);
        }
    }

    private UiResolvedStyle Neutral(UiEmphasis emphasis)
    {
        UiSurfaceStyle surface = theme.RaisedSurface;
        return new UiResolvedStyle(
            surface.Fill,
            surface.Border,
            emphasis == UiEmphasis.Muted ? theme.TextSecondary : theme.TextPrimary);
    }

    private static UiResolvedStyle From(UiSurfaceStyle surface, Color text)
    {
        return new UiResolvedStyle(surface.Fill, surface.Border, text);
    }

    private UiEmphasis ResolveEmphasis(UiEmphasis emphasis)
    {
        switch (emphasis)
        {
            case UiEmphasis.Normal:
                return UiEmphasis.Normal;
            case UiEmphasis.Muted:
                return UiEmphasis.Muted;
            default:
                Record("UiEmphasis " + ((int)emphasis).ToString() + " is not a declared emphasis; fell back to UiEmphasis.Normal.");
                return UiEmphasis.Normal;
        }
    }

    private void Record(string diagnostic)
    {
        fallbackCount++;
        lastFallback = diagnostic;
    }
}
