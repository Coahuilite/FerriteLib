using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel.Widgets;

/// <summary>
/// Greenfield mode-row selector. Reads a string value binding (keyed by <c>Bind</c>, falling back
/// to the element Id) and writes the selected mode string through the same binding. Layout is
/// responsive: wide uses one row, medium uses two columns, narrow stacks vertically, and the cell
/// metrics come from the theme's <see cref="UiGeometry"/> so density is one knob for the whole tree.
/// <para>
/// <b>Localized option labels.</b> Each slot's label is <c>TitleKeyN</c> resolved through the translation
/// seam when declared, else the literal <c>TitleN</c>, else the option's value — the same key-wins rule
/// every other <c>*Key</c> pair in the library uses, and the reason a bilingual page can use this kind at
/// all. Both name families are in the declared label set, so <c>Width="Auto"</c> measures the TRANSLATED
/// title rather than the literal one.
/// </para>
/// <para>
/// <b>Per-option help, published rather than painted.</b> Each option may carry its own help identity in
/// its <c>DescriptionN</c>; the kind does not draw it (a tooltip is the consumer's call, and vanilla's own
/// mode selectors render the help in a panel of their own). Two reads exist, and they are complementary:
/// declare <c>HoverHelpKey</c> and the row writes the hovered option's identity into that binding
/// (<see cref="OptionHelp"/>), and the hovered option's declared help text is also claimed on the session's
/// hover surface (<c>UiSession.ClaimHover</c>), which is the general read the element-level <c>HelpKey</c>
/// uses too. An option that declares no help text claims nothing, so the row's own <c>HelpKey</c> topic
/// stands while the pointer is over it.
/// </para>
/// </summary>
public sealed class InputModeRowWidget : IUiWidget
{
    public const string Kind = "input/mode-row";

    /// <summary>
    /// The element attribute naming the binding key that receives the hovered option's help identity. Public
    /// because the option-level contract is shared with <c>input/dropdown</c> (see <see cref="OptionHelp"/>),
    /// and this is where it was first declared.
    /// </summary>
    public const string HoverHelpKeyAttribute = "HoverHelpKey";

    private const int OptionSlots = 8;

    private UiElementSpec spec = UiElementSpec.Empty;

    string IUiWidget.Kind => Kind;

    public static void Register()
    {
        UiWidgetRegistry.Register(
            UiWidgetRegistry.CoreScope,
            Kind,
            () => new InputModeRowWidget(),
            new[] {
                "Id", "Kind", "Bind", "Height", "Tab", "Hidden", OptionHelp.Attribute,
                "Value1", "Title1", "TitleKey1", "Description1",
                "Value2", "Title2", "TitleKey2", "Description2",
                "Value3", "Title3", "TitleKey3", "Description3",
                "Value4", "Title4", "TitleKey4", "Description4",
                "Value5", "Title5", "TitleKey5", "Description5",
                "Value6", "Title6", "TitleKey6", "Description6",
                "Value7", "Title7", "TitleKey7", "Description7",
                "Value8", "Title8", "TitleKey8", "Description8"
            },
            // The label set is what Width="Auto" measures (UiLayoutEngine.MeasureLabelWidth), and the text this
            // kind paints as a label is a slot's title - the KEY form resolved through the seam when declared,
            // else the literal. Both families therefore belong here (the same pair rule text/wrapped and the
            // other labelled atoms already follow), and Description1..8 stay out of it: they are the options'
            // help identities, published and never painted, so measuring them made an Auto column reserve width
            // for text that never appears there (B8).
            new[] {
                "Title1", "TitleKey1", "Title2", "TitleKey2", "Title3", "TitleKey3", "Title4", "TitleKey4",
                "Title5", "TitleKey5", "Title6", "TitleKey6", "Title7", "TitleKey7", "Title8", "TitleKey8"
            });
    }

