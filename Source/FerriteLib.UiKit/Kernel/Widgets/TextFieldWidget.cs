using System;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel.Widgets;

/// <summary>
/// Core leaf atom <c>input/text-field</c>: one string, edited in place, committed under a rule.
/// <para>
/// <b>Why this earns a kind</b> — the same reason its numeric sibling does, and nothing wider: per-element
/// interaction state no manifest attribute can carry. Focus, the in-progress draft, the placeholder's
/// visibility, and the moment an edit becomes a value all belong to the element rather than to the page,
/// and they live in the session's value bag, which is why this atom sharing a binding key with anything
/// else sees one value instead of two. A page that hand-rolls a text box outside the tree loses the
/// disabled refusal, the recovery slot and the fit audit along with it.
/// </para>
/// <para>
/// Scope, stated because it is a boundary rather than an omission: this is a <b>single-line</b> field. It
/// draws no label band of its own choosing, no selection model, no clipboard behaviour and no multiline
/// wrapping — those are the backend's text field, which this kind drives through the one funnel primitive.
/// What it adds is identity: the draft and the focus are the element's, and a read-only binding stops the
/// write at the same funnel every other interactive kind uses.
/// </para>
/// </summary>
public sealed class TextFieldWidget : IUiWidget
{
    public const string Kind = "input/text-field";

    private UiElementSpec spec = UiElementSpec.Empty;

    string IUiWidget.Kind => Kind;

    public static void Register()
    {
        UiWidgetRegistry.Register(
            UiWidgetRegistry.CoreScope,
            Kind,
            () => new TextFieldWidget(),
            // The label set is the text this kind paints as its own label. The placeholder is NOT in it:
            // it is a hint painted inside the field only while the field is empty, never the element's
            // declared width (B8's rule is "the label set equals what the kind paints *as its label*",
            // not "every string it can paint").
            AtomVocabulary.Schema(
                AtomRoles.ToneAndEmphasis,
                "Bind", "Live", "Placeholder", "PlaceholderKey", "Label", "LabelKey", "Height"),
            new[] { "Label", "LabelKey" });
    }

    public void Configure(UiElementSpec spec)
    {
        this.spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public void Validate(IUiBindings bindings, string elementPath)
    {
        string bindKey = AtomVocabulary.ReadBindKey(spec);
        if (string.IsNullOrEmpty(bindKey))
        {
            throw new InvalidOperationException(
                $"TextFieldWidget at '{elementPath}' requires a non-empty Id or Bind to use as its typed binding key.");
        }

        bindings.ValidateValue<string>(bindKey, elementPath);
    }

    public float Measure(UiWidgetContext ctx)
    {
        return AtomVocabulary.ReadHeight(spec, ctx.Theme.Geometry.RowHeight);
    }

    public void Draw(Rect rect, UiWidgetContext ctx)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;

        string bindKey = AtomVocabulary.ReadBindKey(spec);
        bool live = AtomVocabulary.ReadBool(spec, "Live", true);
        string current = ctx.Bindings.TryGet(bindKey, out string bound) ? bound ?? "" : "";

        Rect field = rect;
        ResolveLabelBand(rect, ctx, out field);
        bool? writable = AtomVocabulary.WritableOf(ctx, bindKey);
        UiResolvedStyle style = AtomVocabulary.ResolveRole(spec, ctx, writable);
        DrawLabel(rect, ctx, style);

        // One funnel call carries the whole interaction: the disabled refusal, focus, the draft, and the
        // commit rule. The returned string is what the field is actually showing, so the placeholder test
        // below reads the truth rather than the model.
        string typed = UiNative.TextField(field, bindKey, ctx.Session, current, out bool committed);

        DrawPlaceholder(field, typed, ctx, writable);

        if (!committed) return;

        // A read-only binding stops here, exactly as it does on the numeric sibling: the draft keeps what
        // was typed, nothing is written, and the field still draws. The recovery band is not the fallback
        // for a read-only key.
        if (writable == false) return;

        // Live=false defers the write while the field holds focus, because an editor that writes on every
        // keystroke cannot be typed into. The deferred write lands when focus leaves - the funnel reports
        // that frame as a commit - which is why the rule is read from the session's focus, not guessed
        // from the returned text.
        if (live || !ctx.Session.GetOrCreateValueState(bindKey).Focused)
        {
            ctx.Bindings.Set(bindKey, typed);
        }
    }

    /// <summary>
    /// The hint painted inside an empty, unfocused field. Deliberately <c>TextSecondary</c>: a placeholder
    /// is information whose absence is not an error, so it takes the secondary ink rather than the disabled
    /// token, which belongs to a control that cannot be used. A read-only field paints none at all - a
    /// hint that invites typing into something that refuses writes is a lie the lane pins from both sides.
    /// </summary>
    private void DrawPlaceholder(Rect field, string typed, UiWidgetContext ctx, bool? writable)
    {
        if (writable == false) return;
        if (typed.Length != 0) return;
        if (ctx.Session.GetOrCreateValueState(AtomVocabulary.ReadBindKey(spec)).Focused) return;

        string placeholder = AtomVocabulary.ResolveText(spec, ctx, "Placeholder", "PlaceholderKey");
        if (placeholder.Length == 0) return;

        float inset = ctx.Theme.Geometry.Spacing;
        UiThemeDraw.Label(
            new Rect(field.x + inset, field.y, Math.Max(1f, field.width - inset * 2f), field.height),
            placeholder,
            ctx.Theme,
            ctx.Theme.TextSecondary,
            AtomVocabulary.TextFont(ctx),
            TextAnchor.MiddleLeft);
    }

    /// <summary>
    /// The label band: the label's own glyph advance plus the theme's spacing, measured with the font the
    /// label is drawn in. The same geometry rule the slider and the number field own; this kind keeps what
    /// the band leaves, so a labelled text field and a labelled number field line up on one page.
    /// </summary>
    private void ResolveLabelBand(Rect rect, UiWidgetContext ctx, out Rect field)
    {
        field = rect;
        string label = AtomVocabulary.ResolveText(spec, ctx, "Label", "LabelKey");
        if (label.Length == 0) return;

        float fieldX = rect.x + BandWidth(label, ctx) + ctx.Theme.Geometry.Gap;
        field = new Rect(fieldX, rect.y, Math.Max(1f, rect.xMax - fieldX), rect.height);
    }

    /// <summary>Paints the measured label band in the role's text colour, before the field is drawn.</summary>
    private void DrawLabel(Rect rect, UiWidgetContext ctx, UiResolvedStyle style)
    {
        string label = AtomVocabulary.ResolveText(spec, ctx, "Label", "LabelKey");
        if (label.Length == 0) return;

        UiThemeDraw.Label(
            new Rect(rect.x, rect.y, BandWidth(label, ctx), rect.height),
            label,
            ctx.Theme,
            style.Text,
            AtomVocabulary.TextFont(ctx),
            TextAnchor.MiddleLeft);
    }

    /// <summary>One label width, shared by the measure and the paint so the band cannot disagree with itself.</summary>
    private static float BandWidth(string label, UiWidgetContext ctx)
    {
        return Math.Max(1f, ctx.Metrics.MeasureWidth(label, AtomVocabulary.TextFont(ctx)) + ctx.Theme.Geometry.Spacing);
    }
}
