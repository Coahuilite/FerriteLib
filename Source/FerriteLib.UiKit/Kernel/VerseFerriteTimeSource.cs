using UnityEngine;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// The production <see cref="IUiTimeSource"/>: Unity's real-time clock, chosen over the simulation clock on
/// purpose. A paused game must still pick up an edited file, and the simulation clock does not advance while
/// paused.
/// </summary>
public sealed class VerseFerriteTimeSource : IUiTimeSource
{
    /// <summary>The one instance; it holds no state, so a host cannot own a private copy of "now".</summary>
    public static readonly VerseFerriteTimeSource Instance = new VerseFerriteTimeSource();

    private VerseFerriteTimeSource()
    {
    }

    public double NowSeconds => Time.realtimeSinceStartup;
}
