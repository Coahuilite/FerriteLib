using Verse;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// The production <see cref="IUiMainThread"/>: the game's own answer, delegated rather than re-derived.
/// <para>
/// <c>Verse.UnityData.IsInMainThread</c> is a thread-id comparison the game maintains itself, and every other
/// subsystem in the game that checks its thread checks that property. Re-implementing the comparison here
/// would give this library a second authority that can disagree with the first - the exact shape of defect
/// the version, style and identity work in this library already refuses.
/// </para>
/// </summary>
public sealed class VerseFerriteMainThread : IUiMainThread
{
    /// <summary>The one instance; the type holds no mutable state, so there is nothing per-host to carry.</summary>
    public static readonly VerseFerriteMainThread Instance = new VerseFerriteMainThread();

    private VerseFerriteMainThread()
    {
    }

    public bool IsCurrent => UnityData.IsInMainThread;
}
