using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

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
public sealed class DropdownWidget : IUiWidget
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

    string IUiWidget.Kind => Kind;

    public static void Register()
    {
        UiWidgetRegistry.Register(
            UiWidgetRegistry.CoreScope,
            Kind,
            () => new DropdownWidget(),
            new[] {
                "Id", "Kind", "Bind", "OptionsBind", "Height", "Label", "LabelKey", "Tab", "Hidden",
                UiAppearanceResolver.Attribute,
                OptionHelp.Attribute,
                "Option1", "Value1", "Option2", "Value2", "Option3", "Value3", "Option4", "Value4",
                "Option5", "Value5", "Option6", "Value6", "Option7", "Value7", "Option8", "Value8",
                "Option9", "Value9", "Option10", "Value10", "Option11", "Value11", "Option12", "Value12",
                "Option13", "Value13", "Option14", "Value14", "Option15", "Value15", "Option16", "Value16"
            },
            new[] { "Label", "LabelKey" },
            null,
            Looks);
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
                $"DropdownWidget at '{elementPath}' requires a non-empty Id or Bind to use as its typed binding key.");
        }

        // The look is refused here, once, and never degraded on every frame after: the same fail-closed
        // rule the checkbox declares (see CheckboxWidget.Validate), so a mistyped appearance is located on
        // the element instead of silently drawing the default forever.
        ResolvedAppearance appearance = Looks.Resolve(spec);
        if (!appearance.Declared && DeclaresAppearance(spec))
        {
            spec.TryGetAttribute(UiAppearanceResolver.Attribute, out string authored);
            throw new InvalidOperationException(
                "DropdownWidget at '" + elementPath + "' declares "
                + UiAppearanceResolver.Attribute + "='" + (authored ?? "").Trim()
                + "'; the accepted values are '" + FieldLook + "' (the default plain field) and '"
                + SelectorLook + "' (accent rail plus arrow zone). Omit the attribute for the default.");
        }

        if (bindings is IUiTypedChoices typedSeam
            && typedSeam.TryGetValueType(bindKey, out Type declared)
            && declared != typeof(string))
        {
            // R4-A: a value bound to anything but a string is served by the TYPED path, and that path can only
            // be fed by UiChoice<T> options. A string list, a UiOption list or the static OptionN/ValueN
            // attributes carry strings only, so each of them would have to spell the value to hand it back -
            // the round-trip this path exists to remove. The refusal therefore names the typed shape instead of
            // accepting a string-shaped stand-in for it.
            ValidateTypedOptions(typedSeam, bindKey, declared, elementPath);
            OptionHelp.Validate(bindings, spec, elementPath, "DropdownWidget");
            return;
        }

        bindings.ValidateValue<string>(bindKey, elementPath);

        if (spec.TryGetAttribute("OptionsBind", out string optionsKey) && optionsKey.Length > 0)
        {
            // FL-16: strings (display == value) or UiOption pairs. The probe uses ValidateOptions, which is the
            // NON-reporting shape check - so a page that declared either accepted shape records nothing, and a
            // page that declared a third type is refused here naming both accepted shapes.
            if (!TryValidateOptions<UiOption>(bindings, optionsKey, elementPath))
            {
                try
                {
                    // The historical call stays the diagnosis for everything that is not a pair list: it already
                    // separates "missing" from "registered as the wrong kind of key", which the A5 lane pins. The
                    // pair shape is appended as the second accepted answer, never substituted for it, and the
                    // typed shape is named last so a page that meant a typed value is told how to get one.
                    bindings.ValidateOptions<string>(optionsKey, elementPath);
                }
                catch (InvalidOperationException ex)
                {
                    throw new InvalidOperationException(
                        ex.Message + " The dropdown also accepts BindOptions<UiOption> (display, value)."
                        + " A typed value (anything but a string) needs BindOptions<UiChoice<T>> with the SAME T"
                        + " as its BindValue<T>, which carries the value itself rather than its spelling.");
                }
            }
        }

        // The same option-level help contract the mode row declares, from one implementation: the popup's
        // rows are options of this element, so the engine's element-level HelpKey cannot reach them.
        OptionHelp.Validate(bindings, spec, elementPath, "DropdownWidget");
    }

    /// <summary>
    /// The typed path's creation-time contract: the options the element reads must be typed choices, and every
    /// one of them must carry a value of the value binding's own type. Both halves are checked here because a
    /// mismatch found at click time would be a control that silently does nothing, which is exactly the
    /// failure mode the typed path exists to make impossible.
    /// </summary>
    private void ValidateTypedOptions(IUiTypedChoices seam, string bindKey, Type declared, string elementPath)
    {
        string optionsKey = ReadOptionsKey(bindKey);
        if (!seam.TryGetChoices(optionsKey, out IReadOnlyList<UiChoice<object?>> choices))
        {
            throw new InvalidOperationException(
                $"Value binding '{bindKey}' at '{elementPath}' is '{declared.Name}', so this dropdown needs typed"
                + $" choices of that same type: bind BindOptions<UiChoice<{declared.Name}>>('{optionsKey}', ...)."
                + " A plain string list, a UiOption list and the static OptionN/ValueN attributes all carry strings"
                + " only, and handing a string to a typed setter is the round-trip this path removes.");
        }

        foreach (UiChoice<object?> choice in choices)
        {
            if (!seam.AcceptsValue(bindKey, choice.Value))
            {
                throw new InvalidOperationException(
                    $"Typed choices '{optionsKey}' at '{elementPath}' carry a value of type '"
                    + (choice.Value == null ? "null" : choice.Value.GetType().Name)
                    + $"' under the label '{choice.Text}', which is not the '{declared.Name}' its value binding"
                    + " accepts.");
            }
        }
    }

    public float Measure(UiWidgetContext ctx)
    {
        return ReadHeight(ctx.Theme.Geometry.RowHeight);
    }

    public void Draw(Rect rect, UiWidgetContext ctx)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;

        string bindKey = ReadBindKey();
        if (ctx.Bindings is IUiTypedChoices typedSeam
            && typedSeam.TryGetValueType(bindKey, out Type declared)
            && declared != typeof(string))
        {
            DrawTyped(rect, ctx, bindKey, typedSeam);
            return;
        }

        ctx.Bindings.TryGet(bindKey, out string current);
        List<Option> options = BuildOptions(ctx);
        if (options.Count == 0) return;

        string display = FindDisplayText(options, current);
        bool open = ctx.Session.IsPopupOpen(bindKey);
        Rect fieldRect = DrawLabelledField(rect, ReadLabel(ctx), display, open || current.Length > 0, ctx);

        // The trigger click stores the popup anchor in Host window space (the engine translates
        // draw rects inside scrolls/groups); the popup pass draws and hit-tests in that same
        // stored space, so both share one anchor source.
        UiNative.DropdownButton(fieldRect, bindKey, ctx);

        string helpSink = OptionHelp.SinkKey(spec);
        if (ctx.Session.IsPopupOpen(bindKey))
        {
            Rect? anchor = ctx.Session.OpenPopupAnchor;
            if (anchor.HasValue)
            {
                ctx.Session.RegisterPopupDraw(() => DrawPopup(anchor.Value, options, current, ctx));
            }
        }
        else
        {
            // A closed popup has no hovered option, so the sink is cleared here rather than left holding the
            // last row's identity. Change-detected, so a closed dropdown costs no writes at all.
            OptionHelp.Publish(ctx, helpSink, "");
        }
    }

    /// <summary>
    /// The element's field, for both draw paths: the optional single-line label in its fixed column, the value
    /// plane and the text outlet, with the layout arithmetic in one place. Returns the field rect, which is what
    /// the trigger's hit test and the popup anchor are taken from. The look is resolved HERE - once per frame,
    /// through the same seam the creation contract and the natural-body entry point use - so the field that
    /// was refused at creation is never a different field at draw.
    /// </summary>
    private Rect DrawLabelledField(Rect rect, string label, string display, bool selected, UiWidgetContext ctx)
    {
        float labelWidth = label.Length > 0 ? LabelWidth : 0f;
        Rect fieldRect = new(rect.x + labelWidth, rect.y, Math.Max(1f, rect.width - labelWidth), rect.height);
        if (labelWidth > 0f)
        {
            // The field label lives in a fixed LabelWidth column: it is a single line by design, so the
            // fitting audit must measure its width rather than assume wrapping will rescue a long word.
            UiThemeDraw.Label(new Rect(rect.x, rect.y, labelWidth, rect.height), label, ctx.Theme, ctx.Theme.TextPrimary, UiFont.Small, TextAnchor.MiddleLeft, singleLine: true);
        }

        DrawField(fieldRect, display, selected, ctx.Theme, Looks.Resolve(spec).Value);
        return fieldRect;
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

    // --- R4-A: the typed path, taken when the value binding's declared type is not a string ------------------

    /// <summary>
    /// Draws the element from TYPED choices: the field shows the chosen option's own label, the popup lists the
    /// same labels, and choosing one hands the option's real value to the typed setter through
    /// <see cref="IUiTypedChoices"/>. Nothing here spells a value: the current value is matched to an option by
    /// identity, so a value that no option carries displays nothing rather than its own text, and an unmatched
    /// value can never be written back by a click.
    /// </summary>
    private void DrawTyped(Rect rect, UiWidgetContext ctx, string bindKey, IUiTypedChoices seam)
    {
        if (!seam.TryGetChoices(ReadOptionsKey(bindKey), out IReadOnlyList<UiChoice<object?>> choices) || choices.Count == 0)
        {
            return;
        }

        bool hasCurrent = seam.TryGetTypedValue(bindKey, out object? current);
        string display = hasCurrent ? FindChoiceText(choices, current) : "";
        bool open = ctx.Session.IsPopupOpen(bindKey);
        Rect fieldRect = DrawLabelledField(rect, ReadLabel(ctx), display, open || hasCurrent, ctx);

        // The trigger click stores the popup anchor in Host window space, exactly as the string path does: the
        // anchor source is the primitive, not this path's choice.
        UiNative.DropdownButton(fieldRect, bindKey, ctx);

        string helpSink = OptionHelp.SinkKey(spec);
        if (ctx.Session.IsPopupOpen(bindKey))
        {
            Rect? anchor = ctx.Session.OpenPopupAnchor;
            if (anchor.HasValue)
            {
                ctx.Session.RegisterPopupDraw(() => DrawTypedPopup(anchor.Value, bindKey, choices, current, ctx, seam));
            }
        }
        else
        {
            OptionHelp.Publish(ctx, helpSink, "");
        }
    }

    private void DrawTypedPopup(
        Rect anchor,
        string bindKey,
        IReadOnlyList<UiChoice<object?>> choices,
        object? current,
        UiWidgetContext ctx,
        IUiTypedChoices seam)
    {
        int hovered = UiPopup.DrawChoiceList(
            UiPopup.RectFor(anchor, choices.Count, ctx.Session.HostViewport),
            bindKey,
            ctx,
            choices,
            current,
            value =>
            {
                // A refused write (a value of the wrong type, or a read-only binding) changes nothing at all:
                // TrySetTypedValue reports the refusal instead of throwing or coercing.
                seam.TrySetTypedValue(bindKey, value);
            });

        // A typed option's option-level identity is its LABEL: the value is not a string, so the popup cannot
        // report one, and the label is the only string the author gave this option.
        OptionHelp.Publish(ctx, OptionHelp.SinkKey(spec), hovered >= 0 && hovered < choices.Count ? choices[hovered].Text : "");
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

    private void DrawPopup(Rect anchor, List<Option> options, string current, UiWidgetContext ctx)
    {
        var pairs = new List<KeyValuePair<string, string>>(options.Count);
        foreach (Option option in options)
        {
            pairs.Add(new KeyValuePair<string, string>(option.Text, option.Value));
        }

        string bindKey = ReadBindKey();
        string hovered = UiPopup.DrawOptionList(
            UiPopup.RectFor(anchor, options.Count, ctx.Session.HostViewport),
            bindKey,
            ctx,
            pairs,
            current,
            value => ctx.Bindings.Set(bindKey, value));

        // The option-level identity is the option's VALUE: a dropdown's options come from the consumer's data
        // (an options binding or the static OptionN/ValueN pairs), so the value is the machine token its help
        // catalog is keyed by - the same choice the mode row falls back to when a cell declares no help text.
        OptionHelp.Publish(ctx, OptionHelp.SinkKey(spec), hovered);
    }

    private static void DrawField(Rect rect, string display, bool selected, UiTheme theme, string look)
    {
        // The mapping is a table query, not a second copy of it: this method used to re-derive the
        // three tokens the status outlet already computes, which is how the two drifted apart. The style
        // ladder below is ONE resolution for both looks - the selector changes the field's SHAPE, never
        // which state paints.
        UiResolvedStyle style = theme.Styles.Resolve(selected ? UiStatusTone.Active : UiStatusTone.Neutral);
        if (IsSelector(look))
        {
            DrawSelectorField(rect, display, style, theme);
            return;
        }

        float padding = theme.Geometry.Padding;
        UiThemeDraw.Surface(rect, style.Surface, theme.Geometry.Hairline);
        UiThemeDraw.Label(
            new Rect(rect.x + padding, rect.y, rect.width - padding * 2f, rect.height),
            display,
            theme,
            style.Text,
            UiFont.Small,
            TextAnchor.MiddleLeft,
            singleLine: true);
    }

    /// <summary>
    /// The selector field is the shared <see cref="UiThemeDraw.SelectorField"/> entry itself - the core kind
    /// composes through the very method a consumer composite calls, so "the same outlets" means the same
    /// CODE, not two spellings of the same arithmetic. No question-mark slot exists in either look.
    /// </summary>
    private static void DrawSelectorField(Rect rect, string display, UiResolvedStyle style, UiTheme theme)
    {
        UiThemeDraw.SelectorField(rect, display, theme, style);
    }

    private string ReadBindKey()
    {
        if (spec.TryGetAttribute("Bind", out string bindKey) && bindKey.Length > 0)
        {
            return bindKey;
        }

        return spec.Id;
    }

    private List<Option> BuildOptions(UiWidgetContext ctx)
    {
        if (spec.TryGetAttribute("OptionsBind", out string optionsKey) && optionsKey.Length > 0)
        {
            // The shape is probed non-reportingly first, then read EXACTLY once, so the authoritative read is
            // always the matching element type: a valid page can never record the FL-23 mismatch report.
            if (TryValidateOptions<UiOption>(ctx.Bindings, optionsKey, ctx.ElementPath))
            {
                IReadOnlyList<UiOption> pairs = ctx.Bindings.GetOptions<UiOption>(optionsKey);
                var pairResult = new List<Option>(pairs.Count);
                foreach (UiOption item in pairs)
                {
                    pairResult.Add(new Option(item.Text.Length > 0 ? item.Text : item.Value, item.Value));
                }

                return pairResult;
            }

            IReadOnlyList<string> dynamic = ctx.Bindings.GetOptions<string>(optionsKey);
            var result = new List<Option>(dynamic.Count);
            foreach (string item in dynamic)
            {
                result.Add(new Option(item, item));
            }

            return result;
        }

        return ParseStaticOptions();
    }

    private List<Option> ParseStaticOptions()
    {
        var result = new List<Option>();
        for (int i = 1; i <= 16; i++)
        {
            string suffix = i.ToString(CultureInfo.InvariantCulture);
            if (!spec.TryGetAttribute("Option" + suffix, out string text) || text.Length == 0)
            {
                continue;
            }

            spec.TryGetAttribute("Value" + suffix, out string? value);
            result.Add(new Option(text, string.IsNullOrEmpty(value) ? text : value));
        }

        return result;
    }

    private static string FindDisplayText(List<Option> options, string current)
    {
        // A4 (0.7): two passes, value first. The old per-item "value OR text" test let an earlier
        // option whose display text happened to equal the bound value hide a later option whose
        // VALUE is exactly that value; the bound value is the authoritative key, so any exact
        // value match wins outright and the text fallback only runs when no value matched.
        // Matching stays ordinal and option order stays authoritative within each pass.
        foreach (Option option in options)
        {
            if (string.Equals(option.Value, current, StringComparison.Ordinal))
            {
                return option.Text;
            }
        }

        foreach (Option option in options)
        {
            if (string.Equals(option.Text, current, StringComparison.Ordinal))
            {
                return option.Text;
            }
        }

        return current;
    }

    private string ReadLabel(UiWidgetContext ctx)
    {
        if (spec.TryGetAttribute("LabelKey", out string key) && key.Length > 0)
        {
            return ctx.Translation.Translate(key);
        }

        return spec.TryGetAttribute("Label", out string label) ? label : "";
    }

    /// <summary>
    /// True when the key is an options binding whose element type is <typeparamref name="T"/>. This is the
    /// NON-reporting probe FL-23 requires: ValidateOptions throws on a mismatch and records nothing, so trying
    /// the other accepted shape never emits a diagnostic for the shape a page did not declare.
    /// </summary>
    private static bool TryValidateOptions<T>(IUiBindings bindings, string key, string elementPath)
    {
        try
        {
            bindings.ValidateOptions<T>(key, elementPath);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
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

    private readonly struct Option
    {
        internal readonly string Text;
        internal readonly string Value;

        internal Option(string text, string value)
        {
            Text = text;
            Value = value;
        }
    }
}