    public void Configure(UiElementSpec spec)
    {
        this.spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public void Validate(IUiBindings bindings, string elementPath)
    {
        string bindKey = ReadBindKey();
        if (string.IsNullOrEmpty(bindKey))
        {
            throw new InvalidOperationException(
                $"InputModeRowWidget at '{elementPath}' requires a non-empty Id or Bind to use as its typed binding key.");
        }

        if (bindings is IUiTypedChoices typedSeam
            && typedSeam.TryGetValueType(bindKey, out Type declared)
            && declared != typeof(string))
        {
            // R4-A: a value bound to anything but a string is served by the typed path, whose cells are the
            // typed choices bound under the value key. ValidateValue<string> would refuse that binding outright,
            // and the static ValueN family carries strings only, so neither can supply a value of this type
            // without spelling it. The orphan rule below is about the static family and does not apply here:
            // in this mode a TitleN/DescriptionN labels a CHOICE, so it needs no ValueN of its own.
            if (!typedSeam.TryGetChoices(bindKey, out IReadOnlyList<UiChoice<object?>> choices))
            {
                throw new InvalidOperationException(
                    $"Value binding '{bindKey}' at '{elementPath}' is '{declared.Name}', so this mode row needs typed"
                    + $" choices of that same type: bind BindOptions<UiChoice<{declared.Name}>>('{bindKey}', ...)."
                    + " The static ValueN attributes and a string list carry strings only, and handing a string to a"
                    + " typed setter is the round-trip this path removes.");
            }

            foreach (UiChoice<object?> choice in choices)
            {
                if (!typedSeam.AcceptsValue(bindKey, choice.Value))
                {
                    throw new InvalidOperationException(
                        $"Typed choices '{bindKey}' at '{elementPath}' carry a value of type '"
                        + (choice.Value == null ? "null" : choice.Value.GetType().Name)
                        + $"' under the label '{choice.Text}', which is not the '{declared.Name}' its value binding"
                        + " accepts.");
                }
            }

            OptionHelp.Validate(bindings, spec, elementPath, "InputModeRowWidget");
            return;
        }

        bindings.ValidateValue<string>(bindKey, elementPath);

        // A per-option declaration must name an option. A TitleN/TitleKeyN/DescriptionN whose index has no
        // ValueN can never be drawn or published, so it is the inert declaration this contract refuses rather
        // than a silently ignored one: the author either meant an option there or meant to delete the line.
        for (int i = 1; i <= OptionSlots; i++)
        {
            string suffix = i.ToString(CultureInfo.InvariantCulture);
            if (HasText("Value" + suffix)) continue;

            foreach (string attribute in new[] { "Title" + suffix, "TitleKey" + suffix, "Description" + suffix })
            {
                if (!HasText(attribute)) continue;

                throw new InvalidOperationException(
                    $"'{attribute}' is declared without 'Value{suffix}': a per-option title, title key or description"
                    + " whose index has no option can never be drawn or published. Declare 'Value" + suffix
                    + "' or remove the declaration.");
            }
        }

        OptionHelp.Validate(bindings, spec, elementPath, "InputModeRowWidget");
    }

    public float Measure(UiWidgetContext ctx)
    {
        UiGeometry geometry = ctx.Theme.Geometry;
        int columns = ColumnsFor(ctx.ViewWidth);
        int rows = (OptionsFor(ctx).Count + columns - 1) / columns;
        float rowHeight = ReadFloat("Height", geometry.RowHeight);
        return Math.Max(1f, rows * rowHeight + Math.Max(0, rows - 1) * geometry.Gap + geometry.Spacing * 2f);
    }

    public void Draw(Rect rect, UiWidgetContext ctx)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;

        List<Option> options = OptionsFor(ctx);
        if (options.Count == 0) return;

        string bindKey = ReadBindKey();
        // The typed mode reads its current value through the seam - TryGet<string> would refuse a binding of
        // another type, which is exactly the binding this path serves - and keeps it as the real instance.
        bool typed = TryTypedSeam(ctx, bindKey, out IUiTypedChoices? seam);
        string current = "";
        object? currentTyped = null;
        if (typed)
        {
            seam!.TryGetTypedValue(bindKey, out currentTyped);
        }
        else
        {
            ctx.Bindings.TryGet(bindKey, out current);
        }

        UiGeometry geometry = ctx.Theme.Geometry;
        int columns = ColumnsFor(rect.width);
        int rows = (options.Count + columns - 1) / columns;
        float rowHeight = ReadFloat("Height", geometry.RowHeight);
        float innerWidth = Math.Max(1f, rect.width - geometry.Spacing * 2f);
        float columnWidth = (innerWidth - (columns - 1) * geometry.Gap) / columns;

        string hoveredHelp = "";
        for (int i = 0; i < options.Count; i++)
        {
            int row = i / columns;
            int col = i % columns;
            float x = rect.x + geometry.Spacing + col * (columnWidth + geometry.Gap);
            float y = rect.y + geometry.Spacing + row * (rowHeight + geometry.Gap);
            var optionRect = new Rect(x, y, columnWidth, rowHeight);
            bool selected = options[i].Typed
                ? Equals(options[i].TypedValue, currentTyped)
                : string.Equals(options[i].Value, current, StringComparison.Ordinal);
            DrawOption(optionRect, options[i], selected, ctx.Theme);

            // Asked through the funnel, so "hovered" means the same thing here as it does to every other
            // kind: the pointer is inside the cell AND this row is not covered by a higher layer. The call
            // consumes nothing, which is why it can sit beside the hit test below.
            if (UiNative.IsMouseOver(optionRect, ctx))
            {
                hoveredHelp = options[i].Help;
                if (options[i].Description.Length > 0)
                {
                    // The general read, and the more specific topic: the engine has already claimed the row's
                    // own HelpKey (if it declares one) earlier in this pass, and a live claim replaces the held
                    // one, so an option with its own help text wins while the pointer is over it. An option
                    // without one claims nothing and lets the row's topic stand.
                    ctx.Session.ClaimHover(options[i].Description);
                }
            }

            if (UiNative.Button(optionRect, ctx))
            {
                if (options[i].Typed)
                {
                    // A refused write (a value of the wrong type) changes nothing: the refusal is reported and
                    // the cell simply does not move the model.
                    seam!.TrySetTypedValue(bindKey, options[i].TypedValue);
                }
                else
                {
                    ctx.Bindings.Set(bindKey, options[i].Value);
                }
            }
        }

        OptionHelp.Publish(ctx, OptionHelp.SinkKey(spec), hoveredHelp);
    }

