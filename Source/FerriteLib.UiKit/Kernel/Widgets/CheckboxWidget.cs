using System;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel.Widgets;

/// <summary>
/// Core kind <c>input/checkbox</c>: two states over one <c>bool</c> value binding, with the element's own
/// hover/armed appearance and its own hit rule.
/// <para>
/// <b>Why this earns a kind</b> - per-element interaction state plus the hit rule behind it. A boolean
/// control is not a small button: it owns the two-state read-back (the click writes the inverse of the value
/// it just read), it owns the body geometry it draws inside whatever band it was arranged, and it owns the
/// hover/armed ladder the library has to paint by hand because <see cref="UiNative.Button"/> is
/// <c>Widgets.ButtonInvisible</c> and carries no chrome. The manifest cannot express any of the three.
/// </para>
/// <para>
/// <b>Appearance is not kind, and not colour.</b> One boolean BEHAVIOUR serves every look: the two-state
/// read-back, the hit rule and the disabled funnel below are identical for both appearances, and only
/// <see cref="Paint"/> diverges. The look is declared ONCE, in <see cref="Register"/>, as this kind's
/// supported set for the shared seam (<see cref="UiAppearanceResolver"/>); the element's own
/// <c>Appearance</c> attribute is resolved through that set by <see cref="Looks"/>, and measurement,
/// creation-time validation and drawing all read the same resolved value, so they cannot disagree. The
/// default is the maintainer's square track and square thumb (<see cref="SwitchLook"/>);
/// <c>Appearance="checkbox"</c> keeps the box-and-mark look as an explicit alternative. A second boolean
/// kind would freeze vocabulary the attribute already carries, and would duplicate this file's input path -
/// the two things the promotion gate exists to prevent.
/// </para>
/// <para>
/// Colour is a third, independent axis: every colour below comes from <see cref="UiResolvedStyle"/> and every
/// dimension from the look's own constants or <c>Theme.Geometry</c>, so a palette change can never move
/// a rect and an appearance change can never pick a colour.
/// </para>
/// <para>
/// <b>The switch's OFF edge is a control edge, not the raised surface's own.</b> The OFF track's fill is the
/// raised plane it sits on, but its outline is read from the theme's control-edge ladder
/// (<see cref="ControlEdge"/>) rather than from <c>RaisedSurface.Border</c>. A scheme that spells "no box" as
/// fill == border would otherwise flatten both halves of the track onto the card face and the control would
/// vanish; a scope cannot express that ambiguity when the fill and the edge are two independent tokens. The
/// ON track is unaffected: the selected surface is a state treatment and keeps its own pair.
/// </para>
/// <para>
/// <b>The thumb's two halves are two kinds of token.</b> OFF paints a neutral ink
/// (<c>TextPrimary</c>) on the neutral track; ON paints the <c>AccentGold</c>, because the accent is what
/// says "this control is on" in this series' look. The ON thumb is deliberately not
/// <c>TextOnGold</c> - that token is ink FOR a gold plane, so a thumb wearing it reads as a label rather
/// than as the control's own state, and the two coincide on a palette whose selected plane is light.
/// </para>
/// <para>
/// <b>The disabled treatment is not this kind's branch.</b> Writability comes from the one funnel P2
/// built: <c>IsWritable</c> feeds the resolved-value table through <see cref="AtomVocabulary.WritableOf"/>,
/// so a read-only binding paints <see cref="UiStatusTone.Disabled"/> and a click is refused - there is no
/// <c>if (disabled)</c> in the input path, and the same funnel already refuses the pointer for a
/// command-bound element (0.5 command state). A boolean control on a read-only <c>bool</c> is therefore a
/// rendered state, not a dead control.
/// </para>
/// <para>
/// An optional <c>ActionBind</c> is the page's way to put the element under that same command funnel: a
/// false <c>CanExecute</c> makes the tree publish the node disabled and refuse the pointer, and a successful
/// toggle fires the command after the value write.
/// </para>
/// <para>
/// A missing or mistyped item-local key is deliberately fail-soft at draw: the control paints its default
/// (off) state and one deduplicated appearance fallback is recorded, which is the same answer P2 chose for an
/// unresolvable <c>VisibleKey</c>. A control inside a repeater template resolves its key against the
/// per-item binding scope, so "not yet bound" is a legal frame there and must not take the page down.
/// </para>
/// </summary>
internal sealed class CheckboxWidget : IUiWidget
{
    internal const string Kind = "input/checkbox";

