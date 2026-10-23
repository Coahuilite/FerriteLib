using System;
using System.Collections.Generic;
using System.Globalization;

namespace FerriteLib.UiKit.Kernel;

/// <summary>Finite paint meanings, independent of platform types and interaction-state ownership.</summary>
public enum UiPaletteRole
{
    WindowSurface, PanelSurface, ControlSurface, HoverSurface, SelectedSurface,
    SuccessSurface, WarningSurface, DangerSurface, WorkspaceSurface, SectionSurface,
    Foreground, SecondaryForeground, SelectedForeground, SuccessForeground, WarningForeground,
    DangerForeground, DisabledForeground, Accent, SwitchOff, Border, StrongBorder, Divider,
    WindowBorder, PanelBorder, ControlBorder, HoverBorder, SelectedBorder, SuccessBorder,
    WarningBorder, DangerBorder
}

/// <summary>Immutable straight-alpha RGBA in the closed unit interval.</summary>
public readonly struct UiColour : IEquatable<UiColour>
{
    public UiColour(float red, float green, float blue, float alpha = 1)
    {
        if (!Channel(red) || !Channel(green) || !Channel(blue) || !Channel(alpha))
            throw new ArgumentOutOfRangeException(nameof(red), "Colour channels must be finite and within 0..1.");
        Red = red;
        Green = green;
        Blue = blue;
        Alpha = alpha;
    }
    public float Red { get; }
    public float Green { get; }
    public float Blue { get; }
    public float Alpha { get; }
    private static bool Channel(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0 && value <= 1;
    public UiColour WithAlpha(float alpha) => new(Red, Green, Blue, alpha);
    public static UiColour White => new(1, 1, 1);
    public bool Equals(UiColour other) => Red == other.Red && Green == other.Green && Blue == other.Blue && Alpha == other.Alpha;
    public override bool Equals(object? value) => value is UiColour colour && Equals(colour);
    public override int GetHashCode() => Red.GetHashCode() ^ Green.GetHashCode() ^ Blue.GetHashCode() ^ Alpha.GetHashCode();
    public static bool operator ==(UiColour left, UiColour right) => left.Equals(right);
    public static bool operator !=(UiColour left, UiColour right) => !left.Equals(right);

    /// <summary>#RGB, #RGBA, #RRGGBB, #RRGGBBAA or invariant r,g,b[,a].</summary>
    public static bool TryParse(string? raw, out UiColour colour)
    {
        colour = default;
        string text = raw?.Trim() ?? "";
        if (text.Length == 0) return false;
        if (text[0] == '#')
        {
            string hex = text.Substring(1);
            if (hex.Length != 3 && hex.Length != 4 && hex.Length != 6 && hex.Length != 8) return false;
            bool shortForm = hex.Length <= 4;
            int count = shortForm ? hex.Length : hex.Length / 2;
            float[] channels = { 0, 0, 0, 1 };
            for (int i = 0; i < count; i++)
            {
                string part = shortForm ? new string(hex[i], 2) : hex.Substring(i * 2, 2);
                if (!byte.TryParse(part, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte value)) return false;
                channels[i] = value / 255f;
            }
            colour = new UiColour(channels[0], channels[1], channels[2], channels[3]);
            return true;
        }
        float[] rgba = { 0, 0, 0, 1 };
        int start = 0;
        int channel = 0;
        for (int i = 0; i <= text.Length; i++)
        {
            if (i < text.Length && text[i] != ',') continue;
            if (channel >= 4 || !float.TryParse(text.Substring(start, i - start), NumberStyles.Float,
                CultureInfo.InvariantCulture, out float value) || !Channel(value)) return false;
            rgba[channel++] = value;
            start = i + 1;
        }
        if (channel < 3) return false;
        colour = new UiColour(rgba[0], rgba[1], rgba[2], rgba[3]);
        return true;
    }
}

/// <summary>Sparse instance declarations over read-only defaults. Explicit same-value writes retain intent.</summary>
public sealed class UiPalette
{
    private readonly Dictionary<UiPaletteRole, UiColour> declarations = new();
    public int Revision { get; private set; }
    public UiColour this[UiPaletteRole role]
    {
        get
        {
            Validate(role);
            return declarations.TryGetValue(role, out UiColour value) ? value : Default(role);
        }
        set
        {
            Validate(role);
            if (declarations.TryGetValue(role, out UiColour prior) && prior == value) return;
            declarations[role] = value;
            Revision++;
        }
    }
    public bool IsDeclared(UiPaletteRole role)
    {
        Validate(role);
        return declarations.ContainsKey(role);
    }
    public void Remove(UiPaletteRole role)
    {
        Validate(role);
        if (declarations.Remove(role)) Revision++;
    }
    public UiPalette Clone()
    {
        var copy = new UiPalette();
        foreach (var pair in declarations) copy.declarations.Add(pair.Key, pair.Value);
        copy.Revision = Revision;
        return copy;
    }
    internal void RestoreFrom(UiPalette source)
    {
        if (ReferenceEquals(this, source)) return;
        bool changed = declarations.Count != source.declarations.Count;
        foreach (var pair in declarations)
            changed |= !source.declarations.TryGetValue(pair.Key, out UiColour value) || value != pair.Value;
        if (!changed) return;
        declarations.Clear();
        foreach (var pair in source.declarations) declarations.Add(pair.Key, pair.Value);
        Revision++;
    }
    public static bool TryRole(string? name, out UiPaletteRole role)
        => Enum.TryParse(name, false, out role) && Enum.IsDefined(typeof(UiPaletteRole), role) && role.ToString() == name;
    private static void Validate(UiPaletteRole role)
    {
        if (!Enum.IsDefined(typeof(UiPaletteRole), role)) throw new ArgumentOutOfRangeException(nameof(role));
    }
    public static UiColour Default(UiPaletteRole role) => role switch
    {
        UiPaletteRole.WindowSurface => new(21f / 255, 25f / 255, 29f / 255),
        UiPaletteRole.PanelSurface => new(42f / 255, 43f / 255, 44f / 255),
        UiPaletteRole.ControlSurface => new(.21f, .21f, .21f),
        UiPaletteRole.HoverSurface => new(.27f, .27f, .27f),
        UiPaletteRole.SelectedSurface => new(.32f, .28f, .21f),
        UiPaletteRole.SuccessSurface => new(.13f, .30f, .18f),
        UiPaletteRole.WarningSurface or UiPaletteRole.DangerSurface or UiPaletteRole.DangerBorder or UiPaletteRole.WarningBorder => new(.42f, .15f, .12f),
        UiPaletteRole.WorkspaceSurface => new(.055f, .066f, .078f),
        UiPaletteRole.SectionSurface => new(.12f, .125f, .13f),
        UiPaletteRole.Foreground or UiPaletteRole.SwitchOff => new(.91f, .91f, .91f),
        UiPaletteRole.SecondaryForeground => new(.62f, .64f, .67f),
        UiPaletteRole.SelectedForeground or UiPaletteRole.SuccessForeground => new(.99f, .87f, .55f),
        UiPaletteRole.WarningForeground or UiPaletteRole.DangerForeground => new(.99f, .72f, .64f),
        UiPaletteRole.DisabledForeground => new(.45f, .46f, .48f),
        UiPaletteRole.Accent or UiPaletteRole.SelectedBorder => new(.93f, .77f, .22f),
        UiPaletteRole.Border or UiPaletteRole.ControlBorder => new(.33f, .35f, .38f),
        UiPaletteRole.StrongBorder or UiPaletteRole.HoverBorder or UiPaletteRole.SuccessBorder => new(.47f, .51f, .57f),
        UiPaletteRole.Divider => new(.20f, .21f, .23f),
        UiPaletteRole.WindowBorder => new(97f / 255, 108f / 255, 122f / 255),
        UiPaletteRole.PanelBorder => new(135f / 255, 135f / 255, 135f / 255),
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };
}


