using System;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel.Widgets;

/// <summary>
/// Core kind <c>input/checkbox</c>: two states over one <c>bool</c> value binding, with the element's own
/// hover/armed appearance and its own hit rule.
/// <para>
/// <b>Why this earns a kind</b> - per-element interaction state plus the hit rule behind it. A checkbox is
/// not a small button: it owns the two-state read-back (the click writes the inverse of the value it just
/// read), it owns the box geometry it draws inside whatever band it was arranged, and it owns the
/// hover/armed ladder the library has to paint by hand because <see cref="UiNative.Button"/> is
/// <c>Widgets.ButtonInvisible</c> and carries no chrome. The manifest cannot express any of the three.
/// </para>
/// <para>
/// <b>The disabled treatment is not this kind's branch.</b> Writability comes from the one funnel P2
/// built: <c>IsWritable</c> feeds the resolved-value table through <see cref="AtomVocabulary.WritableOf"/>,
/// so a read-only binding paints <see cref="UiStatusTone.Disabled"/> and a click is refused - there is no
/// <c>if (disabled)</c> in this file, and the same funnel already refuses the pointer for a command-bound
/// element (0.5 command state). A checkbox on a read-only <c>bool</c> is therefore a rendered state, not a
/// dead control.
/// </para>
/// <para>
/// An optional <c>ActionBind</c> is the page's way to put the element under that same command funnel: a
/// false <c>CanExecute</c> makes the tree publish the node disabled and refuse the pointer, and a successful
/// toggle fires the command after the value write. Both disabled shapes - a value the model publishes
/// read-only and a command the owner has taken away - land on the one disabled treatment, and neither
/// executes nor captures.
/// </para>
/// <para>
/// A missing or mistyped item-local key is deliberately fail-soft at draw: the box paints its default
/// (unchecked) state and one deduplicated appearance fallback is recorded, which is the same answer P2
/// chose for an unresolvable <c>VisibleKey</c>. A checkbox inside a repeater template resolves its key
/// against the per-item binding scope, so "not yet bound" is a legal frame there and must not take the
/// page down.
/// </para>
/// </summary>
internal sealed class CheckboxWidget : IUiWidget
{
    internal const string Kind = "input/checkbox";

    private UiElementSpec spec = UiElementSpec.Empty;

    string IUiWidget.Kind => Kind;

    internal static void Register()
    {
        UiWidgetRegistry.Register(
            UiWidgetRegistry.CoreScope,
            Kind,
            () => new CheckboxWidget(),
            AtomVocabulary.Schema(AtomRoles.ToneAndEmphasis, "Bind", "ActionBind", "Label", "LabelKey", "Height"),
            new[] { "Label", "LabelKey" });
    }

    public void Configure(UiElementSpec spec)
    {
        this.spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    /// <summary>
    /// The creation-time contract: a bool value binding, by <c>Bind</c> or by the documented
    /// element-<c>Id</c> fallback. Writability is deliberately not required - a read-only bool is a legal
    /// checkbox and renders disabled.
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

        // The optional gate/notification command: declaring it is how a page puts the element under the one
        // disabled funnel (a false `CanExecute` makes the tree refuse the pointer and publishes it on the
        // node), and a successful toggle fires it. It is a command rather than an action because the
        // executability read is the funnel's, not a second predicate this kind would invent.
        string actionKey = ReadActionKey();
        if (actionKey.Length > 0)
        {
            bindings.ValidateCommand(actionKey, elementPath);
        }
    }

    public float Measure(UiWidgetContext ctx)
    {
        // The measure contract over its own content: one band, the density's row height unless Height
        // overrides it. The label is single-line, so its text never moves the band.
        return AtomVocabulary.ReadHeight(spec, ctx.Theme.Geometry.RowHeight);
    }

    public void Draw(Rect rect, UiWidgetContext ctx)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;

        string key = ReadBindKey();
        bool? writable = AtomVocabulary.WritableOf(ctx, key);
        if (ctx.Node != null && ctx.Node.IsDisabled)
        {
            // The command funnel's own answer, read back rather than re-derived: a gated checkbox shows the
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

        Paint(rect, shown, state, ctx);

        // The hit rule: the whole arranged band is the target, and the funnel decides whether the element
        // may take it (disabled elements and covered layers never do). Writability only gates the write -
        // a read-only checkbox still reports the click it cannot honour by painting nothing different.
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

    private string ReadActionKey()
    {
        return AtomVocabulary.Read(spec, "ActionBind");
    }

    private void Paint(Rect rect, UiResolvedStyle style, bool state, UiWidgetContext ctx)
    {
        UiTheme theme = ctx.Theme;
        float pad = theme.Geometry.Padding;
        float side = Math.Max(8f, rect.height - pad * 2f);
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

        string label = AtomVocabulary.ResolveText(spec, ctx, "Label", "LabelKey");
        if (label.Length == 0) return;

        float labelX = box.xMax + pad;
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
