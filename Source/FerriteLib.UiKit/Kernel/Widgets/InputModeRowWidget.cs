using System;
using System.Collections.Generic;
using System.Globalization;

namespace FerriteLib.UiKit.Kernel.Widgets;
/// <summary>
/// Mode-row selector. Reads a typed value binding (keyed by <c>Bind</c>, falling back
/// to the element Id) and writes the chosen value through the same binding. Options come from
/// <c>BindChoices&lt;T&gt;</c> or XML <c>Choice</c> children. Layout is
/// responsive: wide uses one row, medium uses two columns, narrow stacks vertically, and the cell
/// metrics come from the theme's <see cref = "UiGeometry"/> so density is one knob for the whole tree.
/// <para>
/// <b>Localized option labels.</b> A choice's nonempty <c>TextKey</c> resolves through the translation
/// seam instead of its literal <c>Text</c>. Natural width measures that resolved label.
/// </para>
/// <para>
/// <b>Per-option help, published rather than painted.</b> Each option may carry its own help identity in
/// its explicit <c>Choice.HelpRef</c>; omitted/empty Help means no option-specific help, never Value/Text
/// inference. The kind does not draw it (a tooltip is the consumer's call, and vanilla's own
/// mode selectors render the help in a panel of their own). Two reads exist, and they are complementary:
/// declare <c>HoveredOptionHelpOutputBind</c> and the row writes the hovered option's identity into that binding
/// (<see cref = "OptionHelp"/>), and the hovered option's declared help text is also claimed on the session's
/// hover surface (<c>UiSession.PublishHelp</c>), which is the general read the element-level <c>HelpRef</c>
/// uses too. An option that declares no help text claims nothing, so the row's own <c>HelpRef</c> topic
/// stands while the pointer is over it.
/// </para>
/// </summary>
internal sealed class InputModeRowWidget : IUiWidget, IUiMeasureParticipant, IUiPointerLayout, IUiRetainedDisplay
{
    public const string Kind = "input/mode-row";
    /// <summary>
    /// The element attribute naming the binding key that receives the hovered option's help identity. Public
    /// because the option-level contract is shared with <c>input/dropdown</c> (see <see cref = "OptionHelp"/>),
    /// and this is where it was first declared.
    /// </summary>
    public const string HoveredOptionHelpOutputBindAttribute = "HoveredOptionHelpOutputBind";
    private UiElementSpec spec = UiElementSpec.Empty;
    private readonly UiRetainedPaint retained = new();
    bool IUiRetainedDisplay.PaintLast(UiLayoutRect rect, UiWidgetContext ctx) => retained.Paint(rect, ctx);
    UiMeasurement? IUiRetainedDisplay.MeasureLast(UiWidgetContext ctx) => retained.Measure(ctx);
    string IUiWidget.Kind => Kind;

    public static void Register()
    {
        UiWidgetRegistry.Define(new UiWidgetDefinition(UiWidgetRegistry.CoreScope, Kind, () => new InputModeRowWidget(), new[] { "Id", "Kind", "Bind", "OptionsBind", "Height", "Tab", "Hidden", OptionHelp.Attribute },naturalBody:NaturalBodyWidth));
    }

    private static float NaturalBodyWidth(UiElementSpec spec,UiWidgetContext ctx)
    {
        float width=0;
        foreach(var choice in UiChoiceSource.Read(ctx.Bindings,spec,ctx.Translation))
            width=Math.Max(width,ctx.Metrics.MeasureWidth(choice.Text,AtomVocabulary.TextFont(ctx)));
        return width;
    }

