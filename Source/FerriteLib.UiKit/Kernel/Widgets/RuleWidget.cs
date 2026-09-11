using System;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel.Widgets;

/// <summary>
/// Core leaf atom <c>chrome/rule</c>: the horizontal hairline that separates two blocks of a page.
/// <para>
/// <b>Why this earns a kind</b> — the geometry rule. Nothing else in the vocabulary can paint a
/// token: a leaf draws text or it is a container with a chrome surface, and a container's surface is
/// a filled band, not a one-pixel line on the divider token. This element owns three facts a manifest
/// cannot state — its natural size is the hairline itself (never a text band, never a declaration),
/// the line sits on the vertical centre of whatever band it was given, and it spans the arranged width
/// inset by <c>Inset</c> on both sides. It also owns a negative rule: it never takes input, so an
/// author may place it over a hit region without stealing clicks.
/// </para>
/// </summary>
public sealed class RuleWidget : IUiWidget
{
    public const string Kind = "chrome/rule";

    private UiElementSpec spec = UiElementSpec.Empty;

    string IUiWidget.Kind => Kind;

    public static void Register()
    {
        UiWidgetRegistry.Register(
            UiWidgetRegistry.CoreScope,
            Kind,
            () => new RuleWidget(),
            AtomVocabulary.Schema(AtomRoles.Tone, "Thickness", "Inset", "Height"));
        // Deliberately no label set: a rule carries no text, so Width="Auto" has nothing honest to
        // measure here and falls back to the unsized distribution. Registering a label set for it
        // would make the Auto seam report a width this kind never draws.
    }

    public void Configure(UiElementSpec spec)
    {
        this.spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public void Validate(IUiBindings bindings, string elementPath)
    {
        // No binding: a rule is not interactive and carries no value.
    }

    public float Measure(UiWidgetContext ctx)
    {
        return ReadThickness(ctx);
    }

    public void Draw(Rect rect, UiWidgetContext ctx)
    {
        if (rect.width <= 0f || rect.height <= 0f) return;

        // A hairline band is legal input (the default band is one pixel tall), so this guard is the
        // degenerate one and not the "too small to bother" one the text atoms use.
        float thickness = Math.Min(ReadThickness(ctx), rect.height);
        float inset = Math.Max(0f, AtomVocabulary.ReadFloat(spec, "Inset", 0f));
        float width = rect.width - inset * 2f;
        if (width <= 0f) return;

        // The untoned rule paints the chrome hairline token; an authored Tone opts it into that
        // treatment's edge colour instead — which is why Tone="Neutral" is visibly different from no
        // tone on this one kind, and why that difference is written down rather than smoothed over.
        // Emphasis is not in this kind's schema at all: it is a text-colour axis, a rule paints no
        // text, and an attribute that cannot move a pixel is the silent no-op creation-time validation
        // exists to refuse.
        Color color = AtomVocabulary.Read(spec, AtomVocabulary.ToneAttribute).Trim().Length > 0
            ? AtomVocabulary.ResolveRole(spec, ctx, writable: null).Border
            : ctx.Theme.Divider;

        UiThemeDraw.Solid(
            new Rect(rect.x + inset, rect.y + (rect.height - thickness) * 0.5f, width, thickness),
            color);
    }

    private float ReadThickness(UiWidgetContext ctx)
    {
        return Math.Max(1f, AtomVocabulary.ReadFloat(spec, "Thickness", ctx.Theme.Geometry.Hairline));
    }
}
