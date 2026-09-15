namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// "Am I on the thread the game's own objects may be touched from", as an injectable seam.
/// <para>
/// The library never assumes it is on that thread when a consumer drives it. One public entry point takes
/// this interface today - the notification adapter - and anything that later grows a caller-driven callback
/// is expected to take it too rather than invent a second answer. The production implementation is
/// <see cref="VerseFerriteMainThread"/>, which answers with the same signal the game's own code uses
/// (<c>Verse.UnityData.IsInMainThread</c>) rather than a second, private notion of "main" that could disagree
/// with it.
/// </para>
/// <para>
/// A seam rather than a static because the harness drives these paths from an arbitrary thread and must be
/// able to answer the question either way without a real game.
/// </para>
/// </summary>
public interface IUiMainThread
{
    /// <summary>True when the caller may touch the game's own objects.</summary>
    bool IsCurrent { get; }
}
