using System.Collections.Generic;
using UnityEngine;

namespace Verse;

public struct TipSignal
{
    public string text;
    public static implicit operator TipSignal(string text) => new TipSignal { text = text };
}

public static class TooltipHandler
{
    public static readonly List<Rect> TipRects = new List<Rect>();
    public static readonly List<string> TipTexts = new List<string>();
    public static void TipRegion(Rect rect, TipSignal signal)
    {
        TipRects.Add(rect);
        TipTexts.Add(signal.text);
    }
}
