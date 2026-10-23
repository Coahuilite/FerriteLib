using System;
using System.Globalization;

namespace FerriteLib.UiKit.Kernel;
/// <summary>Finite invariant numeric candidates; display formatting never repairs the model.</summary>
public sealed class UiNumericEdit
{
    private readonly float min;
    private readonly float max;
    private readonly string format;
    public UiNumericEdit(float min, float max, string format = "0.##")
    {
        // Keep the existing clamp contract for reversed bounds; do not add declaration prerequisites.
        this.min = min;
        this.max = max;
        this.format = string.IsNullOrEmpty(format) ? "0.##" : format;
    }

    public string Format(float value) => value.ToString(format, CultureInfo.InvariantCulture);
    public bool Parse(string text, out float value)
    {
        if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) || float.IsNaN(value) || float.IsInfinity(value))
            return false;
        value = Clamp(value);
        return true;
    }

    public float Clamp(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || max < min)
            return min;
        return Math.Max(min, Math.Min(max, value));
    }

    public bool Step(UiEditSession<float> editor, float actualModel, float increment, Action<float> request, Func<float> readModel)
    {
        if (!editor.TryStepBase(actualModel, out float basis))
            return false;
        float candidate = basis + increment;
        if (float.IsNaN(candidate) || float.IsInfinity(candidate))
            return false;
        editor.TakeOver(Clamp(candidate), request, readModel);
        return true;
    }
}


