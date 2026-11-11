using System;
using System.Collections.Generic;
using System.Globalization;

namespace FerriteLib.UiKit.Kernel;

/// <summary>Detached representation inputs. Value and Choices are separate, exactly typed channels.</summary>
public sealed class UiRegionSnapshot
{
    internal sealed class Slot
    {
        internal readonly Type Type;
        internal readonly object? Data;
        internal Slot(Type type, object? data) { Type = type; Data = data; }
    }
    private readonly Dictionary<string, Slot> values, choices;
    private readonly Dictionary<string, Dictionary<string, UiRegionSnapshot>> items;
    internal UiRegionSnapshot(Dictionary<string, Slot> values, Dictionary<string, Slot> choices,
        Dictionary<string, Dictionary<string, UiRegionSnapshot>> items)
    { this.values = values; this.choices = choices; this.items = items; }

    public static UiRegionSnapshot Create(Action<UiRegionValues> fill)
    {
        if (fill == null) throw new ArgumentNullException(nameof(fill));
        var builder = new UiRegionValues();
        try { fill(builder); return builder.Finish(); }
        finally { builder.Close(); }
    }
    public T Get<T>(string key) => Read<T>(values, key, "Value");
    public IReadOnlyList<UiChoice<T>> Choices<T>(string key)
        => ReadChoices<T>(choices, key);
    public UiRegionSnapshot Item(string source, string item)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (item == null) throw new ArgumentNullException(nameof(item));
        if (items.TryGetValue(source, out var rows) && rows.TryGetValue(item, out var value)) return value;
        throw new KeyNotFoundException("Region item is missing: " + source + "/" + item + ".");
    }
    private static Slot Find(Dictionary<string, Slot> channel, string key, string name)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        if (!channel.TryGetValue(key, out Slot? value)) throw new KeyNotFoundException("Region " + name + " is missing: " + key + ".");
        return value;
    }
    private static void RequireType<T>(Slot slot, string key, string channel)
    {
        if (slot.Type != typeof(T)) throw new InvalidOperationException("Region " + channel + " '" + key
            + "' declares " + slot.Type.FullName + ", not " + typeof(T).FullName + ".");
    }
    private static T Read<T>(Dictionary<string, Slot> channel, string key, string name)
    { Slot slot = Find(channel, key, name); RequireType<T>(slot, key, name); return (T)slot.Data!; }
    private static IReadOnlyList<UiChoice<T>> ReadChoices<T>(Dictionary<string, Slot> channel, string key)
    { Slot slot = Find(channel, key, "Choices"); RequireType<T>(slot, key, "Choices"); return (IReadOnlyList<UiChoice<T>>)slot.Data!; }

    internal bool TryItem(string source, string item, out UiRegionSnapshot result)
    {
        if (items.TryGetValue(source, out var rows) && rows.TryGetValue(item, out result!)) return true;
        foreach (var group in items.Values) foreach (UiRegionSnapshot row in group.Values)
            if (row.TryItem(source, item, out result)) return true;
        result = null!; return false;
    }
    internal bool Has(string key, bool choice) => (choice ? choices : values).ContainsKey(key);
    internal static bool Address(string key, out string source, out string item, out string field)
    {
        source = item = field = "";
        if (!key.StartsWith("@item/", StringComparison.Ordinal)) return false;
        int offset = 6;
        bool Part(out string value)
        {
            value = "";
            int colon = key.IndexOf(':', offset);
            if (colon < offset || !int.TryParse(key.Substring(offset, colon - offset), NumberStyles.None,
                CultureInfo.InvariantCulture, out int count) || count > key.Length - colon - 1) return false;
            value = key.Substring(colon + 1, count); offset = colon + 1 + count; return true;
        }
        return Part(out source) && Part(out item) && Part(out field) && offset == key.Length;
    }
}

/// <summary>A synchronous snapshot builder. Values must already be detached immutable payloads.</summary>
public sealed class UiRegionValues
{
    private bool active = true;
    private readonly Dictionary<string, UiRegionSnapshot.Slot> values = new(StringComparer.Ordinal), choices = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, UiRegionSnapshot>> items = new(StringComparer.Ordinal);
    internal UiRegionValues() { }
    private void Require() { if (!active) throw new ObjectDisposedException(nameof(UiRegionValues)); }
    private void Put<T>(Dictionary<string, UiRegionSnapshot.Slot> channel, string key, object? data)
    {
        Require(); if (key == null) throw new ArgumentNullException(nameof(key));
        if (channel.ContainsKey(key)) throw new ArgumentException("Duplicate region channel key: " + key + ".", nameof(key));
        channel.Add(key, new UiRegionSnapshot.Slot(typeof(T), data));
    }
    public void Value<T>(string key, T value) => Put<T>(values, key, value);
    public void Choices<T>(string key, IReadOnlyList<UiChoice<T>> choices)
    {
        Require(); if (choices == null) throw new ArgumentNullException(nameof(choices));
        var copy = new UiChoice<T>[choices.Count];
        for (int i = 0; i < copy.Length; i++) copy[i] = choices[i];
        Put<T>(this.choices, key, Array.AsReadOnly(copy));
    }
    public void Item(string source, string item, UiRegionSnapshot fields)
    {
        Require();
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (item == null) throw new ArgumentNullException(nameof(item));
        if (fields == null) throw new ArgumentNullException(nameof(fields));
        if (!items.TryGetValue(source, out var rows)) items.Add(source, rows = new(StringComparer.Ordinal));
        if (rows.ContainsKey(item)) throw new ArgumentException("Duplicate region item: " + source + "/" + item + ".", nameof(item));
        rows.Add(item, fields);
    }
    internal UiRegionSnapshot Finish() { Require(); return new UiRegionSnapshot(values, choices, items); }
    internal void Close() => active = false;
}
