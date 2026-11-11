using System;
using System.Collections.Generic;
namespace FerriteLib.UiKit.Kernel;

// Host-owned cache; effective font and purpose are keys. A null environment never earns reuse.
internal sealed class UiTextMetricsCache : ITextMetrics, IUiTextMetricsEnvironment, IUiTextFontMetrics
{
    private sealed class FontCache
    {
        internal UiMeasureInputs? Environment;
        internal readonly Dictionary<string, float> Widths = new(StringComparer.Ordinal);
        internal readonly Dictionary<string, Dictionary<float, float>> Heights = new(StringComparer.Ordinal);
        internal int Entries;
    }
    internal ITextMetrics Source { get; }
    internal Action<UiTextFont, UiTextPurpose, UiMeasureInputs?>? RegionEnvironmentRead;
    private readonly Dictionary<(UiTextFont, UiTextPurpose), FontCache> fonts = new();
    private readonly HashSet<(UiTextFont, UiTextPurpose)> owned = new();
    internal UiTextMetricsCache(ITextMetrics source) { Source = source; }
    internal UiTextMetricsCache Copy()
    {
        owned.Clear();
        var copy = new UiTextMetricsCache(Source);
        foreach (var pair in fonts) copy.fonts.Add(pair.Key, pair.Value);
        return copy;
    }
    public bool Supports(UiTextPurpose purpose) => (Source as IUiTextFontMetrics)?.Supports(purpose) == true;
    public UiMeasureInputs? CaptureEnvironment(UiFont font) => CaptureEnvironment(new UiTextFont(font), UiTextPurpose.Label);
    public UiMeasureInputs? CaptureEnvironment(UiTextFont font, UiTextPurpose purpose)
    {
        UiMeasureInputs? value = font.Size.HasValue ? (Source as IUiTextFontMetrics)?.CaptureEnvironment(font, purpose)
            : (Source as IUiTextMetricsEnvironment)?.CaptureEnvironment(font.Alias);
        RegionEnvironmentRead?.Invoke(font, purpose, value);
        return value;
    }
    private FontCache Writable((UiTextFont, UiTextPurpose) key, FontCache cache)
    {
        if (owned.Add(key))
        {
            var next = new FontCache { Environment = cache.Environment, Entries = cache.Entries };
            foreach (var width in cache.Widths) next.Widths.Add(width.Key, width.Value);
            foreach (var height in cache.Heights) next.Heights.Add(height.Key, new Dictionary<float,float>(height.Value));
            fonts[key] = cache = next;
        }
        if (cache.Entries >= 512) { cache.Widths.Clear(); cache.Heights.Clear(); cache.Entries = 0; }
        return cache;
    }
    private FontCache? Current(UiTextFont font, UiTextPurpose purpose)
    {
        var key = (font, purpose);
        UiMeasureInputs? environment = CaptureEnvironment(font, purpose);
        if (environment == null) { fonts.Remove(key); return null; }
        if (!fonts.TryGetValue(key, out FontCache? cache) || !environment.Equals(cache.Environment))
        { fonts[key] = cache = new FontCache { Environment = environment }; owned.Add(key); }
        return cache;
    }
    public float MeasureWidth(string text, UiFont font) => MeasureWidth(text, new UiTextFont(font), UiTextPurpose.Label);
    public float MeasureText(string text, UiFont font, float width) => MeasureText(text, new UiTextFont(font), width, UiTextPurpose.Label);
    public float MeasureWidth(string text, UiTextFont font, UiTextPurpose purpose)
    {
        FontCache? cache = Current(font,purpose);
        if (cache != null && cache.Widths.TryGetValue(text,out float prior)) return prior;
        float result = font.Size.HasValue ? ((IUiTextFontMetrics)Source).MeasureWidth(text,font,purpose) : Source.MeasureWidth(text,font.Alias);
        if (cache != null) { cache = Writable((font,purpose),cache); cache.Widths[text]=result;cache.Entries++; }
        return result;
    }
    public float MeasureText(string text, UiTextFont font, float width, UiTextPurpose purpose)
    {
        FontCache? cache=Current(font,purpose);
        if(cache!=null && cache.Heights.TryGetValue(text,out Dictionary<float,float>? widths) && widths.TryGetValue(width,out float prior))return prior;
        float result=font.Size.HasValue ? ((IUiTextFontMetrics)Source).MeasureText(text,font,width,purpose) : Source.MeasureText(text,font.Alias,width);
        if(cache!=null)
        {
            cache=Writable((font,purpose),cache);
            if(!cache.Heights.TryGetValue(text,out Dictionary<float,float>? values))cache.Heights.Add(text,values=new Dictionary<float,float>());
            values[width]=result;cache.Entries++;
        }
        return result;
    }
}
