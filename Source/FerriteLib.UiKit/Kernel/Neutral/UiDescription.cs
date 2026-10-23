using System;

namespace FerriteLib.UiKit.Kernel;
public enum UiDescriptionRole
{
    Text,
    Heading,
    Status,
    Image,
    Decoration,
    Progress,
    Chart
}

/// <summary>A value projection for presentation/observation, never a repair or a writable model.</summary>
public readonly struct UiRangeObservation
{
    public UiRangeObservation(float value, float min, float max)
    {
        Value = value;
        Min = min;
        Max = max;
        Finite = !float.IsNaN(value) && !float.IsInfinity(value) && !float.IsNaN(min) && !float.IsInfinity(min) && !float.IsNaN(max) && !float.IsInfinity(max) && max > min;
        Fraction = Finite ? (float)Math.Max(0d, Math.Min(1d, ((double)value - min) / ((double)max - min))) : 0;
    }

    public float Value { get; }
    public float Min { get; }
    public float Max { get; }
    public bool Finite { get; }
    public float Fraction { get; }
}

/// <summary>Read-only descriptive facts sampled from a wrapper's actual inputs, without running Draw.</summary>
public readonly struct UiDescription
{
    public UiDescription(UiDescriptionRole role, string name = "", string text = "", UiRangeObservation? range = null, bool? available = null, int? count = null, string? unknownReason = null)
    {
        Role = role;
        Name = name ?? "";
        Text = text ?? "";
        Range = range;
        Available = available;
        Count = count;
        UnknownReason = unknownReason;
    }

    public UiDescriptionRole Role { get; }
    public string Name { get; }
    public string Text { get; }
    public UiRangeObservation? Range { get; }
    public bool? Available { get; }
    public int? Count { get; }
    public string? UnknownReason { get; }
}


