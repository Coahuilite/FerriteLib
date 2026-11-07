using System;
using FerriteLib.UiKit.Kernel;
using UnityEngine;

namespace FerriteLib.UiKit.AuthorExamples;

/// <summary>Forward the real Mod.DoSettingsWindowContents rectangle and its native input qualification here.</summary>
public sealed class NativeSettingsPage : IDisposable
{
    private readonly CounterPage page;
    private readonly VerseUiExecution execution = new();
    public NativeSettingsPage(IUiTranslation translation)
        : this(translation, null) { }
    /// <summary>The surrounding real window may expose its existing sampler; it still owns sampling and shutdown.</summary>
    public NativeSettingsPage(IUiTranslation translation, IUiCostSampleProvider? costSamples)
    {
        page = new CounterPage(VerseFerriteTextMetrics.Instance, translation);
        if (costSamples != null) page.Host.SetCostSampleProvider(costSamples);
    }
    public UiHost Host => page.Host;
    public void DoSettingsWindowContents(Rect rectangle, bool nativeInputAllowed)
        => page.Embedded(VerseUiExecution.Rect(rectangle), execution.CaptureFrame(nativeInputAllowed), execution);
    /// <summary>The surrounding window owner calls this on actual detach/close, not on each GUI event.</summary>
    public void Dispose()
    {
        try { page.Dispose(); }
        finally { execution.Dispose(); }
    }
}
