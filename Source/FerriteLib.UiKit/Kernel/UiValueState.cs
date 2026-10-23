namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// One named node-local compatibility slot. Native editors reference their sole neutral draft owner;
/// chart identity stores observed series evidence only, with capture owned by UiInteractionSession.
/// Public value/focus fields remain for older custom primitives, not as a second editor or chart state machine.
/// </summary>
public sealed class UiValueState
{
    internal object? Editor;
    internal UiInteractionTarget? EditorTarget;
    internal bool EditorMultiline;
    internal UiChartIdentity? ChartIdentity;
    // Compatibility projections of Editor for native editors; not read as draft authority.
    // Non-editor legacy range/chart callers retain their original fields and entry conditions.
    public float FloatValue;
    public string EditText = "";
    public bool Dragging;

    /// <summary>Compatibility focus projection. UiEditSession owns the draft; the model owns business value.</summary>
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


