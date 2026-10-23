using System;
namespace FerriteLib.UiKit.Kernel;
internal static partial class UiAppearanceProperties
{
    internal static UiColour? ReadColour(UiTheme theme, string name)
        => UiPalette.TryRole(name, out UiPaletteRole role) ? theme.Colour(role) : throw new ArgumentException("Unknown palette role '" + name + "'.");
    internal static void ApplyColour(UiTheme theme, string name, UiColour? value)
    {
        if (!value.HasValue || !UiPalette.TryRole(name, out UiPaletteRole role)) throw new ArgumentException("A valid palette role and colour are required.");
        theme.Palette[role] = value.Value;
    }
}


