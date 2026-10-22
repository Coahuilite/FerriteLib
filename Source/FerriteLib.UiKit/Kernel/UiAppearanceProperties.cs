using System;
using System.Globalization;

namespace FerriteLib.UiKit.Kernel;

internal static class UiAppearanceProperties
{
    internal static readonly string[] Colours = Enum.GetNames(typeof(UiPaletteRole));
    internal static readonly string[] Metrics = { "Padding", "Spacing", "Gap", "RowHeight", "Hairline" };
    internal static bool IsMetric(string name) => Array.IndexOf(Metrics, name) >= 0;
    internal static bool IsMetricAttribute(string name) => name.StartsWith("Metric.", StringComparison.Ordinal) && IsMetric(name.Substring(7));
    internal static bool TryMetric(string text, out float value) => float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
        && value >= 0 && !float.IsNaN(value) && !float.IsInfinity(value);
    internal static UiPropertyDeclaration<float> MetricDeclaration(string name, Action? changed)
    {
        if (!IsMetric(name)) throw new ArgumentException("Unknown density metric '" + name + "'.", nameof(name));
        return new UiPropertyDeclaration<float>(changed, value =>
        {
            if (value < 0 || float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(nameof(value), "Density metric must be finite and non-negative.");
        });
    }
    internal static bool IsPaletteAttribute(string name) => name.StartsWith("Palette.", StringComparison.Ordinal) && UiPalette.TryRole(name.Substring(8), out _);
    internal static bool IsColour(string role) => UiPalette.TryRole(role, out _);
    internal static UiColour? ReadColour(UiTheme theme, string name)
        => UiPalette.TryRole(name, out UiPaletteRole role) ? theme.Colour(role) : throw new ArgumentException("Unknown palette role '" + name + "'.");
    internal static void ApplyColour(UiTheme theme, string name, UiColour? value)
    {
        if (!value.HasValue || !UiPalette.TryRole(name, out UiPaletteRole role)) throw new ArgumentException("A valid palette role and colour are required.");
        theme.Palette[role] = value.Value;
    }
    internal static float ReadMetric(UiDensity density, string token) => token switch
    {
        "Padding" => density.Padding,
        "Spacing" => density.Spacing,
        "Gap" => density.Gap,
        "RowHeight" => density.RowHeight,
        "Hairline" => density.Hairline,
        _ => throw new ArgumentException("Unknown density metric '" + token + "'.")
    };
}
