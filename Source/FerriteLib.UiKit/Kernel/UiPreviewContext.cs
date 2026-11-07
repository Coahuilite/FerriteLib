using System;

namespace FerriteLib.UiKit.Kernel;

/// <summary>Immutable author registration inputs. No Tools assembly, file writes or process settings are required.</summary>
public sealed class UiPreviewContext
{
    public UiPreviewContext(ITextMetrics metrics, IUiTranslation translation, string measurementSource,
        string? authorDirectory = null, bool watch = false, string language = "zh-Hans")
    {
        Metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        Translation = translation ?? throw new ArgumentNullException(nameof(translation));
        MeasurementSource = measurementSource ?? throw new ArgumentNullException(nameof(measurementSource));
        AuthorDirectory = authorDirectory;
        Watch = watch;
        Language = language ?? throw new ArgumentNullException(nameof(language));
    }
    public ITextMetrics Metrics { get; }
    public IUiTranslation Translation { get; }
    public string MeasurementSource { get; }
    /// <summary>Explicit author root, or null for the author's documented embedded/default source. The author owns filenames.</summary>
    public string? AuthorDirectory { get; }
    public bool Watch { get; }
    /// <summary>Startup language tag. The workbench supplies zh-Hans or en; changing it requires a restart.</summary>
    public string Language { get; }
}
