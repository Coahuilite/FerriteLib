using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel.Widgets;

/// <summary>
/// Greenfield dropdown/select control. The current value is a typed string binding keyed by
/// <c>Bind</c> (falling back to the element Id); options come from a typed
/// <c>BindOptions&lt;string&gt;</c> binding or static OptionN/Values attributes. Popup state lives
/// in <see cref="UiSession"/> and is drawn by <see cref="UiHost"/> after content.
/// </summary>
public sealed class DropdownWidget : IUiWidget
{
    public const string Kind = "input/dropdown";

    private const float DefaultHeight = 28f;
    private const float LabelWidth = 80f;
    private const float TextPadding = 6f;

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
                "Option1", "Value1", "Option2", "Value2", "Option3", "Value3", "Option4", "Value4",
                "Option5", "Value5", "Option6", "Value6", "Option7", "Value7", "Option8", "Value8",
                "Option9", "Value9", "Option10", "Value10", "Option11", "Value11", "Option12", "Value12",
                "Option13", "Value13", "Option14", "Value14", "Option15", "Value15", "Option16", "Value16"
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
                $"DropdownWidget at '{elementPath}' requires a non-empty Id or Bind to use as its typed binding key.");
        }

        bindings.ValidateValue<string>(bindKey, elementPath);

        if (spec.TryGetAttribute("OptionsBind", out string optionsKey) && optionsKey.Length > 0)
        {
            bindings.ValidateOptions<string>(optionsKey, elementPath);
        }
    }

    public float Measure(UiWidgetContext ctx)
    {
        return ReadHeight();
    }

    public void Draw(Rect rect, UiWidgetContext ctx)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;

        string bindKey = ReadBindKey();
        ctx.Bindings.TryGet(bindKey, out string current);
        List<Option> options = BuildOptions(ctx);
        if (options.Count == 0) return;

        string label = ReadLabel(ctx);
        float labelWidth = label.Length > 0 ? LabelWidth : 0f;
        Rect fieldRect = new(rect.x + labelWidth, rect.y, Math.Max(1f, rect.width - labelWidth), rect.height);
        if (labelWidth > 0f)
        {
            // The field label lives in a fixed LabelWidth column: it is a single line by design, so the
            // fitting audit must measure its width rather than assume wrapping will rescue a long word.
            UiThemeDraw.Label(new Rect(rect.x, rect.y, labelWidth, rect.height), label, ctx.Theme, ctx.Theme.TextPrimary, UiFont.Small, TextAnchor.MiddleLeft, singleLine: true);
        }

        string display = FindDisplayText(options, current);
        bool open = ctx.Session.IsPopupOpen(bindKey);
        DrawField(fieldRect, display, open || current.Length > 0, ctx.Theme);

        // The trigger click stores the popup anchor in Host window space (the engine translates
        // draw rects inside scrolls/groups); the popup pass draws and hit-tests in that same
        // stored space, so both share one anchor source.
        UiNative.DropdownButton(fieldRect, bindKey, ctx);

        if (ctx.Session.IsPopupOpen(bindKey))
        {
            Rect? anchor = ctx.Session.OpenPopupAnchor;
            if (anchor.HasValue)
            {
                ctx.Session.RegisterPopupDraw(() => DrawPopup(anchor.Value, options, current, ctx));
            }
        }
    }

    private void DrawPopup(Rect anchor, List<Option> options, string current, UiWidgetContext ctx)
    {
        var pairs = new List<KeyValuePair<string, string>>(options.Count);
        foreach (Option option in options)
        {
            pairs.Add(new KeyValuePair<string, string>(option.Text, option.Value));
        }

        string bindKey = ReadBindKey();
        UiPopup.DrawOptionList(
            UiPopup.RectFor(anchor, options.Count, ctx.Session.HostViewport),
            bindKey,
            ctx,
            pairs,
            current,
            value => ctx.Bindings.Set(bindKey, value));
    }

    private static void DrawField(Rect rect, string display, bool selected, UiTheme theme)
    {
        UiThemeDraw.Surface(rect, theme, selected ? theme.Selected : theme.Raised, selected ? theme.AccentGold : theme.Border);
        UiThemeDraw.Label(
            new Rect(rect.x + TextPadding, rect.y, rect.width - TextPadding * 2f, rect.height),
            display,
            theme,
            selected ? theme.TextOnGold : theme.TextPrimary,
            UiFont.Small,
            TextAnchor.MiddleLeft,
            singleLine: true);
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

            spec.TryGetAttribute("Value" + suffix, out string value);
            result.Add(new Option(text, value.Length > 0 ? value : text));
        }

        return result;
    }

    private static string FindDisplayText(List<Option> options, string current)
    {
        foreach (Option option in options)
        {
            if (string.Equals(option.Value, current, StringComparison.Ordinal)
                || string.Equals(option.Text, current, StringComparison.Ordinal))
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

    private float ReadHeight()
    {
        if (spec.TryGetAttribute("Height", out string raw)
            && float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float height)
            && height > 0f)
        {
            return height;
        }

        return DefaultHeight;
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
