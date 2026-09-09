using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel.Widgets;

/// <summary>
/// Greenfield mode-row selector. Reads a string value binding (keyed by <c>Bind</c>, falling back
/// to the element Id) and writes the selected mode string through the same binding. Layout is
/// responsive: wide uses one row, medium uses two columns, narrow stacks vertically.
/// </summary>
public sealed class InputModeRowWidget : IUiWidget
{
    public const string Kind = "input/mode-row";

    private const float DefaultHeight = 28f;
    private const float Gap = 6f;
    private const float Padding = 4f;

    private UiElementSpec spec = UiElementSpec.Empty;

    string IUiWidget.Kind => Kind;

    public static void Register()
    {
        UiWidgetRegistry.Register(
            UiWidgetRegistry.CoreScope,
            Kind,
            () => new InputModeRowWidget(),
            new[] {
                "Id", "Kind", "Bind", "Height", "Tab", "Hidden",
                "Value1", "Title1", "Description1", "Value2", "Title2", "Description2",
                "Value3", "Title3", "Description3", "Value4", "Title4", "Description4",
                "Value5", "Title5", "Description5", "Value6", "Title6", "Description6",
                "Value7", "Title7", "Description7", "Value8", "Title8", "Description8"
            },
            new[] {
                "Title1", "Description1", "Title2", "Description2", "Title3", "Description3",
                "Title4", "Description4", "Title5", "Description5", "Title6", "Description6",
                "Title7", "Description7", "Title8", "Description8"
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
    }

    public float Measure(UiWidgetContext ctx)
    {
        int columns = ColumnsFor(ctx.ViewWidth);
        int rows = (Options.Count + columns - 1) / columns;
        float rowHeight = ReadFloat("Height", DefaultHeight);
        return Math.Max(1f, rows * rowHeight + Math.Max(0, rows - 1) * Gap + Padding * 2f);
    }

    public void Draw(Rect rect, UiWidgetContext ctx)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;

        List<Option> options = Options;
        if (options.Count == 0) return;

        string bindKey = ReadBindKey();
        ctx.Bindings.TryGet(bindKey, out string current);
        int columns = ColumnsFor(rect.width);
        int rows = (options.Count + columns - 1) / columns;
        float rowHeight = ReadFloat("Height", DefaultHeight);
        float innerWidth = Math.Max(1f, rect.width - Padding * 2f);
        float columnWidth = (innerWidth - (columns - 1) * Gap) / columns;

        for (int i = 0; i < options.Count; i++)
        {
            int row = i / columns;
            int col = i % columns;
            float x = rect.x + Padding + col * (columnWidth + Gap);
            float y = rect.y + Padding + row * (rowHeight + Gap);
            var optionRect = new Rect(x, y, columnWidth, rowHeight);
            bool selected = string.Equals(options[i].Value, current, StringComparison.Ordinal);
            DrawOption(optionRect, options[i], selected, ctx.Theme);

            if (UiNative.Button(optionRect))
            {
                ctx.Bindings.Set(bindKey, options[i].Value);
            }
        }
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
        UiThemeDraw.Surface(rect, theme, selected ? theme.Selected : theme.Raised, selected ? theme.AccentGold : theme.Border);
        UiThemeDraw.Label(
            new Rect(rect.x + 6f, rect.y, rect.width - 12f, rect.height),
            option.Title,
            theme,
            selected ? theme.TextOnGold : theme.TextPrimary,
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
            for (int i = 1; i <= 8; i++)
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
        internal readonly string Value;
        internal readonly string Description;

        internal Option(string title, string value, string description)
        {
            Title = title;
            Value = value;
            Description = description;
        }
    }
}
