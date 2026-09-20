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
/// <b>Per-option help, published rather than painted.</b> Each option may carry its own help identity in
/// its <c>DescriptionN</c>; the kind does not draw it (a tooltip is the consumer's call, and vanilla's own
/// mode selectors render the help in a panel of their own). What the kind owes is the answer to "which
/// option is the pointer on", so a consumer can render the help itself without re-deriving this kind's cell
/// geometry: declare <c>HoverHelpKey</c> and the row writes the hovered option's help identity there, and
/// clears it when the pointer leaves. See <see cref="PublishHoverHelp"/> for the exact contract.
/// </para>
/// </summary>
public sealed class InputModeRowWidget : IUiWidget
{
    public const string Kind = "input/mode-row";

    /// <summary>The element attribute naming the binding key that receives the hovered option's help identity.</summary>
    public const string HoverHelpKeyAttribute = "HoverHelpKey";

    private UiElementSpec spec = UiElementSpec.Empty;

    string IUiWidget.Kind => Kind;

    public static void Register()
    {
        UiWidgetRegistry.Register(
            UiWidgetRegistry.CoreScope,
            Kind,
            () => new InputModeRowWidget(),
            new[] {
                "Id", "Kind", "Bind", "Height", "Tab", "Hidden", HoverHelpKeyAttribute,
                "Value1", "Title1", "Description1", "Value2", "Title2", "Description2",
                "Value3", "Title3", "Description3", "Value4", "Title4", "Description4",
                "Value5", "Title5", "Description5", "Value6", "Title6", "Description6",
                "Value7", "Title7", "Description7", "Value8", "Title8", "Description8"
            },
            // The label set is what Width="Auto" measures (UiLayoutEngine.MeasureLabelWidth), and the only
            // text this kind paints is an option's Title (DrawOption). Description1..8 are the options' help
            // identities - published through HoverHelpKey, still never painted - so they stay out of the label
            // set: measuring them made an Auto column reserve width for text that never appears there (B8).
            new[] {
                "Title1", "Title2", "Title3", "Title4", "Title5", "Title6", "Title7", "Title8"
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

        bindings.ValidateValue<string>(bindKey, elementPath);

        // A per-option declaration must name an option. TitleN/DescriptionN whose index has no ValueN can
        // never be drawn or published, so it is the inert declaration this contract refuses rather than a
        // silently ignored one: the author either meant an option there or meant to delete the line.
        for (int i = 1; i <= OptionSlots; i++)
        {
            string suffix = i.ToString(CultureInfo.InvariantCulture);
            if (HasText("Value" + suffix)) continue;

            foreach (string attribute in new[] { "Title" + suffix, "Description" + suffix })
            {
                if (!HasText(attribute)) continue;

                throw new InvalidOperationException(
                    $"'{attribute}' is declared without 'Value{suffix}': a per-option title or description whose"
                    + " index has no option can never be drawn or published. Declare 'Value" + suffix
                    + "' or remove the declaration.");
            }
        }

        // The publication target, validated at creation rather than discovered at draw time: it must be a
        // string value binding, and it must be writable, because the row writes the hovered option's help
        // through it. A read-only key would refuse that write mid-frame and trip the recovery band.
        if (spec.TryGetAttribute(HoverHelpKeyAttribute, out string hoverKey) && hoverKey.Length > 0)
        {
            bindings.ValidateValue<string>(hoverKey, elementPath);
            if (!bindings.IsWritable(hoverKey))
            {
                throw new InvalidOperationException(
                    $"'{HoverHelpKeyAttribute}' names the binding '{hoverKey}', which is not writable: the mode row"
                    + " publishes the hovered option's help through it, so a read-only or unbound key would refuse"
                    + " the write. Bind it with BindValue<string>.");
            }
        }
    }

    public float Measure(UiWidgetContext ctx)
    {
        UiGeometry geometry = ctx.Theme.Geometry;
        int columns = ColumnsFor(ctx.ViewWidth);
        int rows = (Options.Count + columns - 1) / columns;
        float rowHeight = ReadFloat("Height", geometry.RowHeight);
        return Math.Max(1f, rows * rowHeight + Math.Max(0, rows - 1) * geometry.Gap + geometry.Spacing * 2f);
    }

    public void Draw(Rect rect, UiWidgetContext ctx)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;

        List<Option> options = Options;
        if (options.Count == 0) return;

        string bindKey = ReadBindKey();
        ctx.Bindings.TryGet(bindKey, out string current);
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
            bool selected = string.Equals(options[i].Value, current, StringComparison.Ordinal);
            DrawOption(optionRect, options[i], selected, ctx.Theme);

            // Asked through the funnel, so "hovered" means the same thing here as it does to every other
            // kind: the pointer is inside the cell AND this row would take input there. The call consumes
            // nothing, which is why it can sit beside the hit test below.
            if (UiNative.IsMouseOver(optionRect, ctx))
            {
                hoveredHelp = options[i].Help;
            }

            if (UiNative.Button(optionRect, ctx))
            {
                ctx.Bindings.Set(bindKey, options[i].Value);
            }
        }

        PublishHoverHelp(ctx, hoveredHelp);
    }

    /// <summary>
    /// Publishes the hovered option's help identity through the element's declared
    /// <see cref="HoverHelpKeyAttribute"/>, and clears it (an empty string) when no option is hovered. Written
    /// only when the answer changes, so a consumer reading the key sees one write per hover transition rather
    /// than one per frame.
    /// <para>
    /// The identity is the option's declared <c>DescriptionN</c>, or its <c>ValueN</c> when the option declares
    /// no description — so the published string always names the hovered option, with or without help text,
    /// and the consumer needs no second channel to find out which one it is. Nothing is painted: rendering the
    /// help is the consumer's, which is exactly why the identity is published instead of shown.
    /// </para>
    /// </summary>
    private void PublishHoverHelp(UiWidgetContext ctx, string help)
    {
        if (!spec.TryGetAttribute(HoverHelpKeyAttribute, out string key) || key.Length == 0) return;

        if (ctx.Bindings.TryGet(key, out string published)
            && string.Equals(published ?? "", help, StringComparison.Ordinal))
        {
            return;
        }

        ctx.Bindings.Set(key, help);
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

    private List<Option> Options
    {
        get
        {
            var result = new List<Option>();
            for (int i = 1; i <= OptionSlots; i++)
            {
                if (!spec.TryGetAttribute("Value" + i.ToString(CultureInfo.InvariantCulture), out string value)
                    || value.Length == 0)
                {
                    continue;
                }

                spec.TryGetAttribute("Title" + i.ToString(CultureInfo.InvariantCulture), out string title);
                spec.TryGetAttribute("Description" + i.ToString(CultureInfo.InvariantCulture), out string description);
                result.Add(new Option(title.Length > 0 ? title : value, value, description ?? ""));
            }

            return result;
        }
    }

    /// <summary>True when the attribute is present and non-empty — the same rule <see cref="Options"/> uses
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

    /// <summary>How many option slots the vocabulary declares — the loop bound for options and validation.</summary>
    private const int OptionSlots = 8;

    private readonly struct Option
    {
        internal readonly string Title;
        internal readonly string Value;
        internal readonly string Description;

        internal Option(string title, string value, string description)
        {
            Title = title;
            Value = value;
            Description = description;
        }

        /// <summary>The help identity this option publishes: its declared description, or its value when it
        /// declares none, so the hovered option is always named.</summary>
        internal string Help => Description.Length > 0 ? Description : Value;
    }
}