    /// <summary>
    /// True when this element is served by the typed path: its bindings expose the optional seam AND its value
    /// binding is bound to something other than a string. A row whose value is a plain string therefore takes
    /// today's path byte for byte, whether or not the seam exists.
    /// </summary>
    private bool TryTypedSeam(UiWidgetContext ctx, string bindKey, out IUiTypedChoices? seam)
    {
        seam = ctx.Bindings as IUiTypedChoices;
        return seam != null
            && seam.TryGetValueType(bindKey, out Type valueType)
            && valueType != typeof(string);
    }

    private string ReadBindKey()
    {
        if (spec.TryGetAttribute("Bind", out string bindKey) && bindKey.Length > 0)
        {
            return bindKey;
        }

        return spec.Id;
    }

    private void DrawOption(Rect rect, Option option, bool selected, UiTheme theme)
    {
        // One table query for the plane and the text, and the text inset spelled once in the theme
        // instead of as a bare 6f/12f here and a named constant in the dropdown.
        UiResolvedStyle style = theme.Styles.Resolve(selected ? UiStatusTone.Active : UiStatusTone.Neutral);
        float padding = theme.Geometry.Padding;
        UiThemeDraw.Surface(rect, style.Surface, theme.Geometry.Hairline);
        UiThemeDraw.Label(
            new Rect(rect.x + padding, rect.y, rect.width - padding * 2f, rect.height),
            option.Title,
            theme,
            style.Text,
            UiFont.Small,
            TextAnchor.MiddleLeft);
    }

    private int ColumnsFor(float width)
    {
        if (width >= 560f) return 4;
        if (width >= 360f) return 2;
        return 1;
    }

