using System;

namespace FerriteLib.UiKit.Kernel;

/// <summary>Optional production ruler composed over the unchanged legacy ruler.</summary>
public sealed class VerseTextMetrics : ITextMetrics, IUiTextMetricsEnvironment, IUiTextFontMetrics
{
    public VerseTextMetrics(VerseFerriteTextMetrics source) { Source = source ?? throw new ArgumentNullException(nameof(source)); }
    public VerseFerriteTextMetrics Source { get; }
    internal static ITextMetrics Compose(ITextMetrics source) => source is VerseFerriteTextMetrics native ? new VerseTextMetrics(native) : source;
    public bool Supports(UiTextPurpose purpose) => VerseTextExecution.SupportsPurpose(purpose);
    public float MeasureText(string text, UiFont font, float width) => Source.MeasureText(text, font, width);
    public float MeasureWidth(string text, UiFont font) => Source.MeasureWidth(text, font);
    public UiMeasureInputs? CaptureEnvironment(UiFont font) => ((IUiTextMetricsEnvironment)Source).CaptureEnvironment(font);
    public UiMeasureInputs? CaptureEnvironment(UiTextFont font, UiTextPurpose purpose)
    {
        UiMeasureInputs? original = font.Size.HasValue ? Source.NumericEnvironment(font) : CaptureEnvironment(font.Alias);
        return original == null ? null : UiMeasureInputs.Join(original, UiMeasureInputs.FromOwnedArrays(numbers: new[] { (float)(font.Size ?? 0), (float)purpose }));
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
