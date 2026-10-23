using System;
using System.Collections.Generic;

namespace FerriteLib.UiKit.Kernel;

// Host/engine-owned text calculations. Unknown environments deliberately receive no new reuse rights.
internal sealed class UiTextMetricsCache : ITextMetrics, IUiTextMetricsEnvironment
{
    private sealed class FontCache
    {
        internal UiMeasureInputs? Environment;
        internal readonly Dictionary<string, float> Widths = new(StringComparer.Ordinal);
        internal readonly Dictionary<string, Dictionary<float, float>> Heights = new(StringComparer.Ordinal);
        internal int Entries;
    }
    internal ITextMetrics Source { get; }
    private readonly Dictionary<UiFont, FontCache> fonts = new();
    internal UiTextMetricsCache(ITextMetrics source) { Source = source; }
    internal UiTextMetricsCache Copy()
    {
        var copy = new UiTextMetricsCache(Source);
        foreach (var pair in fonts)
        {
            var cache = new FontCache { Environment = pair.Value.Environment, Entries = pair.Value.Entries };
            foreach (var width in pair.Value.Widths) cache.Widths.Add(width.Key, width.Value);
            foreach (var height in pair.Value.Heights) cache.Heights.Add(height.Key, new Dictionary<float, float>(height.Value));
            copy.fonts.Add(pair.Key, cache);
        }
        return copy;
    }
    public UiMeasureInputs? CaptureEnvironment(UiFont font) => (Source as IUiTextMetricsEnvironment)?.CaptureEnvironment(font);
    private FontCache? Current(UiFont font)
    {
        UiMeasureInputs? environment = CaptureEnvironment(font);
        if (environment == null) { fonts.Remove(font); return null; }
        if (!fonts.TryGetValue(font, out FontCache? cache) || !environment.Equals(cache.Environment))
            fonts[font] = cache = new FontCache { Environment = environment };
        if (cache.Entries >= 512) { cache.Widths.Clear(); cache.Heights.Clear(); cache.Entries = 0; }
        return cache;
    }
    public float MeasureWidth(string text, UiFont font)
    {
        FontCache? cache = Current(font);
        if (cache != null && cache.Widths.TryGetValue(text, out float prior)) return prior;
        float result = Source.MeasureWidth(text, font);
        if (cache != null) { cache.Widths[text] = result; cache.Entries++; }
        return result;
    }
    public float MeasureText(string text, UiFont font, float width)
    {
        FontCache? cache = Current(font);
        if (cache != null && cache.Heights.TryGetValue(text, out Dictionary<float, float>? widths) && widths.TryGetValue(width, out float prior)) return prior;
        float result = Source.MeasureText(text, font, width);
        if (cache != null)
        {
            if (!cache.Heights.TryGetValue(text, out Dictionary<float, float>? values)) cache.Heights.Add(text, values = new Dictionary<float, float>());
            values[width] = result; cache.Entries++;
        }
        return result;
    }
}


