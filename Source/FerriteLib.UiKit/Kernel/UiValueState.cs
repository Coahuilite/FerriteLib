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
/// </summary>
public sealed class UiValueState
{
    public float FloatValue;
    public string EditText = "";
    public bool Dragging;
    public bool Focused;
    public int Cursor;
}
