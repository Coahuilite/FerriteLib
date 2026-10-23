using System;
using System.Collections.Generic;
using System.Globalization;

namespace FerriteLib.UiKit.Kernel.Widgets;

/// <summary>
/// Greenfield dropdown/select control. The current value is a typed string binding keyed by
/// <c>Bind</c> (falling back to the element Id); options come from a typed
/// <c>BindOptions&lt;string&gt;</c> binding or static OptionN/Values attributes. The field's plane and
/// its text come from the theme's <see cref="UiStyleTable"/>; its height and text inset from the
/// theme's <see cref="UiGeometry"/>. Popup state lives in <see cref="UiSession"/> and is drawn by
/// <see cref="UiHost"/> after content.
/// <para>
/// <b>Appearance is not kind, and not colour.</b> One selection BEHAVIOUR (typed/string read-back, the
/// one session-owned popup, the option-help publication) serves every field look; only the field's own
/// painting branches. The looks are declared once here through the shared seam
/// (<see cref="UiAppearanceResolver"/>): the default <c>field</c> is the plane this kind has always
/// drawn - old consumers keep it by omission - and <c>selector</c> is the reserved-rail plus arrow-zone
/// field, drawn through the same shared outlets a consumer composite calls
/// (<see cref="UiThemeDraw.SelectorArrowZone"/> / <see cref="UiThemeDraw.SelectorTextOutlet"/> /
/// <see cref="UiThemeDraw.SelectorArrow"/>), so the core kind and the adopted composite cannot diverge.
/// Colours stay tokens (the status ladder resolved for open/current state); the rail width, arrow slot
/// and glyph are these look's shape constants. A second dropdown kind would freeze vocabulary the
/// attribute already carries; a reserved help slot is deliberately NOT part of either look.
/// </para>
/// </summary>
internal sealed class DropdownWidget : IUiWidget, IUiMeasureParticipant, IUiLabelledWidget, IUiPointerLayout, IUiRetainedDisplay
{
    public const string Kind = "input/dropdown";

    /// <summary>This kind's default look: the plain status-toned field, drawn exactly as before.</summary>
    internal const string FieldLook = "field";

    /// <summary>The reserved-rail selector field: accent line left, independent arrow zone right.</summary>
    internal const string SelectorLook = "selector";

    /// <summary>
    /// This kind's appearance seam: the accepted looks, the default, and the one resolver every half of
    /// this file goes through - registered with the kind, so the creation contract and the drawing
    /// resolve against the same set (see <see cref="CheckboxWidget"/> for the pattern's origin).
    /// </summary>
    internal static readonly UiAppearanceResolver Looks = new UiAppearanceResolver(FieldLook, SelectorLook);

    private const float LabelWidth = 80f;

    private UiElementSpec spec = UiElementSpec.Empty;
    private readonly UiRetainedPaint retained = new();
    bool IUiRetainedDisplay.PaintLast(UiLayoutRect rect, UiWidgetContext ctx) => retained.Paint(rect,ctx);
    UiMeasurement? IUiRetainedDisplay.MeasureLast(UiWidgetContext ctx) => retained.Measure(ctx);

    string IUiWidget.Kind => Kind;

    public static void Register(){UiWidgetRegistry.Register(UiWidgetRegistry.CoreScope,Kind,()=>new DropdownWidget(),new[]{"Id","Kind","Bind","OptionsBind","Height","Label","LabelKey","Tab","Hidden",UiAppearanceResolver.Attribute,OptionHelp.Attribute},new[]{"Label","LabelKey"},null,Looks);}

