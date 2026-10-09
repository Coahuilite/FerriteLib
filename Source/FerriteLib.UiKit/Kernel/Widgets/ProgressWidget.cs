using System;
using System.Globalization;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel.Widgets;

/// <summary>
/// Core kind <c>display/progress</c>: one read-only proportion over one <c>float</c> value binding.
/// <para>
/// <b>Why this earns a kind</b> - a value contract over its own content and a geometry rule, with no hit
/// rule at all. It owns the value grammar (a <c>float</c>, read as a fraction of a declared <c>Max</c> and
/// clamped into the track), the measure contract (one fixed band, so a value that moves never moves a
/// rect - which is exactly the <see cref="UiInvalidation.Paint"/> case P2 built the class for), and the
/// fill geometry (inset by the hairline so the track edge is never painted over). A manifest can state
/// none of the three, and the two containers cannot express a partial fill.
/// </para>
/// <para>
/// <b>It never takes input, on purpose.</b> A progress readout that stole the pointer would be the same
/// defect a rule has: an element whose colour communicates, placed anywhere, must not become a
/// dead click target. There is no <c>UiNative.Button</c> call here.
/// </para>
/// <para>
/// Fail-soft like the checkbox: an unbound or mistyped key paints an empty track and records one
/// deduplicated appearance fallback, because a row whose item-local value is not bound yet is a legal
/// frame inside a repeater template.
/// </para>
/// </summary>
internal sealed class ProgressWidget : IUiWidget
{
    internal const string Kind = "display/progress";

    /// <summary>Fraction denominator used when the element declares no <c>Max</c>.</summary>
    private const float DefaultMax = 1f;

    private UiElementSpec spec = UiElementSpec.Empty;

    string IUiWidget.Kind => Kind;

    internal static void Register()
    {
        // Tone only: this kind paints no text, and an attribute that cannot move a pixel is the silent
        // no-op the creation-time contract exists to refuse.
        UiWidgetRegistry.Register(
            UiWidgetRegistry.CoreScope,
            Kind,
            () => new ProgressWidget(),
            AtomVocabulary.Schema(AtomRoles.Tone, "Bind", "Max", "Height"));
        // Deliberately no label set: the proportion is drawn as a fill, so Width="Auto" has no honest text
        // to measure here and falls back to the unsized distribution.
    }

    public void Configure(UiElementSpec spec)
    {
        this.spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    /// <summary>
    /// The creation-time contract: the read-only float value binding, plus a declared <c>Max</c> that has
    /// to be a positive number if it is written at all. A malformed <c>Max</c> is refused here rather than
    /// degraded per frame, because the element still knows its own path at creation time.
    /// </summary>
    public void Validate(IUiBindings bindings, string elementPath)
    {
        string key = ReadBindKey();
        if (key.Length == 0)
        {
            throw new InvalidOperationException(
                "ProgressWidget at '" + elementPath + "' requires a Bind naming the float value binding it "
                + "reads; a progress bar with no binding has no value contract.");
        }

        bindings.ValidateValue<float>(key, elementPath);

        if (spec.TryGetAttribute("Max", out string raw) && raw.Trim().Length > 0
            && (!float.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float declared)
                || declared <= 0f))
        {
            throw new InvalidOperationException(
                "ProgressWidget at '" + elementPath + "' declares Max '" + raw + "'; expected a positive number.");
        }
    }

    public float Measure(UiWidgetContext ctx)
    {
        // One fixed band: the value is a fraction of it and never a measurement input, which is what makes
        // a progress announcement a Paint-class change rather than a re-arrange.
        return AtomVocabulary.ReadHeight(spec, ctx.Theme.Geometry.RowHeight);
    }

    public void Draw(Rect rect, UiWidgetContext ctx)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;

        float value = ReadValue(ctx, ReadBindKey());
        float max = ReadMax();
        float fraction = Math.Max(0f, Math.Min(1f, value / max));

        UiTheme theme = ctx.Theme;
        float edge = Math.Max(1f, theme.Geometry.Hairline);
        UiResolvedStyle style = AtomVocabulary.ResolveRole(spec, ctx, writable: null);

        UiThemeDraw.Surface(rect, theme.BaseSurface, edge);

        float innerWidth = rect.width - edge * 2f;
        float innerHeight = rect.height - edge * 2f;
        if (innerWidth <= 0f || innerHeight <= 0f) return;

        float fillWidth = fraction <= 0f ? 0f : Math.Max(edge, innerWidth * fraction);
        UiThemeDraw.Solid(new Rect(rect.x + edge, rect.y + edge, fillWidth, innerHeight), style.Fill);
    }

    private float ReadMax()
    {
        if (spec.TryGetAttribute("Max", out string raw)
            && float.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float declared)
            && declared > 0f)
        {
            return declared;
        }

        return DefaultMax;
    }

    /// <summary>
    /// The proportion read, under the same contract as the checkbox: an absent key and a key bound to another
    /// type both paint the empty track and are recorded once (<see cref="AtomVocabulary.ReadOr{T}"/>), so an
    /// item-local value that is not there yet cannot take the page down.
    /// </summary>
    private float ReadValue(UiWidgetContext ctx, string key)
    {
        return AtomVocabulary.ReadOr(ctx, Kind, key, 0f, "0");
    }

    private string ReadBindKey()
    {
        return AtomVocabulary.ReadBindKey(spec);
    }
}
