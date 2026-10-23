using System;
using System.Collections.Generic;

namespace FerriteLib.UiKit.Kernel;

/// <summary>Detached, exact inputs supplied by a measurement author; never business-object inspection.</summary>
public sealed class UiMeasureInputs : IEquatable<UiMeasureInputs>
{
    private readonly string[] text;
    private readonly float[] numbers;
    public UiMeasureInputs(string[]? text = null, float[]? numbers = null, bool usesTextMetrics = false)
    {
        this.text = text == null ? Array.Empty<string>() : (string[])text.Clone();
        this.numbers = numbers == null ? Array.Empty<float>() : (float[])numbers.Clone();
        UsesTextMetrics = usesTextMetrics;
        foreach (float value in this.numbers)
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentException("Measurement inputs must be finite.");
    }
    public bool UsesTextMetrics { get; }
    public bool Equals(UiMeasureInputs? other)
    {
        if (other == null || UsesTextMetrics != other.UsesTextMetrics || text.Length != other.text.Length || numbers.Length != other.numbers.Length) return false;
        for (int i = 0; i < text.Length; i++) if (!string.Equals(text[i], other.text[i], StringComparison.Ordinal)) return false;
        for (int i = 0; i < numbers.Length; i++) if (numbers[i] != other.numbers[i]) return false;
        return true;
    }
    public override bool Equals(object? obj) => obj is UiMeasureInputs other && Equals(other);
    public override int GetHashCode() => text.Length * 397 ^ numbers.Length;
}

/// <summary>Optional environment capability. Null revokes text-measure reuse; ITextMetrics is unchanged.</summary>
public interface IUiTextMetricsEnvironment
{
    UiMeasureInputs? CaptureEnvironment(UiFont font);
}

/// <summary>A captured input set and its calculation over those exact captured values.</summary>
public sealed class UiMeasurement
{
    public UiMeasurement(UiMeasureInputs inputs, Func<float> calculate)
    { Inputs = inputs ?? throw new ArgumentNullException(nameof(inputs)); Calculate = calculate ?? throw new ArgumentNullException(nameof(calculate)); }
    public UiMeasureInputs Inputs { get; }
    public Func<float> Calculate { get; }
}

// A plan records the actual calls made by the production layout algorithm. Before reuse, every child
// result is validated at the SAME constraints. No parent is accepted on a child's dirty/source clock.
internal sealed class UiLayoutReuse
{
    private readonly struct Demand
    {
        internal Demand(int index, float width, float available, float? reference, UiLayoutSize result)
        { Index = index; Width = width; Available = available; Reference = reference; Result = result; }
        internal readonly int Index;
        internal readonly float Width, Available;
        internal readonly float? Reference;
        internal readonly UiLayoutSize Result;
    }
    private readonly UiMeasureInputs inputs;
    private readonly Demand[] demands;
    internal UiLayoutPlan Plan { get; }
    private UiLayoutReuse(UiMeasureInputs inputs, Demand[] demands, UiLayoutPlan plan)
    { this.inputs = inputs; this.demands = demands; Plan = plan; }
    internal static UiLayoutReuse Resolve(UiLayoutReuse? prior, UiMeasureInputs inputs,
        UiLayoutMeasure measure, Func<UiLayoutMeasure, UiLayoutPlan> calculate, out bool reused)
    {
        if (prior != null && inputs.Equals(prior.inputs))
        {
            bool equal = true;
            foreach (Demand demand in prior.demands)
            {
                UiLayoutSize result = measure(demand.Index, demand.Width, demand.Available, demand.Reference);
                equal &= result.Width == demand.Result.Width && result.Height == demand.Result.Height;
            }
            if (equal) { reused = true; return prior; }
        }
        var calls = new List<Demand>();
        UiLayoutPlan plan = calculate((i, w, a, r) =>
        {
            UiLayoutSize size = measure(i, w, a, r);
            calls.Add(new Demand(i, w, a, r, size));
            return size;
        });
        reused = false;
        return new UiLayoutReuse(inputs, calls.ToArray(), plan);
    }
}

// Shared stage validity: source identity/revisions select a capture; real captured values and the
// corresponding environment, not a source clock, decide whether its calculation can be reused.
internal static class UiMeasureValidity
{
    internal static bool CanReuse(UiMeasureInputs prior, UiMeasureInputs current,
        UiMeasureInputs? priorEnvironment, UiMeasureInputs? currentEnvironment)
        => current.Equals(prior) && (!current.UsesTextMetrics || currentEnvironment != null && currentEnvironment.Equals(priorEnvironment));
}