    /// <summary>
    /// The maintainer's default boolean appearance: a square track with a square thumb. Its proportions are
    /// the reference the consumer's own toggle established (34x18 track, 14px knob, 2px inset, 6px label
    /// gap); they are declared here rather than derived from the density, because a switch's track is a
    /// shape, not a spacing step - deriving it from <c>RowHeight</c> would silently resize the control
    /// whenever density moved.
    /// </summary>
    internal const string SwitchLook = "switch";

    /// <summary>The box-and-mark appearance, kept as an explicit alternative to the default.</summary>
    internal const string CheckboxLook = "checkbox";

    /// <summary>
    /// This kind's appearance seam: the accepted looks, the default, and the one resolver every half of this
    /// file goes through. It is registered with the kind, so the set the creation contract validates against
    /// and the set <see cref="Measure"/> / <see cref="Draw"/> resolve through are the same object.
    /// </summary>
    internal static readonly UiAppearanceResolver Looks = new(SwitchLook, CheckboxLook);

    internal const float SwitchTrackWidth = 34f;
    internal const float SwitchTrackHeight = 18f;
    internal const float SwitchKnobSize = 14f;
    internal const float SwitchKnobInset = 2f;
    internal const float SwitchLabelGap = 6f;

    /// <summary>The smallest box the checkbox appearance will draw, the same floor <see cref="Paint"/> uses.</summary>
    private const float CheckboxMinSide = 8f;

    private UiElementSpec spec = UiElementSpec.Empty;

    string IUiWidget.Kind => Kind;

    internal static void Register()
    {
        UiWidgetRegistry.Register(
            UiWidgetRegistry.CoreScope,
            Kind,
            () => new CheckboxWidget(),
            AtomVocabulary.Schema(
                AtomRoles.ToneAndEmphasis,
                "Bind", "ActionBind", "Label", "LabelKey", "Height", UiAppearanceResolver.Attribute),
            new[] { "Label", "LabelKey" },
            NaturalBodyWidth,
            Looks);
    }

