namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// The clock the reload scheduler reads, as an injectable seam.
/// <para>
/// Every timing decision in the document service - how long a save burst has been quiet, how long a refused
/// candidate has waited before its retry, how long a commit has been deferred for the user's own input - is
/// measured against this and never against a frame count, a host count or a pump count. Those are not time:
/// a paused game pumps many frames per wall-clock second and a minimised one pumps few, so a scheduler built
/// on them coalesces differently on every machine.
/// </para>
/// <para>
/// The production implementation reads a real-time clock, not the simulation clock, so a paused game still
/// notices a saved file.
/// </para>
/// </summary>
public interface IUiTimeSource
{
    /// <summary>Seconds since process start, monotonic, unaffected by game speed or pause.</summary>
    double NowSeconds { get; }
}