    public void Configure(UiElementSpec spec)
    {
        this.spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public void Validate(IUiBindingReader bindings,string elementPath){if(!Looks.Resolve(spec).Declared && DeclaresAppearance(spec))throw new InvalidOperationException("Unknown dropdown appearance at " + elementPath);UiChoiceSource.Validate(bindings,spec,elementPath);OptionHelp.Validate(bindings,spec,elementPath,Kind);}

    public UiMeasurement CaptureMeasurement(UiWidgetContext ctx)
    {
        string label = ReadLabel(ctx);
        return new UiMeasurement(ctx.Node?.SharedLabelWidth > 0 ? AtomVocabulary.LabelMeasureInputs(ctx, label) : new UiMeasureInputs(numbers: new[] { ctx.Theme.Geometry.RowHeight }), () =>
            ReadHeight(ctx.Node?.SharedLabelWidth > 0
                ? UiSliderLayout.Measure(label, ctx.ViewWidth, AtomVocabulary.TextFont(ctx), UiElementProperties.DensityOf(ctx.Theme), ctx.Metrics, ctx.Node.SharedLabelWidth).Height
                : ctx.Theme.Geometry.RowHeight));
    }

    public float MeasureLabel(UiWidgetContext ctx) { string label=ReadLabel(ctx);return label.Length==0?0:ctx.Metrics.MeasureWidth(label,AtomVocabulary.TextFont(ctx))+ctx.Theme.Geometry.Spacing; }
    public float Measure(UiWidgetContext ctx) => CaptureMeasurement(ctx).Calculate();

    public IEnumerable<UiPointerRegion> PointerRegions(UiWidgetContext ctx,UiLayoutRect bounds){UiLayoutRect field=ResolveField(bounds,ctx);yield return new UiPointerRegion(field,UiChoiceSource.Read(ctx.Bindings,spec).Count>0);}

    public void Draw(UiLayoutRect rect,UiWidgetContext ctx){
 if(rect.Width<=1||rect.Height<=1)return;
 string key=ReadBindKey();UiValueEndpoint endpoint=ctx.Bindings.Value(key);object? current=endpoint.Read();
 var choices=UiChoiceSource.Read(ctx.Bindings,spec);var values=new List<object?>(choices.Count);foreach(var choice in choices)values.Add(choice.Value);
 ctx.Node.Selection=UiValueBehavior.Observe(values,current,_=>endpoint.Writable);if(choices.Count==0)return;
 string display=FindChoiceText(choices,current),label=ReadLabel(ctx);UiLayoutRect field=ResolveField(rect,ctx,label);
 ctx.Controls.DropdownButton(field,key,ctx,ctx.Node.Id.Key);if(!ctx.Session.IsActive)return;
 bool selected=ctx.Session.IsPopupOpen(key,ctx.Node)||ctx.Node.Selection.Value.Index>=0;
 PaintLabelledField(rect,field,display,selected,ctx,label);
 retained.Capture(ctx,(r,c)=>PaintLabelledField(r,ResolveField(r,c,label),display,selected,c,label));
 if(ctx.Session.IsPopupOpen(key,ctx.Node)&&ctx.Session.OpenPopupAnchor.HasValue){UiLayoutRect anchor=ctx.Session.OpenPopupAnchor.Value;
 ctx.Session.RegisterPopupDraw(()=>{int hover=UiPopup.DrawChoiceList(UiPopup.RectFor(anchor,choices.Count,ctx.Session.HostViewport),key,ctx,choices,current,v=>endpoint.TryWrite(v),endpoint.Writable);
 string help=hover>=0&&hover<choices.Count?choices[hover].Help:"";OptionHelp.Publish(ctx,OptionHelp.SinkKey(spec),help);if(help.Length>0)ctx.Session.ClaimHover(help);});
 }else OptionHelp.Publish(ctx,OptionHelp.SinkKey(spec),"");
}

    private UiSliderLayout? SharedLabelLayout(UiWidgetContext ctx, float width, string? capturedLabel = null)
        => ctx.Node?.SharedLabelWidth > 0
            ? UiSliderLayout.Measure(capturedLabel ?? ReadLabel(ctx), width, AtomVocabulary.TextFont(ctx), UiElementProperties.DensityOf(ctx.Theme),
                ctx.Metrics, ctx.Node.SharedLabelWidth)
            : null;

    private UiLayoutRect ResolveField(UiLayoutRect rect, UiWidgetContext ctx, string? capturedLabel = null)
    {
        UiSliderLayout? shared = SharedLabelLayout(ctx, rect.Width, capturedLabel);
        if (shared.HasValue)
        {
            UiSliderLayout layout = shared.Value;
            if (layout.LabelWidth <= 0) return rect;
            float gap = ctx.Theme.Geometry.Gap;
            return layout.Stacked
                ? new UiLayoutRect(rect.X, rect.Y + layout.LabelHeight + gap, rect.Width, Math.Max(1, rect.Height - layout.LabelHeight - gap))
                : new UiLayoutRect(rect.X + layout.LabelWidth + gap, rect.Y, Math.Max(1, rect.Width - layout.LabelWidth - gap), rect.Height);
        }
        // No group opt-in keeps the established fixed-column entry geometry.
        float labelWidth = (capturedLabel ?? ReadLabel(ctx)).Length > 0 ? LabelWidth : 0;
        return new UiLayoutRect(rect.X + labelWidth, rect.Y, Math.Max(1, rect.Width - labelWidth), rect.Height);
    }

    private void PaintLabelledField(UiLayoutRect rect, UiLayoutRect field, string display, bool selected, UiWidgetContext ctx, string? capturedLabel = null)
    {
        string label = capturedLabel ?? ReadLabel(ctx);
        if (label.Length > 0)
        {
            UiSliderLayout? shared = SharedLabelLayout(ctx, rect.Width, capturedLabel);
            float width = shared?.LabelWidth ?? LabelWidth;
            float height = shared?.Stacked == true ? shared.Value.LabelHeight : rect.Height;
            UiThemeDraw.Label(ctx.Drawing,new UiLayoutRect(rect.X, rect.Y, width, height), label, ctx.Theme, ctx.Theme[UiPaletteRole.Foreground],
                AtomVocabulary.TextFont(ctx), UiTextAnchor.MiddleLeft, singleLine: !shared.HasValue);
        }
        DrawField(ctx.Drawing,field, display, selected, ctx.Theme, Looks.Resolve(spec).Value, AtomVocabulary.TextFont(ctx));

    }
    /// <summary>True when the resolved look is the selector field; the name is matched case-insensitively.</summary>
    private static bool IsSelector(string look)
    {
        return string.Equals(look, SelectorLook, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>True when the element declared a non-blank look at all; a blank declaration is the default.</summary>
    private static bool DeclaresAppearance(UiElementSpec spec)
    {
        return spec.TryGetAttribute(UiAppearanceResolver.Attribute, out string raw)
            && !string.IsNullOrWhiteSpace(raw);
    }

    /// <summary>
    /// The label the field shows for the current value: the first option whose value IS that value, by identity.
    /// There is no text pass and no raw-value fallback here - a typed value that no option carries is not a
    /// spelling to be displayed, and inventing one would be the round-trip this path removes.
    /// </summary>
    private static string FindChoiceText(IReadOnlyList<UiChoice<object?>> choices, object? current)
    {
        foreach (UiChoice<object?> choice in choices)
        {
            if (Equals(choice.Value, current))
            {
                return choice.Text;
            }
        }

        return "";
    }

    /// <summary>
    /// The key the element reads its options from: its declared <c>OptionsBind</c>, else its own value key. The
    /// typed path uses the same rule, so a page that binds its choices under the value key works without
    /// declaring anything extra.
    /// </summary>
    private string ReadOptionsKey(string bindKey)
    {
        return spec.TryGetAttribute("OptionsBind", out string optionsKey) && optionsKey.Length > 0 ? optionsKey : bindKey;
    }

    private static void DrawField(IUiDrawing drawing,UiLayoutRect rect, string display, bool selected, UiTheme theme, string look, UiFont font)
    {
        // The mapping is a table query, not a second copy of it: this method used to re-derive the
        // three tokens the status outlet already computes, which is how the two drifted apart. The style
        // ladder below is ONE resolution for both looks - the selector changes the field's SHAPE, never
        // which state paints.
        UiResolvedStyle style = theme.Styles.Resolve(selected ? UiStatusTone.Active : UiStatusTone.Neutral);
        if (IsSelector(look))
        {
            DrawSelectorField(drawing,rect, display, style, theme, font);
            return;
        }

        float padding = theme.Geometry.Padding;
        UiThemeDraw.Surface(drawing,rect, style.Surface, theme.Geometry.Hairline);
        UiThemeDraw.Label(
drawing,            new UiLayoutRect(rect.X + padding, rect.Y, rect.Width - padding * 2f, rect.Height),
            display,
            theme,
            style.Text,
            font,
            UiTextAnchor.MiddleLeft,
            singleLine: true);
    }

    /// <summary>
    /// The selector field is the shared <see cref="UiThemeDraw.SelectorField"/> entry itself - the core kind
    /// composes through the very method a consumer composite calls, so "the same outlets" means the same
    /// CODE, not two spellings of the same arithmetic. No question-mark slot exists in either look.
    /// </summary>
    private static void DrawSelectorField(IUiDrawing drawing,UiLayoutRect rect, string display, UiResolvedStyle style, UiTheme theme, UiFont font)
    {
        UiThemeDraw.SelectorField(drawing,rect, display, theme, style, font);
    }

    private string ReadBindKey()
    {
        if (spec.TryGetAttribute("Bind", out string bindKey) && bindKey.Length > 0)
        {
            return bindKey;
        }

        return spec.Id;
    }

    private string ReadLabel(UiWidgetContext ctx)
    {
        if (spec.TryGetAttribute("LabelKey", out string key) && key.Length > 0)
        {
            return ctx.Translation.Translate(key);
        }

        return spec.TryGetAttribute("Label", out string label) ? label : "";
    }

    private float ReadHeight(float fallback)
    {
        if (spec.TryGetAttribute("Height", out string raw)
            && float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float height)
            && height > 0f)
        {
            return height;
        }

        return fallback;
    }
}




