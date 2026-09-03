namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// Per-control value state for value widgets: edit buffer, focus, drag and dropdown openness.
/// Instances are owned by the Host's <see cref="UiSession"/> and cleared when the session is
/// disposed, so nothing here leaks across windows or between the Settings and Overlay hosts.
/// </summary>
public sealed class UiValueState
{
    public float FloatValue;
    public string EditText = "";
    public bool Dragging;
    public bool Focused;
    public int Cursor;
    public bool Open;
    public string? StringValue;
}
