using System;
using System.Collections.Generic;

namespace FerriteLib.UiKit.Kernel;

public enum UiPropertyOrigin { Instance, LocalStyle, Ancestor, Host, Fallback }

/// <summary>Written intent, separate from effective values; Remove never copies a parent value.</summary>
public sealed class UiPropertyDeclaration<T>
{
    private readonly Action? changed;
    private T value = default!;
    public UiPropertyDeclaration(Action? changed = null) { this.changed = changed; }
    public bool IsDeclared { get; private set; }
    public T Value => IsDeclared ? value : throw new InvalidOperationException("Property is not declared.");
    public void Set(T newValue) { value = newValue; IsDeclared = true; changed?.Invoke(); }
    public void Remove() { if (!IsDeclared) return; IsDeclared = false; value = default!; changed?.Invoke(); }
}

public readonly struct UiResolvedProperty<T>
{
    internal UiResolvedProperty(T value, UiPropertyOrigin origin, string source)
    { Value = value; Origin = origin; Source = source; }
    public T Value { get; }
    public UiPropertyOrigin Origin { get; }
    public string Source { get; }
}

/// <summary>Independent per-property precedence. Ancestors contain declarations, never resolved bags.</summary>
public static class UiPropertyResolver
{
    public static UiResolvedProperty<T> Resolve<T>(UiPropertyDeclaration<T>? instance, UiPropertyDeclaration<T>? localStyle,
        IEnumerable<KeyValuePair<string, UiPropertyDeclaration<T>>>? ancestors, UiPropertyDeclaration<T>? host, T fallback)
    {
        if (instance?.IsDeclared == true) return new UiResolvedProperty<T>(instance.Value, UiPropertyOrigin.Instance, "instance");
        if (localStyle?.IsDeclared == true) return new UiResolvedProperty<T>(localStyle.Value, UiPropertyOrigin.LocalStyle, "local-style");
        if (ancestors != null)
            foreach (var ancestor in ancestors)
                if (ancestor.Value.IsDeclared) return new UiResolvedProperty<T>(ancestor.Value.Value, UiPropertyOrigin.Ancestor, ancestor.Key);
        if (host?.IsDeclared == true) return new UiResolvedProperty<T>(host.Value, UiPropertyOrigin.Host, "host");
        return new UiResolvedProperty<T>(fallback, UiPropertyOrigin.Fallback, "library");
    }
}

/// <summary>Distances, independent of typography. Values are not font scaling factors.</summary>
public readonly struct UiDensity
{
    public UiDensity(float padding, float spacing, float gap, float rowHeight, float hairline = 1)
    { Padding = padding; Spacing = spacing; Gap = gap; RowHeight = rowHeight; Hairline = hairline; }
    public float Padding { get; }
    public float Spacing { get; }
    public float Gap { get; }
    public float RowHeight { get; }
    public float Hairline { get; }
}

/// <summary>Slider's measured internal layout, shared with presentation; uses the assigned width.</summary>
public readonly struct UiSliderLayout
{
    private UiSliderLayout(float labelWidth, float labelHeight, float height, bool stacked)
    { LabelWidth = labelWidth; LabelHeight = labelHeight; Height = height; Stacked = stacked; }
    public float LabelWidth { get; }
    public float LabelHeight { get; }
    public float Height { get; }
    public bool Stacked { get; }
    public static UiSliderLayout Measure(string label, float width, UiFont font, UiDensity density, ITextMetrics metrics)
    {
        if (label.Length == 0) return new UiSliderLayout(0, 0, density.RowHeight, false);
        float natural = metrics.MeasureWidth(label, font) + density.Spacing;
        bool stacked = natural + density.Gap + density.RowHeight * 2 > width;
        float labelHeight = metrics.MeasureText(label, font, Math.Max(1, stacked ? width : natural));
        return new UiSliderLayout(stacked ? width : natural, labelHeight,
            stacked ? labelHeight + density.Gap + density.RowHeight : Math.Max(labelHeight, density.RowHeight), stacked);
    }
}
