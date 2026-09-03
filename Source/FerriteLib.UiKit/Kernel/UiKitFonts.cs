using Verse;

namespace FerriteLib.UiKit.Kernel;

/// <summary>Single <see cref="UiFont"/> to Verse <see cref="GameFont"/> mapping for the whole library and its hosts.</summary>
public static class UiKitFonts
{
    public static GameFont ToGameFont(UiFont font)
    {
        return font switch
        {
            UiFont.Tiny => GameFont.Tiny,
            UiFont.Small => GameFont.Small,
            _ => GameFont.Medium
        };
    }
}
