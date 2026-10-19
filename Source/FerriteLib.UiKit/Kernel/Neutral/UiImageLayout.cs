using System;

namespace FerriteLib.UiKit.Kernel;

public enum UiImageFit { Contain, Cover, Stretch }
public enum UiImageAlignX { Center, Left, Right }
public enum UiImageAlignY { Center, Top, Bottom }

/// <summary>Image placement inside its allocated slot, independent of outer layout alignment.</summary>
public readonly struct UiImageOptions
{
    public UiImageOptions(UiImageFit fit = UiImageFit.Contain,
        UiImageAlignX alignX = UiImageAlignX.Center, UiImageAlignY alignY = UiImageAlignY.Center)
    {
        if (!Enum.IsDefined(typeof(UiImageFit), fit)) throw new ArgumentOutOfRangeException(nameof(fit));
        if (!Enum.IsDefined(typeof(UiImageAlignX), alignX)) throw new ArgumentOutOfRangeException(nameof(alignX));
        if (!Enum.IsDefined(typeof(UiImageAlignY), alignY)) throw new ArgumentOutOfRangeException(nameof(alignY));
        Fit = fit;
        AlignX = alignX;
        AlignY = alignY;
    }
    public UiImageFit Fit { get; }
    public UiImageAlignX AlignX { get; }
    public UiImageAlignY AlignY { get; }

    public static UiImageOptions Parse(string? fit, string? alignX, string? alignY)
        => new(ParseName(fit, UiImageFit.Contain), ParseName(alignX, UiImageAlignX.Center),
            ParseName(alignY, UiImageAlignY.Center));

    private static T ParseName<T>(string? text, T fallback) where T : struct
    {
        if (string.IsNullOrWhiteSpace(text)) return fallback;
        string name = text!.Trim();
        foreach (string legal in Enum.GetNames(typeof(T)))
            if (string.Equals(name, legal, StringComparison.OrdinalIgnoreCase)) return (T)Enum.Parse(typeof(T), legal);
        throw new FormatException("Unknown image " + typeof(T).Name + " '" + name + "'.");
    }
}

/// <summary>Shared finite image geometry. It owns no resource and consumes no input.</summary>
public static class UiImageLayout
{
    public static UiLayoutRect Arrange(UiLayoutRect slot, float sourceWidth, float sourceHeight, UiImageOptions options = default)
    {
        if (!Positive(slot.Width) || !Positive(slot.Height) || !Positive(sourceWidth) || !Positive(sourceHeight)) return default;
        if (options.Fit == UiImageFit.Stretch) return slot;
        float scaleX = slot.Width / sourceWidth;
        float scaleY = slot.Height / sourceHeight;
        float scale = options.Fit == UiImageFit.Cover ? Math.Max(scaleX, scaleY) : Math.Min(scaleX, scaleY);
        float width = sourceWidth * scale;
        float height = sourceHeight * scale;
        float x = options.AlignX == UiImageAlignX.Left ? 0f : options.AlignX == UiImageAlignX.Right ? 1f : .5f;
        float y = options.AlignY == UiImageAlignY.Top ? 0f : options.AlignY == UiImageAlignY.Bottom ? 1f : .5f;
        return new UiLayoutRect(slot.X + (slot.Width - width) * x, slot.Y + (slot.Height - height) * y, width, height);
    }

    public static float AspectHeight(float width, float sourceWidth, float sourceHeight)
        => Positive(width) && Positive(sourceWidth) && Positive(sourceHeight) ? width / sourceWidth * sourceHeight : 0f;

    private static bool Positive(float value) => value > 0f && !float.IsInfinity(value) && !float.IsNaN(value);
}
