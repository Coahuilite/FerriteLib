namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// Per-control value state for value widgets: edit buffer, focus, and (for the chart) which control
/// point a drag is manipulating. One instance belongs to one element's <see cref="UiNode"/> per named
/// slot, and the node table belongs to the Host's <see cref="UiSession"/> - so state is keyed by
/// identity rather than by a path string, and disposing the session still clears everything: nothing
/// here leaks across windows or between hosts (0.4.0 identity layer).
/// <para>
/// <c>Open</c> and <c>StringValue</c> were deleted in 0.3.0. Dropdown openness is owned by
/// <see cref="UiSession"/> (one popup per session), and a grep across the library, its harness and the
/// wired consumer found no reader of either field. An unused state axis is not spare inventory, it is a
/// second source of truth waiting to drift (US→FL round 1, P5).
/// </para>
/// <para>
/// <c>CommitRequested</c> and <c>DiscardRequested</c> are the two ways an edit can be ended by something
/// other than its own control (FL-IC2, interaction contract §3.4). A window's native Accept and Cancel
/// hooks run BEFORE the page is drawn, so neither can reach into the field; each sets one flag on the
/// state of the edit the session records as active, and the funnel applies it at that field's next draw -
/// which is the same event pass, because the hook and the contents are one window pass. The flags are
/// library plumbing and are deliberately <c>internal</c>: a consumer observes an open edit through
/// <see cref="UiSession.ActiveEditNode"/>, not by polling a state bit, and a custom widget that drives
/// <see cref="UiNative"/> directly has the funnel apply the transaction for it.
/// </para>
/// </summary>
public sealed class UiValueState
{
    public float FloatValue;
    public string EditText = "";
    public bool Dragging;

    /// <summary>True while this control holds an open edit: the buffer is the truth, the model is not.</summary>
    public bool Focused;

    public int Cursor;

    /// <summary>
    /// The window's Accept hook asked this edit to finish. Applied by the funnel as an edit-end carrying a
    /// valid draft (so a deferred write happens exactly once), then cleared. Never written by a widget.
    /// </summary>
    internal bool CommitRequested;

    /// <summary>
    /// The window's Cancel hook asked this edit to be dropped. Applied by the funnel as an edit-end that
    /// restores the buffer from the model and writes nothing - unparseable text included, because discarding
    /// means restoring, never coercing to a default. Then cleared.
    /// </summary>
    internal bool DiscardRequested;
}
