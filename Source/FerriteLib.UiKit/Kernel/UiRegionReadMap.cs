using System;
using System.Collections.Generic;

namespace FerriteLib.UiKit.Kernel;

[Flags]
public enum UiRegionUse { Layout = 1, Display = 2 }

/// <summary>Explicit routing for the new representation port, never for ordinary Get or operations.</summary>
public sealed class UiRegionReadMap
{
    internal sealed class Route
    {
        internal readonly Type Type;
        internal readonly UiRegionUse Use;
        internal Route(Type type, UiRegionUse use) { Type = type; Use = use; }
    }
    private readonly Dictionary<string, Route> values = new(StringComparer.Ordinal), choices = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, Route>> itemValues = new(StringComparer.Ordinal), itemChoices = new(StringComparer.Ordinal);
    public UiRegionReadMap() { }
    private static void Add<T>(Dictionary<string, Route> map, string key, UiRegionUse use)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        if (use == 0 || (use & ~(UiRegionUse.Layout | UiRegionUse.Display)) != 0) throw new ArgumentOutOfRangeException(nameof(use));
        if (map.ContainsKey(key)) throw new ArgumentException("Duplicate region read channel: " + key + ".", nameof(key));
        map.Add(key, new Route(typeof(T), use));
    }
    public UiRegionReadMap Value<T>(string key, UiRegionUse use) { Add<T>(values, key, use); return this; }
    public UiRegionReadMap Choices<T>(string key, UiRegionUse use) { Add<T>(choices, key, use); return this; }
    private static void AddItem<T>(Dictionary<string, Dictionary<string, Route>> map, string source, string field, UiRegionUse use)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (!map.TryGetValue(source, out var fields)) map.Add(source, fields = new(StringComparer.Ordinal));
        Add<T>(fields, field, use);
    }
    public UiRegionReadMap ItemValue<T>(string source, string field, UiRegionUse use)
    { AddItem<T>(itemValues, source, field, use); return this; }
    public UiRegionReadMap ItemChoices<T>(string source, string field, UiRegionUse use)
    { AddItem<T>(itemChoices, source, field, use); return this; }
    internal bool Find(string key, bool choice, UiRegionUse use, out Route route, out string? source, out string? item, out string field)
    {
        source = item = null; field = key;
        if ((choice ? choices : values).TryGetValue(key, out route!)) return (route.Use & use) == use;
        if (UiRegionSnapshot.Address(key, out string s, out string i, out string f)
            && (choice ? itemChoices : itemValues).TryGetValue(s, out var fields) && fields.TryGetValue(f, out route!))
        { source = s; item = i; field = f; return (route.Use & use) == use; }
        route = null!; return false;
    }
    internal UiRegionReadMap Freeze()
    {
        var result = new UiRegionReadMap();
        foreach (var pair in values) result.values.Add(pair.Key, pair.Value);
        foreach (var pair in choices) result.choices.Add(pair.Key, pair.Value);
        foreach (var pair in itemValues) result.itemValues.Add(pair.Key, new Dictionary<string, Route>(pair.Value, StringComparer.Ordinal));
        foreach (var pair in itemChoices) result.itemChoices.Add(pair.Key, new Dictionary<string, Route>(pair.Value, StringComparer.Ordinal));
        return result;
    }
    internal bool HasItemSource(string source) => itemValues.ContainsKey(source) || itemChoices.ContainsKey(source);
}
