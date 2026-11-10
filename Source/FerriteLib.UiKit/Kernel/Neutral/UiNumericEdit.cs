using System;
using System.Globalization;

namespace FerriteLib.UiKit.Kernel;
/// <summary>Finite invariant numeric candidates; display formatting never repairs the model.</summary>
public sealed class UiNumericEdit
{
    private readonly float min;
    private readonly float max;
    private readonly string format;
    private readonly object parseLock = new();
    private string? parsedText;
    private float parsedValue;
    private bool parsed, parsedValid;
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
        // This codec is pure and its bounds are immutable. Live publication and validation often
        // ask about the same (possibly very long) draft; retain just that one answer, not a history.
        // Arbitrary UiEditSession parsers keep their original fresh-call contract.
        // The standalone codec has no owner-thread prerequisite.
        lock (parseLock)
        {
            if (!parsed || !string.Equals(parsedText, text, StringComparison.Ordinal))
            {
                parsedValid = float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out parsedValue)
                    && !float.IsNaN(parsedValue) && !float.IsInfinity(parsedValue);
                if (parsedValid) parsedValue = Clamp(parsedValue);
                parsedText = text; parsed = true;
            }
            value = parsedValue;
            return parsedValid;
        }
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
