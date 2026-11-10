using System;
using System.Collections.Generic;

namespace FerriteLib.UiKit.Kernel;

/// <summary>Optional production ruler composed over the unchanged legacy ruler.</summary>
public sealed class VerseTextMetrics : ITextMetrics, IUiTextMetricsEnvironment, IUiTextFontMetrics
{
    public VerseTextMetrics(VerseFerriteTextMetrics source) { Source = source ?? throw new ArgumentNullException(nameof(source)); }
    public VerseFerriteTextMetrics Source { get; }
    private readonly Dictionary<UiTextFont, VerseFerriteTextMetrics.EnvironmentCapture> environments = new();
    private readonly Dictionary<(UiTextFont, UiTextPurpose), (UiMeasureInputs Original, UiMeasureInputs Result)> purposeEnvironments = new();
    internal static ITextMetrics Compose(ITextMetrics source) => source is VerseFerriteTextMetrics native ? new VerseTextMetrics(native) : source;
    public bool Supports(UiTextPurpose purpose) => VerseTextExecution.SupportsPurpose(purpose);
    public float MeasureText(string text, UiFont font, float width) => Source.MeasureText(text, font, width);
    public float MeasureWidth(string text, UiFont font) => Source.MeasureWidth(text, font);
    private UiMeasureInputs? CaptureNativeEnvironment(UiTextFont font)
    {
        if (!environments.TryGetValue(font, out var capture))
            environments.Add(font, capture = new VerseFerriteTextMetrics.EnvironmentCapture());
        return font.Size.HasValue ? Source.NumericEnvironment(font, capture) : Source.CaptureEnvironment(font.Alias, capture);
    }
    public UiMeasureInputs? CaptureEnvironment(UiFont font) => CaptureNativeEnvironment(new UiTextFont(font));
    public UiMeasureInputs? CaptureEnvironment(UiTextFont font, UiTextPurpose purpose)
    {
        UiMeasureInputs? original = CaptureNativeEnvironment(font);
        if (original == null) return null;
        var key = (font, purpose);
        if (purposeEnvironments.TryGetValue(key, out var prior) && ReferenceEquals(prior.Original, original)) return prior.Result;
        UiMeasureInputs result = UiMeasureInputs.Join(original, new UiMeasureInputs(numbers: new[] { (float)(font.Size ?? 0), (float)purpose }));
        purposeEnvironments[key] = (original, result);
        return result;
    }
    public float MeasureText(string text, UiTextFont font, float width, UiTextPurpose purpose)
        => Calculate(text, font, purpose, width, true);
    public float MeasureWidth(string text, UiTextFont font, UiTextPurpose purpose)
        => Calculate(text, font, purpose, 0, false);
    private float Calculate(string text, UiTextFont font, UiTextPurpose purpose, float width, bool wrap)
    {
        if (!font.Size.HasValue) return wrap ? Source.MeasureText(text, font.Alias, width) : Source.MeasureWidth(text, font.Alias);
        if (!Supports(purpose)) throw new NotSupportedException("Resolve the effective font before requesting unsupported numeric metrics.");
        return VerseFerriteTextMetrics.WithFontSize(font, wrap,
            () => wrap ? Source.MeasureText(text, font.Alias, width) : Source.MeasureWidth(text, font.Alias));
    }
}
