using System;

namespace FerriteLib.UiKit.Kernel;

internal static class UiAppearanceProperties
{
    internal static readonly string[] Colours = Enum.GetNames(typeof(UiPaletteRole));
    internal static readonly string[] Metrics = { "Padding", "Spacing", "Gap", "RowHeight", "Hairline" };
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
