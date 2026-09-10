namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// Per-control value state for value widgets: edit buffer, focus, and (for the chart) which control
/// point a drag is manipulating. Instances are owned by the Host's <see cref="UiSession"/> and cleared
/// when the session is disposed, so nothing here leaks across windows or between hosts.
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
