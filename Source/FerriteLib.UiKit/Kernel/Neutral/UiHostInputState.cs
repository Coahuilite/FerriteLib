namespace FerriteLib.UiKit.Kernel;

/// <summary>Current local input lifetime, without exposing the session's runtime tables.</summary>
public readonly struct UiHostInputState
{
    internal UiHostInputState(bool active, bool editing, bool captured)
    { IsActive = active; IsEditing = editing; HasPointerCapture = captured; }
    public bool IsActive { get; }
    public bool IsEditing { get; }
    public bool HasPointerCapture { get; }
}


