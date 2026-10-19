using System;
using System.Collections.Generic;

namespace FerriteLib.UiKit.Kernel;

/// <summary>A local input region. Decoration is represented by no regions, not an opaque outer box.</summary>
public readonly struct UiPointerRegion
{
    public UiPointerRegion(UiLayoutRect bounds, bool eligible = true)
    {
        Bounds = bounds;
        Eligible = eligible;
    }
    public UiLayoutRect Bounds { get; }
    public bool Eligible { get; }
}

/// <summary>One current draw-order fact, in a common host coordinate space.</summary>
public readonly struct UiPointerLayer
{
    public UiPointerLayer(string owner, UiLayoutRect bounds, UiLayoutRect? clip = null, bool eligible = true)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        Bounds = bounds;
        Clip = clip;
        Eligible = eligible;
    }
    public string Owner { get; }
    public UiLayoutRect Bounds { get; }
    public UiLayoutRect? Clip { get; }
    public bool Eligible { get; }
}

/// <summary>Stateless topmost input decision. The existing interaction session remains the capture owner.</summary>
public static class UiPointerLayers
{
    public static bool IsCovered(IReadOnlyList<UiPointerLayer> bottomToTop, string owner, float x, float y)
    {
        int ownOrder = -1;
        for (int i = bottomToTop.Count - 1; i >= 0; i--)
            if (bottomToTop[i].Owner == owner) { ownOrder = i; break; }
        if (ownOrder < 0) return false;
        for (int i = bottomToTop.Count - 1; i > ownOrder; i--)
        {
            UiPointerLayer layer = bottomToTop[i];
            if (layer.Owner == owner || !layer.Eligible || !Contains(layer.Bounds, x, y)) continue;
            if (!layer.Clip.HasValue || Contains(layer.Clip.Value, x, y)) return true;
        }
        return false;
    }
    private static bool Contains(UiLayoutRect rect, float x, float y)
        => rect.Width > 0 && rect.Height > 0 && x >= rect.X && x < rect.X + rect.Width && y >= rect.Y && y < rect.Y + rect.Height;
}