    public void Configure(UiElementSpec spec)
    {
        this.spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    /// <summary>
    /// The creation-time contract: a bool value binding, by <c>Bind</c> or by the documented
    /// element-<c>Id</c> fallback, and - when declared - a look the kind actually implements. Writability is
    /// deliberately not required: a read-only bool is a legal control and renders disabled. An unknown
    /// appearance is refused rather than falling back, which is the same fail-closed rule the closed
    /// <c>Height</c> vocabulary uses: a look that silently degraded would be a page nobody authored.
    /// </summary>
    public void Validate(IUiBindings bindings, string elementPath)
    {
        string key = ReadBindKey();
        if (key.Length == 0)
        {
            throw new InvalidOperationException(
                "CheckboxWidget at '" + elementPath + "' requires a Bind naming the bool value binding it "
                + "shows; a checkbox with no binding could never be read or written.");
        }

        bindings.ValidateValue<bool>(key, elementPath);

        ResolvedAppearance look = Looks.Resolve(spec);
        if (!look.Declared && DeclaresAppearance(spec))
        {
            spec.TryGetAttribute(UiAppearanceResolver.Attribute, out string authored);
            throw new InvalidOperationException(
                "CheckboxWidget at '" + elementPath + "' declares "
                + UiAppearanceResolver.Attribute + "='" + (authored ?? "").Trim()
                + "'; the accepted values are '" + SwitchLook + "' (the default square track and "
                + "thumb) and '" + CheckboxLook + "'. Omit the attribute for the default.");
        }

        string actionKey = ReadActionKey();
        if (actionKey.Length > 0)
        {
            bindings.ValidateCommand(actionKey, elementPath);
        }
    }

    /// <summary>True when the element declared a non-blank look at all; a blank declaration is the default.</summary>
    private static bool DeclaresAppearance(UiElementSpec spec)
    {
        return spec.TryGetAttribute(UiAppearanceResolver.Attribute, out string raw)
            && !string.IsNullOrWhiteSpace(raw);
    }

    /// <summary>
    /// The kind's natural band height, resolved from the SELECTED appearance: the switch reserves its track
    /// plus the band's own padding, the checkbox reserves at least its box floor. A declared <c>Height</c>
    /// still wins, so a page that sizes the band keeps doing so.
    /// </summary>
    public float Measure(UiWidgetContext ctx)
    {
        return AtomVocabulary.ReadHeight(spec, NaturalHeight(ctx));
    }

    /// <summary>The appearance-resolved natural height, before any declared override.</summary>
    private float NaturalHeight(UiWidgetContext ctx)
    {
        float pad = ctx.Theme.Geometry.Padding;
        float row = ctx.Theme.Geometry.RowHeight;
        return IsSwitch(Looks.Resolve(spec).Value)
            ? Math.Max(row, SwitchTrackHeight + pad * 2f)
            : Math.Max(row, CheckboxMinSide + pad * 2f);
    }

    /// <summary>
    /// The body half of this kind's natural width: the DRAWN body plus the gap it holds before a label the
    /// element actually declares. <c>Width="Auto"</c> measured the label text alone, so a boolean control
    /// reserved too little room for its own body and the label was clipped or the body overlapped the next
    /// child. It is resolved from the SELECTED look through the shared seam and reads the theme's own
    /// metrics, exactly as <see cref="Paint"/> derives them, so the measured body and the drawn body cannot
    /// drift apart. The body counts even with a blank or absent label, because the shape is still drawn; the
    /// GAP does not, because a gap reserved for a label that is not drawn is space the page never uses.
    /// </summary>
    private static float NaturalBodyWidth(UiElementSpec spec, UiWidgetContext ctx)
    {
        string look = Looks.Resolve(spec).Value;
        if (IsSwitch(look))
        {
            return SwitchTrackWidth + (HasLabel(spec) ? SwitchLabelGap : 0f);
        }

        float side = CheckboxSide(AtomVocabulary.ReadHeight(spec, ctx.Theme.Geometry.RowHeight), ctx.Theme.Geometry.Padding);
        return side + (HasLabel(spec) ? ContentGap(ctx.Theme, look) : 0f);
    }

    /// <summary>
    /// The ONE gap between a body and its label: the switch holds its own declared step, the checkbox uses
    /// the theme's content padding. Measurement and drawing both call this, so the reserved gap and the
    /// painted gap cannot drift apart.
    /// </summary>
    private static float ContentGap(UiTheme theme, string look)
    {
        return IsSwitch(look) ? SwitchLabelGap : theme.Geometry.Padding;
    }

    /// <summary>True when the element declares label content at all; a blank declaration reserves no gap.</summary>
    private static bool HasLabel(UiElementSpec spec)
    {
        return DeclaresText(spec, "Label") || DeclaresText(spec, "LabelKey");
    }

    private static bool DeclaresText(UiElementSpec spec, string attribute)
    {
        return spec.TryGetAttribute(attribute, out string value) && !string.IsNullOrWhiteSpace(value);
    }

    private static bool IsSwitch(string look)
    {
        return string.Equals(look, SwitchLook, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The checkbox appearance's box side in a band of <paramref name="bandHeight"/>: the band less its own
    /// content padding, floored at <see cref="CheckboxMinSide"/>. Measure and draw read this one function
    /// against the same band, so the reserved body and the painted box are one number.
    /// </summary>
    private static float CheckboxSide(float bandHeight, float padding)
    {
        return Math.Max(CheckboxMinSide, bandHeight - padding * 2f);
    }

    /// <summary>
    /// The OFF track's outline: the theme's control-edge ladder - the stronger shared edge when the palette
    /// claims one, the shared edge otherwise. It is deliberately NOT <c>RaisedSurface.Border</c>: that token is
    /// the raised plane's own edge, and a scope that flattens a plane (fill == border, its way of saying "no
    /// box") would then flatten the track onto the very plane it sits on. Reading the control edge from the
    /// shared ladder keeps the control visible under such a scope while still letting a palette choose it.
    /// </summary>
    private static Color ControlEdge(UiTheme theme)
    {
        return theme.BorderStrong.a > 0f ? theme.BorderStrong : theme.Border;
    }

    public void Draw(Rect rect, UiWidgetContext ctx)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;

        string key = ReadBindKey();
        bool? writable = AtomVocabulary.WritableOf(ctx, key);
        if (ctx.Node != null && ctx.Node.IsDisabled)
        {
            // The command funnel's own answer, read back rather than re-derived: a gated control shows the
            // one disabled treatment the resolved-value table already owns.
            writable = false;
        }

        bool state = ReadState(ctx, key);

        // Armed wins over hover, exactly as the button atom orders its ladder: live state beats the
        // pointer's proximity.
        bool armed = UiNative.IsMouseDownOver(rect);
        bool hovered = !armed && UiNative.IsMouseOver(rect);

        UiResolvedStyle idle = AtomVocabulary.ResolveRole(spec, ctx, writable);
        UiResolvedStyle shown = idle;
        if (armed)
        {
            shown = ctx.Theme.Styles.Resolve(UiStatusTone.Active);
        }
        else if (hovered)
        {
            shown = new UiResolvedStyle(ctx.Theme.HoverSurface.Fill, ctx.Theme.HoverSurface.Border, idle.Text);
        }

        // One resolved look for the whole frame, through the same shared seam measurement used: the paint
        // below reads this value and never re-parses the attribute.
        string look = Looks.Resolve(spec).Value;
        Paint(rect, shown, state, look, ctx);

        TakeInput(rect, ctx, writable, key, state);
    }

    /// <summary>
    /// The ONE boolean input path, shared by every appearance. Only <see cref="Paint"/> differs between
    /// them: the two-state read-back, the whole-band hit rule and the command funnel are identical, so an
    /// appearance can never change what a click MEANS or how many writes it makes.
    /// </summary>
    private void TakeInput(Rect rect, UiWidgetContext ctx, bool? writable, string key, bool state)
    {
        // The hit rule: the whole arranged band is the target, and the funnel decides whether the element may
        // take it (disabled elements and covered layers never do). Writability only gates the write - a
        // read-only control still reports the click it cannot honour by painting nothing different.
        if (writable == false) return;

        if (UiNative.Button(rect, ctx))
        {
            ctx.Bindings.Set<bool>(key, !state);
            string actionKey = ReadActionKey();
            if (actionKey.Length > 0)
            {
                ctx.Bindings.Invoke(actionKey);
            }
        }
    }

    private void Paint(Rect rect, UiResolvedStyle style, bool state, string look, UiWidgetContext ctx)
    {
        UiTheme theme = ctx.Theme;

        // The band the body is sized in: the element's declared Height when it has one, otherwise the
        // resolved look's own natural band. Reading the measured band here - rather than the arranged rect -
        // is what makes the drawn body the same number the natural width reserved, and it is what lets an
        // explicit Height still win over the look's band: the body grows with the band the page asked for.
        float band = AtomVocabulary.ReadHeight(spec, NaturalHeight(ctx));

        float body;
        if (IsSwitch(look))
        {
            body = PaintSwitch(rect, state, theme, band);
        }
        else
        {
            body = PaintCheckbox(rect, style, state, theme, band);
        }

        string label = AtomVocabulary.ResolveText(spec, ctx, "Label", "LabelKey");
        if (label.Length == 0) return;

        // The SAME gap the measurement reserved, from the one helper both call.
        float labelX = rect.x + body + ContentGap(theme, look);

        float labelWidth = rect.xMax - labelX;
        if (labelWidth <= 1f) return;

        UiThemeDraw.Label(
            new Rect(labelX, rect.y, labelWidth, rect.height),
            label,
            theme,
            style.Text,
            AtomVocabulary.TextFont(ctx),
            TextAnchor.MiddleLeft,
            singleLine: true);
    }

    /// <summary>
    /// The default appearance: a square track with a square thumb, centred in <paramref name="band"/>. The
    /// track is drawn at its documented size and shrinks rather than overflowing when the band is narrower,
    /// so a cramped page degrades visibly instead of painting outside its own rect.
    /// <para>
    /// The OFF track takes the raised plane as its FILL and the theme's control-edge ladder as its OUTLINE.
    /// The ON track keeps the selected surface's own pair, because a selected state is a treatment the
    /// resolved-value table owns. The split is what keeps the control visible under a scope that flattens the
    /// raised plane (fill == border, that vocabulary's way of saying "no box"): a fill and an edge read from
    /// two independent tokens cannot both collapse onto the card face.
    /// </para>
    /// </summary>
    private float PaintSwitch(Rect rect, bool state, UiTheme theme, float band)
    {
        float trackWidth = Math.Min(SwitchTrackWidth, Math.Max(1f, rect.width));
        float trackHeight = Math.Min(SwitchTrackHeight, Math.Max(1f, band));
        var track = new Rect(rect.x, rect.y + (rect.height - trackHeight) * 0.5f, trackWidth, trackHeight);

        UiSurfaceStyle surface = state
            ? theme.SelectedSurface
            : new UiSurfaceStyle(theme.RaisedSurface.Fill, ControlEdge(theme));
        UiThemeDraw.Surface(track, surface, theme.Geometry.Hairline);

        float knob = Math.Min(SwitchKnobSize, Math.Max(1f, trackWidth - SwitchKnobInset * 2f));
        float throwWidth = Math.Max(0f, trackWidth - knob - SwitchKnobInset * 2f);
        float knobX = track.x + SwitchKnobInset + (state ? throwWidth : 0f);
        var knobRect = new Rect(knobX, track.y + SwitchKnobInset, knob, knob);

        // The two halves of the thumb are DIFFERENT KINDS OF TOKEN, and that is the point: OFF is a neutral
        // ink on a neutral track, ON is the ACCENT, because "this is on" is what the accent means here. The
        // accent is deliberately not the selected surface's text colour (<c>TextOnGold</c>, which is ink FOR
        // a gold plane): a thumb painted in the plane's text colour would read as a label rather than as the
        // control's own state, and on a palette whose selected plane is light the two coincide.
        UiThemeDraw.Solid(knobRect, state ? theme.AccentGold : theme.TextPrimary);

        return trackWidth;
    }

    /// <summary>The explicit alternative: today's box and mark, at the size this kind always drew.</summary>
    private static float PaintCheckbox(Rect rect, UiResolvedStyle style, bool state, UiTheme theme, float band)
    {
        float padding = theme.Geometry.Padding;
        float side = CheckboxSide(band, padding);
        var box = new Rect(rect.x, rect.y + (rect.height - side) * 0.5f, side, side);

        UiThemeDraw.Surface(box, style.Surface, theme.Geometry.Hairline);

        if (state)
        {
            // The mark is the same answer's text colour rather than a second token: an authored tone moves
            // plane, edge and mark together, and the disabled row keeps the disabled text colour.
            float inset = Math.Max(2f, side * 0.25f);
            UiThemeDraw.Solid(
                new Rect(box.x + inset, box.y + inset, Math.Max(1f, side - inset * 2f), Math.Max(1f, side - inset * 2f)),
                style.Text);
        }

        return side + padding;
    }

    private string ReadActionKey()
    {
        return AtomVocabulary.Read(spec, "ActionBind");
    }

    /// <summary>
    /// The two-state read, and the contract that makes it safe inside a repeater template: an absent key and a
    /// key bound to another type both answer the default state and are recorded once through the appearance
    /// channel (<see cref="AtomVocabulary.ReadBoolOr"/>). Fail-soft must not mean silent, and a data-driven
    /// row whose item-local value is not there yet or lost its type must keep drawing rather than replace the
    /// slot with a recovery band.
    /// </summary>
    private bool ReadState(UiWidgetContext ctx, string key)
    {
        return AtomVocabulary.ReadBoolOr(ctx, Kind, key, fallback: false);
    }

    private string ReadBindKey()
    {
        return AtomVocabulary.ReadBindKey(spec);
    }
}
