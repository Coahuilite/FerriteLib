using System;
using System.Collections.Generic;

namespace FerriteLib.UiKit.Kernel;
internal readonly struct UiChartCoordinate
{
    public UiChartCoordinate(float x, float y)
    {
        X = x;
        Y = y;
    }

    public float X { get; }
    public float Y { get; }

    public bool Same(UiChartCoordinate other) => X == other.X && Y == other.Y;
}

/// <summary>Observed identity evidence only. The interaction session remains the sole capture owner.</summary>
internal sealed class UiChartIdentity
{
    private UiChartCoordinate[] previous = Array.Empty<UiChartCoordinate>();
    private bool keyed;
    private string keyBinding = "";
    private UiChartCoordinate? requested;
    private object? requestedKey;
    public bool Observe(IReadOnlyList<UiChartCoordinate> points, IReadOnlyList<string>? keys, string binding, object? heldKey)
    {
        if (heldKey == null)
        {
            requested = null;
            requestedKey = null;
        }

        bool known = heldKey == null || (keyed == (keys != null) && keyBinding == binding);
        if (heldKey != null && known)
        {
            if (keys != null)
            {
                known = false;
                for (int i = 0; i < keys.Count; i++)
                    if (Equals(keys[i], heldKey))
                        known = true;
            }
            else
            {
                known = points.Count == previous.Length && heldKey is int index && index >= 0 && index < points.Count;
                for (int i = 0; known && i < points.Count; i++)
                {
                    if (points[i].Same(previous[i]))
                        continue;
                    bool ownEcho = Equals(i, heldKey) && Equals(requestedKey, heldKey) && requested.HasValue && points[i].Same(requested.Value);
                    if (!ownEcho)
                        known = false;
                }
            }
        }

        previous = new UiChartCoordinate[points.Count];
        for (int i = 0; i < points.Count; i++)
            previous[i] = points[i];
        keyed = keys != null;
        keyBinding = binding;
        return known;
    }

    public void NoteRequest(object key, float x, float y)
    {
        requestedKey = key;
        requested = new UiChartCoordinate(x, y);
    }
}