    public void Configure(UiElementSpec spec)
    {
        this.spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public void Validate(IUiBindingAccess bindings, string elementPath)
    {
        UiChoiceSource.Validate(bindings, spec, elementPath);
        OptionHelp.Validate(bindings, spec, elementPath, Kind);
    }

    public UiMeasurement CaptureMeasurement(UiWidgetContext ctx)
    {
        int count = OptionsFor(ctx).Count;
        retained.SetMeasure(c => CaptureCount(c, count));
        return CaptureCount(ctx, count);
    }

    private UiMeasurement CaptureCount(UiWidgetContext ctx, int count)
    {
        int columns = ColumnsFor(ctx.ViewWidth);
        return new UiMeasurement(new UiMeasureInputs(numbers: new[] { (float)count, columns, ctx.Theme.Geometry.RowHeight, ctx.Theme.Geometry.Gap, ctx.Theme.Geometry.Spacing }), () =>
        {
            int rows = (count + columns - 1) / columns;
            UiGeometry geometry = ctx.Theme.Geometry;
            return Math.Max(1, rows * ReadFloat("Height", geometry.RowHeight) + Math.Max(0, rows - 1) * geometry.Gap + geometry.Spacing * 2);
        });
    }

    public float Measure(UiWidgetContext ctx) => CaptureMeasurement(ctx).Calculate();
    public IEnumerable<UiPointerRegion> PointerRegions(UiWidgetContext ctx, UiLayoutRect bounds)
    {
        IReadOnlyList<UiChoice<object?>> options = OptionsFor(ctx);
        for (int i = 0; i < options.Count; i++)
        {
            UiLayoutRect cell = OptionRect(new UiLayoutRect(bounds.X, bounds.Y, bounds.Width, bounds.Height), ctx, i);
            yield return new UiPointerRegion(new UiLayoutRect(cell.X, cell.Y, cell.Width, cell.Height));
        }
    }

    private UiLayoutRect OptionRect(UiLayoutRect rect, UiWidgetContext ctx, int index)
    {
        UiGeometry geometry = ctx.Theme.Geometry;
        int columns = ColumnsFor(rect.Width);
        float rowHeight = ReadFloat("Height", geometry.RowHeight);
        float innerWidth = Math.Max(1, rect.Width - geometry.Spacing * 2);
        float columnWidth = (innerWidth - (columns - 1) * geometry.Gap) / columns;
        int row = index / columns;
        int column = index % columns;
        return new UiLayoutRect(rect.X + geometry.Spacing + column * (columnWidth + geometry.Gap), rect.Y + geometry.Spacing + row * (rowHeight + geometry.Gap), columnWidth, rowHeight);
    }

    public void Draw(UiLayoutRect rect, UiWidgetContext ctx)
    {
        string key = ReadBindKey();
        var endpoint = ctx.Bindings.Value(key);
        object? current = endpoint.Read();
        var options = OptionsFor(ctx);
        var values = new List<object?>();
        foreach (var c in options)
            values.Add(c.Value);
        ctx.Node.Selection = UiValueBehavior.Observe(values, current, _ => endpoint.Writable);
        string help = "";
        var selected = new bool[options.Count];
        for (int i = 0; i < options.Count; i++)
        {
            var choice = options[i];
            var cell = OptionRect(rect, ctx, i);
            selected[i] = Equals(choice.Value, current);
            DrawOption(ctx.Drawing, cell, choice, selected[i], ctx.Theme);
            if (ctx.Controls.IsMouseOver(cell, ctx))
            {
                help = choice.HelpRef;
                if (help.Length > 0)
                    ctx.Session.PublishHelp(ctx.Node, help);
            }

            var target = ctx.Controls.InteractionTarget(ctx, key, "option", choice.Value);
            var input = ctx.Controls.Activation(cell, ctx, key, "option", choice.Value, endpoint.Writable);
            UiValueBehavior.Request(target, UiValueBehavior.Selection(input, choice.Value), v => endpoint.TryWrite(v));
            if (!ctx.Session.IsActive)
                return;
        }

        OptionHelp.Publish(ctx, OptionHelp.SinkKey(spec), help);
        retained.Capture(ctx, (r, c) =>
        {
            for (int i = 0; i < options.Count; i++)
                DrawOption(c.Drawing, OptionRect(r, c, i), options[i], selected[i], c.Theme);
        });
    }

    private string ReadBindKey()
    {
        if (spec.TryGetAttribute("Bind", out string bindKey) && bindKey.Length > 0)
        {
            return bindKey;
        }

        return spec.Id;
    }

    private void DrawOption(IUiDrawing drawing, UiLayoutRect rect, UiChoice<object?> option, bool selected, UiTheme theme)
    {
        // One table query for the plane and the text, and the text inset spelled once in the theme
        // instead of as a bare 6f/12f here and a named constant in the dropdown.
        UiResolvedStyle style = theme.Styles.Resolve(selected ? UiStatusTone.Active : UiStatusTone.Neutral);
        float padding = theme.Geometry.Padding;
        UiThemeDraw.Surface(drawing, rect, style.Surface, theme.Geometry.Hairline);
        UiThemeDraw.Label(drawing, new UiLayoutRect(rect.X + padding, rect.Y, rect.Width - padding * 2f, rect.Height), option.Text, theme, style.Text, theme.Styles.Font, UiTextAnchor.MiddleLeft);
    }

    private int ColumnsFor(float width)
    {
        if (width >= 560f)
            return 4;
        if (width >= 360f)
            return 2;
        return 1;
    }

    private IReadOnlyList<UiChoice<object?>> OptionsFor(UiWidgetContext ctx) => UiChoiceSource.Read(ctx.Bindings, spec,ctx.Translation);
    private float ReadFloat(string name, float fallback)
    {
        return spec.TryGetAttribute(name, out string raw) && spec.TryGetNumber(name, out float value) ? value : fallback;
    }
}