    private List<Option> OptionsFor(UiWidgetContext ctx)
    {
        string bindKey = ReadBindKey();
        if (TryTypedSeam(ctx, bindKey, out IUiTypedChoices? seam)
            && seam!.TryGetChoices(bindKey, out IReadOnlyList<UiChoice<object?>> choices))
        {
            // R4-A: the cells are the typed choices bound under the VALUE key. The slot's TitleKeyN/TitleN still
            // names the cell when it is declared (the localization rule above is unchanged), and the choice's own
            // label is the fallback - so a translated page keeps its words and a typed page gets the label its
            // consumer wrote. ValueN is not read on this path: it carries a string, and a string is exactly what
            // this path refuses to hand a typed setter.
            var typedCells = new List<Option>(choices.Count);
            for (int i = 0; i < choices.Count; i++)
            {
                string suffix = (i + 1).ToString(CultureInfo.InvariantCulture);
                spec.TryGetAttribute("Description" + suffix, out string description);
                typedCells.Add(new Option(ResolveTitle(suffix, choices[i].Text, ctx), choices[i], description ?? ""));
            }

            return typedCells;
        }

        var result = new List<Option>();
        for (int i = 1; i <= OptionSlots; i++)
        {
            string suffix = i.ToString(CultureInfo.InvariantCulture);
            if (!spec.TryGetAttribute("Value" + suffix, out string value) || value.Length == 0)
            {
                continue;
            }

            spec.TryGetAttribute("Description" + suffix, out string description);
            result.Add(new Option(ResolveTitle(suffix, value, ctx), value, description ?? ""));
        }

        return result;
    }

    /// <summary>
    /// One slot's label: the <c>TitleKeyN</c> through the translation seam when declared, else the literal
    /// <c>TitleN</c>, else the option's value — the key-wins rule every other labelled kind in the library
    /// uses, so a translated page and a literal page cannot disagree about which one is shown.
    /// </summary>
    private string ResolveTitle(string suffix, string value, UiWidgetContext ctx)
    {
        if (spec.TryGetAttribute("TitleKey" + suffix, out string key) && key.Length > 0)
        {
            return ctx.Translation.Translate(key);
        }

        return spec.TryGetAttribute("Title" + suffix, out string title) && title.Length > 0 ? title : value;
    }

    /// <summary>True when the attribute is present and non-empty — the same rule <see cref="OptionsFor"/> uses
    /// to decide an option exists, so validation and drawing cannot disagree about which indices are real.</summary>
    private bool HasText(string attribute)
    {
        return spec.TryGetAttribute(attribute, out string value) && value.Length > 0;
    }

    private float ReadFloat(string name, float fallback)
    {
        return spec.TryGetAttribute(name, out string raw)
            && float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
            ? value
            : fallback;
    }

    private readonly struct Option
    {
        internal readonly string Title;

        /// <summary>The string path's value. Empty on a typed cell, whose value is <see cref="TypedValue"/>.</summary>
        internal readonly string Value;

        /// <summary>The typed path's value: the real instance, boxed. Unused on a string cell.</summary>
        internal readonly object? TypedValue;

        /// <summary>Which of the two values above this cell carries. A typed value may legitimately BE null.</summary>
        internal readonly bool Typed;

        internal readonly string Description;

        /// <summary>A string-path cell: the static ValueN family or nothing.</summary>
        internal Option(string title, string value, string description)
        {
            Title = title;
            Value = value;
            TypedValue = null;
            Typed = false;
            Description = description;
        }

        /// <summary>A typed-path cell: one UiChoice&lt;T&gt; of the value key's typed choices.</summary>
        internal Option(string title, UiChoice<object?> choice, string description)
        {
            Title = title;
            Value = "";
            TypedValue = choice.Value;
            Typed = true;
            Description = description;
        }

        /// <summary>The option-level help identity this row publishes: its declared description, else the
        /// option's own machine token - its value on the string path, its label on the typed one, because a
        /// typed value is not a string the help catalog could be keyed by.</summary>
        internal string Help => Description.Length > 0 ? Description : (Typed ? Title : Value);
    }
}
